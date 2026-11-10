using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

internal static partial class PovCompatibilityContracts
{
    private static bool IconFixturesActive, RejectIconDecode, RejectIconSprite;
    private static int IconTextures, IconSprites, IconDecodes;
    private static readonly HashSet<UnityEngine.Object> ReleasedIcons = new(ReferenceComparer.Instance);
    private static object? SavedIconDecoder, SavedIconAssembly;
    private static bool NestedFinalWrite;

    public static class IntFlags
    {
        public enum Mod : int { Low = 1, High = int.MinValue }
        public static Mod On(Character character) => Mod.Low | Mod.High;
    }
    public static class LongFlags
    {
        public enum Mod : long { Low = 1, Upper = 1L << 51, High = long.MinValue }
        public static Mod On(Character character) => Mod.Low | Mod.Upper | Mod.High;
    }

    private static void CheckMaskReaders()
    {
        MethodInfo reader = Bridge.GetMethod("ModifierReader", All)!;
        foreach (Type fixture in new[] { typeof(IntFlags), typeof(LongFlags) })
        {
            var read = (Func<Character, long>)reader.Invoke(null, new object[] { fixture.GetMethod("On")! })!;
            long expected = fixture == typeof(IntFlags) ? 0x80000001L : long.MinValue | (1L << 51) | 1L;
            Require(read(Actor) == expected, "32-bit unsigned extension / 64-bit upper and sign flags preserved: " + fixture.Name);
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<UnityEngine.Object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public bool Equals(UnityEngine.Object? a, UnityEngine.Object? b) => ReferenceEquals(a, b);
        public int GetHashCode(UnityEngine.Object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

    private static void CheckRawHealth(Harmony harness)
    {
        MethodInfo? derived = Difficulty.GetMethod("DerivedFromRead", All);
        if (derived == null) return;
        FixtureFrame++; Maximum = 400; Health = 200;
        Actor.GetMaxHealth(); Actor.SetMaxHealth(150);
        Require(Maximum == 150, "original PoV same-frame read heuristic still applies to unscoped foreign writes");
        FixtureFrame++; Maximum = 400; Health = 200;
        MethodInfo apply = Copy(Levels.GetMethod("ApplyHealthMultiplier", All)!);
        Require((bool)apply.Invoke(null, new object[] { Actor, 1.5f })! && Maximum == 600 && Health == 400,
            "real max-read hook cannot mistake CM's deficit bookkeeping for a scaled write");
        FieldInfo supported = Bridge.GetField("_rawHealthSupported", All)!;
        supported.SetValue(null, false);
        try
        {
            Require(!(bool)apply.Invoke(null, new object[] { Actor, 2f })! && Maximum == 600 && Health == 400,
                "rejected raw integration reports no application and does not write HP");
            Copy(Bridge.GetMethod("SetFinalMaxHealth", All)!).Invoke(null, new object[] { Actor, 660f });
            Require(Maximum == 660, "unavailable raw integration does not disable the known final-health API");
        }
        finally { supported.SetValue(null, true); }

        MethodInfo raw = Copy(Bridge.GetMethod("TrySetRawMaxHealth", All)!);
        ThrowWrite = true;
        try { raw.Invoke(null, new object[] { Actor, 150f }); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { }
        finally { ThrowWrite = false; }
        Require(Bridge.GetField("_rawHealthCharacter", All)!.GetValue(null) == null, "raw scope unwinds after an exception");
        Actor.GetMaxHealth(); Actor.SetMaxHealth(440);
        Require(Maximum == 440, "genuine read-derived final write remains unscaled after raw failure");

        var nested = new Harmony("CM.tests.PoV.nestedRaw");
        try
        {
            nested.Patch(AccessTools.Method(typeof(Character), "SetMaxHealth"), postfix:
                new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(NestedHealthPostfix)) { priority = Priority.Last });
            FixtureFrame++; Maximum = 400; Health = 200; NestedFinalWrite = true;
            raw.Invoke(null, new object[] { Actor, 150f });
            Require(Maximum == 601, "raw exception is one-use; a nested foreign read-derived write is not scaled again");
        }
        finally { NestedFinalWrite = false; nested.UnpatchSelf(); }
        MethodInfo prefix = Difficulty.GetNestedType("MaxHealth", All)!.GetMethod("Prefix", All)!;
        var changed = PatchProcessor.GetOriginalInstructions(prefix);
        int guard = changed.FindIndex(code => code.Calls(derived));
        changed[guard + 1].opcode = OpCodes.Brtrue_S;
        Rejected("ValidateRawHealthCaller", changed, derived);
        System.Console.WriteLine("PoV raw HP checks passed: original read heuristic, raw/final distinction, preserved deficit, rejected writes, exception cleanup and nested foreign write. Unity equality/clock are fixtures.");
    }

    private static void NestedHealthPostfix(Character __instance)
    {
        if (!NestedFinalWrite || !ReferenceEquals(__instance, Actor) ||
            (int)Difficulty.GetField("alreadyScaledMaxHealth", All)!.GetValue(null)! != 0) return;
        NestedFinalWrite = false;
        __instance.SetMaxHealth(__instance.GetMaxHealth() + 1);
    }

    private static void BeginIconFixtures(Type hud, Harmony harness)
    {
        IconFixturesActive = true;
        IconTextures = IconSprites = IconDecodes = 0; ReleasedIcons.Clear();
        SavedIconDecoder = hud.GetField("_loadImage", All)!.GetValue(null);
        SavedIconAssembly = hud.GetField("_iconAssembly", All)!.GetValue(null);
        hud.GetField("_loadImage", All)!.SetValue(null, (Func<Texture2D, byte[], bool, bool>)DecodeIcon);
        foreach (string method in new[] { "EmbeddedIcon", "ReleaseOwnedIcons" })
            harness.Patch(hud.GetMethod(method, All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
    }

    private static MethodInfo? IconBoundary(MethodInfo method)
    {
        if (!IconFixturesActive) return null;
        string? name = method.DeclaringType == typeof(UnityEngine.Object) ? method.Name switch
        {
            "Destroy" when method.GetParameters().Length == 1 => nameof(DestroyIcon),
            "set_name" => nameof(NameIcon), _ => null
        } : method.DeclaringType == typeof(Texture) ? method.Name switch
        {
            "get_width" or "get_height" => nameof(IconSize),
            "set_filterMode" => nameof(FilterIcon), "set_wrapMode" => nameof(WrapIcon), _ => null
        } : method.DeclaringType == typeof(Sprite) && method.Name == "Create" && method.GetParameters().Length == 6 ? nameof(CreateIconSprite) : null;
        return name == null ? null : AccessTools.Method(typeof(PovCompatibilityContracts), name);
    }

    private static Texture2D NewIconTexture(int width, int height, TextureFormat format, bool mips) { IconTextures++; return New<Texture2D>(); }
    private static bool DecodeIcon(Texture2D texture, byte[] bytes, bool discard)
    {
        Require(bytes.Length > 8 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71,
            "native decoder receives the original PoV embedded PNG bytes");
        IconDecodes++; return !RejectIconDecode;
    }
    private static Sprite CreateIconSprite(Texture2D texture, Rect rect, Vector2 pivot, float ppu, uint extrude, SpriteMeshType mesh)
    {
        if (RejectIconSprite) throw new InvalidOperationException("fixture Sprite.Create failure");
        IconSprites++; return New<Sprite>();
    }
    private static void DestroyIcon(UnityEngine.Object value)
    {
        if (!ReferenceEquals(value, null)) Require(ReleasedIcons.Add(value), "owned native icon released once");
    }
    private static void NameIcon(UnityEngine.Object value, string name) { }
    private static int IconSize(Texture texture) => 64;
    private static void FilterIcon(Texture texture, FilterMode mode) { }
    private static void WrapIcon(Texture texture, TextureWrapMode mode) { }

    private static void CheckIconFixtures(Type hud, MethodInfo read, Func<object> newHud, FieldInfo source, Type rowType)
    {
        var cache = (IDictionary)hud.GetField("OwnedIcons", All)!.GetValue(null)!;
        if (cache.Count == 0) return; // Legacy atlas-only contract.
        Require(cache.Count == 46 && IconTextures == 46 && IconSprites == 46 && IconDecodes == 46,
            "46 PoV PNGs loaded once across seven displays, HUD recreation and row cleanup");
        source.SetValue(null, (Func<Character, long>)(_ => (1L << 32) | (1L << 51)));
        HudTime += 1;
        object result = read.Invoke(null, new object[] { Actor, newHud() })!;
        Require(((IList)rowType.GetField("Sprites", All)!.GetValue(result)!).Count == 2 && IconTextures == 46,
            "upper-bit Volley and Glutton icons survive without truncation or extra decoding");
        MethodInfo release = hud.GetMethod("ReleaseOwnedIcons", All)!;
        release.Invoke(null, null); release.Invoke(null, null);
        Require(cache.Count == 0 && ReleasedIcons.Count == 92, "remove releases only the 46 owned texture/sprite pairs and is idempotent");
        hud.GetField("_iconAssembly", All)!.SetValue(null, SavedIconAssembly);
        hud.GetField("_loadImage", All)!.SetValue(null, (Func<Texture2D, byte[], bool, bool>)DecodeIcon);
        MethodInfo load = hud.GetMethod("EmbeddedIcon", All)!;
        foreach (bool decodeFailure in new[] { true, false })
        {
            RejectIconDecode = decodeFailure; RejectIconSprite = !decodeFailure;
            int released = ReleasedIcons.Count;
            try { load.Invoke(null, new object[] { "PassiveTree.enemymod.swift.png" }); throw new Exception("Expected icon failure"); }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException || ex.InnerException is InvalidOperationException) { }
            finally { RejectIconDecode = RejectIconSprite = false; }
            Require(cache.Count == 0 && ReleasedIcons.Count == released + 1, "failed decode/create releases its partial texture without caching it");
        }
        System.Console.WriteLine("PoV PNG cache checks passed: original resource bytes, high-bit icons, cross-HUD/world-row reuse, idempotent own-only release, decode/create failures. Native graphics are fixtures.");
    }

    private static void EndIconFixtures(Type hud)
    {
        hud.GetMethod("ReleaseOwnedIcons", All)!.Invoke(null, null);
        hud.GetField("_loadImage", All)!.SetValue(null, SavedIconDecoder);
        hud.GetField("_iconAssembly", All)!.SetValue(null, SavedIconAssembly);
        IconFixturesActive = false;
    }
}
