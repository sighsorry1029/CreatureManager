using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute the built victim policy and nearby-candidate loop with original ZDO data.
// Substitute native scene queries and the downstream RPC request, not a second policy.
internal static class ReapingContracts
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Modifiers = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly Dictionary<int, Actor> Actors = new();
    private static readonly Dictionary<ZDOID, Character> Loaded = new();
    private static readonly Dictionary<int, Character> Prefabs = new();
    private static readonly Dictionary<int, Character> Colliders = new();
    private static readonly Dictionary<int, Vector3> Positions = new();
    private static readonly List<Collider> Overlaps = new();
    private static readonly List<Character> Requests = new();
    private static readonly Dictionary<ZDOID, ZDO> SyncedZdos = new();
    private static readonly ZDOMan Manager = New<ZDOMan>();
    private static readonly ZNetScene Scene = New<ZNetScene>();
    private static readonly GameObject PrefabObject = New<GameObject>();
    private static Character? QueriedPrefab;
    private static int Checks, Queries, Authorizations;
    private static float Now;
    private static long VictimOwner = 42L;
    private static uint NextId = 93000;

    private sealed class Actor
    {
        internal Character Character = New<Character>();
        internal Transform Transform = New<Transform>();
        internal ZDO Zdo = New<ZDO>();
        internal bool Player, Tamed, Dead, Effects = true, Valid = true, LocalOwner = true;
        internal float Health = 100f;
    }

    internal static void Run(Assembly plugin)
    {
        Modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        try
        {
            CheckVictims();
            CheckNearbyDeaths();
            CheckPlayerDeaths();
            CheckDeathSynchronization();
            CheckAuthorizationLedger();
            CheckRespawnSynchronization();
            CheckCallPaths(plugin);
        }
        finally
        {
            Reset();
            Copies.Clear();
        }
        System.Console.WriteLine($"Reaping: {Checks} checks passed; victim categories, loaded/remote ZDO policy, death/24m boundaries, candidate deduplication and existing authorization/player-death paths. Scene queries and outgoing requests substituted; no gameplay/network session.");
    }

    private static void CheckVictims()
    {
        Reset();
        Actor wild = Add();
        Require(Eligible(wild), "wild creature is eligible");
        wild.Tamed = true;
        Require(Eligible(wild), "loaded tamed creature is eligible");
        wild.Tamed = false;
        wild.Character.m_faction = Character.Faction.PlayerSpawned;
        Require(Eligible(wild), "loaded PlayerSpawned is eligible");
        wild.Character.m_faction = Character.Faction.Players;
        Require(Eligible(wild), "Players faction NPC is not an actual player or PlayerSpawned");
        wild.Character.m_faction = Character.Faction.ForestMonsters;
        Set(wild.Zdo, ZDOVars.s_tamed, 1);
        Require(Eligible(wild), "stored tame state does not exclude a loaded creature");
        Set(wild.Zdo, ZDOVars.s_tamed, 0);
        Set(wild.Zdo, "faction".GetStableHashCode(), (int)Character.Faction.PlayerSpawned);
        Require(Eligible(wild), "numeric runtime faction does not exclude a loaded instance");
        Set(wild.Zdo, "faction".GetStableHashCode(), (int)Character.Faction.ForestMonsters);
        ZDOExtraData.Set(wild.Zdo.m_uid, "faction".GetStableHashCode(), "PlayerSpawned");
        Require(Eligible(wild), "string faction does not exclude a loaded instance");
        ZDOExtraData.Set(wild.Zdo.m_uid, "faction".GetStableHashCode(), "ForestMonsters");
        Require(Eligible(wild), "ordinary registered faction remains eligible");

        Loaded.Remove(wild.Zdo.m_uid);
        Prefabs[wild.Zdo.GetPrefab()] = wild.Character;
        Require(Eligible(wild), "unloaded creature uses known prefab and stored fields");
        Set(wild.Zdo, ZDOVars.s_tamed, 1);
        Require(Eligible(wild), "remote tamed creature is eligible");
        Set(wild.Zdo, ZDOVars.s_tamed, 0);
        ZDOExtraData.Set(wild.Zdo.m_uid, "faction".GetStableHashCode(), "unknown-faction");
        Set(wild.Zdo, "faction".GetStableHashCode(), (int)Character.Faction.PlayerSpawned);
        Require(Eligible(wild), "remote PlayerSpawned is eligible regardless of faction name");
        Prefabs.Clear();
        Require(!Eligible(wild), "missing character prefab fails closed");

        Actor player = Add(player: true);
        Require(Eligible(player), "server accepts player victims");
        Loaded.Remove(player.Zdo.m_uid); Prefabs[player.Zdo.GetPrefab()] = player.Character;
        Require(Eligible(player), "remote player is recognized by its Character prefab");
    }

    private static void CheckNearbyDeaths()
    {
        Reset();
        Actor victim = Add(); victim.Health = 0f;
        Actor near = Reaper(new Vector3(23.9f, 0f, 0f));
        Actor edge = Reaper(new Vector3(24f, 0f, 0f));
        Actor far = Reaper(new Vector3(24.01f, 0f, 0f));
        Actor deadReaper = Reaper(Vector3.zero); deadReaper.Dead = true;
        Actor playerReaper = Reaper(Vector3.zero); playerReaper.Player = true;
        Actor disabled = Reaper(Vector3.zero); disabled.Effects = false;
        Actor invalid = Reaper(Vector3.zero); invalid.Valid = false;
        Actor noModifier = Reaper(Vector3.zero);
        ZDOExtraData.Set(noModifier.Zdo.m_uid, "CreatureManager_ModifierMask64".GetStableHashCode(), 0L);
        Dictionary("ActiveReapingModifierCharacters")[Id(victim.Character)] = victim.Character;
        Dictionary("UnqueryableReapingModifierCharacters")[Id(victim.Character)] = victim.Character;
        AddCollider(near.Character); AddCollider(near.Character); AddCollider(edge.Character);
        // Fallback includes the same actors already returned through multiple colliders.
        Call("ApplyReapingForNearbyDeaths", victim.Character);
        Require(Requests.Count == 2 && Requests.Contains(near.Character, ReferenceComparer.Instance) &&
                Requests.Contains(edge.Character, ReferenceComparer.Instance), "only live enabled Reapers at or inside 24m receive one request each");
        Require(!Dictionary("ActiveReapingModifierCharacters").Contains(Id(deadReaper.Character)) &&
                !Dictionary("ActiveReapingModifierCharacters").Contains(Id(playerReaper.Character)), "stale dead/player candidates are removed after enumeration");
        Require(((IList)Field("ReapingStaleCandidateIds")).Count == 0 &&
                ((Collider[])Field("ReapingOverlapBuffer")).All(c => ReferenceEquals(c, null)), "scratch collider references are cleared");

        foreach (string reason in new[] { "living", "tamed", "summoned", "player" })
        {
            Requests.Clear(); int before = Queries;
            victim.Health = reason == "living" ? 100f : 0f;
            victim.Tamed = reason == "tamed"; victim.Player = reason == "player";
            victim.Character.m_faction = reason == "summoned" ? Character.Faction.PlayerSpawned : Character.Faction.ForestMonsters;
            Call("ApplyReapingForNearbyDeaths", victim.Character);
            Require(reason == "living" ? Requests.Count == 0 && Queries == before : Requests.Count == 2 && Queries == before + 1,
                reason + " victim: only living victims are excluded");
        }
        Requests.Clear();
        victim.Player = victim.Tamed = false; victim.Dead = true; victim.Health = 100f;
        Call("ApplyReapingForNearbyDeaths", victim.Character);
        Require(Requests.Count == 2, "actual death flag permits stale positive health");
        Requests.Clear(); victim.Dead = false; victim.Health = float.NaN;
        Call("ApplyReapingForNearbyDeaths", victim.Character);
        Require(Requests.Count == 0, "missing health is not proof of death");
    }

    private static void CheckAuthorizationLedger()
    {
        // Existing server ledger must continue to deduplicate per Reaper, not globally.
        ZDOID first = new(76543L, 1), second = new(76543L, 2), dead = new(76543L, 3);
        try
        {
            Require((bool)Call("TryAddReapingDeathAuthorization", first, dead)!, "first death authorization succeeds");
            Require(!(bool)Call("TryAddReapingDeathAuthorization", first, dead)!, "repeated delivery is rejected for the same Reaper");
            Require((bool)Call("TryAddReapingDeathAuthorization", second, dead)!, "another nearby Reaper can absorb the same death once");
            Call("ClearReapingDeathAuthorization", dead);
            Require((bool)Call("TryAddReapingDeathAuthorization", first, dead)!, "existing respawn reset permits a new death of that ZDO");
        }
        finally
        {
            Call("ClearReapingDeathAuthorization", dead);
            Call("CompactReapingAuthorizationQueue");
        }
    }

    private static void CheckPlayerDeaths()
    {
        Reset();
        Actor player = Add(player: true); player.Health = 0f;
        Reaper(Vector3.zero); Reaper(new Vector3(24f, 0f, 0f));
        // Player.IsDead reads s_dead, which vanilla sets after this Prefix.
        Require(!player.Dead, "player fixture models pre-OnDeath flag");
        Call("HandlePlayerDeath", player.Character);
        Require(Requests.Count == 2, "owner player at zero health broadcasts without an attacker or dead flag");
        Requests.Clear(); player.LocalOwner = false;
        Call("HandlePlayerDeath", player.Character);
        Require(Requests.Count == 0, "non-owner player cannot broadcast");
        player.LocalOwner = true; player.Valid = false;
        Call("HandlePlayerDeath", player.Character);
        Require(Requests.Count == 0, "invalid player view cannot broadcast");
    }

    private static void CheckRespawnSynchronization()
    {
        MethodInfo factory = Modifiers.GetMethod("AuthorizeReapingRespawnAfterSync", All)!;
        MethodInfo moveNext = Copy(factory.GetCustomAttribute<IteratorStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", All)!);
        Reset(); Actor player = Add(player: true);
        ZDOID reaper = new(76543L, 4), other = new(76543L, 5), dead = player.Zdo.m_uid;
        Call("TryAddReapingDeathAuthorization", reaper, dead);
        Call("TryAddReapingDeathAuthorization", other, dead);
        Set(player.Zdo, ZDOVars.s_dead, 1);
        ZDOExtraData.Set(dead, ZDOVars.s_health, 0f);
        var iterator = (IEnumerator)factory.Invoke(null, new object[] { player.Character, 42L, dead })!;
        try
        {
            Require((bool)moveNext.Invoke(null, new object[] { iterator })!, "respawn waits for live authoritative state");
            Require(!(bool)Call("TryAddReapingDeathAuthorization", reaper, dead)!, "old death stays deduplicated before respawn confirmation");
            Set(player.Zdo, ZDOVars.s_dead, 0); ZDOExtraData.Set(dead, ZDOVars.s_health, 100f);
            Require(!(bool)moveNext.Invoke(null, new object[] { iterator })!, "confirmed player respawn completes");
            Require((bool)Call("TryAddReapingDeathAuthorization", reaper, dead)! &&
                    (bool)Call("TryAddReapingDeathAuthorization", other, dead)!, "next death can count once again for every Reaper");
        }
        finally
        {
            Call("ClearReapingDeathAuthorization", dead); Call("CompactReapingAuthorizationQueue");
        }
    }

    private static void CheckDeathSynchronization()
    {
        MethodInfo factory = Modifiers.GetMethod("AuthorizeReapingAfterDeathSync", All)!;
        MethodInfo moveNext = Copy(factory.GetCustomAttribute<IteratorStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", All)!);
        foreach (string scenario in new[] { "confirmed", "tamed-late", "summoned-late", "owner-changed", "removed", "timeout", "world-reset" })
        {
            Reset();
            Actor victim = Add(), reaper = Add();
            ZDOExtraData.Set(victim.Zdo.m_uid, ZDOVars.s_health, 100f);
            object request = Activator.CreateInstance(Modifiers.GetNestedType("ReapingRequestKey", All)!, All, null,
                new object[] { reaper.Zdo.m_uid, victim.Zdo.m_uid }, null)!;
            Dictionary("ServerPendingReapingRequests")[request] = 42L;
            Dictionary("ServerPendingReapingRequestCounts")[42L] = 1;
            uint epoch = (uint)Field("ReapingDeathEpoch");
            var iterator = (IEnumerator)factory.Invoke(null, new object[] {
                reaper.Character, 42L, victim.Zdo.m_uid, Vector3.zero, request, Manager, epoch })!;
            Require((bool)moveNext.Invoke(null, new object[] { iterator })! && Authorizations == 0, scenario + ": live victim waits without granting");
            ZDOExtraData.Set(victim.Zdo.m_uid, ZDOVars.s_health, 0f);
            switch (scenario)
            {
                case "tamed-late": Set(victim.Zdo, ZDOVars.s_tamed, 1); break;
                case "summoned-late": Set(victim.Zdo, "faction".GetStableHashCode(), (int)Character.Faction.PlayerSpawned); break;
                case "owner-changed": VictimOwner = 99L; break;
                case "removed": SyncedZdos.Remove(victim.Zdo.m_uid); break;
                case "timeout": Now = 3f; break;
                case "world-reset": Modifiers.GetField("ReapingDeathEpoch", All)!.SetValue(null, epoch + 1); break;
            }
            try
            {
                Require(!(bool)moveNext.Invoke(null, new object[] { iterator })! &&
                        Authorizations == (scenario is "confirmed" or "tamed-late" or "summoned-late" ? 1 : 0),
                    scenario + ": authoritative death synchronization outcome");
                Require(!Dictionary("ServerPendingReapingRequests").Contains(request) &&
                        !Dictionary("ServerPendingReapingRequestCounts").Contains(42L), scenario + ": completion releases pending request and peer count");
            }
            finally
            {
                Modifiers.GetField("ReapingDeathEpoch", All)!.SetValue(null, epoch);
                Dictionary("ServerPendingReapingRequests").Remove(request);
                Dictionary("ServerPendingReapingRequestCounts").Remove(42L);
            }
        }
    }

    private static void CheckCallPaths(Assembly plugin)
    {
        string[] Calls(MethodInfo method)
        {
            using var body = new DynamicMethodDefinition(method);
            return body.Definition.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().Select(m => m.Name).ToArray();
        }
        string[] death = Calls(Modifiers.GetMethod("HandleDeath", All)!);
        Require(death.Contains("ApplyReapingForNearbyDeaths") && !death.Contains("ResolveFinalDeathAttributionCharacter") &&
                !death.Contains("TryApplyDirectReapingGain"), "creature death dispatch no longer gates on killer attribution");
        Require(death.Contains("IsOwner") && Array.IndexOf(death, "IsOwner") < Array.IndexOf(death, "ApplyReapingForNearbyDeaths"), "existing owner gate precedes nearby broadcast");
        string[] player = Calls(Modifiers.GetMethod("HandlePlayerDeath", All)!);
        Require(player.Contains("ApplyReapingForNearbyDeaths") && !player.Contains("CaptureFinalDeathAttribution"), "player deaths use nearby broadcast without killer attribution");
        Require(Modifiers.GetMethod("ResolveFinalDeathAttributionCharacter", All) == null &&
                Modifiers.GetMethod("TryApplyDirectReapingGain", All) == null, "obsolete Reaping-only killer paths are removed");
        Require(Calls(Modifiers.GetMethod("RPC_ReapingDirectKillRequest", All)!).Contains("IsEligibleReapingDeathVictim"), "server request checks victim eligibility");
        Type iterator = Modifiers.GetMethod("AuthorizeReapingAfterDeathSync", All)!
            .GetCustomAttribute<IteratorStateMachineAttribute>()!.StateMachineType;
        string[] sync = Calls(iterator.GetMethod("MoveNext", All)!);
        Require(sync.Contains("IsEligibleReapingDeathVictim") && sync.Contains("AuthorizeReapingGain"), "delayed server confirmation rechecks victim before reward");
        MethodInfo hook = plugin.GetType("CreatureManager.CreatureManagerCharacterOnDeathPatch", true)!.GetMethod("Prefix", All)!;
        string[] entry = Calls(hook);
        Require(Array.IndexOf(entry, "CaptureFinalDeathAttribution") < Array.IndexOf(entry, "HandleDeath") &&
                Array.IndexOf(entry, "HandleDeath") < Array.IndexOf(entry, "RecordDeath"), "Karma snapshot is preserved before modifier cleanup");
    }

    private static Actor Add(bool player = false)
    {
        var actor = new Actor();
        if (player) { actor.Character = New<Player>(); actor.Player = true; }
        actor.Character.m_faction = Character.Faction.ForestMonsters;
        ZNetView view = New<ZNetView>();
        typeof(Character).GetField("m_nview", All)!.SetValue(actor.Character, view);
        actor.Zdo.m_uid = new ZDOID(76543L, ++NextId);
        typeof(ZDO).GetField("m_prefab", All)!.SetValue(actor.Zdo, (int)NextId);
        Actors[Id(actor.Character)] = actor; Actors[Id(view)] = actor;
        Positions[Id(actor.Transform)] = Vector3.zero;
        Loaded[actor.Zdo.m_uid] = actor.Character;
        SyncedZdos[actor.Zdo.m_uid] = actor.Zdo;
        return actor;
    }
    private static Actor Reaper(Vector3 position)
    {
        Actor actor = Add(); Positions[Id(actor.Transform)] = position;
        long mask = Convert.ToInt64(Enum.Parse(Modifiers.GetNestedType("ModifierMask", All)!, "Reaping"));
        ZDOExtraData.Set(actor.Zdo.m_uid, "CreatureManager_ModifierMask64".GetStableHashCode(), mask);
        Dictionary("ActiveReapingModifierCharacters")[Id(actor.Character)] = actor.Character;
        Dictionary("UnqueryableReapingModifierCharacters")[Id(actor.Character)] = actor.Character;
        return actor;
    }
    private static void AddCollider(Character character)
    {
        Collider collider = New<CapsuleCollider>(); Colliders[Id(collider)] = character; Overlaps.Add(collider);
    }
    private static void Reset()
    {
        foreach (string name in new[] { "ActiveReapingModifierCharacters", "UnqueryableReapingModifierCharacters" }) Dictionary(name).Clear();
        Actors.Clear(); Loaded.Clear(); Prefabs.Clear(); Positions.Clear(); Colliders.Clear(); Overlaps.Clear(); Requests.Clear();
        SyncedZdos.Clear(); QueriedPrefab = null; Queries = Authorizations = 0; Now = 0f; VictimOwner = 42L;
    }
    private static object Field(string name) => Modifiers.GetField(name, All)!.GetValue(null)!;
    private static IDictionary Dictionary(string name) => (IDictionary)Field(name);
    private static bool Eligible(Actor actor) => (bool)Call("IsEligibleReapingDeathVictim", actor.Zdo)!;
    private static object? Call(string name, params object[] args) => Copy(Modifiers.GetMethod(name, All)!).Invoke(null, args);
    private static void Set(ZDO zdo, int key, int value) => ZDOExtraData.Set(zdo.m_uid, key, value);
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo found)) return found;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(ReapingContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference method) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch {
                "op_Equality" => nameof(Equal), "op_Inequality" => nameof(Different), "GetInstanceID" => nameof(Id), _ => null }
                : type == "Character" ? method.Name switch {
                    "IsPlayer" => nameof(IsPlayer), "IsTamed" => nameof(IsTamed), "IsDead" => nameof(IsDead),
                    "GetHealth" => nameof(Health), "GetFaction" => nameof(Faction), "GetZDOID" => nameof(CharacterId), _ => null }
                : type == "UnityEngine.Component" ? method.Name switch {
                    "get_transform" => nameof(Transform), "GetComponentInParent" => nameof(ParentCharacter), _ => null }
                : type == "UnityEngine.Transform" && method.Name == "get_position" ? nameof(Position)
                : type == "UnityEngine.GameObject" && method.Name == "GetComponent" ? nameof(PrefabCharacter)
                : type == "ZNetScene" ? method.Name switch { "get_instance" => nameof(GetScene), "GetPrefab" => nameof(GetPrefab), _ => null }
                : type == "ZNetView" ? method.Name switch { "IsValid" => nameof(Valid), "IsOwner" => nameof(LocalOwner), "GetZDO" => nameof(GetZdo), _ => null }
                : type == "ZDOMan" ? method.Name switch { "get_instance" => nameof(GetManager), "GetZDO" => nameof(SyncedZdo), _ => null }
                : type == "ZDO" && method.Name == "GetOwner" ? nameof(Owner)
                : type == "UnityEngine.Time" && method.Name == "get_realtimeSinceStartup" ? nameof(Time)
                : type == "CreatureManager.CreatureLevelManager" ? method.Name switch {
                    "AllowsModifierEffects" => nameof(Effects), "IsLevelSystemEnabled" => nameof(LevelsEnabled), _ => null }
                : type == Modifiers.FullName ? method.Name switch {
                    "TryFindCharacter" => nameof(FindCharacter), "GetNearbyReapingOverlapCount" => nameof(Overlap),
                    "TryGetCharacterZdo" => nameof(CharacterZdo),
                    "RequestReapingGain" => nameof(Request), "AuthorizeReapingGain" => nameof(Authorize), _ => null } : null;
            MethodInfo? replacement = boundary == null ? null : typeof(ReapingContracts).GetMethod(boundary, All);
            if (replacement == null && type == Modifiers.FullName) replacement = Copy((MethodInfo)method.ResolveReflection());
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(replacement);
        }
        return Copies[source] = copy.Generate();
    }

    private static int Id(UnityEngine.Object value) => RuntimeHelpers.GetHashCode(value);
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static bool IsPlayer(Character c) => Actors[Id(c)].Player;
    private static bool IsTamed(Character c) => Actors[Id(c)].Tamed;
    private static bool IsDead(Character c) => Actors[Id(c)].Dead;
    private static float Health(Character c) => Actors[Id(c)].Health;
    private static Character.Faction Faction(Character c) => c.m_faction;
    private static bool Effects(Character c) => Actors[Id(c)].Effects;
    private static Transform Transform(Component c) => Actors[Id(c)].Transform;
    private static Vector3 Position(Transform t) => Positions[Id(t)];
    private static Character ParentCharacter(Component collider) => Colliders[Id(collider)];
    private static bool Valid(ZNetView view) => Actors[Id(view)].Valid;
    private static bool LocalOwner(ZNetView view) => Actors[Id(view)].LocalOwner;
    private static bool LevelsEnabled() => true;
    private static ZDO GetZdo(ZNetView view) => Actors[Id(view)].Zdo;
    private static bool CharacterZdo(Character c, out ZDO zdo)
    {
        Actor actor = Actors[Id(c)]; zdo = actor.Zdo; return actor.Valid;
    }
    private static ZDOID CharacterId(Character c) => Actors[Id(c)].Zdo.m_uid;
    private static ZDOMan GetManager() => Manager;
    private static ZDO? SyncedZdo(ZDOMan manager, ZDOID id) => SyncedZdos.TryGetValue(id, out ZDO zdo) ? zdo : null;
    private static long Owner(ZDO zdo) => VictimOwner;
    private static float Time() => Now;
    private static void Authorize(Character reaper, ZDOID deadId, Vector3 position) => Authorizations++;
    private static bool FindCharacter(ZDOID id, out Character character) => Loaded.TryGetValue(id, out character);
    private static ZNetScene GetScene() => Scene;
    private static GameObject? GetPrefab(ZNetScene scene, int hash) => Prefabs.TryGetValue(hash, out QueriedPrefab) ? PrefabObject : null;
    private static Character? PrefabCharacter(GameObject prefab) => QueriedPrefab;
    private static int Overlap(Vector3 origin)
    {
        Queries++; Overlaps.CopyTo((Collider[])Field("ReapingOverlapBuffer")); return Overlaps.Count;
    }
    private static bool Request(Character reaper, Character dead, Vector3 position) { Requests.Add(reaper); return true; }
    private static void Require(bool passed, string reason)
    { Checks++; if (!passed) throw new InvalidOperationException("Reaping: " + reason); }
    private sealed class ReferenceComparer : IEqualityComparer<Character>
    {
        internal static readonly ReferenceComparer Instance = new();
        public bool Equals(Character? x, Character? y) => ReferenceEquals(x, y);
        public int GetHashCode(Character obj) => Id(obj);
    }
}
