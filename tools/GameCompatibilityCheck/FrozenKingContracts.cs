using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute production method bodies against original game metadata. Only Unity/world boundaries
// are substituted; this does not simulate the actual boss AI, death effects, or network transport.
internal static class FrozenKingContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly HashSet<string> CopiedHelpers = new()
    {
        "IsFrozenKingPhaseTwo", "GetPrefabName", "GetSpawnPolicy", "GetModifierMode", "AreModifiersEnabled",
        "AllowsModifierEffects", "TryGetStatRuleScope", "TryGetCharacterZdo", "TryGetZdo",
        "TryGetDeathwardHealth", "IsDeathwardReady", "ApplyRuntimeModifierStats"
    };
    private static string PrefabName = "";
    private static readonly GameObject Prefab = Uninitialized<GameObject>();
    private static bool Owner = true;
    private static int SpawnSource;
    private static int ReadinessProbes;
    private static int NativeMutations;
    private static int NameReads;
    private static Type Levels = null!;
    private static Type Modifiers = null!;

    internal static void Run(Assembly plugin)
    {
        Levels = plugin.GetType("CreatureManager.CreatureLevelManager", true)!;
        Modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        Type domain = plugin.GetType("CreatureManager.CreatureDomainManager", true)!;
        Character character = Uninitialized<Character>();
        Character source = Uninitialized<Character>();
        ZNetView view = Uninitialized<ZNetView>();
        ZDO zdo = Uninitialized<ZDO>();
        zdo.m_uid = new ZDOID(987654321L, 501u);
        FieldInfo networkView = typeof(Character).GetField("m_nview", Instance)!;
        Require(networkView.IsFamily && networkView.FieldType == typeof(ZNetView), "original protected Character.m_nview");
        networkView.SetValue(character, view);
        FieldInfo viewZdo = typeof(ZNetView).GetField("m_zdo", Instance)!;
        viewZdo.SetValue(view, zdo);

        MethodInfo identity = Method(Levels, "IsFrozenKingPhaseTwo", typeof(Character));
        MethodInfo networkIdentity = Method(Levels, "IsFrozenKingPhaseTwo", typeof(ZDO));
        MethodInfo policy = Method(Levels, "GetSpawnPolicy", typeof(Character));
        MethodInfo effects = Method(Levels, "AllowsModifierEffects", typeof(Character));
        MethodInfo networkEffects = Method(Levels, "AllowsModifierEffects", typeof(ZDO), typeof(bool), typeof(bool));
        MethodInfo health = Method(domain, "ApplyHealthTuple");
        foreach (string prefab in new[] { "FrozenKing_p2", "FrozenKing", "FrozenKing_p3", "Aspect_Eikthyr", "Aspect_Elder",
                     "Aspect_Bonemass", "Aspect_Moder", "Aspect_Yagluth", "Aspect_Fader", "Aspect_SeekerQueen",
                     "FrozenKing_p2_custom", "CustomFrozenKing", "frozenking_p2" })
        {
            SetPrefab(zdo, prefab);
            bool protectedPhase = prefab == "FrozenKing_p2";
            PrefabName = "FrozenKing_p2(Clone)"; // A renamed/custom prefab must use its ZDO identity.
            Require((bool)Call(identity, character)! == protectedPhase, "network identity: " + prefab);
            Require((bool)networkIdentity.Invoke(null, new object[] { zdo })! == protectedPhase, "ZDO-only identity: " + prefab);
            Require((bool)Call(effects, character)! == !protectedPhase, "runtime effects: " + prefab);
            Require((bool)networkEffects.Invoke(null, new object[] { zdo, true, false })! == !protectedPhase, "server effects: " + prefab);
            object result = Call(policy, character)!;
            Require((bool)result.GetType().GetField("RollLevel", Instance)!.GetValue(result)! == !protectedPhase, "natural level policy: " + prefab);
            Require((result.GetType().GetField("StatScope", Instance)!.GetValue(result) == null) == protectedPhase, "stat rule scope: " + prefab);
            character.m_health = 7000f;
            character.m_regenAllHPTime = 3600f;
            Call(health, character, "99000, 1");
            Require(character.m_health == (protectedPhase ? 7000f : 99000f) &&
                    character.m_regenAllHPTime == (protectedPhase ? 3600f : 1f), "prefab health/regen override: " + prefab);
        }
        Require(NameReads == 0, "network path does not fetch/allocate prefab names");
        Require(!(bool)Call(identity, new object?[] { null })!, "missing character");
        Require(!(bool)networkIdentity.Invoke(null, new object?[] { null })!, "missing ZDO");

        viewZdo.SetValue(view, null);
        foreach (string name in new[] { "FrozenKing_p2", "FrozenKing_p2(Clone)", "FrozenKing_p3(Clone)", "CustomFrozenKing(Clone)" })
        {
            PrefabName = name;
            Require((bool)Call(identity, character)! == name.StartsWith("FrozenKing_p2", StringComparison.Ordinal), "pre-Awake/prefab identity: " + name);
        }
        viewZdo.SetValue(view, zdo);
        SetPrefab(zdo, "FrozenKing_p2");
        for (SpawnSource = 0; SpawnSource <= 7; SpawnSource++)
        {
            object result = Call(policy, character)!;
            Require(!(bool)result.GetType().GetField("RollLevel", Instance)!.GetValue(result)! &&
                    result.GetType().GetField("GeneralScope", Instance)!.GetValue(result) == null &&
                    result.GetType().GetField("ModifierMode", Instance)!.GetValue(result)!.ToString() == "Block", "every spawn source stays protected");
        }
        SpawnSource = 0;

        // A protected spawn exits before sync/Karma queries or pending-application bookkeeping.
        Require(!(bool)Call(Method(Levels, "TryApplyLevelState"), character)! && ReadinessProbes == 0, "no level/stat initialization");
        object?[] multiplier = { character, 0f };
        Require(!(bool)Call(Method(Levels, "TrySelectHealthMultiplier"), multiplier)! && (float)multiplier[1]! == 1f, "no inherited health/distance rule");

        // Seed externally supplied runtime state, not a migration fixture. Neither restore APIs nor
        // passive/reaping/deathward application may activate these powers on the protected phase.
        long mask = 0;
        Type maskType = Modifiers.GetNestedType("ModifierMask", BindingFlags.NonPublic)!;
        foreach (string modifier in new[] { "Armored", "Regenerating", "Reaping", "Deathward" })
            mask |= Convert.ToInt64(Enum.Parse(maskType, modifier));
        Set(zdo, "AppliedKey", 1);
        Set(zdo, "Mask64Key", mask);
        Set(zdo, "RegeneratingPowerKey", 1f);
        Set(zdo, "ReapingBaseMaxHealthKey", 7000f);
        Set(zdo, "ReapingBonusHealthKey", 7000f);
        Set(zdo, "DeathwardHealthKey", 0.2f);
        Set(zdo, "DeathwardMaxActivationsKey", 3);
        SetPrefab(zdo, "FrozenKing");
        string snapshot = (string)Call(Method(Modifiers, "CaptureModifierState"), character)!;
        Require(snapshot.Length > 0, "real snapshot produced for ordinary boss");
        object?[] deathward = { character, 0f };
        Require((bool)Call(Method(Modifiers, "TryGetDeathwardHealth"), deathward)! && (float)deathward[1]! == 0.2f, "ordinary boss retains Deathward");
        SetPrefab(zdo, "FrozenKing_p2");
        uint revision = zdo.DataRevision;
        Require(!(bool)Call(Method(Modifiers, "RestoreModifierState"), character, snapshot)!, "snapshot restore rejected");
        Require(!(bool)Call(Method(Modifiers, "InheritModifiers"), source, character)!, "modifier inheritance rejected");
        Require(float.IsNaN((float)Call(Method(Levels, "CaptureStoredHealthDeficit"), character)!), "no CM SetLevel health restoration");

        foreach (bool owner in new[] { true, false })
        {
            Owner = owner;
            Call(Method(Modifiers, "UpdatePassiveModifiers"), character);
            Call(Method(Modifiers, "ApplyRuntimeModifierStats"), character, zdo);
            Call(Method(Modifiers, "RefreshStoredReapingScale"), character);
            float remaining = 7000f; // Verified 1.0.15 resource value, deliberately not a production constant.
            for (int aspect = 0; aspect < 7; aspect++)
            {
                var hit = new HitData { m_damage = new HitData.DamageTypes { m_nonPlayer = 1000f } };
                Require((bool)Call(Method(Modifiers, "ApplyDamageModifiers"), character, hit)!, "vanilla damage handler is allowed");
                float damage = (float)Call(Method(Modifiers, "ResolveFinalDeathwardDamage"), character, hit, hit.m_damage.GetTotalDamage())!;
                Require(damage == 1000f && hit.m_damage.m_nonPlayer == 1000f, "script damage and lethal hit preserved");
                remaining -= damage;
            }
            Require(remaining == 0f, "seven unscaled hits exhaust original health");
        }
        Owner = true;
        object?[] forcedLevel = { character, 2, "" };
        Require(!(bool)Call(Method(Levels, "TryApplyForcedLevel"), forcedLevel)! && ((string)forcedLevel[2]!).Length > 0, "forced high level rejected");
        forcedLevel[1] = 1;
        Require((bool)Call(Method(Levels, "TryApplyForcedLevel"), forcedLevel)!, "level-one command allowed without rewriting health");
        object?[] forcedModifiers = { character, new[] { "Reaping" }, "" };
        Require(!(bool)Call(Method(Modifiers, "TryApplyForcedModifiers"), forcedModifiers)!, "forced modifier rejected");
        forcedModifiers[1] = Array.Empty<string>();
        Require((bool)Call(Method(Modifiers, "TryApplyForcedModifiers"), forcedModifiers)!, "unmodified command allowed");
        Owner = false;
        Require(!(bool)Call(Method(Levels, "TryApplyForcedLevel"), forcedLevel)! &&
                !(bool)Call(Method(Modifiers, "TryApplyForcedModifiers"), forcedModifiers)!, "command owner checks retained");
        Owner = true;
        Require(NativeMutations == 0 && zdo.DataRevision == revision, "no health, level, heal, or ZDO writes");
        Require((string)Call(Method(Modifiers, "CaptureModifierState"), character)! == snapshot, "rejected state stays untouched; no migration");
        System.Console.WriteLine("FrozenKing contracts passed: exact prefab/pre-Awake identity, all spawn sources, health override, modifier restore/inheritance, passive/Reaping, seven unchanged hits/Deathward, and owner-gated commands. Native/world boundaries substituted; no encounter or multiplayer execution.");
    }

    private static void Set<T>(ZDO zdo, string field, T value)
    {
        string key = (string)Modifiers.GetField(field, Static)!.GetRawConstantValue()!;
        if (value is int integer) ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), integer);
        else if (value is long wide) ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), wide);
        else if (value is float single) ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), single);
        else throw new InvalidOperationException("Unsupported fixture value");
    }

    private static void SetPrefab(ZDO zdo, string name) => typeof(ZDO).GetField("m_prefab", Instance)!.SetValue(zdo, name.GetStableHashCode());
    private static MethodInfo Method(Type type, string name, params Type[] parameters) => parameters.Length == 0
        ? type.GetMethod(name, Static)! : type.GetMethod(name, Static, null, parameters, null)!;
    private static object? Call(MethodInfo method, params object?[] args) => Copy(method).Invoke(null, args);

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        // Mono runs a DynamicMethod owner's initializer. The fixture owns these copied bodies,
        // so DomainManager's native MaterialPropertyBlock initializer must not be selected.
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(FrozenKingContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch
            {
                "op_Equality" => nameof(SameObject), "op_Inequality" => nameof(DifferentObject),
                "get_name" => nameof(GetName), "GetInstanceID" => nameof(GetInstanceId), _ => null
            } : type == "UnityEngine.Component" && method.Name == "get_gameObject" ? nameof(GetGameObject)
                : type == "UnityEngine.Time" && (method.Name == "get_time" || method.Name == "get_unscaledTime") ? nameof(NetworkTime)
                : type == "Character" ? method.Name switch
                {
                    "IsPlayer" => nameof(IsPlayer), "IsBoss" => nameof(IsBoss),
                    "SetHealth" or "SetMaxHealth" => nameof(WriteHealth), "Heal" => nameof(Heal),
                    "SetLevel" => nameof(WriteLevel), _ => null
                }
                : type == "ZNetView" && method.Name == "IsOwner" ? nameof(IsOwner)
                : type == "CreatureManager.CreatureKarmaManager" && method.Name == "IsEnforcer" ? nameof(IsEnforcer)
                : type == "CreatureManager.CreatureManagerSpawnLifecycle" && method.Name == "GetSpawnSource" && method.Parameters[0].ParameterType.Name == "Character" ? nameof(GetSource)
                : type == "CreatureManager.CreatureDomainManager" && method.Name == "IsSynchronizedConfigurationReady" ? nameof(ConfigurationReady)
                : type == "CreatureManager.CreatureModifierManager" && method.Name == "GetNetworkTimeSeconds" ? nameof(NetworkTime)
                : type == "HitData" && method.Name == "GetAttacker" ? nameof(GetAttacker)
                : null;
            MethodInfo? replacement = boundary == null ? null : Method(typeof(FrozenKingContracts), boundary);
            if (replacement == null && type.StartsWith("CreatureManager.", StringComparison.Ordinal) && CopiedHelpers.Contains(method.Name))
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
    private static string GetName(UnityEngine.Object value) { NameReads++; return PrefabName; }
    private static GameObject GetGameObject(Component value) => Prefab;
    private static int GetInstanceId(UnityEngine.Object value) => 501;
    private static bool IsPlayer(Character value) => false;
    private static bool IsBoss(Character value) => true;
    private static bool IsEnforcer(Character value) => false;
    private static int GetSource(Character value) => SpawnSource;
    private static bool IsOwner(ZNetView value) => Owner;
    private static bool ConfigurationReady() { ReadinessProbes++; return false; }
    private static float NetworkTime() => 0f;
    private static Character GetAttacker(HitData hit) => throw new InvalidOperationException("Protected phase consulted attacker scaling");
    private static void WriteHealth(Character value, float health) { NativeMutations++; }
    private static void Heal(Character value, float health, bool showText) { NativeMutations++; }
    private static void WriteLevel(Character value, int level) { NativeMutations++; }
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FrozenKing contract failed: " + label);
    }
}
