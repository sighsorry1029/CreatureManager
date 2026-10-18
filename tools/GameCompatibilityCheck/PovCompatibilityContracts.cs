using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Optional tests against the supplied, unmodified PoV DLL. Managed detours are installed
// only in this offline process. Unity, scene, RNG, ownership and FX boundaries are fixtures.
internal static class PovCompatibilityContracts
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static Type Bridge = null!, Levels = null!, Modifiers = null!, Difficulty = null!;
    private static Character Actor = null!;
    private static ZDO Data = null!;
    private static float Maximum, Health;
    private static int Writes;
    private static bool ThrowWrite, ThrowLoot;
    private static CharacterDrop Dropper = null!;
    private static readonly List<CharacterDrop.Drop> Rows = new();
    private static MethodInfo Generate = null!;
    private static Func<bool> ForeignLevels = null!;
    private static int CurrentLevel = 1;

    internal static void Run(Assembly cm, Assembly pov)
    {
        Bridge = cm.GetType("CreatureManager.CreaturePovCompatibility", true)!;
        Levels = cm.GetType("CreatureManager.CreatureLevelManager", true)!;
        Modifiers = cm.GetType("CreatureManager.CreatureModifierManager", true)!;
        Difficulty = pov.GetType("PassiveTree.EnemyDifficulty", true)!;
        Type stars = pov.GetType("PassiveTree.CreatureStars", true)!;
        ForeignLevels = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), stars.GetProperty("ForeignLevels")!.GetGetMethod()!);
        Type loot = stars.GetNestedType("CharacterDrop_GenerateDropList_Stars", BindingFlags.NonPublic)!;
        Type entry = cm.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        FieldInfo levelField = entry.GetField("EnableLevelSystem", All)!;
        FieldInfo lootField = entry.GetField("LootSystem", All)!;
        object? savedLevel = levelField.GetValue(null), savedLoot = lootField.GetValue(null);
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CM-PoV-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        ConfigEntryBase levelMode = Bind(config, levelField, "Full"), lootMode = Bind(config, lootField, "Vanilla");
        var harness = new Harmony("CM.tests.PoV.boundaries");
        var foreign = new Harmony("CM.tests.PoV.originalPatches");
        try
        {
            // Character's static animation hashes normally require the native engine.
            harness.Patch(AccessTools.Method(AccessTools.TypeByName("UnityEngine.Animator"), "StringToHash", new[] { typeof(string) }),
                transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(HashBody)));
            Actor = New<Character>();
            Data = New<ZDO>();
            Data.m_uid = new ZDOID(780, 19);
            ZNetView view = New<ZNetView>();
            typeof(Character).GetField("m_nview", All)!.SetValue(Actor, view);
            typeof(ZNetView).GetField("m_zdo", All)!.SetValue(view, Data);
            SetFloat(Data, "PT_EnemyHealthFactor", 4f);
            foreach (string name in new[] { "GetHealth", "GetMaxHealth", "GetMaxHealthBase", "GetLevel", "SetHealth", "SetMaxHealth", "SetLevel", "IsPlayer" })
                harness.Patch(AccessTools.DeclaredMethod(typeof(Character), name), transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(CharacterBody)));
            foreach (string name in new[] { "IsValid", "IsOwner" })
                harness.Patch(AccessTools.DeclaredMethod(typeof(ZNetView), name), transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(TrueBody)));
            foreach (MethodInfo method in new[] {
                Difficulty.GetMethod("SetScaledMaxHealth", All)!, Difficulty.GetMethod("WriteHealth", All)!,
                Difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!.GetMethod("Prefix", All)!,
                Difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!.GetMethod("Postfix", All)!,
                Difficulty.GetNestedType("FollowMaxHealth", BindingFlags.NonPublic)!.GetMethod("Prefix", All)!,
                loot.GetMethod("Prefix", All)!, loot.GetMethod("Postfix", All)!,
                pov.GetType("PassiveTree.RarityLoot", true)!.GetMethod("ScaleAmount", All)! })
                harness.Patch(method, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            harness.Patch(stars.GetNestedType("LootState", BindingFlags.NonPublic)!.GetConstructors(All).Single(),
                transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            Type max = Difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!;
            foreign.Patch(AccessTools.Method(typeof(Character), "SetMaxHealth"),
                prefix: new HarmonyMethod(max.GetMethod("Prefix", All)!), postfix: new HarmonyMethod(max.GetMethod("Postfix", All)!));
            foreign.Patch(AccessTools.Method(typeof(Character), "SetHealth"),
                prefix: new HarmonyMethod(Difficulty.GetNestedType("FollowMaxHealth", BindingFlags.NonPublic)!.GetMethod("Prefix", All)!));
            Generate = AccessTools.DeclaredMethod(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList));
            harness.Patch(Generate, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(LootBody)));
            foreign.Patch(Generate, prefix: new HarmonyMethod(loot.GetMethod("Prefix", All)!),
                postfix: new HarmonyMethod(loot.GetMethod("Postfix", All)!), finalizer: new HarmonyMethod(loot.GetMethod("Finalizer", All)!));
            Dropper = New<CharacterDrop>();
            typeof(CharacterDrop).GetField("m_character", All)!.SetValue(Dropper, Actor);
            foreach (bool chanceOnly in new[] { false, true })
                Rows.Add(new CharacterDrop.Drop { m_prefab = New<GameObject>(), m_levelMultiplier = true, m_levelMultiplierNotAmount = chanceOnly });
            typeof(CharacterDrop).GetField("m_drops", All)!.SetValue(Dropper, Rows);

            // Warm the real caller before CM installs its late optional hooks.
            Require(Roll().All(row => row.Value == 4), "pre-bridge PoV owns count scaling");
            Require(Rows.All(row => row.m_levelMultiplier), "original finalizer restores flags");
            Maximum = 400; Health = 200;
            Actor.SetMaxHealth(440);
            Require(Maximum == 1760, "reproduced double factor on an already-scaled max");
            Require(!ForeignLevels(), "PoV cached the absent-provider result before CM bridge");

            string[] registeredLootPatches = LootPatchRegistrations();
            Bridge.GetMethod("Install", All)!.Invoke(null, new object[] { pov });
            Require(registeredLootPatches.SequenceEqual(LootPatchRegistrations()), "late wrapper rebuild preserves original patch registrations and priorities");
            foreach (string mode in new[] { "Full", "ExceptLevel", "Off", "Full" })
            {
                levelMode.SetSerializedValue(mode);
                Require(ForeignLevels() == (mode == "Full"), "live level ownership: " + mode);
            }
            Require(Roll().All(row => row.Value == 1), "Full lets CM own levels even with CM loot Vanilla");
            levelMode.SetSerializedValue("ExceptLevel");
            lootMode.SetSerializedValue("CalculateChance");
            Require(Roll().All(row => row.Value == 1) && Rows.All(row => row.m_levelMultiplier), "CM owns star loot; chance-only flags stay intact");
            ThrowLoot = true;
            try { Roll(); throw new InvalidOperationException("expected fixture exception"); }
            catch (TargetInvocationException e) { Require(e.InnerException is InvalidOperationException, "loot exception preserved"); }
            finally { ThrowLoot = false; }
            Require(Rows.All(row => row.m_levelMultiplier), "exception did not corrupt shared drop flags");
            lootMode.SetSerializedValue("Vanilla");
            Require(Roll().All(row => row.Value == 4), "Vanilla retains PoV ownership");
            Chainloader.PluginInfos.Add("sighsorry.DropNSpawn", null!);
            Require(Roll().All(row => row.Value == 1), "DNS owns star loot even when CM is Vanilla");
            Chainloader.PluginInfos.Remove("sighsorry.DropNSpawn");

            CheckHealth();
            Bridge.GetMethod("Reset", All)!.Invoke(null, null);
            Require(registeredLootPatches.SequenceEqual(LootPatchRegistrations()), "cleanup preserves other owners' loot patch registrations");
            Require(Roll().All(row => row.Value == 4), "cleanup restores PoV loot without removing its patches");
            Require(!ForeignLevels(), "cleanup restores PoV original cached getter");
            Require(!(bool)Bridge.GetProperty("HasHealthBridge", All)!.GetValue(null)!, "cleanup releases health delegates");
            Chainloader.PluginInfos.Add("fixture.other.levels", new PluginInfo());
            typeof(PluginInfo).GetProperty("Metadata")!.SetValue(Chainloader.PluginInfos["fixture.other.levels"],
                new BepInPlugin("fixture", "Creature Level and Loot", "1.0.0"));
            Bridge.GetMethod("Install", All)!.Invoke(null, new object[] { pov });
            levelMode.SetSerializedValue("Off");
            Require(ForeignLevels(), "other provider detection remains authoritative");
            System.Console.WriteLine("PoV 5.0.7 contracts passed: real managed Harmony callers before/after late install, cached/live level ownership, CM/DNS/PoV loot priority and exception cleanup, raw vs final HP, Reaping cap/restore/heal, per-character pending state and shutdown. Unity/world/FX boundaries substituted; no game session.");
        }
        finally
        {
            Bridge.GetMethod("Reset", All)!.Invoke(null, null);
            foreign.UnpatchSelf(); harness.UnpatchSelf();
            levelField.SetValue(null, savedLevel); lootField.SetValue(null, savedLoot);
            Chainloader.PluginInfos.Remove("sighsorry.DropNSpawn");
            Chainloader.PluginInfos.Remove("fixture.other.levels");
            Rows.Clear(); Copies.Clear();
        }
    }

    private static void CheckHealth()
    {
        Maximum = 400; Health = 50;
        Copy(Levels.GetMethod("SetManagedLevel", All)!).Invoke(null, new object[] { Actor, 1 });
        Require(Maximum == 400 && Health == 50, "managed SetLevel preserves severe injury despite same-frame PoV pending conversion");
        SeedPending(Actor, 100, 400);
        Copy(Levels.GetMethod("RestoreStoredHealthDeficit", All)!).Invoke(null, new object[] { Actor, 350f });
        Require(Maximum == 400 && Health == 50, "stored deficit restores final HP without raw conversion");
        SeedPending(Actor, 100, 400);
        Copy(Modifiers.GetMethod("TryRollbackDeathwardCommit", All)!).Invoke(null, new object?[] { Actor, new HitData(), default(HitData.DamageTypes), 50f, null, 0, 0f });
        Require(Health == 50 && !HasPending(Actor), "Deathward rollback restores absolute HP and consumes only target pending conversion");
        Maximum = 400; Health = 200;
        Copy(Levels.GetMethod("ApplyHealthMultiplier", All)!).Invoke(null, new object[] { Actor, 1.5f });
        Require(Maximum == 600 && Health == 400, "raw CM health factor is scaled once and retains 200 missing HP");
        for (int i = 0; i < 3; i++)
            Copy(Levels.GetMethod("ApplyHealthMultiplier", All)!).Invoke(null, new object[] { Actor, 1.5f });
        Require(Maximum == 600 && Health == 400, "repeated restoration neither heals nor loses HP");
        // Genuine old/raw follow-up conversions still work outside CM's final writes.
        Actor.SetMaxHealth(100); Actor.SetHealth(80);
        Require(Maximum == 400 && Health == 380, "PoV raw follow-up still converts missing HP");
        Maximum = 400; Health = 200;
        SeedPending(Actor, 400, 1600);
        Character other = New<Character>(); SeedPending(other, 100, 400);
        Copy(Bridge.GetMethod("SetFinalHealth", All)!).Invoke(null, new object[] { Actor, 250f });
        Require(Health == 250 && HasPending(other) && !HasPending(Actor), "only the target pending conversion is cleared");

        Type settingsType = Modifiers.GetNestedType("ReapingSettings", BindingFlags.NonPublic)!;
        object settings = Activator.CreateInstance(settingsType, All, null, new object[] { .1f, 2, .1f, .2f, 0f, 0f, 0f, 0f }, null)!;
        var gain = Copy(Modifiers.GetMethod("ApplyReapingGain", All)!);
        var restore = Copy(Modifiers.GetMethod("ApplyStoredReapingHealth", All)!);
        SetFloat(Data, Key("ReapingBaseMaxHealthKey"), 400);
        SetFloat(Data, Key("ReapingBonusHealthKey"), 0);
        SetFloat(Data, Key("ReapingMaxHealthCapKey"), .2f);
        Maximum = 400; Health = 200;
        SeedPending(Actor, 400, 1600);
        gain.Invoke(null, new[] { Actor, Data, settings });
        Require(Maximum == 440 && Health == 240, "first Reaping gain adds 40 HP and heals 40 without rescaling");
        gain.Invoke(null, new[] { Actor, Data, settings });
        Require(Maximum == 480 && Health == 280, "Reaping reaches cap after second gain");
        Require(!(bool)gain.Invoke(null, new[] { Actor, Data, settings })!, "exhausted capped gain is unchanged");
        for (int i = 0; i < 3; i++)
        {
            Maximum = 400;
            restore.Invoke(null, new object[] { Actor, Data });
            Require(Maximum == 480 && Health == 280, "stored Reaping restore is final HP");
        }
        // PoV's thread-local suppression must unwind even if the actual write fails.
        Writes = 0; ThrowWrite = true;
        try { Copy(Bridge.GetMethod("SetFinalMaxHealth", All)!).Invoke(null, new object[] { Actor, 500f }); }
        catch (TargetInvocationException) { }
        finally { ThrowWrite = false; }
        Require(Writes == 1 && (int)Difficulty.GetField("alreadyScaledMaxHealth", All)!.GetValue(null)! == 0, "exception unwinds PoV scope without retrying the write");
    }

    private static ConfigEntryBase Bind(ConfigFile config, FieldInfo field, string value)
    {
        Type type = field.FieldType.GetGenericArguments()[0];
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        var entry = (ConfigEntryBase)bind.MakeGenericMethod(type).Invoke(config, new[] { "fixture", field.Name, Enum.Parse(type, value), (object)new ConfigDescription("") })!;
        field.SetValue(null, entry); return entry;
    }
    private static List<KeyValuePair<GameObject, int>> Roll() => (List<KeyValuePair<GameObject, int>>)Generate.Invoke(Dropper, null)!;
    private static string[] LootPatchRegistrations()
    {
        Patches patches = Harmony.GetPatchInfo(Generate);
        return patches.Prefixes.Select(p => "pre:" + PatchIdentity(p)).Concat(patches.Postfixes.Select(p => "post:" + PatchIdentity(p)))
            .Concat(patches.Finalizers.Select(p => "final:" + PatchIdentity(p))).Concat(patches.Transpilers.Select(p => "il:" + PatchIdentity(p))).OrderBy(p => p).ToArray();
    }
    private static string PatchIdentity(Patch patch) => $"{patch.owner}:{patch.index}:{patch.priority}:{patch.PatchMethod}:{string.Join(",", patch.before)}:{string.Join(",", patch.after)}";
    private static IEnumerable<CodeInstruction> LootBody() => new[] { new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PovCompatibilityContracts), nameof(LootResult))), new CodeInstruction(OpCodes.Ret) };
    private static List<KeyValuePair<GameObject, int>> LootResult()
    {
        if (ThrowLoot) throw new InvalidOperationException("loot fixture failure");
        return Rows.Select(row => new KeyValuePair<GameObject, int>(row.m_prefab, 1)).ToList();
    }
    private static IEnumerable<CodeInstruction> TrueBody() => new[] { new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Ret) };
    private static IEnumerable<CodeInstruction> HashBody() => new[] { new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ret) };
    private static IEnumerable<CodeInstruction> CharacterBody(MethodBase __originalMethod)
    {
        string method = __originalMethod.Name;
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        if (method.StartsWith("Set", StringComparison.Ordinal)) yield return new CodeInstruction(OpCodes.Ldarg_1);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PovCompatibilityContracts), method));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    private static MethodInfo? Boundary(MethodInfo method)
    {
        string type = method.DeclaringType!.FullName!, name = method.Name;
        string? replacement = type == "UnityEngine.Object" ? name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(Different), "GetInstanceID" => nameof(InstanceId), _ => null }
            : type == "UnityEngine.Time" && name == "get_frameCount" ? nameof(Frame)
            : type == "UnityEngine.Random" && name == "get_value" ? nameof(RandomValue)
            : type == "PassiveTree.EnemyDifficulty" && name == "Owner" ? nameof(TrueCharacter)
            : type == "PassiveTree.CreatureStars" ? name switch { "OwnStars" => nameof(OwnStars), "Of" => nameof(TwoStars), "VanillaLootFactor" => nameof(Four), "Trophy" or "Single" => nameof(FalsePrefab), _ => null }
            : type == "ZDO" && name == "Set" && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(float) }) ? nameof(SetFloat)
            : type == "ZDO" && name == "Set" && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(int) }) ? nameof(SetInt)
            : type == Levels.FullName ? name switch { "IsDungeonCreature" => nameof(TrueCharacter), _ => null }
            : type == Modifiers.FullName ? name switch { "HasModifier" => method.GetParameters()[0].ParameterType == typeof(ZDO) ? nameof(HasReaping) : null,
                "ApplyStoredReapingScale" => nameof(NoScale), "BroadcastReapingFeedback" => nameof(NoFeedback), "ShowReapingHealText" => nameof(NoHealText), _ => null }
            : null;
        return replacement == null ? null : AccessTools.Method(typeof(PovCompatibilityContracts), replacement);
    }
    private static IEnumerable<CodeInstruction> PovBoundaries(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction code in instructions)
        {
            if (code.operand is MethodInfo method)
            {
                if (method.DeclaringType == Difficulty && method.Name == "get_Current") { code.opcode = OpCodes.Ldnull; code.operand = null; }
                else if (Boundary(method) is MethodInfo replacement) { code.opcode = OpCodes.Call; code.operand = replacement; }
            }
            else if (code.operand is ConstructorInfo ctor && ctor.DeclaringType == typeof(HashSet<GameObject>))
            {
                code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(PovCompatibilityContracts), nameof(PrefabSet));
            }
            yield return code;
        }
    }
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo found)) return found;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(PovCompatibilityContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference reference || reference.ResolveReflection() is not MethodInfo method) continue;
            MethodInfo? replacement = Boundary(method);
            if (replacement == null && (method.DeclaringType == Levels || method.DeclaringType == Modifiers || method.DeclaringType == Bridge) && method != source)
                replacement = Copy(method);
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call; instruction.Operand = copy.Module.ImportReference(replacement);
        }
        return Copies[source] = copy.Generate();
    }
    private static void SeedPending(Character c, float raw, float scaled)
    {
        object table = Difficulty.GetField("justScaled", All)!.GetValue(null)!;
        Type valueType = table.GetType().GetGenericArguments()[1];
        object value = Activator.CreateInstance(valueType, true)!;
        valueType.GetField("Raw")!.SetValue(value, raw); valueType.GetField("Scaled")!.SetValue(value, scaled); valueType.GetField("Frame")!.SetValue(value, Frame());
        table.GetType().GetMethod("Remove")!.Invoke(table, new object[] { c });
        table.GetType().GetMethod("Add")!.Invoke(table, new[] { c, value });
    }
    private static bool HasPending(Character c)
    {
        object table = Difficulty.GetField("justScaled", All)!.GetValue(null)!;
        return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, new object?[] { c, null })!;
    }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static string Key(string key) => (string)Modifiers.GetField(key, All)!.GetValue(null)!;
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static bool TrueCharacter(Character c) => true;
    private static bool FalsePrefab(GameObject prefab) => false;
    private static int TwoStars(Character c) => 2;
    private static float Four(Character c) => 4f;
    private static int Frame() => 42;
    private static float RandomValue() => .5f;
    private static float GetMaxHealth(Character c) => Maximum;
    private static float GetHealth(Character c) => Health;
    private static float GetMaxHealthBase(Character c) => 100;
    private static int GetLevel(Character c) => CurrentLevel;
    private static void SetLevel(Character c, int level) { CurrentLevel = level; c.SetMaxHealth(100f * level); }
    private static bool IsPlayer(Character c) => false;
    private static int InstanceId(UnityEngine.Object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    private static bool OwnStars(Character c) => !ForeignLevels(); // rarity enabled, non-player fixture
    private static void SetMaxHealth(Character c, float value) { Writes++; if (ThrowWrite) throw new InvalidOperationException("write failure"); Maximum = value; if (Health > value) c.SetHealth(value); }
    private static void SetHealth(Character c, float value) => Health = value;
    private static void SetFloat(ZDO zdo, string key, float value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static void SetInt(ZDO zdo, string key, int value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static bool HasReaping(ZDO zdo, long mask) => true;
    private static HashSet<GameObject> PrefabSet() => new(new PrefabComparer());
    private sealed class PrefabComparer : IEqualityComparer<GameObject>
    {
        public bool Equals(GameObject? a, GameObject? b) => ReferenceEquals(a, b);
        public int GetHashCode(GameObject obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
    private static bool NoScale(Character c, ZDO zdo) => false;
    private static void NoFeedback(Character c, bool scale) { }
    private static void NoHealText(Character c, float heal) { }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("PoV compatibility: " + message); }
}
