using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Run the built plugin's bodies, substituting only scene identity/health/source lookup.
// No second implementation of damage attribution and no live scene/network are used.
internal static class DotAttributionContracts
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static Type Modifiers = null!;
    private static Character Target = null!;
    private static Character PlayerSource = null!;
    private static Character Attacker = null!;
    private static float Health;
    private static int IdentityReads;
    private static int Checks;

    internal static void Run(Assembly plugin)
    {
        Modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        Type evidence = Modifiers.GetNestedType("DelayedDamageAttribution", All)!;
        object Source(uint id, bool player = false, bool side = false) => evidence.GetMethod("FromSource", All)!
            .Invoke(null, new object[] { new ZDOID(34567L, id), player, side })!;
        object Merge(object a, object b) => Invoke("MergeDelayedDamageAttribution", a, b)!;
        ZDOID Reward(object e) => (ZDOID)Property(e, "RewardSource")!;
        bool Exact(object e) => (bool)Property(e, "IsExact")!;
        object a = Source(1, true), b = Source(2, true), pet = Source(3, side: true), wild = Source(4);
        object unknown = Activator.CreateInstance(evidence)!;
        object mixed = Merge(a, b);
        Require(Exact(Merge(a, a)) && Reward(Merge(a, a)) == Reward(a), "same source remains exact");
        Require(!Exact(mixed) && Reward(mixed) == Reward(a), "two players keep first confirmed contributor, not an exact killer");
        Require(Reward(Merge(a, wild)) == Reward(a) && Reward(Merge(unknown, a)) == Reward(a), "hostile/unknown mixing retains known player contribution");
        Require(Reward(Merge(pet, a)) == Reward(a) && Reward(Merge(a, pet)) == Reward(a), "actual player has priority over summon");
        Require(Reward(Merge(pet, wild)) == Reward(pet) && !(bool)Property(Merge(pet, wild), "RewardSourceWasPlayer")!, "summon-only mixture is not a player/Omen candidate");
        Require(Reward(Merge(unknown, wild)) == ZDOID.None, "unknown/hostile-only mixture has no candidate");
        Require(Reward(Invoke("CombineDelayedDamageTickAttribution", a, true, b, true)!) == Reward(a), "simultaneous fire/spirit uses stable fire-first candidate");
        Require(Reward(Invoke("CombineDelayedDamageTickAttribution", pet, true, a, true)!) == Reward(a), "actual spirit player takes priority over fire summon");
        Require(Reward(Invoke("CombineDelayedDamageTickAttribution", wild, true, a, false)!) == ZDOID.None, "inactive spirit cannot grant a fire kill");

        Target = Uninitialized<Character>();
        PlayerSource = Uninitialized<Character>();
        Character hostile = Uninitialized<Character>();
        hostile.m_faction = Character.Faction.ForestMonsters;
        Character summon = Uninitialized<Character>();
        summon.m_faction = Character.Faction.PlayerSpawned;
        SE_Poison poison = Uninitialized<SE_Poison>(); poison.m_character = Target;
        SE_Burning fire = Uninitialized<SE_Burning>(); fire.m_character = Target;
        Health = 100f;
        var poisonHit = new HitData { m_hitType = HitData.HitType.Poisoned };
        poisonHit.m_damage.m_poison = 5f;
        var fireHit = new HitData { m_hitType = HitData.HitType.Burning };
        fireHit.m_damage.m_fire = 5f;
        try
        {
            void Record(Character source, uint id, string method, StatusEffect status, params object[] rest)
            {
                Attacker = source;
                FieldInfo contextField = Modifiers.GetField("CurrentRpcDamageContext", All)!;
                object previous = contextField.GetValue(null)!;
                object context = Activator.CreateInstance(contextField.FieldType)!;
                contextField.FieldType.GetField("Target", All)!.SetValue(context, Target);
                contextField.FieldType.GetField("Hit", All)!.SetValue(context, new HitData { m_attacker = new ZDOID(34567L, id) });
                contextField.SetValue(null, context);
                try { Invoke(method, new object[] { status }.Concat(rest).ToArray()); }
                finally { contextField.SetValue(null, previous); }
            }
            object Tick(StatusEffect status, HitData hit)
            {
                object scope = Invoke(status is SE_Poison ? "BeginPoisonDamageTick" : "BeginBurningDamageTick", status)!;
                try { return Invoke("GetCurrentDelayedDamageTickAttribution", Target, hit)!; }
                finally { Invoke("EndDelayedDamageTick", scope); }
            }
            Record(PlayerSource, 1, "RecordPoisonDamageSource", poison, true);
            Record(hostile, 4, "RecordPoisonDamageSource", poison, false);
            Require(Reward(Tick(poison, poisonHit)) == Reward(a), "unaccepted poison leaves current source unchanged");
            Record(hostile, 4, "RecordPoisonDamageSource", poison, true);
            Require(Reward(Tick(poison, poisonHit)) == ZDOID.None, "accepted replacement poison forgets past contribution");
            Record(summon, 3, "RecordPoisonDamageSource", poison, true);
            typeof(SE_Poison).GetField("m_damageLeft", All)!.SetValue(poison, 0f);
            Require(Reward(Tick(poison, poisonHit)) == Reward(pet), "PlayerSpawned qualifies on last poison tick, including zero remainder");
            Record(PlayerSource, 1, "RecordFireDamageSource", fire, true, true);
            Record(hostile, 4, "RecordFireDamageSource", fire, false, true);
            object frozen = Tick(fire, fireHit);
            Require(!Exact(frozen) && Reward(frozen) == Reward(a), "real accumulated pool retains contributor separately from exact source");
            Record(hostile, 4, "RecordFireDamageSource", fire, true, true);
            Require(Reward(Tick(fire, fireHit)) == ZDOID.None, "restarted fire pool clears previous contributors");
            Record(PlayerSource, 1, "RecordSpiritDamageSource", fire, true, true);
            Require(Reward(Tick(fire, fireHit)) == ZDOID.None, "fire-only hit cannot borrow another spirit pool");
            SE_Burning replacement = Uninitialized<SE_Burning>(); replacement.m_character = Target;
            Require(Reward(Tick(replacement, fireHit)) == ZDOID.None, "new status object cannot inherit old evidence");

            IdentityReads = 0;
            for (int i = 0; i < 100; i++) Invoke("EndDelayedDamageTick", Invoke("BeginPoisonDamageTick", poison)!);
            Require(IdentityReads == 0, "100 status updates without damage perform zero ledger lookups");
            Tick(poison, poisonHit);
            Require(IdentityReads == 1, "an actual tick performs one ledger lookup");
            Type stateType = Modifiers.GetNestedType("ApplyDamageState", All)!;
            object state = Activator.CreateInstance(stateType, All, null, new object[] {
                Activator.CreateInstance(Modifiers.GetNestedType("DirectDamageState", All)!)!, true, 100f, true, frozen }, null)!;
            Health = 0f;
            typeof(Character).GetField("m_lastHit", All)!.SetValue(Target, fireHit);
            Invoke("CaptureDelayedDamageDeathCredit", Target, fireHit, state);
            object final = Invoke("CaptureFinalDeathAttribution", Target)!;
            Require((bool)Property(final, "HasRewardSource")! && (ZDOID)Property(final, "RewardSource")! == Reward(a),
                "frozen mixed lethal snapshot retains Karma/Omen reward source");
            // A later tick with no living->dead transition cannot steal the first credit.
            object late = Activator.CreateInstance(stateType, All, null, new object[] {
                Activator.CreateInstance(Modifiers.GetNestedType("DirectDamageState", All)!)!, true, 0f, true, wild }, null)!;
            Invoke("CaptureDelayedDamageDeathCredit", Target, fireHit, late);
            Require((ZDOID)Property(Invoke("CaptureFinalDeathAttribution", Target)!, "RewardSource")! == Reward(a), "later dead-body tick cannot overwrite lethal contribution");
            Health = 100f;
            Invoke("ClearRecoveredDelayedDamageDeathCredit", Target);
            Require(((IDictionary)Modifiers.GetField("PendingDelayedDamageDeathCredits", All)!.GetValue(null)!).Count == 0, "recovery clears lethal evidence");
            Health = 0f;
            object exactFallback = Activator.CreateInstance(stateType, All, null, new object[] {
                Activator.CreateInstance(Modifiers.GetNestedType("DirectDamageState", All)!)!, true, 100f, true, wild }, null)!;
            Invoke("CaptureDelayedDamageDeathCredit", Target, fireHit, exactFallback);
            Require((ZDOID)Property(Invoke("CaptureFinalDeathAttribution", Target)!, "RewardSource")! == new ZDOID(34567L, 4),
                "exact source without a reward contributor remains available for server-side classification");
            Invoke("ClearDelayedDamageTracking", Target);
            Require(((IDictionary)Modifiers.GetField("DelayedDamageSourceLedgers", All)!.GetValue(null)!).Count == 0, "death/destroy cleanup releases source ledger");

            using var entry = new DynamicMethodDefinition(Modifiers.GetMethod("BeginApplyDamage", All)!);
            string[] calls = entry.Definition.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().Select(m => m.Name).ToArray();
            int snapshot = Array.IndexOf(calls, "GetCurrentDelayedDamageTickAttribution");
            int callbacks = Array.IndexOf(calls, "BeginDirectDamage");
            Require(snapshot >= 0 && callbacks >= 0 && snapshot < callbacks, "production entry snapshots before nested modifier callbacks");
            using var karma = new DynamicMethodDefinition(plugin.GetType("CreatureManager.CreatureKarmaManager", true)!.GetMethods(All)
                .Single(m => m.Name == "RecordDeath" && m.GetParameters().Length == 2));
            Require(karma.Definition.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_RewardSource") &&
                    !karma.Definition.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_Source"), "Karma reads reward contribution rather than exact source");
            foreach (string name in new[] { "HandleDeath", "HandlePlayerDeath" })
            {
                using var reaping = new DynamicMethodDefinition(Modifiers.GetMethod(name, All)!);
                Require(!reaping.Definition.Body.Instructions.Any(i => i.Operand is MethodReference m &&
                    (m.Name == "get_RewardSource" || m.Name == "get_HasRewardSource")), name + " does not consume relaxed reward evidence");
            }
        }
        finally
        {
            ((IDictionary)Modifiers.GetField("DelayedDamageSourceLedgers", All)!.GetValue(null)!).Clear();
            ((IDictionary)Modifiers.GetField("PendingDelayedDamageDeathCredits", All)!.GetValue(null)!).Clear();
            Copies.Clear();
        }
        System.Console.WriteLine($"DoT attribution: {Checks} checks passed; source replacement/mixing, exact vs reward policy, actual-tick lookup counts, frozen lethal snapshot and cleanup. Native boundaries substituted; no gameplay/network benchmark.");
    }

    private static object? Property(object value, string name) => value.GetType().GetProperty(name, All)!.GetValue(value);
    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static object? Invoke(string name, params object[] args) => Copy(Modifiers.GetMethod(name, All)!).Invoke(null, args);
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(DotAttributionContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch {
                "op_Equality" => nameof(Equal), "op_Inequality" => nameof(Different), "GetInstanceID" => nameof(Identity), _ => null
            } : type == "Character" ? method.Name switch {
                "GetHealth" => nameof(GetHealth), "IsPlayer" => nameof(IsPlayer), "IsTamed" => nameof(IsTamed), "GetFaction" => nameof(Faction), _ => null
            } : type == "HitData" && method.Name == "GetAttacker" ? nameof(GetAttacker) : null;
            MethodInfo? replacement = boundary == null ? null : typeof(DotAttributionContracts).GetMethod(boundary, All);
            if (replacement == null && type == Modifiers.FullName && method.Name != "TryFindCharacter")
                replacement = Copy((MethodInfo)method.ResolveReflection());
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(replacement);
        }
        return Copies[source] = copy.Generate();
    }
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static int Identity(UnityEngine.Object value) { IdentityReads++; return RuntimeHelpers.GetHashCode(value); }
    private static float GetHealth(Character value) => ReferenceEquals(value, Target) ? Health : 100f;
    private static bool IsPlayer(Character value) => ReferenceEquals(value, PlayerSource);
    private static bool IsTamed(Character value) => false;
    private static Character.Faction Faction(Character value) => value.m_faction;
    private static Character GetAttacker(HitData hit) => Attacker;
    private static void Require(bool passed, string message) { Checks++; if (!passed) throw new InvalidOperationException("DoT: " + message); }
}
