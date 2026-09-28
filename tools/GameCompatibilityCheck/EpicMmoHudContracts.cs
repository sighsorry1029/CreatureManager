using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

// Execute the compiled adapter with native Unity object/layout boundaries substituted.
// The optional EpicMMO DLL is inspected as original metadata, never loaded or patched here.
internal static class EpicMmoHudContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Guid = "WackyMole.EpicMMOSystem";
    private static Type Adapter = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly Dictionary<Transform, List<Transform>> Children = new(ReferenceComparer.Instance);
    private static readonly Dictionary<Transform, Transform?> Parents = new(ReferenceComparer.Instance);
    private static readonly Dictionary<RectTransform, Vector2> Positions = new(ReferenceComparer.Instance);
    private static readonly HashSet<Object> Destroyed = new(ReferenceComparer.Instance);
    private static readonly Character Creature = New<Character>();
    private static readonly ZNet Net = New<ZNet>();
    private static bool Boss, Player, Dedicated;
    private static int Finds, Writes, Assertions;
    private static ConfigEntryBase Option = null!;
    private static ConfigEntry<bool> EpicEnabled = null!;
    private static ConfigEntry<Vector2> EpicPosition = null!;
    private static PluginInfo EpicInfo = null!;

    internal static void Run(Assembly plugin)
    {
        Adapter = plugin.GetType("CreatureManager.CreatureEpicMmoHud", true)!;
        // Object equality/hash initialization requires Unity's native runtime. Substitute
        // only this fixture dictionary comparer; retain the production cache and flow.
        object labels = Adapter.GetField("Labels", Static)!.GetValue(null)!;
        FieldInfo comparerField = labels.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(IEqualityComparer<RectTransform>));
        object? priorComparer = comparerField.GetValue(labels);
        comparerField.SetValue(labels, ReferenceComparer.Instance);
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        FieldInfo optionField = entry.GetField("AdjustEpicMmoLevelBarPosition", Static)!;
        object? priorOption = optionField.GetValue(null);
        Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo? previousPlugin);
        var cfg = new ConfigFile(Path.Combine(Path.GetTempPath(), "CM-EpicHud-" + System.Guid.NewGuid() + ".cfg"), false)
        { SaveOnConfigSet = false };
        Type toggle = optionField.FieldType.GetGenericArguments()[0];
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) &&
            m.GetParameters()[3].ParameterType == typeof(string));
        Option = (ConfigEntryBase)bind.MakeGenericMethod(toggle).Invoke(cfg, new object[] { "Test", "Adjust", Enum.ToObject(toggle, 1), "" })!;
        optionField.SetValue(null, Option);
        EpicEnabled = cfg.Bind("2.Creature level control", "Enabled_creature_level", true);
        EpicPosition = cfg.Bind("2.Creature level control", "LevelBar Position", new Vector2(123f, -45f));
        var epicInstance = New<FakeEpicPlugin>();
        typeof(BaseUnityPlugin).GetField("<Config>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(epicInstance, cfg);
        EpicInfo = (PluginInfo)FormatterServices.GetUninitializedObject(typeof(PluginInfo));
        typeof(PluginInfo).GetProperty("Instance")!.SetValue(EpicInfo, epicInstance);
        try
        {
            CheckAvailability();
            CheckPositionsAndLiveChanges();
            CheckLabelLifetime();
            CheckIntegration(plugin);
            CheckEpicMetadata();
            Require(EpicPosition.Value == new Vector2(123f, -45f), "EpicMMO position config is never changed");
            Require(!File.Exists(cfg.ConfigFilePath), "fixture never writes a config file");
        }
        finally
        {
            Call("Reset");
            comparerField.SetValue(labels, priorComparer);
            optionField.SetValue(null, priorOption);
            if (previousPlugin == null) Chainloader.PluginInfos.Remove(Guid);
            else Chainloader.PluginInfos[Guid] = previousPlugin;
        }
        System.Console.WriteLine($"EpicMMO HUD: {Assertions} checks passed. Config/role gates, native and fallback coordinates, live restore, cached lookup and HUD lifecycle; Unity boundaries substituted. No scene or network session.");
    }

    private static void CheckAvailability()
    {
        Reset();
        Chainloader.PluginInfos.Remove(Guid);
        Require(!Begin(), "missing optional mod is inert");
        Chainloader.PluginInfos[Guid] = EpicInfo;
        Require(!Begin(), "availability is not searched every frame");
        Call("Reset");
        Require(Begin(), "HUD/world reset reprobes the installed optional mod");
        Call("EndUpdate");
        Dedicated = true;
        Require(!Begin(), "dedicated server never adjusts UI");
        Dedicated = false;
        EpicEnabled.Value = false;
        Require(!Begin(), "EpicMMO level control Off is respected");
        EpicEnabled.Value = true;
        SetOption(false);
        Require(!Begin(), "CM option Off is inert");
    }

    private static void CheckPositionsAndLiveChanges()
    {
        Reset();
        RectTransform name = Name(), label = AddLabel(name, new Vector2(40f, -30f));
        Frame(name);
        Require(At(label, 70, -15), "normal creation uses requested position");
        int found = Finds, written = Writes;
        for (int i = 0; i < 1000; i++) Frame(name);
        Require(Finds == found && Writes == written, "stable 1000 updates add no Find or position writes");
        SetOption(false);
        Require(!Begin() && At(label, 40, -30) && Count() == 0, "live Off restores original and releases references");
        SetOption(true);
        Frame(name);
        EpicEnabled.Value = false;
        Require(!Begin() && At(label, 40, -30), "EpicMMO live Off also restores");
        EpicEnabled.Value = true;
        Frame(name);
        Positions[label] = new Vector2(9f, 11f);
        Frame(name);
        SetOption(false);
        Require(!Begin() && At(label, 9, 11), "restore latest external layout before adjustment");
        SetOption(true);
        Frame(name);
        Positions[label] = new Vector2(15f, 17f);
        SetOption(false);
        Require(!Begin() && At(label, 15, 17), "do not undo another mod's subsequent change on Off");
        SetOption(true);
        Frame(name);
        Call("Reset");
        Require(At(label, 15, 17) && Count() == 0, "shutdown restores and releases labels");

        foreach (string exclusion in new[] { "boss", "player", "mount" })
        {
            Reset();
            name = Name(); label = AddLabel(name, new Vector2(0f, 30f));
            Boss = exclusion == "boss"; Player = exclusion == "player";
            Frame(name, exclusion == "mount");
            Require(At(label, 0, 30) && Finds == 0 && Count() == 0, exclusion + " HUD untouched");
        }
        Reset();
        name = Name(); label = AddLabel(name, new Vector2(40, -30));
        Frame(name); Boss = true; Frame(name);
        Require(At(label, 40, -30) && Count() == 0, "changed boss classification releases previous adjustment");
    }

    private static void CheckLabelLifetime()
    {
        Reset();
        RectTransform name = Name();
        Frame(name);
        int found = Finds;
        for (int i = 0; i < 20; i++) Frame(name);
        Require(Finds == found, "stable absent label is not searched each frame");
        RectTransform label = AddLabel(name, new Vector2(37, -30));
        Frame(name);
        Require(At(label, 70, -15), "EpicMMO fallback creation is detected after initial miss");
        Destroyed.Add(label);
        Children[name].Clear();
        RectTransform replacement = AddLabel(name, new Vector2(80, -5));
        Frame(name);
        Require(At(replacement, 70, -15), "same-count replacement of a destroyed label is rediscovered");
        SetOption(false);
        Require(!Begin() && At(replacement, 80, -5), "replacement keeps its own restore coordinate");
        SetOption(true);
        Frame(name);
        Begin(); Call("EndUpdate");
        Require(At(replacement, 80, -5) && Count() == 0, "departed HUD is restored and removed without world reset");

        Reset();
        name = Name(); label = AddLabel(name, new Vector2(37, -30));
        Frame(name);
        SetOption(false);
        Require(!Begin() && At(label, 37, -30), "Off preserves EpicMMO fallback behavior, not its unrelated config value");
        SetOption(true);
        Frame(name);
        Children[name].Clear(); Parents[label] = Name();
        Frame(name);
        Require(At(label, 37, -30), "reparented label is restored and no longer managed");
        replacement = AddLabel(name, new Vector2(40, -30));
        Frame(name);
        Require(At(replacement, 70, -15), "new child after reparent is discovered");
        Destroyed.Add(name); Destroyed.Add(replacement);
        Begin(); Call("EndUpdate");
        Require(Count() == 0, "destroyed HUD references do not accumulate");
    }

    private static void CheckIntegration(Assembly plugin)
    {
        using var module = ModuleDefinition.ReadModule(plugin.Location);
        TypeDefinition entry = module.GetType("CreatureManager.CreatureManagerPlugin");
        var instructions = entry.Methods.Single(m => m.Name == "Awake").Body.Instructions;
        int key = instructions.ToList().FindIndex(i => Equals(i.Operand, "Adjust EpicMMO LevelBar Position"));
        var bind = instructions.Skip(key).First(i => i.Operand is MethodReference m && m.Name == "config");
        Require(key >= 0 && instructions[key + 1].OpCode == Mono.Cecil.Cil.OpCodes.Ldc_I4_1 &&
                bind.Previous.OpCode == Mono.Cecil.Cil.OpCodes.Ldc_I4_1, "default On and synchronized binding");
        TypeDefinition patch = module.GetType("CreatureManager.CreatureManagerEnemyHudUpdateHudsPatch");
        var after = patch.Methods.Single(m => m.Name == "Postfix").CustomAttributes.Single(a => a.AttributeType.Name == "HarmonyAfter");
        Require(((CustomAttributeArgument[])after.ConstructorArguments[0].Value).Any(a => Equals(a.Value, Guid)),
            "CM HUD update runs after EpicMMO's patch owner");
        var update = module.GetType("CreatureManager.CreatureModifierManager").Methods.Single(m => m.Name == "UpdateEnemyHuds");
        Require(update.Body.ExceptionHandlers.Any(h => h.HandlerType == Mono.Cecil.Cil.ExceptionHandlerType.Finally),
            "cache cleanup runs even when the existing HUD update throws");
        foreach (var source in new[] {
                     entry.Methods.Single(m => m.Name == "CleanupRuntime"),
                     module.GetType("CreatureManager.CreatureManagerEnemyHudDestroyPatch").Methods.Single(m => m.Name == "Prefix"),
                     module.GetType("CreatureManager.CreatureManagerZNetSceneOnDestroyPatch").Methods.Single(m => m.Name == "Prefix") })
            Require(source.Body.Instructions.Any(i => i.Operand is MethodReference m &&
                m.DeclaringType.FullName == Adapter.FullName && m.Name == "Reset"), "cleanup hook: " + source.DeclaringType.Name);
        Require(!module.AssemblyReferences.Any(r => r.Name == "EpicMMOSystem"), "no hard EpicMMO assembly dependency");
        Require(!module.GetType(Adapter.FullName).Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Any(i => i.Operand is MethodReference m && (m.Name == "set_Value" || m.Name == "Save")), "adapter never edits external config");
    }

    private static void CheckEpicMetadata()
    {
        string? path = Environment.GetEnvironmentVariable("CREATUREMANAGER_CHECK_EPIC_MMO");
        if (string.IsNullOrEmpty(path)) { System.Console.WriteLine("Optional EpicMMO binary inspection skipped (set CREATUREMANAGER_CHECK_EPIC_MMO)."); return; }
        using var module = ModuleDefinition.ReadModule(path);
        var epic = module.GetType("EpicMMOSystem.EpicMMOSystem");
        var strings = epic.Methods.Single(m => m.Name == "Awake").Body.Instructions.Select(i => i.Operand).OfType<string>().ToArray();
        Require(strings.Contains("2.Creature level control") && strings.Contains("Enabled_creature_level") &&
                strings.Contains("LevelBar Position"), "original EpicMMO config keys");
        var hud = module.GetType("EpicMMOSystem.DataMonsters").NestedTypes.Single(t => t.Name == "MonsterColorTexts");
        var normal = hud.Methods.Single(m => m.Name == "Postfix").Body.Instructions;
        var fallback = hud.NestedTypes.Single(t => t.Name == "StarVisibilityMMO").Methods.Single(m => m.Name == "Postfix").Body.Instructions;
        Require(normal.Any(i => i.Operand is FieldReference f && f.Name == "MobLevelPosition") &&
                normal.Any(i => Equals(i.Operand, "Name/Name(Clone)")), "original normal label path");
        Require(fallback.Any(i => Equals(i.Operand, "Name/Name(Clone)")) &&
                fallback.Any(i => i.Operand is float v && v == 37f), "original fallback uses the same label path and fixed X=37");
        System.Console.WriteLine("Inspected original optional EpicMMO binary: " + module.Assembly.Name.FullName);
    }

    private static void Reset()
    {
        Call("Reset");
        Children.Clear(); Parents.Clear(); Positions.Clear(); Destroyed.Clear();
        Boss = Player = Dedicated = false; Finds = Writes = 0;
        Chainloader.PluginInfos[Guid] = EpicInfo;
        EpicEnabled.Value = true; SetOption(true);
    }
    private static RectTransform Name() { var name = New<RectTransform>(); Children[name] = new List<Transform>(); return name; }
    private static RectTransform AddLabel(RectTransform name, Vector2 position)
    {
        var label = New<RectTransform>(); Children[name].Add(label); Parents[label] = name; Positions[label] = position; return label;
    }
    private static void Frame(RectTransform name, bool mount = false)
    {
        if (!Begin()) throw new InvalidOperationException("Expected active adapter.");
        Call("Update", Creature, name, mount); Call("EndUpdate");
    }
    private static bool Begin() => (bool)Call("BeginUpdate")!;
    private static bool At(RectTransform label, float x, float y) => Positions[label] == new Vector2(x, y);
    private static int Count() => ((IDictionary)Adapter.GetField("Labels", Static)!.GetValue(null)!).Count;
    private static void SetOption(bool on) => Option.BoxedValue = Enum.ToObject(Option.SettingType, on ? 1 : 0);
    private static object? Call(string name, params object?[] args) => Copy(Adapter.GetMethod(name, Static)!).Invoke(null, args);

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (!(instruction.Operand is MethodReference m)) continue;
            string? boundary = m.DeclaringType.FullName switch
            {
                "UnityEngine.Object" => m.Name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(NotEqual), _ => null },
                "UnityEngine.Transform" => m.Name switch { "get_parent" => nameof(Parent), "get_childCount" => nameof(ChildCount), "Find" => nameof(Find), _ => null },
                "UnityEngine.RectTransform" => m.Name switch { "get_anchoredPosition" => nameof(GetPosition), "set_anchoredPosition" => nameof(SetPosition), _ => null },
                "Character" => m.Name switch { "IsPlayer" => nameof(IsPlayer), "IsBoss" => nameof(IsBoss), _ => null },
                "ZNet" => m.Name switch { "get_instance" => nameof(Network), "IsDedicated" => nameof(IsDedicated), _ => null },
                _ => null
            };
            MethodInfo? target = boundary == null ? null : typeof(EpicMmoHudContracts).GetMethod(boundary, Static);
            if (target == null && m.DeclaringType.FullName == Adapter.FullName) target = Copy((MethodInfo)m.ResolveReflection());
            if (target == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(target);
        }
        return Copies[source] = copy.Generate();
    }

    private sealed class FakeEpicPlugin : BaseUnityPlugin { }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private sealed class ReferenceComparer : IEqualityComparer<Object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public bool Equals(Object? a, Object? b) => ReferenceEquals(a, b);
        public int GetHashCode(Object value) => RuntimeHelpers.GetHashCode(value);
    }
    private static bool Equal(Object? a, Object? b) => ReferenceEquals(a, b) ||
        ((ReferenceEquals(a, null) || Destroyed.Contains(a)) && (ReferenceEquals(b, null) || Destroyed.Contains(b)));
    private static bool NotEqual(Object? a, Object? b) => !Equal(a, b);
    private static Transform? Parent(Transform t) => Parents.TryGetValue(t, out Transform? parent) ? parent : null;
    private static int ChildCount(Transform t) => Children[t].Count;
    private static Transform? Find(Transform t, string name) { Finds++; return name == "Name(Clone)" ? Children[t].FirstOrDefault(c => !Destroyed.Contains(c)) : null; }
    private static Vector2 GetPosition(RectTransform t) => Positions[t];
    private static void SetPosition(RectTransform t, Vector2 p) { Writes++; Positions[t] = p; }
    private static bool IsPlayer(Character _) => Player;
    private static bool IsBoss(Character _) => Boss;
    private static ZNet Network() => Net;
    private static bool IsDedicated(ZNet _) => Dedicated;
    private static void Require(bool condition, string why)
    {
        Assertions++;
        if (!condition) throw new InvalidOperationException("EpicMMO HUD: " + why);
    }
}
