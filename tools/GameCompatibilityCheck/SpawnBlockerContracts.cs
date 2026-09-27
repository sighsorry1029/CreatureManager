using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Run the compiled policy/query/scope bodies with real original ZDO data. Only native
// scene/transport queries are substituted; do not claim this starts Unity or a network.
internal static class SpawnBlockerContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Blocker = null!, Karma = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly ZNet Net = New<ZNet>();
    private static readonly ZDOMan Manager = New<ZDOMan>();
    private static readonly ZNetScene Scene = New<ZNetScene>();
    private static readonly ZoneSystem Zone = New<ZoneSystem>();
    private static readonly List<ZDO> Zdos = new();
    private static readonly List<Character> Loaded = new();
    private static readonly Dictionary<int, GameObject> Prefabs = new();
    private static readonly Dictionary<UnityEngine.Object, Character> Characters = new(ReferenceComparer.Instance);
    private static readonly Dictionary<Character, ZDO> CharacterZdos = new(ReferenceComparer.Instance);
    private static readonly Dictionary<ZDO, ZNetView> Instances = new();
    private static readonly Dictionary<Component, Transform> Transforms = new(ReferenceComparer.Instance);
    private static readonly Dictionary<Transform, Vector3> Positions = new(ReferenceComparer.Instance);
    private static readonly HashSet<Character> Dead = new(ReferenceComparer.Instance);
    private static readonly Dictionary<string, ConfigEntryBase> Settings = new();
    private static SimulationDistance Distance;
    private static bool QueryThrows;
    private static int Queries, Errors;
    private static uint NextId = 85000;
    private static GameObject Ordinary = null!, BossPrefab = null!, Item = null!;

    internal static void Run(Assembly plugin)
    {
        Blocker = plugin.GetType("CreatureManager.CreatureSpawnBlocker", true)!;
        Karma = plugin.GetType("CreatureManager.CreatureKarmaManager", true)!;
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CreatureManager-spawn-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        var saved = new Dictionary<FieldInfo, object?>();
        foreach (string name in new[] { "BlockNearbySpawnsWhileBossActive", "BlockNearbySpawnsWhileEnforcerActive", "KarmaMode", "EnableLevelSystem" })
        {
            FieldInfo field = entry.GetField(name, Static)!;
            saved[field] = field.GetValue(null);
            Type type = field.FieldType.GetGenericArguments()[0];
            MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
            var setting = (ConfigEntryBase)bind.MakeGenericMethod(type).Invoke(config,
                new[] { "fixture", name, Enum.ToObject(type, 1), new ConfigDescription("") })!;
            Settings[name] = setting; field.SetValue(null, setting);
        }
        try
        {
            Zone.m_zoneSize = 64f;
            Ordinary = Prefab("Skeleton", false); BossPrefab = Prefab("Bonemass", true); Item = New<GameObject>();
            CheckOptionsAndLives();
            CheckArea();
            CheckScopes(plugin);
            CheckHooks(plugin);
            CheckGroupTranspilers(plugin);
        }
        finally
        {
            foreach (var item in saved) item.Key.SetValue(null, item.Value);
            Blocker.GetField("CurrentQuery", Static)!.SetValue(null, null);
            Reset();
        }
        System.Console.WriteLine("Spawn blocking contracts passed: independent toggles/Karma+levels Off, live/dead/remote ZDO and loaded-instance precedence, near sectors/classic/circular/interior boundaries, ordinary vs boss/item/raid exclusions, live settings and synchronous nested/exception scope cleanup, fresh queries after spawn, group filtering/empty diagnostics, original private targets and one-shot write ordering. Native scene/transport boundaries substituted; no Unity/network session.");
    }

    private static void CheckOptionsAndLives()
    {
        foreach (bool bossOption in new[] { false, true })
        foreach (bool enforcerOption in new[] { false, true })
        foreach (bool enforcer in new[] { false, true })
        foreach (bool boss in new[] { false, true })
        {
            Reset(); SetOptions(bossOption, enforcerOption);
            Settings["KarmaMode"].SetSerializedValue("Off"); Settings["EnableLevelSystem"].SetSerializedValue("Off");
            Add(boss, enforcer, Vector3.zero);
            Require(Allowed(Vector3.zero) == !(enforcer ? enforcerOption : boss && bossOption), $"independent category/options with Karma+levels Off: options={bossOption}/{enforcerOption}, creature={boss}/{enforcer}, queries={Queries}");
            if (!bossOption && !enforcerOption) Require(Queries == 0, "both Off avoid world queries");
        }
        foreach (bool loaded in new[] { false, true })
        {
            Reset(); ZDO boss = Add(true, false, Vector3.zero, loaded);
            Require(!Allowed(Vector3.zero), "loaded or received remote boss blocks");
            SetBool(boss, ZDOVars.s_dead, true);
            if (loaded) Dead.Add(Loaded[0]);
            Require(Allowed(Vector3.zero), "dead blocker releases");
            SetBool(boss, ZDOVars.s_dead, false); SetHealth(boss, 0f);
            Require(Allowed(Vector3.zero), "zero health is not active");
        }
        Reset(); Add(true, false, Vector3.zero, true);
        SetBoss(Loaded[0], false);
        Require(Allowed(Vector3.zero), "live instance overrides boss prefab metadata");
        SetBoss(Loaded[0], true); Dead.Add(Loaded[0]);
        Require(Allowed(Vector3.zero), "live death overrides stale positive ZDO health");
        Reset(); Add(true, true, Vector3.zero); SetOptions(true, false);
        Require(Allowed(Vector3.zero), "boss-shaped Enforcer is not a regular boss");
        Reset(); Add(true, false, Vector3.zero);
        Require((bool)Call("AllowSpawn", BossPrefab, Vector3.zero)! && (bool)Call("AllowSpawn", Item, Vector3.zero)!, "boss progression and item spawners stay allowed");
        Zdos.Clear(); Require(Allowed(Vector3.zero), "removed/unreceived blocker releases without persistent cache");
    }

    private static void CheckArea()
    {
        Reset(); Add(true, false, new Vector3(128, 0, 128));
        Distance = new SimulationDistance(2, 2, true);
        Require(!Allowed(Vector3.zero), "classic near square includes corner");
        Distance = new SimulationDistance(2, 2, false);
        Require(Allowed(Vector3.zero), "circular near excludes corner");
        Distance = new SimulationDistance(1, 2, true);
        Require(Allowed(Vector3.zero), "far-loaded sectors do not block");
        Distance = new SimulationDistance(3, 2, false);
        Require(!Allowed(Vector3.zero), "synced larger near distance is respected");
        foreach (var test in new[]
        {
            (new Vector3(0, 4000, 0), Vector3.zero, true),
            (Vector3.zero, new Vector3(0, 4000, 0), true),
            (new Vector3(64, 4000, 0), new Vector3(0, 4000, 0), true),
            (new Vector3(10, 4000, 10), new Vector3(0, 4000, 0), false),
            (new Vector3(6400, 0, 0), Vector3.zero, true),
            (new Vector3(float.NaN, 0, 0), Vector3.zero, true)
        })
        {
            Reset(); Add(true, false, test.Item1);
            Require(Allowed(test.Item2) == test.Item3, "region/interior/finite position boundary");
        }
    }

    private static void CheckScopes(Assembly plugin)
    {
        Reset(); object scope = Call("BeginQuery", false)!;
        Require(Allowed(Vector3.zero) && Allowed(Vector3.zero) && Queries == 1, "one query per sector in one list attempt");
        Add(true, false, Vector3.zero); Call("InvalidateQueries");
        Require(!Allowed(Vector3.zero) && Queries == 2, "new spawn invalidates cached negative decision");
        SetOptions(false, false); Require(Allowed(Vector3.zero), "live Off takes effect inside scope");
        SetOptions(true, true); Require(!Allowed(Vector3.zero), "live On applies");
        object raid = Call("BeginQuery", true)!;
        Require((bool)Call("AllowSystemSpawn", Ordinary, Vector3.zero)!, "raid spawns excluded");
        Call("EndQuery", raid);
        Require(!(bool)Call("AllowSystemSpawn", Ordinary, Vector3.zero)!, "nested scope restores ordinary-spawn policy");
        var exception = new InvalidOperationException("fixture");
        MethodInfo finalizer = plugin.GetType("CreatureManager.CreatureManagerSpawnBlockQueryPatch", true)!.GetMethod("Finalizer", Static)!;
        Require(ReferenceEquals(Copy(finalizer).Invoke(null, new[] { (object)exception, scope }), exception), "finalizer preserves exception");
        Require(Blocker.GetField("CurrentQuery", Static)!.GetValue(null) == null, "exception path releases scope");
        Zdos.Clear(); Require(Allowed(Vector3.zero), "next attempt rechecks departed/dead blockers");
        Reset(); QueryThrows = true;
        try { Allowed(Vector3.zero); throw new Exception("expected world query failure"); }
        catch (TargetInvocationException e) when (e.GetBaseException().Message == "query boundary") { }
        Require(((IList)Blocker.GetField("NearbyZdos", Static)!.GetValue(null)!).Count == 0, "query exception clears scratch references");
        QueryThrows = false;
        Call("EndQuery", new object?[] { null });
    }

    private static void CheckHooks(Assembly plugin)
    {
        Reset(); Add(true, false, Vector3.zero);
        var area = New<SpawnArea>(); Position(area, Vector3.zero);
        var spawner = New<CreatureSpawner>(); spawner.m_creaturePrefab = Ordinary; Position(spawner, Vector3.zero);
        var list = new SpawnSystem.SpawnData { m_prefab = Ordinary };
        object?[] pointArgs = { list, Vector3.zero, true };
        Hook(plugin, "CreatureManagerSpawnPointBlockPatch", "Postfix", pointArgs);
        Require(!(bool)pointArgs[2]!, "world candidate becomes invalid before Spawn side effects");
        object?[] areaArgs = { area, Ordinary, Vector3.one, true };
        Require(!(bool)Hook(plugin, "CreatureManagerSpawnAreaBlockPatch", "Prefix", areaArgs)! && !(bool)areaArgs[3]!, "SpawnArea returns unsuccessful attempt");
        object?[] spawnArgs = { spawner, New<ZNetView>() };
        Require(!(bool)Hook(plugin, "CreatureManagerCreatureSpawnerBlockPatch", "Prefix", spawnArgs)! && spawnArgs[1] == null, "CreatureSpawner leaves one-shot connection untouched");
        object scope = Call("BeginQuery", false)!;
        Require(!(bool)Call("AllowGroupCandidate", spawner)!, "group rejects blocked member");
        Call("LogEmptyGroup", "empty"); Require(Errors == 0, "fully blocked group is not an error");
        spawner.m_creaturePrefab = BossPrefab;
        Require((bool)Call("AllowGroupCandidate", spawner)!, "boss member retains group opportunity");
        Call("EndQuery", scope); Call("LogEmptyGroup", "invalid"); Require(Errors == 1, "native invalid group diagnostic retained");

        foreach (var target in new[] { (typeof(SpawnSystem), "IsSpawnPointGood"), (typeof(SpawnArea), "FindSpawnPoint"), (typeof(CreatureSpawner), "Spawn") })
            Require(AccessTools.Method(target.Item1, target.Item2).IsPrivate, "original private target accessed only through Harmony");
        Require(AccessTools.Method(typeof(ZDOMan), "FindSectorObjects").IsPublic && AccessTools.Method(typeof(ZNet), "GetSyncedSimulationDistance").IsPublic,
            "new world queries use original public APIs");
        var spawn = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(CreatureSpawner), "Spawn"));
        int creation = spawn.FindIndex(i => i.operand is MethodInfo m && m.Name == "Instantiate");
        int write = spawn.FindIndex(i => i.operand is MethodInfo m && m.Name == "SetConnection");
        Require(creation >= 0 && write > creation, "blocked original cannot consume one-shot state before instantiate");
    }

    private static void CheckGroupTranspilers(Assembly plugin)
    {
        MethodInfo method = plugin.GetType("CreatureManager.CreatureManagerSpawnerGroupBlockPatch", true)!.GetMethod("Transpiler", Static)!;
        MethodInfo target = AccessTools.Method(typeof(CreatureSpawner.Group), "SpawnWeighted");
        List<CodeInstruction> Apply(MethodInfo patch, List<CodeInstruction> code) => ((IEnumerable<CodeInstruction>)patch.Invoke(null, new object[] { code })!).ToList();
        void Inspect(List<CodeInstruction> code)
        {
            int gate = code.FindIndex(i => i.operand is MethodInfo m && m.Name == "AllowGroupCandidate");
            int add = code.FindIndex(i => i.Calls(AccessTools.Method(typeof(List<CreatureSpawner>), "Add")));
            Require(gate >= 0 && add > gate && code[gate + 1].opcode == OpCodes.Brfalse && code[gate + 1].operand is Label, "group gate branches before insertion");
            Label skip = (Label)code[gate + 1].operand;
            int destination = code.FindIndex(i => i.labels.Contains(skip));
            int weight = code.FindIndex(add, i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.Name == "m_spawnerWeight");
            Require(destination > weight && weight > add, "rejected candidate loses both entry and weight");
            Require(code.Any(i => i.Calls(AccessTools.Method(Blocker, "LogEmptyGroup"))), "empty-group log respects rejection");
        }
        Inspect(Apply(method, PatchProcessor.GetOriginalInstructions(target)));
        string? path = Environment.GetEnvironmentVariable("CREATUREMANAGER_CHECK_DROPN_SPAWN");
        if (string.IsNullOrEmpty(path)) { System.Console.WriteLine("DropNSpawn group composition not requested (set CREATUREMANAGER_CHECK_DROPN_SPAWN to its DLL)."); return; }
        Assembly dns = Assembly.LoadFrom(Path.GetFullPath(path));
        MethodInfo other = dns.GetType("DropNSpawn.CreatureSpawnerGroupSpawnPatch", true)!.GetMethod("Transpiler", Static)!;
        var combined = Apply(method, Apply(other, PatchProcessor.GetOriginalInstructions(target)));
        Inspect(combined);
        Require(combined.Any(i => i.operand is MethodInfo m && m.Name == "IsCreatureSpawnerGroupCandidate"), "DropNSpawn candidate policy preserved");
        Require(method.GetCustomAttribute<HarmonyAfter>()!.info.after.Contains("sighsorry.DropNSpawn"), "declared composition order");
        System.Console.WriteLine("DropNSpawn group transpiler composition passed: " + path);
    }

    private static object? Hook(Assembly plugin, string type, string method, object?[] args) => Copy(plugin.GetType("CreatureManager." + type, true)!.GetMethod(method, Static)!).Invoke(null, args);
    private static object? Call(string name, params object?[] args) => Copy(Blocker.GetMethod(name, Static)!).Invoke(null, args);
    private static bool Allowed(Vector3 position) => (bool)Call("AllowSpawn", Ordinary, position)!;
    private static void SetOptions(bool boss, bool enforcer)
    {
        Settings["BlockNearbySpawnsWhileBossActive"].SetSerializedValue(boss ? "On" : "Off");
        Settings["BlockNearbySpawnsWhileEnforcerActive"].SetSerializedValue(enforcer ? "On" : "Off");
    }
    private static void Reset()
    {
        Zdos.Clear(); Loaded.Clear(); Instances.Clear(); CharacterZdos.Clear(); Dead.Clear();
        Queries = Errors = 0; QueryThrows = false; Distance = SimulationDistance.OriginalDistance;
        if (Settings.Count > 0) SetOptions(true, true);
    }
    private static GameObject Prefab(string name, bool boss)
    {
        var obj = New<GameObject>(); var character = New<Character>();
        SetBoss(character, boss); Characters[obj] = character; Prefabs[name.GetStableHashCode()] = obj;
        return obj;
    }
    private static ZDO Add(bool boss, bool enforcer, Vector3 position, bool loaded = false)
    {
        var zdo = New<ZDO>();
        zdo.m_uid = new ZDOID(918273L, ++NextId);
        typeof(ZDO).GetField("m_prefab", Instance)!.SetValue(zdo, (boss ? "Bonemass" : "Skeleton").GetStableHashCode());
        typeof(ZDO).GetField("m_position", Instance)!.SetValue(zdo, position);
        SetHealth(zdo, 100f); SetBool(zdo, "CreatureManager_KarmaEnforcer".GetStableHashCode(), enforcer); Zdos.Add(zdo);
        if (loaded)
        {
            var character = New<Character>(); SetBoss(character, boss); Position(character, position);
            Loaded.Add(character); CharacterZdos[character] = zdo;
            var view = New<ZNetView>(); Instances[zdo] = view; Characters[view] = character;
        }
        return zdo;
    }
    private static void Position(Component component, Vector3 value)
    {
        var transform = New<Transform>(); Transforms[component] = transform; Positions[transform] = value;
    }
    private static void SetBoss(Character value, bool boss) => typeof(Character).GetField("m_boss", Instance)!.SetValue(value, boss);
    private static void SetBool(ZDO zdo, int key, bool value) => ZDOExtraData.Set(zdo.m_uid, key, value ? 1 : 0);
    private static void SetHealth(ZDO zdo, float value) => ZDOExtraData.Set(zdo.m_uid, ZDOVars.s_health, value);
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo found)) return found;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(SpawnBlockerContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is FieldReference field && field.FullName == "ZoneSystem ZoneSystem::instance")
            { instruction.Operand = copy.Module.ImportReference(typeof(SpawnBlockerContracts).GetField(nameof(Zone), Static)!); continue; }
            if (instruction.Operand is not MethodReference method) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == "UnityEngine.Object" ? method.Name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(NotEqual), _ => null }
                : type == "ZNet" ? method.Name switch { "get_instance" => nameof(GetNet), "GetSyncedSimulationDistance" => nameof(GetDistance), _ => null }
                : type == "ZDOMan" ? method.Name switch { "get_instance" => nameof(GetManager), "FindSectorObjects" => nameof(FindRegion), _ => null }
                : type == "ZoneSystem" && method.Name == "get_instance" ? nameof(GetZone)
                : type == "ZNetScene" ? method.Name switch { "get_instance" => nameof(GetScene), "GetPrefab" => nameof(GetPrefab), "FindInstance" => nameof(FindInstance), _ => null }
                : type == "UnityEngine.GameObject" && method.Name == "GetComponent" ? nameof(GetCharacter)
                : type == "UnityEngine.Component" ? method.Name switch { "GetComponent" => nameof(GetComponentCharacter), "get_transform" => nameof(GetTransform), _ => null }
                : type == "UnityEngine.Transform" && method.Name == "get_position" ? nameof(GetPosition)
                : type == "Character" ? method.Name switch { "GetAllCharacters" => nameof(GetLoaded), "IsDead" => nameof(IsDead), "IsPlayer" => nameof(IsPlayer), _ => null }
                : type == Karma.FullName && method.Name == "IsEnforcer" ? nameof(IsEnforcer)
                : type == "ZLog" && method.Name == "LogError" ? nameof(LogError) : null;
            MethodInfo? replacement = boundary == null ? null : typeof(SpawnBlockerContracts).GetMethod(boundary, Static);
            if (replacement == null && (type == Blocker.FullName || type == Karma.FullName ||
                type == "Character" && method.Name is "InInterior" or "IsBoss" ||
                type == "ZoneSystem" && method.Name is "GetZone" or "GetZonePos" or "ZonesWithinRadius" ||
                type == "Utils" && method.Name == "FloorToInt")) replacement = Copy((MethodInfo)method.ResolveReflection());
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call; instruction.Operand = copy.Module.ImportReference(replacement);
        }
        return Copies[source] = copy.Generate();
    }

    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool NotEqual(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static ZNet GetNet() => Net;
    private static ZoneSystem GetZone() => Zone;
    private static SimulationDistance GetDistance(ZNet net) => Distance;
    private static ZDOMan GetManager() => Manager;
    private static ZNetScene GetScene() => Scene;
    private static List<Character> GetLoaded() => Loaded;
    private static bool IsDead(Character c) => Dead.Contains(c) || CharacterZdos[c].GetFloat(ZDOVars.s_health) <= 0;
    private static bool IsPlayer(Character c) => false;
    private static bool IsEnforcer(Character c) => CharacterZdos[c].GetBool("CreatureManager_KarmaEnforcer");
    private static GameObject? GetPrefab(ZNetScene scene, int hash) => Prefabs.TryGetValue(hash, out var obj) ? obj : null;
    private static ZNetView? FindInstance(ZNetScene scene, ZDO zdo) => Instances.TryGetValue(zdo, out var obj) ? obj : null;
    private static Character? GetCharacter(GameObject obj) => Characters.TryGetValue(obj, out var c) ? c : null;
    private static Character? GetComponentCharacter(Component obj) => Characters.TryGetValue(obj, out var c) ? c : null;
    private static Transform GetTransform(Component obj) => Transforms[obj];
    private static Vector3 GetPosition(Transform obj) => Positions[obj];
    private static void FindRegion(ZDOMan manager, Vector2s center, SimulationDistance distance, List<ZDO> result, List<ZDO>? distant)
    {
        Queries++; Require(distance.FarSimulationDistance == 0, "query never requests far sectors");
        result.AddRange(Zdos); // Deliberately overbroad input: the real policy must reject foreign areas.
        if (QueryThrows) throw new InvalidOperationException("query boundary");
    }
    private static void LogError(object message) => Errors++;
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Spawn blocking contract failed: " + message); }
    private sealed class ReferenceComparer : IEqualityComparer<UnityEngine.Object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public bool Equals(UnityEngine.Object? a, UnityEngine.Object? b) => ReferenceEquals(a, b);
        public int GetHashCode(UnityEngine.Object value) => RuntimeHelpers.GetHashCode(value);
    }
}
