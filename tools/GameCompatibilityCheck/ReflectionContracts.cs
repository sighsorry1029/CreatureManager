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

// Execute compiled request/observation/coroutine bodies with real ZDO data and packages.
// Only scene, transport, clock, config readiness and coroutine scheduling are substituted.
internal static class ReflectionContracts
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type Modifiers = null!, Factions = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly Dictionary<ZDOID, ZDO> Zdos = new();
    private static readonly Dictionary<ZDOID, Character> Prefabs = new();
    private static readonly Dictionary<ZDOID, long> Owners = new();
    private static readonly Dictionary<ZDOID, Vector3> Positions = new();
    private static readonly List<IEnumerator> Pending = new();
    private static readonly List<(long Owner, ZDOID Target, string Name, float Damage)> Sent = new();
    private static readonly ZDOMan Manager = New<ZDOMan>();
    private static readonly ZNet Net = New<ZNet>();
    private static readonly ZRoutedRpc Router = New<ZRoutedRpc>();
    private static readonly BaseAI PrefabAi = New<BaseAI>();
    private static bool HasAi = true;
    private static float Now;
    private static bool Effects = true;
    private static int Fx, Checks;
    private static uint NextId = 99000;
    private static long Mask;

    internal static void Run(Assembly plugin)
    {
        Modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        Factions = plugin.GetType("CreatureManager.CreatureFactionManager", true)!;
        Mask = Convert.ToInt64(Enum.Parse(Modifiers.GetNestedType("ModifierMask", All)!, "Reflection"));
        object snapshot = Factions.GetField("ActiveSnapshot", All)!.GetValue(null)!;
        uint epoch = (uint)Modifiers.GetField("ReflectionRequestEpoch", All)!.GetValue(null)!;
        try
        {
            object defaults = Factions.GetMethod("DefaultFactionDefinitions", All)!.Invoke(null, null)!;
            Require((bool)Factions.GetMethod("Load", All)!.Invoke(null, new[] { defaults })!, "load real default faction policy");
            CheckOrderingAndReplay();
            CheckWaitCancellation();
            CheckValidation();
            CheckObservations();
            CheckFactions();
            CheckTransport(plugin);
        }
        finally
        {
            Reset();
            Factions.GetField("ActiveSnapshot", All)!.SetValue(null, snapshot);
            Modifiers.GetField("ReflectionRequestEpoch", All)!.SetValue(null, epoch);
        }
        System.Console.WriteLine($"Reflection network contracts: {Checks} checks passed. Unloaded source/target, first full-health hit, both sync orders, one-shot budget/cap, timeout/replay/owner/reset validation, NPC and faction policies. Compiled RPC/coroutine bodies with scene/transport boundaries substituted; no multiplayer session.");
    }

    private static void CheckOrderingAndReplay()
    {
        foreach (bool syncFirst in new[] { false, true })
        foreach (bool npc in new[] { false, true })
        {
            Reset();
            ZDO source = Add(false, 42, Character.Faction.ForestMonsters);
            ZDO target = Add(!npc, 43, npc ? Character.Faction.Undead : Character.Faction.Players);
            Observe(source); // s_health deliberately absent, as in vanilla's full-health ZDO.
            if (syncFirst) SyncHealth(source, 80f);
            Request(source, target, 1, 2f);
            Require(Pending.Count == 1, "unloaded source and target request admitted");
            if (!syncFirst)
            {
                Require(Step(Pending[0]) && Sent.Count == 0, "request waits for health evidence");
                SyncHealth(source, 80f);
            }
            Require(!Step(Pending[0]), "confirmed first hit completes");
            Require(Sent.Count == 1 && Sent[0].Owner == 43 && Sent[0].Target == target.m_uid &&
                    Sent[0].Damage == 2f && Fx == 1, "exact damage goes to target owner with one FX");
            Require(Dictionary("ServerPendingReflectionRequests").Count == 0 &&
                    Dictionary("ServerPendingReflectionRequestCounts").Count == 0, "completion releases pending counters");
            Request(source, target, 1, 2f);
            Require(Pending.Count == 1, "duplicate request ID rejected before scheduling");
            Request(source, target, 2, 2f);
            Require(Step(Pending[1]), "new ID cannot spend consumed health loss again");
            Now = 3f;
            Require(!Step(Pending[1]) && Sent.Count == 1 && Fx == 1, "replay without fresh damage times out");
        }

        Reset();
        ZDO capped = Add(false, 42, Character.Faction.ForestMonsters), player = Add(true, 43, Character.Faction.Players);
        Set(capped, ZDOVars.s_maxHealth, 2000f); Observe(capped); SyncHealth(capped, 1000f);
        Request(capped, player, 1, 100f);
        Require(!Step(Pending[0]) && Sent.Single().Damage == 25f, "cap applies to approved outgoing damage");
        Require((float)State(capped).GetType().GetField("UnclaimedDamage", All)!.GetValue(State(capped))! == 0f,
            "cap consumes the full original damage evidence");
    }

    private static void CheckWaitCancellation()
    {
        foreach (string reason in new[] { "timeout", "owner", "removed", "reset", "off", "target-dead", "power", "disconnect" })
        {
            Reset(); ZDO source = Add(false, 42, Character.Faction.ForestMonsters), target = Add(true, 43, Character.Faction.Players);
            Observe(source); Request(source, target, 1, 2f);
            Require(Step(Pending[0]), reason + " initially waits");
            if (reason != "timeout") SyncHealth(source, 80f);
            switch (reason)
            {
                case "timeout": Now = 3f; break;
                case "owner": Owners[source.m_uid] = 43; break;
                case "removed": Zdos.Remove(source.m_uid); break;
                case "reset": Modifiers.GetField("ReflectionRequestEpoch", All)!.SetValue(null,
                    (uint)Modifiers.GetField("ReflectionRequestEpoch", All)!.GetValue(null)! + 1); break;
                case "off": Effects = false; break;
                case "target-dead": Set(target, ZDOVars.s_dead, 1); break;
                case "power": Set(source, "CreatureManager_ReflectionPower".GetStableHashCode(), .2f); break;
                case "disconnect": Owners[target.m_uid] = 999; break;
            }
            Require(!Step(Pending[0]) && Sent.Count == 0 && Fx == 0, reason + " cancels without effects");
            Require(Dictionary("ServerPendingReflectionRequests").Count == 0 &&
                    Dictionary("ServerPendingReflectionRequestCounts").Count == 0, reason + " releases reservations");
        }
    }

    private static void CheckValidation()
    {
        foreach (string reason in new[] { "sender", "self", "missing", "prefab", "range", "position", "zero", "nan", "infinite", "amount", "modifier", "chance", "player-source", "friendly" })
        {
            Reset(); ZDO source = Add(false, 42, Character.Faction.ForestMonsters), target = Add(true, 43, Character.Faction.Players);
            long sender = 42; float amount = 2;
            switch (reason)
            {
                case "sender": sender = 43; break;
                case "self": target = source; break;
                case "missing": Zdos.Remove(target.m_uid); break;
                case "prefab": Prefabs.Remove(source.m_uid); break;
                case "range": Positions[target.m_uid] = new Vector3(999f, 0f, 0f); break;
                case "position": Positions[target.m_uid] = new Vector3(float.NaN, 0f, 0f); break;
                case "zero": amount = 0; break;
                case "nan": amount = float.NaN; break;
                case "infinite": amount = float.PositiveInfinity; break;
                case "amount": amount = 11f; break;
                case "modifier": ZDOExtraData.Set(source.m_uid, "CreatureManager_ModifierMask64".GetStableHashCode(), 0L); break;
                case "chance": Set(source, "CreatureManager_ReflectionChance".GetStableHashCode(), 0f); break;
                case "player-source": Prefabs[source.m_uid] = New<Player>(); break;
                case "friendly": Prefabs[source.m_uid].m_faction = Character.Faction.Dverger; break;
            }
            Request(source, target, 1, amount, sender);
            Require(Pending.Count == 0 && Sent.Count == 0 && Fx == 0, reason + " request rejected");
        }
        Call("RPC_ReflectionDamageRequest", 42L, new ZPackage(new byte[] { 0 }));
        Require(Pending.Count == 0, "malformed packet rejected");
    }

    private static void CheckObservations()
    {
        Reset(); ZDO source = Add(false, 42, Character.Faction.ForestMonsters);
        Observe(source); SyncHealth(source, 80f); Observe(source); Observe(source);
        Require(Budget(source) == 20f, "prefix/postfix/maintenance do not double-count");
        Owners[source.m_uid] = 43;
        Observe(source, true); Set(source, ZDOVars.s_health, 60f); Observe(source);
        Require(Budget(source) == 0f, "new owner's snapshot starts baseline without old damage credit");
        SyncHealth(source, 50f);
        Require(Budget(source) == 10f, "new owner can establish fresh evidence");
        Owners[source.m_uid] = 42; Observe(source);
        Require(Budget(source) == 0f, "owner-only update clears old evidence without Deserialize");
        Set(source, ZDOVars.s_health, float.NaN); Observe(source);
        Require(Budget(source) == 0f, "invalid HP never creates credit");

        Reset(); source = Add(false, 42, Character.Faction.ForestMonsters);
        Set(source, ZDOVars.s_health, 60f); Observe(source);
        Require(Budget(source) == 0f, "first already wounded snapshot invents no history");
        SyncHealth(source, 40f);
        Require(Budget(source) == 20f, "next observed hit works after wounded initialization");
        Now = 3f; Observe(source);
        Require(Budget(source) == 0f, "old evidence expires");
        ZDOExtraData.Set(source.m_uid, "CreatureManager_ModifierMask64".GetStableHashCode(), 0L); Observe(source);
        Require(Dictionary("ServerReflectionRequestStates").Count == 0, "modifier removal clears observation");

        Reset(); source = Add(false, 42, Character.Faction.ForestMonsters);
        ZDO target = Add(true, 43, Character.Faction.Players);
        Observe(source); Request(source, target, 1, 2f); Require(Step(Pending[0]), "old owner is waiting");
        Owners[source.m_uid] = 43; Observe(source, true); Set(source, ZDOVars.s_health, 90f); Observe(source);
        Request(source, target, 1, 2f, 43);
        Require(!Step(Pending[0]) && Dictionary("ServerPendingReflectionRequests").Count == 1,
            "old coroutine cannot release a new owner's request with the same ID");
        SyncHealth(source, 70f);
        Require(!Step(Pending[1]) && Sent.Count == 1, "new owner's independent request completes");
    }

    private static void CheckFactions()
    {
        Reset(); ZDO monster = Add(false, 42, Character.Faction.ForestMonsters), player = Add(true, 43, Character.Faction.Players);
        bool Hostile() => (bool)Copy(Factions.GetMethod("IsHostileFromSynchronizedState", All)!).Invoke(null,
            new object[] { monster, Prefabs[monster.m_uid], player, Prefabs[player.m_uid] })!;
        Require(Hostile(), "default hostile faction");
        Set(monster, ZDOVars.s_tamed, 1); Require(!Hostile(), "tamed creature is friendly to player");
        Set(monster, ZDOVars.s_tamed, 0); Prefabs[monster.m_uid].m_faction = Character.Faction.Dverger;
        Require(!Hostile(), "neutral Dverger");
        Set(monster, ZDOVars.s_aggravated, 1); Require(Hostile(), "aggravated Dverger");
        HasAi = false; Require(!Hostile(), "stale aggravated flag without AI cannot change relationships"); HasAi = true;
        ZDOExtraData.Set(monster.m_uid, "faction".GetStableHashCode(), "Players");
        Require(!Hostile(), "saved named faction takes precedence over prefab");
        Prefabs[monster.m_uid].m_group = Prefabs[player.m_uid].m_group = "same";
        Require(!Hostile(), "same group excluded");

        Prefabs[monster.m_uid].m_group = Prefabs[player.m_uid].m_group = "";
        Type definition = Factions.Assembly.GetType("CreatureManager.FactionDefinition", true)!;
        IList definitions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(definition))!;
        foreach (var item in new[] { (Name: "Players", Id: 0), (Name: "Watchers", Id: 100) })
        {
            object value = Activator.CreateInstance(definition)!;
            definition.GetProperty("Faction")!.SetValue(value, item.Name);
            definition.GetProperty("Id")!.SetValue(value, item.Id);
            definition.GetProperty("Friendly")!.SetValue(value, new List<string> { "Players", "Watchers" });
            if (item.Id == 100)
            {
                definition.GetProperty("AggravatedFriendly")!.SetValue(value, new List<string> { "Players" });
                definition.GetProperty("AlertedFriendly")!.SetValue(value, new List<string>());
            }
            definitions.Add(value);
        }
        Require((bool)Factions.GetMethod("Load", All)!.Invoke(null, new object[] { definitions })!, "load custom faction rules");
        ZDOExtraData.Set(monster.m_uid, "faction".GetStableHashCode(), "Watchers");
        Require(!Hostile(), "custom aggravated friendly relationship");
        Set(monster, ZDOVars.s_alert, 1); Require(Hostile(), "custom alerted relationship takes precedence");
        HasAi = false; Require(!Hostile(), "custom alert/aggravation ignored without AI"); HasAi = true;
    }

    private static void CheckTransport(Assembly plugin)
    {
        var send = PatchProcessor.GetOriginalInstructions(Modifiers.GetMethod("SendExactReflectionDamage", All)!).ToList();
        Require(send.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(ZRoutedRpc) && m.Name == "InvokeRoutedRPC") &&
                !send.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(ZNetView) && m.Name == "InvokeRPC"),
            "request uses global routing without a source instance on server");
        Type hook = plugin.GetType("CreatureManager.CreatureManagerReflectionZdoHealthPatch", true)!;
        Require(hook.GetMethod("Prefix", All) != null && hook.GetMethod("Postfix", All) != null, "health hook observes both sides of Deserialize");
    }

    private static ZDO Add(bool player, long owner, Character.Faction faction)
    {
        ZDO zdo = New<ZDO>(); zdo.m_uid = new ZDOID(98765L, ++NextId);
        Character prefab = player ? New<Player>() : New<Character>();
        prefab.m_health = 100f; prefab.m_faction = faction; prefab.m_group = "";
        Zdos[zdo.m_uid] = zdo; Prefabs[zdo.m_uid] = prefab; Owners[zdo.m_uid] = owner; Positions[zdo.m_uid] = Vector3.zero;
        Set(zdo, ZDOVars.s_maxHealth, 100f);
        ZDOExtraData.Set(zdo.m_uid, "CreatureManager_ModifierMask64".GetStableHashCode(), player ? 0L : Mask);
        Set(zdo, "CreatureManager_ReflectionPower".GetStableHashCode(), .1f);
        Set(zdo, "CreatureManager_ReflectionChance".GetStableHashCode(), 100f);
        return zdo;
    }
    private static void Reset()
    {
        foreach (string name in new[] { "ServerReflectionRequestStates", "ServerPendingReflectionRequests",
                     "ServerPendingReflectionRequestCounts", "ServerReflectionNextAllowedTimes" }) Dictionary(name).Clear();
        Zdos.Clear(); Prefabs.Clear(); Owners.Clear(); Positions.Clear(); Pending.Clear(); Sent.Clear(); Now = 0f; Fx = 0; Effects = true; HasAi = true;
    }
    private static void Observe(ZDO zdo, bool before = false) => Call("ObserveSynchronizedReflectionHealth", zdo, before);
    private static void SyncHealth(ZDO zdo, float health) { Observe(zdo, true); Set(zdo, ZDOVars.s_health, health); Observe(zdo); }
    private static object State(ZDO zdo) => Dictionary("ServerReflectionRequestStates")[zdo.m_uid]!;
    private static float Budget(ZDO zdo) => (float)State(zdo).GetType().GetField("UnclaimedDamage", All)!.GetValue(State(zdo))!;
    private static void Request(ZDO source, ZDO target, long id, float amount, long sender = 42)
    {
        ZPackage package = new(); package.Write(source.m_uid); package.Write(id); package.Write(target.m_uid); package.Write(amount);
        package.SetPos(0); Call("RPC_ReflectionDamageRequest", sender, package);
    }
    private static bool Step(IEnumerator iterator) => (bool)Copy(iterator.GetType().GetMethod("MoveNext", All)!).Invoke(null, new object[] { iterator })!;
    private static IDictionary Dictionary(string name) => (IDictionary)Modifiers.GetField(name, All)!.GetValue(null)!;
    private static object? Call(string name, params object[] args) => Copy(Modifiers.GetMethod(name, All)!).Invoke(null, args);
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void Set(ZDO zdo, int key, float value) => ZDOExtraData.Set(zdo.m_uid, key, value);
    private static void Set(ZDO zdo, int key, int value) => ZDOExtraData.Set(zdo.m_uid, key, value);
    private static void Require(bool value, string message) { Checks++; if (!value) throw new InvalidOperationException("Reflection: " + message); }

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo found)) return found;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(ReflectionContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference method) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(Different), _ => null }
                : type == "ZNet" ? method.Name switch { "get_instance" => nameof(GetNet), "IsServer" => nameof(IsServer), _ => null }
                : type == "ZDOMan" ? method.Name switch { "get_instance" => nameof(GetManager), "GetZDO" => nameof(GetZdo), _ => null }
                : type == "ZDO" ? method.Name switch { "GetOwner" => nameof(Owner), "GetPosition" => nameof(Position), _ => null }
                : type == "ZRoutedRpc" ? method.Name switch { "get_instance" => nameof(GetRouter), "InvokeRoutedRPC" => nameof(Send), _ => null }
                : type == "UnityEngine.MonoBehaviour" && method.Name == "StartCoroutine" ? nameof(Start)
                : type == "UnityEngine.Time" && method.Name == "get_realtimeSinceStartup" ? nameof(Time)
                : type == "UnityEngine.Random" && method.Name == "Range" ? nameof(Random)
                : type == "Character" ? method.Name switch { "IsPlayer" => nameof(IsPlayer), "IsBoss" => nameof(IsBoss), _ => null }
                : type == "UnityEngine.Component" && method.Name == "GetComponent" ? nameof(GetAi)
                : type == "CreatureManager.CreatureLevelManager" && method.Name == "AllowsModifierEffects" ? nameof(Allowed)
                : type == Modifiers.FullName ? method.Name switch {
                    "GetReflectionCharacterPrefab" => nameof(Prefab), "IsReflectionPeerAvailable" => nameof(PeerAvailable),
                    "TryFindCharacter" => nameof(Find), "GetNetworkTimeSeconds" => nameof(Time), "GetNetworkTimeSecondsDouble" => nameof(PreciseTime),
                    "PlayReflectionEffects" => nameof(PlayFx), _ => null } : null;
            MethodInfo? replacement = boundary == null ? null : typeof(ReflectionContracts).GetMethod(boundary, All);
            if (replacement == null && method.Name != ".ctor" && (type == Modifiers.FullName || type == Factions.FullName ||
                type.StartsWith(Modifiers.FullName + "/<AuthorizeReflection", StringComparison.Ordinal)))
                replacement = Copy((MethodInfo)method.ResolveReflection());
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(replacement);
        }
        // Use real generated methods: DynamicMethod proxies cannot marshal multiple out
        // arguments when the same validator is called from an iterator on Unity Mono.
        return Copies[source] = DMDGenerator<DMDCecilGenerator>.Generate(copy);
    }
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static ZNet GetNet() => Net;
    private static bool IsServer(ZNet _) => true;
    private static ZDOMan GetManager() => Manager;
    private static ZRoutedRpc GetRouter() => Router;
    private static ZDO? GetZdo(ZDOMan _, ZDOID id) => Zdos.TryGetValue(id, out ZDO value) ? value : null;
    private static long Owner(ZDO zdo) => Owners[zdo.m_uid];
    private static Vector3 Position(ZDO zdo) => Positions[zdo.m_uid];
    private static Character? Prefab(ZDO zdo) => Prefabs.TryGetValue(zdo.m_uid, out Character value) ? value : null;
    private static bool PeerAvailable(long owner) => owner == 42 || owner == 43;
    private static bool Find(ZDOID _, out Character character) { character = null!; return false; }
    private static bool IsPlayer(Character c) => c is Player;
    private static bool IsBoss(Character c) => false;
    private static BaseAI? GetAi(Component c) => HasAi && c is not Player ? PrefabAi : null;
    private static bool Allowed(ZDO _, bool boss, bool enforcer) => Effects;
    private static float Time() => Now;
    private static double PreciseTime() => Now;
    private static float Random(float min, float max) => 0f;
    private static Coroutine Start(MonoBehaviour _, IEnumerator iterator) { Pending.Add(iterator); return null!; }
    private static void PlayFx(ZDO source, ZDO target) => Fx++;
    private static void Send(ZRoutedRpc _, long owner, ZDOID target, string name, params object[] args) =>
        Sent.Add((owner, target, name, (float)args[1]));
}
