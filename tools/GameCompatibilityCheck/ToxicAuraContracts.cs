using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute the compiled aura geometry and damage selection with only native scene/player
// boundaries substituted. This does not simulate Unity particles or a network session.
internal static class ToxicAuraContracts
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type Aura = null!;
    private static Type Modifiers = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly Character Source = New<Character>();
    private static readonly Player Target = New<Player>();
    private static readonly CapsuleCollider Body = New<CapsuleCollider>(), PlayerBody = New<CapsuleCollider>();
    private static readonly Transform SourceTransform = New<Transform>(), PlayerTransform = New<Transform>();
    private static readonly List<Player> Players = new() { Target };
    private static Bounds SourceBounds, TargetBounds;
    private static bool Dead, Teleporting, Enemy, Blocked, Active, ColliderEnabled, Trigger;
    private static int Hits, Casts, Checks, MaskReads, Mask;
    private static Vector3 LastFrom, LastTo;
    private static HitData? LastHit;
    private static readonly ZDO Data = New<ZDO>();
    private static readonly ZNetView View = New<ZNetView>();
    private static bool LevelOn, EffectsOn, Owner, Applied;
    private static float Now;
    private static int Pulses;
    private static float DefaultPower;

    internal static void Run(Assembly plugin)
    {
        Aura = plugin.GetType("CreatureManager.CreatureToxicAura", true)!;
        Modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        DefaultPower = (float)Modifiers.GetField("ToxicAuraDefaultPower", All)!.GetRawConstantValue()!;
        Require(DefaultPower == 0.1f, "runtime default poison strength is 10 percent");
        CheckGeometry();
        CheckDamage();
        CheckSchedule();
        CheckWiring(plugin);
        System.Console.WriteLine($"Toxic Aura: {Checks} checks passed. Live XZ radius, vertical/realm/LOS filtering, no broad character scan, poison payload and authority/lifecycle wiring. Native scene boundaries substituted; no game/network/particle session.");
    }

    private static void CheckSchedule()
    {
        typeof(Character).GetField("m_nview", All)!.SetValue(Source, View);
        Modifiers.GetField("PassiveModifierSchedules", All)!.GetValue(null)!.GetType().GetMethod("Clear")!
            .Invoke(Modifiers.GetField("PassiveModifierSchedules", All)!.GetValue(null), Array.Empty<object>());
        LevelOn = EffectsOn = Owner = Applied = true; Now = 0f; Pulses = 0;
        void Update() => Copy(Modifiers.GetMethod("UpdatePassiveModifiers", All)!).Invoke(null, new object[] { Source });
        Update(); Require(Pulses == 0, "new owner has a full one-second first-tick delay");
        Now = 0.99f; Update(); Require(Pulses == 0, "no early tick");
        Now = 1f; Update(); Update(); Require(Pulses == 1, "one poison application at deadline, not every fixed update");
        Now = 10f; Update(); Require(Pulses == 2, "late update sends one pulse rather than replaying missed ticks");
        Owner = false; Now = 11f; Update(); Require(Pulses == 2, "nonowner never applies poison");
        var active = (HashSet<int>)Modifiers.GetField("ActivePassiveModifierCharacters", All)!.GetValue(null)!;
        active.Add(7);
        Require(!(bool)Copy(Modifiers.GetMethod("NeedsPassiveModifierUpdate", All)!).Invoke(null, new object[] { Source })!,
            "losing ownership cancels owner schedule");
        active.Remove(7);
        Owner = true; Now = 20f; Update(); Require(Pulses == 2, "ownership regain starts a fresh deadline");
        Now = 21f; Update(); Require(Pulses == 3, "new owner resumes after its deadline");
        LevelOn = false; Now = 22f; Update(); Require(Pulses == 3, "Level System Off prevents damage");
        LevelOn = true; EffectsOn = false; Update(); Require(Pulses == 3, "category/P2 effect exclusion prevents damage");
        EffectsOn = true; Applied = false; Update(); Require(Pulses == 3, "unapplied state prevents damage");
        Applied = true; Update(); Require(Pulses == 4, "enabled applied state resumes without a burst");
    }

    private static void CheckGeometry()
    {
        Reset();
        Require(Radius(SourceBounds) == 3.5f, "unit body uses actual radius plus 3m, not sqrt(3) scaling");
        Require(Radius(new Bounds(Vector3.zero, new Vector3(4f, 12f, 6f))) == 6f,
            "nonuniform/large live bounds use the larger XZ radius, not height");
        Require(Radius(new Bounds(Vector3.zero, new Vector3(8f, 2f, 1f))) == 7f,
            "horizontal/rotated body footprint is included");
        Require(float.IsNaN(Radius(new Bounds(Vector3.zero, Vector3.zero))), "disabled/empty bounds cannot poison");
        Require(float.IsNaN(Radius(new Bounds(new Vector3(float.NaN, 0f, 0f), Vector3.one))), "NaN position rejected");
        Require(float.IsNaN(Radius(new Bounds(Vector3.zero, new Vector3(float.PositiveInfinity, 1f, 1f)))), "infinite size rejected");
        TargetBounds.center = new Vector3(3.5f, 1f, 0f);
        Require(Within(), "exact radius edge is included");
        TargetBounds.center = new Vector3(3.51f, 1f, 0f);
        Require(!Within(), "just outside radius is excluded");
        TargetBounds.center = new Vector3(3f, 1f, 3f);
        Require(!Within(), "uses a circle, not an XZ square");
        TargetBounds.center = new Vector3(0f, 10f, 0f);
        Require(!Within(), "same XZ on a distant floor is excluded");
        TargetBounds.center = new Vector3(0f, -10f, 0f);
        Require(!Within(), "same XZ below source is excluded");
        TargetBounds.center = new Vector3(0f, 5f, 0f);
        Require(Within(), "vertical overlap still includes the original 2m padding boundary");
        TargetBounds.center = new Vector3(0f, 5.01f, 0f);
        Require(!Within(), "wider XZ reach does not extend the vertical padding to 3m");
    }

    private static void CheckDamage()
    {
        Reset(); Pulse();
        Require(Hits == 1 && Casts == 1 && LastHit != null && Math.Abs(LastHit.m_damage.m_poison - 20f) < 0.001f,
            "default 10 percent sends 20 poison for a 200-HP player");
        Require(LastHit!.m_attacker == ZDOID.None && Math.Abs(LastHit.m_damage.GetTotalDamage() - 20f) < 0.001f,
            "poison-only payload has no outgoing attacker scaling");
        Require(LastFrom.y == TargetBounds.center.y && LastTo == TargetBounds.center,
            "LOS originates near target height");
        Pulse(); Require(MaskReads == 1 && Hits == 2, "mask is cached but scene selection is repeated");

        foreach (string state in new[] { "far", "floor", "realm", "dead", "teleport", "friendly", "disabled", "trigger", "inactive", "no layers" })
        {
            Reset();
            switch (state)
            {
                case "far": TargetBounds.center = new Vector3(30f, 1f, 0f); break;
                case "floor": TargetBounds.center = new Vector3(0f, 10f, 0f); break;
                case "realm": TargetBounds.center = new Vector3(0f, 5001f, 0f); break;
                case "dead": Dead = true; break;
                case "teleport": Teleporting = true; break;
                case "friendly": Enemy = false; break;
                case "disabled": ColliderEnabled = false; break;
                case "trigger": Trigger = true; break;
                case "inactive": Active = false; break;
                case "no layers": Mask = 0; break;
            }
            Pulse(); Require(Hits == 0 && Casts == 0, "no damage/physics query for " + state);
        }
        Reset(); Blocked = true; Pulse();
        Require(Hits == 0 && Casts == 1, "wall blocks poison after broadphase");
        Blocked = false; Pulse(); Require(Hits == 1, "removed wall is observed next tick");
        Reset(); SourceBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(8f, 20f, 8f));
        TargetBounds.center = new Vector3(5f, 1f, 0f); Pulse();
        Require(Hits == 1 && LastFrom.y == 1f, "large boss aura reaches beyond body without casting over low walls");
        SourceBounds = new Bounds(Vector3.up, new Vector3(1f, 2f, 1f)); Hits = Casts = 0; Pulse();
        Require(Hits == 0 && Casts == 0, "live shrink immediately reduces reach; no stale radius cache");
        Reset(); TargetBounds.center = new Vector3(0f, 4.5f, 0f); Blocked = true; Pulse();
        Require(Hits == 0 && Casts == 1 && LastFrom.y < LastTo.y, "nearby higher floor still needs an unobstructed path");
    }

    private static void CheckWiring(Assembly plugin)
    {
        using var module = ModuleDefinition.ReadModule(plugin.Location);
        var modifiers = module.GetType("CreatureManager.CreatureModifierManager");
        MethodReference[] Calls(MethodDefinition method) => method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
        var tick = modifiers.Methods.Single(m => m.Name == "UpdatePassiveModifiers");
        var calls = Calls(tick);
        Require(Array.FindIndex(calls, m => m.Name == "IsOwner") < Array.FindIndex(calls, m => m.Name == "ApplyPoison") &&
                calls.Any(m => m.Name == "TryGetToxicAuraPower"), "existing owner and eligibility gates precede aura damage");
        Require(!Calls(modifiers.Methods.Single(m => m.Name == "HandleDeath")).Any(m => m.Name == "Damage" || m.Name == "ApplyPoison"),
            "death no longer produces a poison burst");
        foreach (string name in new[] { "GetPassiveModifierSchedule", "IsPassiveModifierUpdateDue", "UpdatePassiveModifiers" })
            Require(modifiers.Methods.Single(m => m.Name == name).Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "NextToxicAura"),
                "shared scheduler owns next aura deadline: " + name);
        Require(Calls(modifiers.Methods.Single(m => m.Name == "UntrackRuntimeModifiers")).Any(m => m.DeclaringType.Name == "CreatureToxicAura" && m.Name == "Forget") &&
                Calls(modifiers.Methods.Single(m => m.Name == "ResetRuntimeState")).Any(m => m.DeclaringType.Name == "CreatureToxicAura" && m.Name == "Reset"),
            "individual and scene teardown clean visuals");
        var aura = module.GetType(Aura.FullName);
        Require(!Calls(aura.Methods.Single(m => m.Name == "LateUpdate")).Any(m => m.Name == "IsOwner"), "nonowners can display the replicated aura");
        Require(Calls(aura.Methods.Single(m => m.Name == "Track")).Any(m => m.Name == "IsDedicated"), "headless server does not attach visuals");
        var factory = module.GetType("CreatureManager.CreaturePrefabRegistry").Methods.Single(m => m.Name == "RegisterToxicAuraEffect");
        Require(!Calls(factory).Any(m => m.Name == "AddComponent" || m.Name == "RegisterPrefab" || m.Name == "RegisterWithZNetScene"),
            "visual template adds no networking/gameplay/lifetime components or network registration");
        Require(!Calls(aura.Methods.Single(m => m.Name == "ApplyPoison")).Any(m => m.Name == "GetAllCharacters" || m.Name == "GetCharactersInRange" || m.Name == "OverlapSphere"),
            "selection uses only existing players, without an all-creature or overlap scan");
        Require(typeof(Character).GetMethod("GetCollider")!.IsPublic && typeof(BaseAI).GetMethod("IsEnemy", new[] { typeof(Character), typeof(Character) })!.IsPublic,
            "new game calls are public on the original DLL");
        // The shared parser runs in both Level/Karma contexts in YamlSyncContractCheck.
        // Inspect this call without starting Karma's Unity-dependent static initializer.
        var karmaReader = module.GetType("CreatureManager.CreatureKarmaManager").Methods.Single(m => m.Name == "TryReadModifierBlock");
        Require(Calls(karmaReader).Any(m => m.DeclaringType.FullName == "CreatureManager.CreatureYaml" && m.Name == "TryReadModifierBlock"),
            "Karma modifier blocks use the shared alias parser");
    }

    private static float Radius(Bounds bounds)
    {
        object[] args = { bounds, 0f };
        return (bool)Call("TryGetRadius", args)! ? (float)args[1] : float.NaN;
    }
    private static bool Within() => (bool)Call("IsWithinReach", SourceBounds, TargetBounds, Radius(SourceBounds))!;
    private static void Pulse() => Call("ApplyPoison", Source, DefaultPower);
    private static object? Call(string name, params object[] args) => Copy(Aura.GetMethod(name, All)!).Invoke(null, args);
    private static void Reset()
    {
        SourceBounds = new Bounds(Vector3.up, new Vector3(1f, 2f, 1f));
        TargetBounds = new Bounds(new Vector3(2f, 1f, 0f), new Vector3(0.5f, 2f, 0.5f));
        Dead = Teleporting = Blocked = Trigger = false;
        Active = ColliderEnabled = Enemy = true;
        Hits = Casts = MaskReads = 0; Mask = 32; LastHit = null;
        Aura.GetField("SolidMask", All)!.SetValue(null, 0);
    }
    private static void Require(bool ok, string message)
    {
        Checks++;
        if (!ok) throw new InvalidOperationException("Toxic Aura: " + message);
    }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo existing)) return existing;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(ToxicAuraContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference method) continue;
            string? boundary = method.DeclaringType.FullName switch
            {
                "UnityEngine.Object" when method.Name == "op_Equality" => nameof(Equal),
                "UnityEngine.Object" when method.Name == "op_Inequality" => nameof(Different),
                "UnityEngine.Object" when method.Name == "GetInstanceID" => nameof(Id),
                "UnityEngine.Time" when method.Name == "get_time" || method.Name == "get_unscaledTime" => nameof(TimeNow),
                "ZNetView" => method.Name switch { "IsOwner" => nameof(IsOwner), "IsValid" => nameof(IsValid), _ => null },
                "ZDO" => method.Name switch { "GetBool" => nameof(GetBool), "GetFloat" => nameof(GetFloat), _ => null },
                "CreatureManager.CreatureLevelManager" => method.Name switch { "IsLevelSystemEnabled" => nameof(LevelEnabled), "AllowsModifierEffects" => nameof(EffectsAllowed), _ => null },
                "CreatureManager.CreatureModifierManager" => method.Name switch
                {
                    "TryGetZdo" => nameof(GetZdo), "GetStoredModifierMask" => nameof(StoredMask),
                    "ApplyRuntimeModifierStats" => nameof(Track), "TrackRuntimeModifiers" => nameof(Track),
                    "UntrackRuntimeModifiers" => nameof(Untrack), "TryGetToxicAuraPower" => nameof(Power), _ => null
                },
                "CreatureManager.CreatureToxicAura" when method.Name == "ApplyPoison" => nameof(PoisonPulse),
                "UnityEngine.Behaviour" when method.Name == "get_isActiveAndEnabled" => nameof(IsActive),
                "UnityEngine.Collider" => method.Name switch { "get_bounds" => nameof(GetBounds), "get_enabled" => nameof(IsEnabled), "get_isTrigger" => nameof(IsTrigger), _ => null },
                "UnityEngine.Component" when method.Name == "get_transform" => nameof(GetTransform),
                "UnityEngine.Transform" when method.Name == "get_position" => nameof(Position),
                "UnityEngine.LayerMask" when method.Name == "GetMask" => nameof(GetMask),
                "UnityEngine.Physics" when method.Name == "Linecast" => nameof(Linecast),
                "BaseAI" when method.Name == "IsEnemy" => nameof(IsEnemy),
                "Character" => method.Name switch { "IsPlayer" => nameof(IsPlayer), "GetCollider" => nameof(GetCollider), "IsDead" => nameof(IsDead), "IsTeleporting" => nameof(IsTeleporting), "GetMaxHealth" => nameof(MaxHealth), "Damage" => nameof(Damage), _ => null },
                "Player" => method.Name switch { "GetAllPlayers" => nameof(AllPlayers), "IsTeleporting" => nameof(IsTeleporting), _ => null },
                _ => null
            };
            if (boundary != null)
            {
                instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
                instruction.Operand = copy.Module.ImportReference(typeof(ToxicAuraContracts).GetMethod(boundary, All)!);
            }
            else if (method.DeclaringType.FullName == Aura.FullName)
                instruction.Operand = copy.Module.ImportReference(Copy(Aura.GetMethod(method.Name, All)!));
            else if (method.DeclaringType.FullName == Modifiers.FullName && new[] { "GetPassiveModifierSchedule", "HasModifier", "ResolveChameleonInterval", "IsPassiveModifierUpdateDue" }.Contains(method.Name))
                instruction.Operand = copy.Module.ImportReference(Copy((MethodInfo)method.ResolveReflection()));
            else if (method.DeclaringType.FullName == "Character" && method.Name == "InInterior")
                instruction.Operand = copy.Module.ImportReference(Copy(typeof(Character).GetMethod("InInterior", new[] { typeof(Vector3) })!));
        }
        return Copies[source] = DMDGenerator<DMDCecilGenerator>.Generate(copy);
    }
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static int Id(UnityEngine.Object _) => 7;
    private static float TimeNow() => Now;
    private static bool IsOwner(ZNetView _) => Owner;
    private static bool IsValid(ZNetView _) => true;
    private static bool IsPlayer(Character _) => false;
    private static bool LevelEnabled() => LevelOn;
    private static bool EffectsAllowed(Character _) => EffectsOn;
    private static bool GetZdo(Character _, out ZDO data) { data = Data; return true; }
    private static bool GetBool(ZDO _, string key, bool fallback) => Applied;
    private static float GetFloat(ZDO _, string key, float fallback) => fallback;
    private static long StoredMask(ZDO _) => 1L << 9;
    private static void Track(Character _, ZDO data) { }
    private static void Untrack(Character _) { }
    private static bool Power(Character _, out float power) { power = DefaultPower; return true; }
    private static void PoisonPulse(Character _, float power) => Pulses++;
    private static bool IsActive(Behaviour _) => Active;
    private static CapsuleCollider GetCollider(Character c) => ReferenceEquals(c, Source) ? Body : PlayerBody;
    private static bool IsEnabled(Collider _) => ColliderEnabled;
    private static bool IsTrigger(Collider _) => Trigger;
    private static Bounds GetBounds(Collider c) => ReferenceEquals(c, Body) ? SourceBounds : TargetBounds;
    private static Transform GetTransform(Component c) => ReferenceEquals(c, Source) ? SourceTransform : PlayerTransform;
    private static Vector3 Position(Transform t) => ReferenceEquals(t, SourceTransform) ? SourceBounds.center : TargetBounds.center;
    private static int GetMask(string[] _) { MaskReads++; return Mask; }
    private static List<Player> AllPlayers() => Players;
    private static bool IsDead(Character _) => Dead;
    private static bool IsTeleporting(Character _) => Teleporting;
    private static bool IsEnemy(Character a, Character b) => Enemy;
    private static float MaxHealth(Character _) => 200f;
    private static void Damage(Character _, HitData hit) { Hits++; LastHit = hit; }
    private static bool Linecast(Vector3 from, Vector3 to, int mask, QueryTriggerInteraction trigger)
    {
        Require(mask == Mask && trigger == QueryTriggerInteraction.Ignore, "only solid, nontrigger obstruction queries");
        Casts++; LastFrom = from; LastTo = to; return Blocked;
    }
}
