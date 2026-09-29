using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Run the compiled level/state/stat logic. Rule inputs and Unity/world/network boundaries
// are substituted; no scene, native Harmony detour, or multiplayer session is executed.
internal static class LevelModeContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static Type Levels = null!;
    private static object Rule = null!;
    private static ZDO CurrentZdo = null!;
    private static int Source, LevelWrites, KarmaQueries, WeightQueries;
    private static uint NextId = 600;
    private static bool Owner = true, Ready = true;
    private static float MaxHealth, Health;
    private static bool Dungeon, Saddle;
    private static int ScaleWrites;
    private static Vector3 LocalScale;
    private static readonly Transform ScaleTransform = Uninitialized<Transform>();
    private static readonly FieldInfo LevelField = typeof(Character).GetField("m_level", Instance)!;

    internal static void Run(Assembly plugin)
    {
        Levels = plugin.GetType("CreatureManager.CreatureLevelManager", true)!;
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        FieldInfo field = entry.GetField("EnableLevelSystem", Static)!;
        object? saved = field.GetValue(null);
        Type modeType = field.FieldType.GetGenericArguments()[0];
        string configPath = Path.Combine(Path.GetTempPath(), "CreatureManager-level-mode-" + Guid.NewGuid() + ".cfg");
        File.WriteAllText(configPath, "[2 - Levels]\nEnable Level System = Vanilla\n");
        var config = new ConfigFile(configPath, false) { SaveOnConfigSet = false };
        var acceptable = (AcceptableValueBase)Activator.CreateInstance(
            entry.GetNestedType("AcceptableLevelSystemModes", BindingFlags.NonPublic)!, true)!;
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        var mode = (ConfigEntryBase)bind.MakeGenericMethod(modeType).Invoke(config,
            new[] { "2 - Levels", "Enable Level System", Enum.Parse(modeType, "Full"), (object)new ConfigDescription("", acceptable) })!;
        Rule = Activator.CreateInstance(plugin.GetType("CreatureManager.LevelDefinition", true)!)!;
        foreach (var pair in new Dictionary<string, float> { ["Health"] = 2f, ["HealthPerLevel"] = 0.5f,
                     ["Damage"] = 3f, ["DamagePerLevel"] = 0.25f, ["ScalePerLevel"] = 0.1f })
            Rule.GetType().GetProperty(pair.Key)!.SetValue(Rule, pair.Value);
        field.SetValue(null, mode);
        try
        {
            Require(mode.GetSerializedValue() == "ExceptLevel", "saved Vanilla loads without falling back to Full");
            Require(mode.DefaultValue.ToString() == "Full", "default remains full application");
            string[] choices = ((IEnumerable)acceptable.GetType().GetProperty("AcceptableValues")!.GetValue(acceptable)!)
                .Cast<object>().Select(v => v.ToString()!).ToArray();
            Require(choices.SequenceEqual(new[] { "Off", "ExceptLevel", "Full" }), "three canonical UI choices");
            foreach (string value in choices)
            {
                mode.SetSerializedValue(value);
                Require(mode.GetSerializedValue() == value, "config round trip: " + value);
                Require((bool)Call("IsLevelSystemEnabled")! == (value != "Off"), "master gate: " + value);
            }
            Require(Convert.ToInt32(Enum.Parse(modeType, "Off")) == 0 && Convert.ToInt32(Enum.Parse(modeType, "On")) == 1 &&
                    Convert.ToInt32(Enum.Parse(modeType, "Vanilla")) == 2, "old enum values retained");
            foreach (var item in new[] { ("0", "Off"), ("1", "Full"), ("2", "ExceptLevel"), ("On", "Full"),
                         ("on", "Full"), ("full", "Full"), ("3", "ExceptLevel"), ("4", "Full"),
                         ("Vanilla", "ExceptLevel"), ("vanilla", "ExceptLevel"), ("exceptlevel", "ExceptLevel") })
            {
                mode.SetSerializedValue(item.Item1);
                Require(mode.GetSerializedValue() == item.Item2, "legacy/case-insensitive config: " + item.Item1);
            }
            mode.BoxedValue = Enum.Parse(modeType, "Vanilla");
            Require(mode.GetSerializedValue() == "ExceptLevel", "boxed legacy value normalizes before runtime use");
            config.Save();
            Require(File.ReadAllText(configPath).Contains("Enable Level System = ExceptLevel"), "save uses canonical mode name");
            mode.SetSerializedValue("Off");
            config.Reload();
            Require(mode.GetSerializedValue() == "ExceptLevel", "saved mode reloads without changing behavior");
            File.WriteAllText(configPath, "[2 - Levels]\nEnable Level System = 2\n");
            config.Reload();
            Require(mode.GetSerializedValue() == "ExceptLevel", "live numeric legacy reload normalizes");

            mode.BoxedValue = Enum.Parse(modeType, "On");
            Require(mode.GetSerializedValue() == "Full", "boxed legacy On normalizes before runtime use");
            mode.SetSerializedValue("Full");
            Character character = NewCharacter(1);
            Require((bool)Call("TryApplyLevelState", character)! && GetLevel(character) == 6 && LevelWrites == 1 &&
                    KarmaQueries == 1 && WeightQueries == 1, "Full still assigns weighted level plus Karma bonus");
            mode.SetSerializedValue("Off");
            character = NewCharacter(3);
            Require(!(bool)Call("TryApplyLevelState", character)! && GetLevel(character) == 3 && MaxHealth == 300f &&
                    !CurrentZdo.GetBool(Key("ProcessingCompleteKey")), "Off does not initialize stats or levels");

            mode.SetSerializedValue("ExceptLevel");
            foreach (int initialLevel in new[] { 1, 3 })
            {
                character = NewCharacter(initialLevel);
                Ready = false;
                Require(!(bool)Call("TryApplyLevelState", character)! && !CurrentZdo.GetBool(Key("AppliedKey")), "wait for synchronized configuration");
                Ready = true;
                Owner = false;
                Require(!(bool)Call("TryApplyLevelState", character)! && !CurrentZdo.GetBool(Key("AppliedKey")), "non-owner cannot initialize");
                Owner = true;
                Require((bool)Call("TryApplyLevelState", character)!, "ExceptLevel initializes owner stats");
                CheckStats(character, initialLevel);
                Require(LevelWrites == 0 && KarmaQueries == 0 && WeightQueries == 0, "ExceptLevel never requests a CM level or Karma bonus");
                object?[] scale = { character, 0f };
                Require((bool)Call("TrySelectScalePerLevel", scale)! && (float)scale[1]! == 0.1f, "scale rule remains eligible");
                Require((bool)Call("AllowsModifierEffects", character)! && (bool)Call("ShouldRollModifiers", character)!, "natural spawn modifiers remain eligible");

                foreach (int externalLevel in new[] { 3, 2, 1, 1 })
                {
                    float deficit = (float)Call("CaptureStoredHealthDeficit", character)!;
                    // Model the game's SetLevel between the existing Harmony prefix/postfix.
                    LevelField.SetValue(character, externalLevel);
                    SetInt(CurrentZdo, "level", externalLevel);
                    MaxHealth = Health = 100f * externalLevel;
                    Call("RestoreConfiguredLevel", character, externalLevel);
                    Call("RestoreStoredHealthDeficit", character, deficit);
                    CheckStats(character, externalLevel);
                }
                Require(LevelWrites == 0, "external levels adopted without recursively overriding SetLevel");
                mode.SetSerializedValue("Full");
                Require((bool)Call("TryApplyLevelState", character)!, "completed creature restores across mode change");
                CheckStats(character, 1);
                Require(LevelWrites == 0 && KarmaQueries == 0 && WeightQueries == 0, "mode change does not reroll saved creature");
                mode.SetSerializedValue("ExceptLevel");
            }

            // Keep lifecycle restrictions independent of the choice of level source.
            character = NewCharacter(1);
            for (Source = 0; Source <= 7; Source++)
            {
                mode.SetSerializedValue("Full");
                object original = Call("GetSpawnPolicy", character)!;
                mode.SetSerializedValue("ExceptLevel");
                object keptLevel = Call("GetSpawnPolicy", character)!;
                Require(original.GetType().GetFields(Instance).All(f => Equals(f.GetValue(original), f.GetValue(keptLevel))), "unchanged spawn policy: " + Source);
                Require(!(bool)Call("ShouldRollLevel", character)!, "no automatic level roll for source: " + Source);
            }
            Source = 0;
            CheckScaleModes(plugin, mode, character);
            CheckDefaultTemplate(plugin);
            typeof(ZDO).GetField("m_prefab", Instance)!.SetValue(CurrentZdo, "FrozenKing_p2".GetStableHashCode());
            Require(!(bool)Call("TryApplyLevelState", character)! && !(bool)Call("AllowsModifierEffects", character)!, "FrozenKing phase two stays protected in ExceptLevel");
        }
        finally
        {
            field.SetValue(null, saved);
            Source = 0;
            Owner = Ready = true;
            Dungeon = Saddle = false;
            File.Delete(configPath);
        }
        System.Console.WriteLine("Level mode contracts passed: three config choices, legacy On/Vanilla/numeric/boxed values, canonical save/reload, weighted Full, disabled Off, ExceptLevel levels 1/3, sync/owner gates, health/damage/distance/scale/modifier eligibility, later level increases/decreases, missing-health preservation, no reroll, spawn policies, FrozenKing guard, native prefab sizes vs explicit overrides, default YAML. Rule inputs and native/world boundaries substituted.");
    }

    private static void CheckScaleModes(Assembly plugin, ConfigEntryBase mode, Character character)
    {
        LevelEffects effects = Uninitialized<LevelEffects>();
        effects.m_levelSetups = new List<LevelEffects.LevelSetup>
        {
            new() { m_scale = 1.23f }, new() { m_scale = 1.7f }
        };
        Type stateType = plugin.GetType("CreatureManager.CreatureLevelEffectsState", true)!;
        object state = FormatterServices.GetUninitializedObject(stateType);
        Vector3 original = new(2f, 3f, 4f);
        stateType.GetProperty("OriginalLocalScale", Instance)!.SetValue(state, original);
        PropertyInfo scaleRule = Rule.GetType().GetProperty("ScalePerLevel")!;
        scaleRule.SetValue(Rule, null);
        mode.SetSerializedValue("ExceptLevel");

        foreach (bool restricted in new[] { false, true })
        {
            // Native sizes bypass CM's dungeon and saddle-size restrictions.
            Dungeon = Saddle = restricted;
            LocalScale = original;
            ScaleWrites = 0;
            Call("ApplyLevelEffectsScale", effects, state, character, 1);
            Require(ScaleWrites == 0 && LocalScale == original, "native level one is untouched");
            Call("ApplyLevelEffectsScale", effects, state, character, 2);
            Require(LocalScale == Vector3.one * 1.23f, "native setup uses absolute scale, not original-scale multiplication");
            Call("ApplyLevelEffectsScale", effects, state, character, 3);
            Call("ApplyLevelEffectsScale", effects, state, character, 3);
            Require(LocalScale == Vector3.one * 1.7f, "native size follows each setup and never compounds");
            int writes = ScaleWrites;
            Call("ApplyLevelEffectsScale", effects, state, character, 4);
            Call("ApplyLevelEffectsScale", effects, state, character, 1);
            Require(ScaleWrites == writes && LocalScale == Vector3.one * 1.7f, "native unsupported/level-one calls leave current size alone");
        }

        Dungeon = Saddle = false;
        scaleRule.SetValue(Rule, 0f);
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Require(LocalScale == original, "explicit zero is not a native-size fallback");
        scaleRule.SetValue(Rule, 0.1f);
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Require(LocalScale == original * 1.2f, "explicit scale uses captured base after native-size application");
        Dungeon = true;
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Require(LocalScale == original, "explicit scale retains dungeon restriction");
        Dungeon = false;
        Saddle = true; // The unset saddle config acts as Off, matching production's existing gate.
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Require(LocalScale == original, "explicit scale retains saddle restriction");
        Saddle = false;

        mode.SetSerializedValue("Full");
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Require(LocalScale == original * 1.2f, "Full retains explicit size formula");
        scaleRule.SetValue(Rule, null);
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Require(LocalScale == original, "Full retains no-growth meaning for omitted scale");
        mode.SetSerializedValue("ExceptLevel");
        LocalScale = original;
        ScaleWrites = 0;
        effects.m_levelSetups.Clear();
        Call("ApplyLevelEffectsScale", effects, state, character, 3);
        Call("ApplyCharacterScaleFallback", character, 3);
        Require(ScaleWrites == 0 && LocalScale == original, "missing prefab setups leave size alone without creating a root scale component");
        scaleRule.SetValue(Rule, 0.1f);
    }

    private static void CheckDefaultTemplate(Assembly plugin)
    {
        Type domain = plugin.GetType("CreatureManager.CreatureDomainManager", true)!;
        string yaml = (string)Copy(domain.GetMethod("BuildDefaultLevelOverrideYaml", Static)!).Invoke(null, Array.Empty<object>())!;
        Type parser = plugin.GetType("CreatureManager.CreatureYaml", true)!;
        object?[] args = { yaml, "level-mode default template", null };
        Require((bool)parser.GetMethod("TryReadLevelDefinitions", Static)!.Invoke(null, args)!, "generated default YAML parses");
        object[] rules = ((IEnumerable)args[2]!).Cast<object>().ToArray();
        object global = rules.Single(r => (string)r.GetType().GetProperty("Target")!.GetValue(r)! == "Global");
        object boss = rules.Single(r => (string)r.GetType().GetProperty("Target")!.GetValue(r)! == "Boss");
        Require(global.GetType().GetProperty("ScalePerLevel")!.GetValue(global) == null &&
                (float)global.GetType().GetProperty("DamagePerLevel")!.GetValue(global)! == 0.25f, "Global omits scale and keeps 0.25 damage growth");
        Require((float)boss.GetType().GetProperty("ScalePerLevel")!.GetValue(boss)! == 0.1f &&
                (float)boss.GetType().GetProperty("DamagePerLevel")!.GetValue(boss)! == 0.1f, "Boss defaults stay unchanged");
    }

    private static void CheckStats(Character character, int level)
    {
        Require(GetLevel(character) == level && CurrentZdo.GetInt(Key("DesiredLevelKey")) == level, "game level retained and tracked");
        Require(Math.Abs(MaxHealth - 300f * (1f + (level - 1) * 0.5f)) < 0.001f && Math.Abs(MaxHealth - Health - 10f) < 0.001f,
            "actual-level health/distance scaling without healing or compounding");
        object?[] damage = { character, 0f };
        Require((bool)Call("TryGetDamageMultiplier", damage)! && Math.Abs((float)damage[1]! - 3.75f * (1f + (level - 1) * 0.25f)) < 0.001f &&
                (bool)Call("ReplacesVanillaLevelDamage", character)!, "actual-level damage/distance scaling and replacement flag");
    }

    private static Character NewCharacter(int level)
    {
        Character character = Uninitialized<Character>();
        ZNetView view = Uninitialized<ZNetView>();
        CurrentZdo = Uninitialized<ZDO>();
        CurrentZdo.m_uid = new ZDOID(987654321L, ++NextId);
        typeof(ZDO).GetField("m_prefab", Instance)!.SetValue(CurrentZdo, "Boar".GetStableHashCode());
        typeof(ZNetView).GetField("m_zdo", Instance)!.SetValue(view, CurrentZdo);
        typeof(Character).GetField("m_nview", Instance)!.SetValue(character, view);
        LevelField.SetValue(character, level);
        SetInt(CurrentZdo, "level", level);
        MaxHealth = 100f * level;
        Health = MaxHealth - 10f;
        Source = LevelWrites = KarmaQueries = WeightQueries = 0;
        return character;
    }

    private static string Key(string field) => (string)Levels.GetField(field, Static)!.GetRawConstantValue()!;
    private static object? Call(string name, params object?[] args)
    {
        MethodInfo method = Levels.GetMethods(Static).Single(m => m.Name == name && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => p.ParameterType.IsByRef || args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(matches => matches));
        return Copy(method).Invoke(null, args);
    }

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(LevelModeContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch
            {
                "op_Equality" => nameof(SameObject), "op_Inequality" => nameof(DifferentObject), "GetInstanceID" => nameof(GetInstanceId), _ => null
            } : type == "UnityEngine.Component" ? method.Name switch
                {
                    "get_gameObject" => nameof(GetGameObject), "get_transform" => nameof(GetTransform), _ => null
                }
                : type == "UnityEngine.Transform" && method.Name == "set_localScale" ? nameof(SetScale)
                : type == "UnityEngine.Random" && method.Name == "Range" ? nameof(RandomRange)
                : type == "Character" ? method.Name switch
                {
                    "IsPlayer" or "IsBoss" or "IsDead" => nameof(FalseCharacter), "GetLevel" => nameof(GetLevel),
                    "GetMaxHealthBase" => nameof(GetBaseHealth), "GetMaxHealth" => nameof(GetMaxHealth), "GetHealth" => nameof(GetHealth),
                    "SetMaxHealth" => nameof(SetMaxHealth), "SetHealth" => nameof(SetHealth), "SetLevel" => nameof(SetLevel),
                    "GetZDOID" => nameof(GetZdoId), _ => null
                }
                : type == "ZNetView" && method.Name == "IsOwner" ? nameof(IsOwner)
                : type == "ZDO" && method.Name == "Set" && method.Parameters[0].ParameterType.FullName == "System.String" ? method.Parameters[1].ParameterType.Name switch
                {
                    "Int32" => nameof(SetInt), "Single" => nameof(SetFloat), "Boolean" => nameof(SetBool), _ => null
                }
                : type == "CreatureManager.CreatureDomainManager" && method.Name == "IsSynchronizedConfigurationReady" ? nameof(ConfigurationReady)
                : type == "CreatureManager.CreatureKarmaManager" && method.Name == "IsEnforcer" ? nameof(FalseCharacter)
                : type == "CreatureManager.CreatureManagerSpawnLifecycle" ? method.Name switch
                {
                    "GetSpawnSource" => nameof(GetSource), "IsManagedSpawn" => nameof(IsManaged), _ => null
                }
                : type == Levels.FullName ? method.Name switch
                {
                    "TrySelectFloatValue" => nameof(SelectFloat), "TrySelectDistanceScalingMultiplier" => nameof(SelectDistance),
                    "TryResolveKarmaBonus" => nameof(KarmaBonus), "TrySelectLevelWeights" => nameof(LevelWeights), "GetPrefabName" => nameof(GetPrefabName),
                    "IsDungeonCreature" => nameof(IsDungeon), "IsSaddleableCreature" => nameof(IsSaddleable), _ => null
                } : null;
            MethodInfo? replacement = boundary == null ? null : typeof(LevelModeContracts).GetMethod(boundary, Static);
            if (replacement == null && (type == Levels.FullName ||
                type == "CreatureManager.CreatureDomainManager" && method.Name.StartsWith("Append", StringComparison.Ordinal)))
                replacement = Copy((MethodInfo)method.ResolveReflection());
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(replacement);
        }
        return Copies[source] = copy.Generate();
    }

    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static bool SameObject(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool DifferentObject(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static int GetInstanceId(UnityEngine.Object value) => (int)NextId;
    private static GameObject GetGameObject(Component value) => null!;
    private static Transform GetTransform(Component value) => ScaleTransform;
    private static void SetScale(Transform transform, Vector3 scale) { LocalScale = scale; ScaleWrites++; }
    private static bool IsDungeon(Character character) => Dungeon;
    private static bool IsSaddleable(Character character) => Saddle;
    private static string GetPrefabName(GameObject value) => "Boar";
    private static bool FalseCharacter(Character character) => false;
    private static bool IsOwner(ZNetView view) => Owner;
    private static bool ConfigurationReady() => Ready;
    private static int GetSource(Character character) => Source;
    private static bool IsManaged(Character character) => true;
    private static int GetLevel(Character character) => (int)LevelField.GetValue(character)!;
    private static ZDOID GetZdoId(Character character) => CurrentZdo.m_uid;
    private static float GetBaseHealth(Character character) => 100f;
    private static float GetMaxHealth(Character character) => MaxHealth;
    private static float GetHealth(Character character) => Health;
    private static void SetMaxHealth(Character character, float value) { MaxHealth = Health = value; }
    private static void SetHealth(Character character, float value) { Health = value; }
    private static void SetLevel(Character character, int level) { LevelWrites++; LevelField.SetValue(character, level); SetInt(CurrentZdo, "level", level); MaxHealth = Health = 100f * level; }
    private static void SetInt(ZDO zdo, string key, int value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static void SetBool(ZDO zdo, string key, bool value) => SetInt(zdo, key, value ? 1 : 0);
    private static void SetFloat(ZDO zdo, string key, float value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static bool SelectFloat(Character character, Delegate selector, out float value, int scope)
    {
        object? result = selector.DynamicInvoke(Rule);
        value = result is float single ? single : 0f;
        return result != null;
    }
    private static bool SelectDistance(Character character, int index, out float multiplier, int scope) { multiplier = index == 1 ? 1.5f : 1.25f; return true; }
    private static bool KarmaBonus(Character character, ZDO zdo, out int bonus) { KarmaQueries++; bonus = 2; return true; }
    private static bool LevelWeights(Character character, out List<float> weights) { WeightQueries++; weights = new List<float> { 0f, 0f, 0f, 1f }; return true; }
    private static float RandomRange(float min, float max) => (min + max) / 2f;
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Level mode contract failed: " + label);
    }
}
