using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace CreatureManager;

// Spawn owners query their received world state, independently of the server's Karma ledger.
// Cache only inside a synchronous list/group attempt. No timer, RPC, or persisted blocker state.
internal static class CreatureSpawnBlocker
{
    internal sealed class QueryScope
    {
        internal QueryScope? Previous;
        internal bool EventSpawns;
        internal bool RejectedGroupCandidate;
        internal readonly Dictionary<(Vector2s, bool, bool, bool, int, bool), bool> Results = new();
    }

    private static QueryScope? CurrentQuery;
    private static readonly Stack<QueryScope> QueryPool = new();
    private static readonly List<ZDO> NearbyZdos = new();
    private static readonly Dictionary<int, bool> NearbyBossPrefabs = new();

    private static bool BlockBoss => CreatureManagerPlugin.BlockNearbySpawnsWhileBossActive?.Value == CreatureManagerPlugin.Toggle.On;
    private static bool BlockEnforcer => CreatureManagerPlugin.BlockNearbySpawnsWhileEnforcerActive?.Value == CreatureManagerPlugin.Toggle.On;

    internal static QueryScope? BeginQuery(bool eventSpawns)
    {
        if (!BlockBoss && !BlockEnforcer && CurrentQuery == null) return null;
        QueryScope scope = QueryPool.Count > 0 ? QueryPool.Pop() : new QueryScope();
        scope.Previous = CurrentQuery;
        scope.EventSpawns = eventSpawns;
        return CurrentQuery = scope;
    }

    internal static void EndQuery(QueryScope? scope)
    {
        if (scope == null) return;
        CurrentQuery = scope.Previous;
        scope.Previous = null;
        scope.EventSpawns = false;
        scope.RejectedGroupCandidate = false;
        scope.Results.Clear();
        QueryPool.Push(scope);
    }

    internal static void InvalidateQueries()
    {
        // Another spawn/hook can create a boss during this very list/group attempt.
        for (QueryScope? scope = CurrentQuery; scope != null; scope = scope.Previous) scope.Results.Clear();
    }

    internal static bool AllowSystemSpawn(GameObject prefab, Vector3 position)
    {
        return CurrentQuery?.EventSpawns == true || AllowSpawn(prefab, position);
    }

    internal static bool AllowSpawn(GameObject prefab, Vector3 position)
    {
        if (!BlockBoss && !BlockEnforcer) return true;
        Character? creature = prefab != null ? prefab.GetComponent<Character>() : null;
        // Some CreatureSpawners produce items, and encounter progression can spawn boss prefabs.
        return creature == null || creature.IsPlayer() || creature.IsBoss() || !ShouldBlock(position);
    }

    internal static bool AllowGroupCandidate(CreatureSpawner spawner)
    {
        bool allowed = AllowSpawn(spawner.m_creaturePrefab, spawner.transform.position);
        if (!allowed && CurrentQuery != null) CurrentQuery.RejectedGroupCandidate = true;
        return allowed;
    }

    internal static void LogEmptyGroup(object message)
    {
        // Keep the native diagnostic for genuinely invalid groups, but not fully blocked groups.
        if (CurrentQuery?.RejectedGroupCandidate != true) ZLog.LogError(message);
    }

    private static bool ShouldBlock(Vector3 position)
    {
        bool bosses = BlockBoss, enforcers = BlockEnforcer;
        if ((!bosses && !enforcers) || ZNet.instance == null || ZoneSystem.instance == null || ZNetScene.instance == null) return false;
        Vector2s zone = ZoneSystem.GetZone(position);
        bool interior = Character.InInterior(position);
        SimulationDistance distance = ZNet.instance.GetSyncedSimulationDistance();
        var key = (zone, interior, bosses, enforcers, distance.NearSimulationDistance, distance.IsClassic);
        if (CurrentQuery != null && CurrentQuery.Results.TryGetValue(key, out bool cached)) return cached;
        bool blocked = FindBlocker(zone, interior, distance, bosses, enforcers);
        if (CurrentQuery != null) CurrentQuery.Results[key] = blocked;
        return blocked;
    }

    private static bool FindBlocker(Vector2s zone, bool interior, SimulationDistance distance, bool bosses, bool enforcers)
    {
        foreach (Character character in Character.GetAllCharacters())
        {
            if (character == null || character.IsPlayer() || character.IsDead()) continue;
            bool enforcer = CreatureKarmaManager.IsEnforcer(character);
            if (!(enforcer ? enforcers : bosses && character.IsBoss())) continue;
            if (InQueryArea(character.transform.position, zone, interior, distance)) return true;
        }

        if (ZDOMan.instance == null) return false;
        try
        {
            // Near-only: the optional distant output would otherwise be merged into this list.
            ZDOMan.instance.FindSectorObjects(zone,
                new SimulationDistance(interior ? 0 : distance.NearSimulationDistance, 0, distance.IsClassic), NearbyZdos);
            foreach (ZDO zdo in NearbyZdos)
            {
                if (!CreatureKarmaManager.IsTrackedCharacterZdoAlive(zdo) ||
                    !InQueryArea(zdo.GetPosition(), zone, interior, distance)) continue;
                ZNetView? instance = ZNetScene.instance.FindInstance(zdo);
                // A loaded instance's live death/boss state above takes precedence over its prefab.
                if (instance != null && instance.GetComponent<Character>() != null) continue;
                bool enforcer = CreatureKarmaManager.IsEnforcerZdo(zdo);
                if (enforcer)
                {
                    if (enforcers) return true;
                    continue;
                }
                if (!bosses) continue;
                int prefabHash = zdo.GetPrefab();
                if (!NearbyBossPrefabs.TryGetValue(prefabHash, out bool boss))
                {
                    Character? prefab = ZNetScene.instance.GetPrefab(prefabHash)?.GetComponent<Character>();
                    boss = prefab != null && prefab.IsBoss() && !prefab.IsPlayer();
                    NearbyBossPrefabs[prefabHash] = boss;
                }
                if (boss) return true;
            }
            return false;
        }
        finally
        {
            NearbyZdos.Clear();
            // Classification is shared only within this search; prefab reloads and later
            // registrations are observed by the next search, including negative results.
            NearbyBossPrefabs.Clear();
        }
    }

    private static bool InQueryArea(Vector3 position, Vector2s center, bool interior, SimulationDistance distance)
    {
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
            float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z) ||
            Character.InInterior(position) != interior) return false;
        Vector2s zone = ZoneSystem.GetZone(position);
        if (interior) return zone == center;
        return Math.Abs(zone.x - center.x) <= distance.NearSimulationDistance &&
               Math.Abs(zone.y - center.y) <= distance.NearSimulationDistance &&
               (distance.IsClassic || ZoneSystem.instance.ZonesWithinRadius(center, zone, distance.NearSimulationDistance));
    }
}

[HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
internal static class CreatureManagerSpawnBlockQueryPatch
{
    private static void Prefix(bool eventSpawners, out CreatureSpawnBlocker.QueryScope? __state) => __state = CreatureSpawnBlocker.BeginQuery(eventSpawners);
    private static Exception? Finalizer(Exception? __exception, CreatureSpawnBlocker.QueryScope? __state)
    {
        CreatureSpawnBlocker.EndQuery(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(SpawnSystem), "IsSpawnPointGood")]
internal static class CreatureManagerSpawnPointBlockPatch
{
    private static void Postfix(SpawnSystem.SpawnData spawn, Vector3 spawnPoint, ref bool __result)
    {
        if (__result && !CreatureSpawnBlocker.AllowSystemSpawn(spawn.m_prefab, spawnPoint)) __result = false;
    }
}

[HarmonyPatch(typeof(SpawnArea), "FindSpawnPoint")]
internal static class CreatureManagerSpawnAreaBlockPatch
{
    // Reject the selected ordinary prefab before DropNSpawn initializes its spawn data.
    // The native SpawnOne failure path keeps the interval and consumes no spawn count.
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("sighsorry.DropNSpawn")]
    private static bool Prefix(SpawnArea __instance, GameObject prefab, ref Vector3 point, ref bool __result)
    {
        if (CreatureSpawnBlocker.AllowSpawn(prefab, __instance.transform.position)) return true;
        point = default;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
internal static class CreatureManagerCreatureSpawnerBlockPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("sighsorry.DropNSpawn")]
    private static bool Prefix(CreatureSpawner __instance, ref ZNetView? __result)
    {
        if (CreatureSpawnBlocker.AllowSpawn(__instance.m_creaturePrefab, __instance.transform.position)) return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureSpawner.Group), nameof(CreatureSpawner.Group.SpawnWeighted))]
internal static class CreatureManagerSpawnerGroupBlockPatch
{
    private static void Prefix(out CreatureSpawnBlocker.QueryScope? __state) => __state = CreatureSpawnBlocker.BeginQuery(false);
    private static Exception? Finalizer(Exception? __exception, CreatureSpawnBlocker.QueryScope? __state)
    {
        CreatureSpawnBlocker.EndQuery(__state);
        return __exception;
    }

    [HarmonyAfter("sighsorry.DropNSpawn")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = instructions.ToList();
        int exists = result.FindIndex(i => i.Calls(AccessTools.Method(typeof(CreatureSpawner), "SpawnedCreatureStillExists")));
        int add = result.FindIndex(i => i.Calls(AccessTools.Method(typeof(List<CreatureSpawner>), nameof(List<CreatureSpawner>.Add))));
        int start = add - 3;
        int error = result.FindIndex(i => i.Calls(AccessTools.Method(typeof(ZLog), nameof(ZLog.LogError))));
        if (exists < 0 || exists + 1 >= result.Count || result[exists + 1].operand is not Label skip ||
            (result[exists + 1].opcode != OpCodes.Brtrue && result[exists + 1].opcode != OpCodes.Brtrue_S) ||
            start < 0 || result[start].opcode != OpCodes.Ldarg_0 || result[start + 1].opcode != OpCodes.Ldfld || error < 0)
            throw new InvalidOperationException("CreatureSpawner group candidate/weight contract changed.");

        result[error].operand = AccessTools.Method(typeof(CreatureSpawnBlocker), nameof(CreatureSpawnBlocker.LogEmptyGroup));
        var load = new CodeInstruction(result[add - 1].opcode, result[add - 1].operand);
        load.labels.AddRange(result[start].labels);
        load.blocks.AddRange(result[start].blocks);
        result[start].labels.Clear();
        result[start].blocks.Clear();
        result.InsertRange(start, new[]
        {
            load,
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CreatureSpawnBlocker), nameof(CreatureSpawnBlocker.AllowGroupCandidate))),
            new CodeInstruction(OpCodes.Brfalse, skip)
        });
        return result;
    }
}
