using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute the compiled discovery/decision bodies against real original ZDO data.
// Substitute scene, transport, clock and query boundaries; no native detours/world run.
internal static class BossBlockerContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Karma = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    // Redirect only the production state touched by these bodies, avoiding its native
    // LayerMask initializer. These are fixture storage, not another implementation.
#pragma warning disable CS0169, CS0414, CS0649 // Read/written by the copied production IL below.
    private static readonly HashSet<ZDOID> TrackedBossZdoIds = new(), TrackedEnforcerZdoIds = new();
    private static readonly Stack<(HashSet<ZDOID> Observed, List<ZDOID> Tracked)> BlockerQueryBuffers = new();
    private static readonly Dictionary<ZDOID, float> EnforcerNoPlayerSince = new(), PendingBlockerZdos = new();
    private static readonly List<ZDOID> CompletedBlockerZdos = new();
    private static readonly List<string> BossBootstrapPrefabs = new();
    private static readonly List<ZDO> BossDiscoveryBuffer = new();
    private static ZNetScene? BossDiscoveryScene;
    private static bool BossDiscoveryActive, BossBootstrapPending = true, BossBootstrapInitialized;
    private static int BossDiscoveryPrefabCount = -1, BossBootstrapPrefabIndex, BossBootstrapZdoIndex;
#pragma warning restore CS0169, CS0414, CS0649

    private static readonly ZNet Net = Uninitialized<ZNet>();
    private static readonly ZDOMan Manager = Uninitialized<ZDOMan>();
    private static readonly ZNetScene Scene = Uninitialized<ZNetScene>();
    private static readonly Dictionary<ZDOID, ZDO> Zdos = new();
    private static readonly Dictionary<int, GameObject> Prefabs = new();
    private static readonly Dictionary<GameObject, Character> Characters = new(ReferenceComparer.Instance);
    private static readonly Dictionary<UnityEngine.Object, string> Names = new(ReferenceComparer.Instance);
    private static readonly Dictionary<Character, ZDO> LoadedZdos = new(ReferenceComparer.Instance);
    private static readonly Dictionary<Component, Transform> Transforms = new(ReferenceComparer.Instance);
    private static readonly Dictionary<Transform, Vector3> Positions = new(ReferenceComparer.Instance);
    private static readonly List<Character> Loaded = new();
    private static uint NextId = 15000;
    private static float Now;
    private static bool Server = true, FinishSlice = true;
    private static int Slices, RegionalQueries;
    private static Action? OnZdoLookup;
    private static readonly Dictionary<string, ConfigEntryBase> Config = new();

    internal static void Run(Assembly plugin)
    {
        Karma = plugin.GetType("CreatureManager.CreatureKarmaManager", true)!;
        var entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CreatureManager-boss-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        var saved = new Dictionary<FieldInfo, object?>();
        foreach (string name in new[] { "KarmaMode", "EnableLevelSystem", "BlockEnforcerWhileBossActive", "BlockKarmaGainWhileBossActive", "BlockKarmaGainWhileEnforcerActive" })
        {
            FieldInfo field = entry.GetField(name, Static)!;
            saved[field] = field.GetValue(null);
            Type type = field.FieldType.GetGenericArguments()[0];
            MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
            var value = (ConfigEntryBase)bind.MakeGenericMethod(type).Invoke(config,
                new[] { "fixture", name, Enum.ToObject(type, 1), (object)new ConfigDescription("") })!;
            Config[name] = value;
            field.SetValue(null, value);
        }
        try
        {
            MethodInfo create = typeof(ZDOMan).GetMethod("CreateNewZDO", Instance, null,
                new[] { typeof(ZDOID), typeof(Vector3), typeof(int) }, null)!;
            Require(create.IsPrivate, "original creation overload remains private");
            Require(typeof(ZDOMan).GetMethod("FindSectorObjects", Instance)!.IsPublic, "regional query uses original public API");
            Scene.m_prefabs = new List<GameObject>();
            AddPrefab("Bonemass", true);
            AddPrefab("Skeleton", false);
            AddPrefab("CM_CloneBoss", true);
            CheckStoredBosses();
            CheckReceivedZdos();
            CheckBootstrapAndToggles();
            CheckRegionAndDeaths();
            CheckNestedAndFailedQueries();
        }
        finally
        {
            foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
        }
        System.Console.WriteLine("Boss blocker contracts passed: unloaded saved bosses, ExceptLevel/KarmaLevelOnly, delayed prefab/duplicate observation, owner changes, bounded bootstrap, clone/dirty/Off-On discovery, region union/realm, last-vs-second boss, dead/missing data, Enforcer classification and loaded/tracked deduplication. Production bodies with native/world/query boundaries substituted; no network or Unity scene.");
    }

    private static void CheckStoredBosses()
    {
        foreach (string level in new[] { "Full", "ExceptLevel", "Off" })
        {
            Reset();
            Set("EnableLevelSystem", level);
            Set("KarmaMode", "KarmaLevelOnly");
            Set("BlockEnforcerWhileBossActive", "Off");
            ZDO boss = NewZdo("Bonemass", Vector3.zero);
            Require(Loaded.Count == 0 && PendingBlockerZdos.Count == 0, "no Character or observation supplied");
            Require(Blocks(Vector3.zero) && TrackedBossZdoIds.Contains(boss.m_uid), "saved unloaded boss blocks kills with level mode " + level);
            Require(RegionalQueries == 1 && Slices == 0, "decision uses region, not a global scan");
            Require(Blocks(Vector3.zero) && RegionalQueries == 1, "known boss needs no repeated fallback");
            Set("BlockEnforcerWhileBossActive", "On");
            Require(Call("GetEnforcerBlockerFailure", Vector3.zero, null, ZDOID.None, false)!.ToString() == "ActiveBoss",
                "same discovered boss blocks Enforcer summon decisions");
        }
    }

    private static void CheckReceivedZdos()
    {
        Reset();
        Tick(); Tick(); // Both registered boss prefabs have been searched.
        Require(!BossBootstrapPending, "empty-world bootstrap completed");
        ZDO zdo = NewZdo(null, Vector3.zero);
        Call("QueueCreatedBlockerZdo", zdo);
        Call("QueueCreatedBlockerZdo", zdo);
        Require(PendingBlockerZdos.Count == 1, "duplicate creation observation merged");
        Tick();
        Require(PendingBlockerZdos.Count == 1 && TrackedBossZdoIds.Count == 0, "prefab zero retained");
        SetPrefab(zdo, "Bonemass");
        ZDOExtraData.SetOwner(zdo.m_uid, ZDOID.AddUser(789L));
        typeof(ZDO).GetProperty("Owned", Instance)!.SetValue(zdo, true);
        Require(zdo.GetOwner() == 789L, "remote owner supplied without starting networking");
        Require(Blocks(Vector3.zero) && TrackedBossZdoIds.Count == 1 && PendingBlockerZdos.Count == 0,
            "completed remote data applied before kill decision, independent of owner");
        Require(RegionalQueries == 0, "received boss needs no fallback or full scan");
        ZDO incomplete = NewZdo(null, Vector3.zero);
        Call("QueueCreatedBlockerZdo", incomplete);
        Now += 6f;
        Tick();
        Require(PendingBlockerZdos.Count == 0, "permanently incomplete observations expire");
    }

    private static void CheckBootstrapAndToggles()
    {
        Reset();
        ZDO boss = NewZdo("CM_CloneBoss", Vector3.zero);
        FinishSlice = false;
        Tick();
        Require(Slices == 1 && BossBootstrapZdoIndex == 1 && BossBootstrapPending, "one incomplete slice preserves cursor");
        Require(Blocks(Vector3.zero), "unvisited clone boss found while bootstrap is incomplete");
        FinishSlice = true;
        Tick(); Tick();
        Require(Slices == 3 && !BossBootstrapPending, "one slice per tick, including clone prefab");
        Set("KarmaMode", "Off");
        Tick();
        TrackedBossZdoIds.Clear(); // Model a boss created while the feature was disabled.
        Call("QueueCreatedBlockerZdo", boss);
        Require(!BossDiscoveryActive && PendingBlockerZdos.Count == 0, "Off does not queue discovery");
        Set("KarmaMode", "KarmaLevelOnly");
        Require(Blocks(Vector3.zero), "reenable restores existing bosses without Enforcer or level requests");
        Tick(); Tick();
        TrackedBossZdoIds.Clear();
        Call("InvalidateBossBlockerDiscovery");
        Require(Blocks(Vector3.zero), "template/catalog invalidation reopens regional protection");
        Character clone = Characters[Prefabs["CM_CloneBoss".GetStableHashCode()]];
        SetCharacterBoss(clone, false);
        Call("InvalidateBossBlockerDiscovery");
        Require(!Blocks(Vector3.zero), "unloaded tracked boss follows updated prefab classification");
        SetCharacterBoss(clone, true);
        Call("InvalidateBossBlockerDiscovery");
        Require(Blocks(Vector3.zero), "boss flag restored without changing prefab count");
        Reset();
        NewZdo("Bonemass", Vector3.zero);
        Set("BlockEnforcerWhileBossActive", "Off");
        Set("BlockKarmaGainWhileBossActive", "Off");
        Require(!Blocks(Vector3.zero) && Slices == 0 && RegionalQueries == 0, "disabled boss blockers perform no discovery");
        Server = false;
        Set("BlockKarmaGainWhileBossActive", "On");
        Tick();
        Require(Slices == 0 && RegionalQueries == 0, "remote client does not scan server world");
    }

    private static void CheckRegionAndDeaths()
    {
        Reset();
        ZDO boss = NewZdo("Bonemass", new Vector3(128f, 0f, 0f));
        Require(!Blocks(Vector3.zero), "outside 3x3 does not block");
        var union = new HashSet<string>(StringComparer.Ordinal) { "O:0,0", "O:2,0" };
        Require(State(Vector3.zero, union).boss, "complete merged-player region includes far edge");
        SetPosition(boss, new Vector3(0f, 4000f, 0f));
        Require(!Blocks(Vector3.zero) && Blocks(new Vector3(0f, 4000f, 0f)), "outdoor/dungeon remain separate");
        SetPosition(boss, Vector3.zero);
        Require(!Blocks(Vector3.zero, boss.m_uid), "last killed boss excluded even before its death ZDO arrives");
        ZDO second = NewZdo("Bonemass", Vector3.zero);
        Call("QueueCreatedBlockerZdo", second);
        Require(Blocks(Vector3.zero, boss.m_uid), "another living boss still blocks");
        SetBool(second, ZDOVars.s_dead, true);
        Require(!Blocks(Vector3.zero, boss.m_uid), "dead flag releases blocker");
        SetBool(second, ZDOVars.s_dead, false);
        foreach (float hp in new[] { 0f, float.NaN, -1f })
        {
            SetFloat(second, ZDOVars.s_health, hp);
            Call("QueueCreatedBlockerZdo", second);
            Require(!Blocks(Vector3.zero, boss.m_uid), "nonliving health does not block: " + hp);
        }
        Zdos.Remove(boss.m_uid);
        Require(!Blocks(Vector3.zero), "removed ZDO no longer blocks");
        Reset();
        boss = NewZdo("Bonemass", Vector3.zero);
        SetBool(boss, "CreatureManager_KarmaEnforcer".GetStableHashCode(), true);
        Call("QueueCreatedBlockerZdo", boss);
        var state = State(Vector3.zero);
        Require(!state.boss && state.enforcers == 1, "boss-shaped Enforcer is not a normal boss");
        Character loaded = Uninitialized<Character>();
        SetCharacterBoss(loaded, true);
        Loaded.Add(loaded);
        LoadedZdos[loaded] = boss;
        Transform transform = Uninitialized<Transform>();
        Transforms[loaded] = transform;
        Positions[transform] = Vector3.zero;
        Require(State(Vector3.zero).enforcers == 1, "loaded and tracked Enforcer counted once");
        Loaded.Clear(); // Scene unload does not delete the persistent ZDO.
        Require(State(Vector3.zero).enforcers == 1, "unloaded Enforcer stays tracked");
        Reset();
        boss = NewZdo("Bonemass", Vector3.zero);
        Require(Blocks(Vector3.zero), "ordinary boss initially tracked");
        SetBool(boss, "CreatureManager_KarmaEnforcer".GetStableHashCode(), true);
        state = State(Vector3.zero);
        Require(!state.boss && state.enforcers == 1, "late Enforcer classification moves ID without losing or duplicating count");
    }

    private static void CheckNestedAndFailedQueries()
    {
        Reset();
        ZDO enforcer = NewZdo("Skeleton", Vector3.zero);
        SetBool(enforcer, "CreatureManager_KarmaEnforcer".GetStableHashCode(), true);
        TrackedEnforcerZdoIds.Add(enforcer.m_uid);
        NewZdo("Bonemass", Vector3.zero);
        Require(State(Vector3.zero).enforcers == 1, "warm query discovers both blocker categories");
        OnZdoLookup = () =>
        {
            var inner = State(new Vector3(6400f, 0f, 0f));
            Require(inner.enforcers == 0 && !inner.boss, "nested query has its own region");
        };
        var outer = State(Vector3.zero);
        Require(outer.enforcers == 1 && outer.boss, "nested query cannot overwrite the outer ID snapshot");

        OnZdoLookup = () => throw new InvalidOperationException("query fixture failure");
        try { State(Vector3.zero); throw new Exception("expected query failure"); }
        catch (TargetInvocationException e) when (e.GetBaseException().Message == "query fixture failure") { }
        SetBool(enforcer, ZDOVars.s_dead, true);
        var afterFailure = State(Vector3.zero);
        Require(afterFailure.enforcers == 0 && afterFailure.boss, "failed query leaves no stale IDs or counts");
        Zdos.Clear();
        Require(!Blocks(Vector3.zero), "reused buffers do not retain removed world objects");
    }

    private static void Reset()
    {
        TrackedBossZdoIds.Clear(); TrackedEnforcerZdoIds.Clear(); EnforcerNoPlayerSince.Clear();
        PendingBlockerZdos.Clear(); CompletedBlockerZdos.Clear(); Zdos.Clear(); Loaded.Clear(); LoadedZdos.Clear();
        BossDiscoveryScene = null; BossDiscoveryActive = false; BossDiscoveryPrefabCount = -1;
        BlockerQueryBuffers.Clear(); OnZdoLookup = null;
        Call("InvalidateBossBlockerDiscovery");
        Now = 0; Server = FinishSlice = true; Slices = RegionalQueries = 0;
        Set("KarmaMode", "KarmaLevelAndEnforcer"); Set("EnableLevelSystem", "Vanilla");
        Set("BlockEnforcerWhileBossActive", "On"); Set("BlockKarmaGainWhileBossActive", "On");
        Set("BlockKarmaGainWhileEnforcerActive", "Off");
    }
    private static void Tick() => Call("UpdateServerBossDiscovery", true);
    private static void Set(string name, string value) => Config[name].SetSerializedValue(value);
    private static bool Blocks(Vector3 position, ZDOID excluded = default) => (bool)Call("ShouldBlockKarmaGain", position, excluded)!;
    private static (int enforcers, bool boss) State(Vector3 position, HashSet<string>? region = null)
    {
        object?[] args = { position, 0, false, null, region, ZDOID.None };
        Call("GetEnforcerBlockerState", args);
        return ((int)args[1]!, (bool)args[2]!);
    }
    private static object? Call(string name, params object?[] args) => Copy(Karma.GetMethod(name, Static)!).Invoke(null, args);

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(BossBlockerContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is FieldReference field && field.DeclaringType.FullName == Karma.FullName)
            {
                FieldInfo replacement = typeof(BossBlockerContracts).GetField(field.Name, Static) ??
                    throw new InvalidOperationException("Unexpected production state in boss fixture: " + field.Name);
                instruction.Operand = copy.Module.ImportReference(replacement);
                continue;
            }
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch
            {
                "op_Equality" => nameof(SameObject), "op_Inequality" => nameof(DifferentObject), "get_name" => nameof(Name), _ => null
            } : type == "UnityEngine.Time" && method.Name == "get_realtimeSinceStartup" ? nameof(Clock)
                : type == "ZNet" ? method.Name switch { "get_instance" => nameof(GetNet), "IsServer" => nameof(IsServer), _ => null }
                : type == "ZDOMan" ? method.Name switch
                {
                    "get_instance" => nameof(GetManager), "GetZDO" => nameof(GetZdo), "FindSectorObjects" => nameof(FindRegion),
                    "GetAllZDOsWithPrefabIterative" => nameof(FindSlice), _ => null
                }
                : type == "ZNetScene" ? method.Name switch { "get_instance" => nameof(GetScene), "GetPrefab" => nameof(GetPrefab), _ => null }
                : type == "UnityEngine.GameObject" ? method.Name switch { "GetComponent" => nameof(GetCharacter), "TryGetComponent" => nameof(TryGetCharacter), _ => null }
                : type == "UnityEngine.Component" && method.Name == "get_transform" ? nameof(GetTransform)
                : type == "UnityEngine.Transform" && method.Name == "get_position" ? nameof(GetPosition)
                : type == "Character" ? method.Name switch
                {
                    "GetAllCharacters" => nameof(GetLoaded), "IsBoss" => nameof(IsBoss), "IsPlayer" or "IsDead" => nameof(FalseCharacter),
                    "GetZDOID" => nameof(GetCharacterId), _ => null
                }
                : type == Karma.FullName ? method.Name switch
                {
                    "IsEnforcer" => nameof(IsEnforcer),
                    // Reservation admission is covered by the dedicated delay fixtures.
                    "CountPendingEnforcers" => nameof(NoReservations), _ => null
                } : null;
            MethodInfo? target = boundary == null ? null : typeof(BossBlockerContracts).GetMethod(boundary, Static);
            if (target == null && (type == Karma.FullName || type == "Character" && method.Name == "InInterior" ||
                type == "ZoneSystem" && method.Name == "GetZone" || type == "Utils" && method.Name == "FloorToInt"))
                target = Copy((MethodInfo)method.ResolveReflection());
            if (target == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(target);
        }
        return Copies[source] = copy.Generate();
    }

    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static ZDO NewZdo(string? prefab, Vector3 position)
    {
        ZDO zdo = Uninitialized<ZDO>();
        zdo.m_uid = new ZDOID(876543210L, ++NextId);
        SetPrefab(zdo, prefab); SetPosition(zdo, position); SetFloat(zdo, ZDOVars.s_health, 100f);
        Zdos[zdo.m_uid] = zdo;
        return zdo;
    }
    private static void AddPrefab(string name, bool boss)
    {
        GameObject prefab = Uninitialized<GameObject>(); Character character = Uninitialized<Character>();
        SetCharacterBoss(character, boss); Characters[prefab] = character; Names[prefab] = name;
        Prefabs[name.GetStableHashCode()] = prefab; Scene.m_prefabs.Add(prefab);
    }
    private static void SetCharacterBoss(Character character, bool boss) => typeof(Character).GetField("m_boss", Instance)!.SetValue(character, boss);
    private static void SetPrefab(ZDO zdo, string? prefab) => typeof(ZDO).GetField("m_prefab", Instance)!.SetValue(zdo, prefab?.GetStableHashCode() ?? 0);
    private static void SetPosition(ZDO zdo, Vector3 value) => typeof(ZDO).GetField("m_position", Instance)!.SetValue(zdo, value);
    private static void SetBool(ZDO zdo, int key, bool value) => ZDOExtraData.Set(zdo.m_uid, key, value ? 1 : 0);
    private static void SetFloat(ZDO zdo, int key, float value) => ZDOExtraData.Set(zdo.m_uid, key, value);
    private static bool SameObject(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool DifferentObject(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static string Name(UnityEngine.Object value) => Names[value];
    private static float Clock() => Now;
    private static ZNet GetNet() => Net;
    private static bool IsServer(ZNet value) => Server;
    private static ZDOMan GetManager() => Manager;
    private static ZNetScene GetScene() => Scene;
    private static ZDO? GetZdo(ZDOMan manager, ZDOID id)
    {
        Action? callback = OnZdoLookup;
        OnZdoLookup = null;
        callback?.Invoke();
        return Zdos.TryGetValue(id, out ZDO value) ? value : null;
    }
    private static GameObject? GetPrefab(ZNetScene scene, int hash) => Prefabs.TryGetValue(hash, out GameObject value) ? value : null;
    private static Character GetCharacter(GameObject prefab) => Characters[prefab];
    private static bool TryGetCharacter(GameObject prefab, out Character character) => Characters.TryGetValue(prefab, out character);
    private static bool IsBoss(Character character) => (bool)typeof(Character).GetField("m_boss", Instance)!.GetValue(character)!;
    private static bool FalseCharacter(Character character) => false;
    private static int NoReservations(Vector3 position, HashSet<string>? region) => 0;
    private static List<Character> GetLoaded() => Loaded;
    private static ZDOID GetCharacterId(Character character) => LoadedZdos[character].m_uid;
    private static bool IsEnforcer(Character character) => LoadedZdos[character].GetBool("CreatureManager_KarmaEnforcer");
    private static Transform GetTransform(Component character) => Transforms[character];
    private static Vector3 GetPosition(Transform transform) => Positions[transform];
    private static void FindRegion(ZDOMan manager, Vector2s center, SimulationDistance distance, List<ZDO> result, List<ZDO>? distant)
    {
        RegionalQueries++;
        foreach (ZDO zdo in Zdos.Values)
        {
            Vector2s zone = (Vector2s)Copy(typeof(ZoneSystem).GetMethod("GetZone", Static)!).Invoke(null, new object[] { zdo.GetPosition() })!;
            if (Math.Abs(zone.x - center.x) <= distance.NearSimulationDistance && Math.Abs(zone.y - center.y) <= distance.NearSimulationDistance)
                result.Add(zdo);
        }
    }
    private static bool FindSlice(ZDOMan manager, string prefab, List<ZDO> result, ref int index)
    {
        Slices++; index++;
        if (FinishSlice) result.AddRange(Zdos.Values.Where(z => z.GetPrefab() == prefab.GetStableHashCode()));
        return FinishSlice;
    }
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Boss blocker contract failed: " + label);
    }

    private sealed class ReferenceComparer : IEqualityComparer<UnityEngine.Object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public bool Equals(UnityEngine.Object? x, UnityEngine.Object? y) => ReferenceEquals(x, y);
        public int GetHashCode(UnityEngine.Object value) => RuntimeHelpers.GetHashCode(value);
    }
}
