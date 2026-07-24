using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CreatureManager;

internal static class CreaturePrefabRegistry
{
    private sealed class CloneRecord
    {
        internal readonly GameObject Prefab;
        internal readonly string SourceName;

        internal CloneRecord(GameObject prefab, string sourceName)
        {
            Prefab = prefab;
            SourceName = sourceName;
        }
    }

    private static readonly Dictionary<string, CloneRecord> ClonedPrefabs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, GameObject> ResourceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<GameObject> PrefabsToRegister = new();
    private static readonly List<GameObject> RetiredClones = new();
    private static readonly HashSet<string> ClonesPresentAtApplyStart = new(StringComparer.OrdinalIgnoreCase);
    private static GameObject? Root;
    private static ZNetScene? FejdZNetScene;
    private static ObjectDB? FejdObjectDb;
    private static bool CloneApplyInProgress;

    internal static void CacheFejdStartup(FejdStartup fejdStartup)
    {
        if (fejdStartup.m_objectDBPrefab == null)
        {
            return;
        }

        FejdZNetScene = fejdStartup.m_objectDBPrefab.GetComponent<ZNetScene>();
        FejdObjectDb = fejdStartup.m_objectDBPrefab.GetComponent<ObjectDB>();
    }

    internal static GameObject? GetPrefab(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            return null;
        }

        if (ClonedPrefabs.TryGetValue(prefabName, out CloneRecord cloneRecord) && cloneRecord.Prefab != null)
        {
            return cloneRecord.Prefab;
        }

        if (ZNetScene.instance != null)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab != null)
            {
                return prefab;
            }
        }

        if (FejdZNetScene != null)
        {
            GameObject prefab = FejdZNetScene.m_prefabs.FirstOrDefault(candidate => candidate != null && candidate.name == prefabName);
            if (prefab != null)
            {
                return prefab;
            }
        }

        if (ObjectDB.instance != null)
        {
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (itemPrefab != null)
            {
                return itemPrefab;
            }
        }

        if (FejdObjectDb != null)
        {
            GameObject itemPrefab = FejdObjectDb.m_items.FirstOrDefault(candidate => candidate != null && candidate.name == prefabName);
            if (itemPrefab != null)
            {
                return itemPrefab;
            }
        }

        CacheResourcesIfNeeded();
        return ResourceCache.TryGetValue(prefabName, out GameObject cachedPrefab) ? cachedPrefab : null;
    }

    internal static List<GameObject> GetCreaturePrefabs()
    {
        IEnumerable<GameObject> prefabs = Enumerable.Empty<GameObject>();
        if (ZNetScene.instance != null)
        {
            prefabs = prefabs.Concat(ZNetScene.instance.m_prefabs);
        }

        if (FejdZNetScene != null)
        {
            prefabs = prefabs.Concat(FejdZNetScene.m_prefabs);
        }

        return prefabs
            .Where(prefab => prefab != null &&
                             !IsClonedPrefab(prefab) &&
                             prefab.GetComponent<Character>() != null &&
                             !IsPlayerPrefab(prefab))
            .GroupBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<GameObject> GetAttackPrefabs()
    {
        HashSet<string> creatureAttackItemNames = GetCreatureAttackItemNames();
        IEnumerable<GameObject> prefabs = Enumerable.Empty<GameObject>();
        if (ObjectDB.instance != null)
        {
            prefabs = prefabs.Concat(ObjectDB.instance.m_items);
        }

        if (FejdObjectDb != null)
        {
            prefabs = prefabs.Concat(FejdObjectDb.m_items);
        }

        if (ZNetScene.instance != null)
        {
            prefabs = prefabs.Concat(ZNetScene.instance.m_prefabs);
        }

        if (FejdZNetScene != null)
        {
            prefabs = prefabs.Concat(FejdZNetScene.m_prefabs);
        }

        return prefabs
            .Where(prefab => prefab != null &&
                             !IsClonedPrefab(prefab) &&
                             creatureAttackItemNames.Contains(prefab.name) &&
                             IsAttackItem(prefab))
            .GroupBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<GameObject> GetItemPrefabs()
    {
        IEnumerable<GameObject> prefabs = Enumerable.Empty<GameObject>();
        if (ObjectDB.instance != null)
        {
            prefabs = prefabs.Concat(ObjectDB.instance.m_items);
        }

        if (FejdObjectDb != null)
        {
            prefabs = prefabs.Concat(FejdObjectDb.m_items);
        }

        if (ZNetScene.instance != null)
        {
            prefabs = prefabs.Concat(ZNetScene.instance.m_prefabs);
        }

        if (FejdZNetScene != null)
        {
            prefabs = prefabs.Concat(FejdZNetScene.m_prefabs);
        }

        return prefabs
            .Where(prefab => prefab != null &&
                             !IsClonedPrefab(prefab) &&
                             prefab.GetComponent<ItemDrop>() != null)
            .GroupBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<GameObject> GetProjectilePrefabs()
    {
        IEnumerable<GameObject> prefabs = Enumerable.Empty<GameObject>();
        if (ZNetScene.instance != null)
        {
            prefabs = prefabs.Concat(ZNetScene.instance.m_prefabs);
        }

        if (FejdZNetScene != null)
        {
            prefabs = prefabs.Concat(FejdZNetScene.m_prefabs);
        }

        if (ObjectDB.instance != null)
        {
            prefabs = prefabs.Concat(ObjectDB.instance.m_items);
        }

        if (FejdObjectDb != null)
        {
            prefabs = prefabs.Concat(FejdObjectDb.m_items);
        }

        return prefabs
            .Where(prefab => prefab != null &&
                             !IsClonedPrefab(prefab) &&
                             HasProjectileLikeComponent(prefab))
            .GroupBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static bool IsCreatureManagerClone(GameObject prefab)
    {
        return IsClonedPrefab(prefab);
    }

    internal static bool TryGetCloneSource(string cloneName, out string sourceName)
    {
        if (ClonedPrefabs.TryGetValue(cloneName, out CloneRecord record) && record.Prefab != null)
        {
            sourceName = record.SourceName;
            return true;
        }

        sourceName = "";
        return false;
    }

    internal static bool IsPlayerPrefab(GameObject prefab)
    {
        return prefab.GetComponent<Player>() != null;
    }

    internal static bool TryValidateCloneRegistration(GameObject source, string cloneName, out string error)
    {
        error = "";
        if (source == null || string.IsNullOrWhiteSpace(cloneName))
        {
            error = "Cannot create a CreatureManager prefab clone without a source and clone name.";
            return false;
        }

        cloneName = cloneName.Trim();
        GameObject? externalPrefab = FindExternalPrefabByName(cloneName);
        if (externalPrefab != null)
        {
            error = $"CreatureManager clone '{cloneName}' was not created because an external prefab with that name is already registered.";
            return false;
        }

        if (TryFindExternalHashOwner(source, cloneName, out GameObject? hashOwner))
        {
            error =
                $"CreatureManager clone '{cloneName}' was not created because its stable hash collides with external prefab '{hashOwner!.name}'.";
            return false;
        }

        return true;
    }

    internal static GameObject? ClonePrefab(GameObject source, string cloneName)
    {
        if (source == null || string.IsNullOrWhiteSpace(cloneName))
        {
            CreatureManagerPlugin.Log.LogError("Cannot create a CreatureManager prefab clone without a source and clone name.");
            return null;
        }

        cloneName = cloneName.Trim();
        string sourceName = source.name;
        GameObject? existingClone = null;
        if (ClonedPrefabs.TryGetValue(cloneName, out CloneRecord existingRecord))
        {
            if (existingRecord.Prefab == null)
            {
                RemoveOwnedClone(cloneName);
            }
            else if (!string.Equals(existingRecord.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
            {
                CreatureManagerPlugin.Log.LogError(
                    $"CreatureManager clone '{cloneName}' is already based on '{existingRecord.SourceName}' and cannot be changed to '{sourceName}' at runtime. Restart the game after changing clonedFrom.");
                return null;
            }
            else
            {
                existingClone = existingRecord.Prefab;
            }
        }

        if (!TryValidateCloneRegistration(source, cloneName, out string error))
        {
            CreatureManagerPlugin.Log.LogError(error);
            return null;
        }

        if (existingClone != null)
        {
            return existingClone;
        }

        Transform root = GetRootTransform();
        GameObject clone = Object.Instantiate(source, root, false);
        clone.name = cloneName;
        ClonedPrefabs[cloneName] = new CloneRecord(clone, sourceName);
        if (!RegisterPrefab(clone))
        {
            RemoveOwnedClone(cloneName);
            return null;
        }

        return clone;
    }

    internal static void BeginCloneApplyPass()
    {
        CloneApplyInProgress = true;
        ClonesPresentAtApplyStart.Clear();
        ClonesPresentAtApplyStart.UnionWith(ClonedPrefabs.Keys);
    }

    internal static void CompleteCloneApplyPass()
    {
        if (!CloneApplyInProgress)
        {
            return;
        }

        CloneApplyInProgress = false;
        ClonesPresentAtApplyStart.Clear();
    }

    internal static void CancelCloneApplyPass()
    {
        if (!CloneApplyInProgress)
        {
            return;
        }

        CloneApplyInProgress = false;
        string[] clonesCreatedByFailedApply = ClonedPrefabs.Keys
            .Where(name => !ClonesPresentAtApplyStart.Contains(name))
            .ToArray();
        ClonesPresentAtApplyStart.Clear();

        foreach (string cloneName in clonesCreatedByFailedApply)
        {
            RemoveOwnedClone(cloneName);
        }
    }

    internal static void ResetOwnedClones()
    {
        CloneApplyInProgress = false;
        ClonesPresentAtApplyStart.Clear();

        foreach (string cloneName in ClonedPrefabs.Keys.ToArray())
        {
            RemoveOwnedClone(cloneName);
        }

        ClonedPrefabs.Clear();
        PrefabsToRegister.Clear();
        ResourceCache.Clear();
        if (Root != null)
        {
            Object.Destroy(Root);
            Root = null;
        }
    }

    private static bool RegisterPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        if (!PrefabsToRegister.Contains(prefab))
        {
            PrefabsToRegister.Add(prefab);
        }

        bool registered = RegisterWithZNetScene(ZNetScene.instance, prefab);
        registered &= RegisterWithZNetScene(FejdZNetScene, prefab);
        registered &= RegisterWithObjectDb(ObjectDB.instance, prefab);
        registered &= RegisterWithObjectDb(FejdObjectDb, prefab);
        return registered;
    }

    internal static void RegisterPendingPrefabs(ZNetScene scene)
    {
        PrefabsToRegister.RemoveAll(prefab => prefab == null);
        foreach (GameObject prefab in PrefabsToRegister.ToArray())
        {
            RegisterWithZNetScene(scene, prefab);
        }
    }

    internal static void RegisterPendingPrefabs(ObjectDB objectDb)
    {
        PrefabsToRegister.RemoveAll(prefab => prefab == null);
        foreach (GameObject prefab in PrefabsToRegister.ToArray())
        {
            RegisterWithObjectDb(objectDb, prefab);
        }
    }

    private static Transform GetRootTransform()
    {
        if (Root != null)
        {
            return Root.transform;
        }

        Root = new GameObject("CreatureManager_prefab_root");
        Object.DontDestroyOnLoad(Root);
        Root.SetActive(false);
        return Root.transform;
    }

    private static bool IsClonedPrefab(GameObject prefab)
    {
        return ClonedPrefabs.Values.Any(record => record.Prefab == prefab);
    }

    internal static bool IsAttackItem(GameObject prefab)
    {
        ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
        return itemDrop?.m_itemData?.m_shared?.m_attack != null &&
               !string.IsNullOrWhiteSpace(itemDrop.m_itemData.m_shared.m_attack.m_attackAnimation);
    }

    private static bool HasProjectileLikeComponent(GameObject prefab)
    {
        return prefab.GetComponent("Projectile") != null ||
               prefab.GetComponent("Aoe") != null ||
               prefab.GetComponent("SpawnAbility") != null ||
               prefab.GetComponent("TriggerSpawnAbility") != null;
    }

    private static HashSet<string> GetCreatureAttackItemNames()
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameObject creaturePrefab in GetCreaturePrefabs())
        {
            Humanoid humanoid = creaturePrefab.GetComponent<Humanoid>();
            if (humanoid == null)
            {
                continue;
            }

            AddAttackItems(names, humanoid.m_defaultItems);
            AddAttackItems(names, humanoid.m_randomWeapon);
            if (humanoid.m_randomItems != null)
            {
                foreach (Humanoid.RandomItem randomItem in humanoid.m_randomItems)
                {
                    AddAttackItem(names, randomItem?.m_prefab);
                }
            }

            if (humanoid.m_randomSets != null)
            {
                foreach (Humanoid.ItemSet set in humanoid.m_randomSets)
                {
                    AddAttackItems(names, set?.m_items);
                }
            }
        }

        return names;
    }

    private static void AddAttackItems(HashSet<string> names, IEnumerable<GameObject>? items)
    {
        if (items == null)
        {
            return;
        }

        foreach (GameObject item in items)
        {
            AddAttackItem(names, item);
        }
    }

    private static void AddAttackItem(HashSet<string> names, GameObject? item)
    {
        if (item != null && IsAttackItem(item))
        {
            names.Add(item.name);
        }
    }

    private static bool RegisterWithZNetScene(ZNetScene? scene, GameObject prefab)
    {
        if (scene == null || prefab.GetComponent<ZNetView>() == null)
        {
            return true;
        }

        int hash = StringExtensionMethods.GetStableHashCode(prefab.name);
        if (scene.m_prefabs.Any(candidate => candidate != null &&
                                             candidate != prefab &&
                                             string.Equals(candidate.name, prefab.name, StringComparison.OrdinalIgnoreCase)) ||
            scene.m_namedPrefabs != null &&
            scene.m_namedPrefabs.TryGetValue(hash, out GameObject existing) &&
            existing != null && existing != prefab)
        {
            CreatureManagerPlugin.Log.LogError(
                $"CreatureManager prefab '{prefab.name}' was not registered in ZNetScene because its name or stable hash is already owned by another prefab.");
            return false;
        }

        if (!scene.m_prefabs.Contains(prefab))
        {
            scene.m_prefabs.Add(prefab);
        }

        if (scene.m_namedPrefabs != null)
        {
            scene.m_namedPrefabs[hash] = prefab;
        }

        return true;
    }

    private static bool RegisterWithObjectDb(ObjectDB? objectDb, GameObject prefab)
    {
        ItemDrop? itemDrop = prefab.GetComponent<ItemDrop>();
        if (objectDb == null || itemDrop == null)
        {
            return true;
        }

        int hash = StringExtensionMethods.GetStableHashCode(prefab.name);
        ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
        if (objectDb.m_items.Any(candidate => candidate != null &&
                                              candidate != prefab &&
                                              string.Equals(candidate.name, prefab.name, StringComparison.OrdinalIgnoreCase)) ||
            objectDb.m_itemByHash != null &&
            objectDb.m_itemByHash.TryGetValue(hash, out GameObject existing) &&
            existing != null && existing != prefab ||
            objectDb.m_itemByData != null &&
            objectDb.m_itemByData.TryGetValue(shared, out GameObject existingByData) &&
            existingByData != null && existingByData != prefab)
        {
            CreatureManagerPlugin.Log.LogError(
                $"CreatureManager prefab '{prefab.name}' was not registered in ObjectDB because its name or stable hash is already owned by another prefab.");
            return false;
        }

        if (!objectDb.m_items.Contains(prefab))
        {
            objectDb.m_items.Add(prefab);
        }

        if (objectDb.m_itemByHash != null)
        {
            objectDb.m_itemByHash[hash] = prefab;
        }

        if (objectDb.m_itemByData != null)
        {
            objectDb.m_itemByData[shared] = prefab;
        }

        return true;
    }

    private static void RemoveOwnedClone(string cloneName)
    {
        if (!ClonedPrefabs.TryGetValue(cloneName, out CloneRecord record))
        {
            return;
        }

        ClonedPrefabs.Remove(cloneName);
        GameObject prefab = record.Prefab;
        CreaturePrefabBaseline.Forget(prefab);
        RetiredClones.Add(prefab);
        PrefabsToRegister.RemoveAll(candidate => ReferenceEquals(candidate, prefab));
        RemoveFromZNetScene(ZNetScene.instance, prefab);
        RemoveFromZNetScene(FejdZNetScene, prefab);
        RemoveFromObjectDb(ObjectDB.instance, prefab);
        RemoveFromObjectDb(FejdObjectDb, prefab);

        foreach (string cacheKey in ResourceCache
                     .Where(pair => ReferenceEquals(pair.Value, prefab))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            ResourceCache.Remove(cacheKey);
        }

        if (prefab != null)
        {
            Object.Destroy(prefab);
        }
    }

    private static void RemoveFromZNetScene(ZNetScene? scene, GameObject prefab)
    {
        if (scene == null)
        {
            return;
        }

        scene.m_prefabs.RemoveAll(candidate => ReferenceEquals(candidate, prefab));
        if (scene.m_namedPrefabs == null)
        {
            return;
        }

        foreach (int hash in scene.m_namedPrefabs
                     .Where(pair => ReferenceEquals(pair.Value, prefab))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            scene.m_namedPrefabs.Remove(hash);
            GameObject? replacement = scene.m_prefabs.FirstOrDefault(candidate =>
                candidate != null &&
                !IsClonedPrefab(candidate) &&
                StringExtensionMethods.GetStableHashCode(candidate.name) == hash);
            if (replacement != null)
            {
                scene.m_namedPrefabs[hash] = replacement;
            }
        }
    }

    private static void RemoveFromObjectDb(ObjectDB? objectDb, GameObject prefab)
    {
        if (objectDb == null)
        {
            return;
        }

        objectDb.m_items.RemoveAll(candidate => ReferenceEquals(candidate, prefab));
        if (objectDb.m_itemByHash != null)
        {
            foreach (int hash in objectDb.m_itemByHash
                         .Where(pair => ReferenceEquals(pair.Value, prefab))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                objectDb.m_itemByHash.Remove(hash);
                GameObject? replacement = objectDb.m_items.FirstOrDefault(candidate =>
                    candidate != null &&
                    !IsClonedPrefab(candidate) &&
                    StringExtensionMethods.GetStableHashCode(candidate.name) == hash);
                if (replacement != null)
                {
                    objectDb.m_itemByHash[hash] = replacement;
                }
            }
        }

        if (objectDb.m_itemByData != null)
        {
            foreach (ItemDrop.ItemData.SharedData shared in objectDb.m_itemByData
                         .Where(pair => ReferenceEquals(pair.Value, prefab))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                objectDb.m_itemByData.Remove(shared);
                GameObject? replacement = objectDb.m_items.FirstOrDefault(candidate =>
                    candidate != null &&
                    !IsClonedPrefab(candidate) &&
                    ReferenceEquals(candidate.GetComponent<ItemDrop>()?.m_itemData?.m_shared, shared));
                if (replacement != null)
                {
                    objectDb.m_itemByData[shared] = replacement;
                }
            }
        }
    }

    private static GameObject? FindExternalPrefabByName(string prefabName)
    {
        IEnumerable<GameObject> prefabs = Enumerable.Empty<GameObject>();
        if (ZNetScene.instance != null)
        {
            prefabs = prefabs.Concat(ZNetScene.instance.m_prefabs);
        }

        if (FejdZNetScene != null)
        {
            prefabs = prefabs.Concat(FejdZNetScene.m_prefabs);
        }

        if (ObjectDB.instance != null)
        {
            prefabs = prefabs.Concat(ObjectDB.instance.m_items);
        }

        if (FejdObjectDb != null)
        {
            prefabs = prefabs.Concat(FejdObjectDb.m_items);
        }

        GameObject? registered = prefabs.FirstOrDefault(candidate =>
            candidate != null &&
            !IsClonedPrefab(candidate) &&
            string.Equals(candidate.name, prefabName, StringComparison.OrdinalIgnoreCase));
        if (registered != null)
        {
            return registered;
        }

        CacheResourcesIfNeeded();
        return ResourceCache.TryGetValue(prefabName, out GameObject resource) &&
               resource != null &&
               !IsClonedPrefab(resource)
            ? resource
            : null;
    }

    private static bool TryFindExternalHashOwner(GameObject source, string cloneName, out GameObject? owner)
    {
        int hash = StringExtensionMethods.GetStableHashCode(cloneName);
        if (source.GetComponent<ZNetView>() != null &&
            (TryGetExternalHashOwner(ZNetScene.instance?.m_namedPrefabs, hash, out owner) ||
             TryGetExternalHashOwner(FejdZNetScene?.m_namedPrefabs, hash, out owner)))
        {
            return true;
        }

        if (source.GetComponent<ItemDrop>() != null &&
            (TryGetExternalHashOwner(ObjectDB.instance?.m_itemByHash, hash, out owner) ||
             TryGetExternalHashOwner(FejdObjectDb?.m_itemByHash, hash, out owner)))
        {
            return true;
        }

        owner = null;
        return false;
    }

    private static bool TryGetExternalHashOwner(
        Dictionary<int, GameObject>? registry,
        int hash,
        out GameObject? owner)
    {
        if (registry != null &&
            registry.TryGetValue(hash, out GameObject candidate) &&
            candidate != null &&
            !IsClonedPrefab(candidate))
        {
            owner = candidate;
            return true;
        }

        owner = null;
        return false;
    }

    private static void CacheResourcesIfNeeded()
    {
        RetiredClones.RemoveAll(prefab => prefab == null);
        if (ResourceCache.Count > 0)
        {
            return;
        }

        foreach (GameObject prefab in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (prefab == null ||
                prefab.GetInstanceID() < 0 ||
                RetiredClones.Any(retired => ReferenceEquals(retired, prefab)) ||
                ResourceCache.ContainsKey(prefab.name))
            {
                continue;
            }

            ResourceCache[prefab.name] = prefab;
        }
    }
}
