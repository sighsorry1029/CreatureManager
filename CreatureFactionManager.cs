using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CreatureManager;

internal static class CreatureFactionManager
{
    private const int CustomFactionStartId = 100;
    private static readonly int FactionHash = StringExtensionMethods.GetStableHashCode("faction");
    private static readonly object Sync = new();
    private static FactionSnapshot ActiveSnapshot = new(
        new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), new(), new());

    internal static bool Load(List<FactionDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            definitions = DefaultFactionDefinitions();
        }

        if (!TryBuildSnapshot(definitions, out FactionSnapshot snapshot, out List<string> errors))
        {
            foreach (string error in errors)
            {
                CreatureManagerPlugin.Log.LogError($"Faction configuration rejected: {error}");
            }

            CreatureManagerPlugin.Log.LogWarning(
                $"Faction configuration was not applied because {errors.Count} validation error(s) were found; the previously published faction rules remain active.");
            return false;
        }

        lock (Sync)
        {
            ActiveSnapshot = snapshot;
        }

        RefreshLiveBaseAis();
        return true;
    }

    private static bool TryBuildSnapshot(
        IReadOnlyList<FactionDefinition> definitions,
        out FactionSnapshot snapshot,
        out List<string> errors)
    {
        errors = new List<string>();
        Dictionary<string, Character.Faction> names = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<Character.Faction, string> displayNames = new();
        Dictionary<string, Character.Faction> runtimeFactions = BuildRuntimeFactionMap();
        HashSet<int> runtimeFactionIds = runtimeFactions.Values
            .Select(faction => (int)faction)
            .ToHashSet();
        HashSet<int> reservedIds = new(runtimeFactionIds);

        for (int index = 0; index < definitions.Count; index++)
        {
            FactionDefinition? definition = definitions[index];
            if (definition == null)
            {
                errors.Add($"entry {index + 1} is null; each faction entry must be a mapping.");
                continue;
            }

            if (definition.Id.HasValue)
            {
                reservedIds.Add(definition.Id.Value);
            }
        }

        int nextCustomId = CustomFactionStartId;
        for (int index = 0; index < definitions.Count; index++)
        {
            FactionDefinition? definition = definitions[index];
            if (definition == null)
            {
                continue;
            }

            string? name = NormalizeName(definition.Faction);
            if (name == null)
            {
                errors.Add($"entry {index + 1} has an empty faction name.");
                continue;
            }

            if (int.TryParse(name, out _))
            {
                errors.Add($"entry {index + 1} faction name '{name}' is numeric; use a non-numeric name and the id field instead.");
                continue;
            }

            if (names.ContainsKey(name))
            {
                errors.Add($"entry {index + 1} duplicates faction name '{name}' (names are case-insensitive).");
                continue;
            }

            int id;
            if (runtimeFactions.TryGetValue(name, out Character.Faction runtimeFaction))
            {
                id = (int)runtimeFaction;
                if (definition.Id.HasValue && definition.Id.Value != id)
                {
                    errors.Add(
                        $"entry {index + 1} faction '{name}' is already registered at runtime and must use id {id}, not {definition.Id.Value}.");
                    continue;
                }
            }
            else if (definition.Id.HasValue)
            {
                id = definition.Id.Value;
                if (runtimeFactionIds.Contains(id))
                {
                    string runtimeName = GetRuntimeNameForId(runtimeFactions, id);
                    errors.Add(
                        $"entry {index + 1} custom faction '{name}' cannot alias runtime faction id {id} ({runtimeName}).");
                    continue;
                }
            }
            else
            {
                id = GetNextCustomId(reservedIds, ref nextCustomId);
            }

            Character.Faction faction = (Character.Faction)id;
            if (displayNames.TryGetValue(faction, out string existingName))
            {
                errors.Add(
                    $"entry {index + 1} faction '{name}' duplicates id {id}, which is already assigned to '{existingName}'.");
                continue;
            }

            names[name] = faction;
            displayNames[faction] = name;
            reservedIds.Add(id);
        }

        Dictionary<Character.Faction, FactionData> data = new();
        HashSet<Character.Faction> allFactions = displayNames.Keys.ToHashSet();
        for (int index = 0; index < definitions.Count; index++)
        {
            FactionDefinition? definition = definitions[index];
            if (definition == null)
            {
                continue;
            }

            string? name = NormalizeName(definition.Faction);
            if (name == null || !names.TryGetValue(name, out Character.Faction faction))
            {
                continue;
            }

            bool friendlyValid = TryResolveFactionSet(
                definition.Friendly,
                names,
                allFactions,
                name,
                "friendly",
                errors,
                out HashSet<Character.Faction> friendly);
            bool aggravatedValid = TryResolveOptionalFactionSet(
                definition.AggravatedFriendly,
                names,
                allFactions,
                name,
                "aggravatedFriendly",
                errors,
                out HashSet<Character.Faction>? aggravatedFriendly);
            bool alertedValid = TryResolveOptionalFactionSet(
                definition.AlertedFriendly,
                names,
                allFactions,
                name,
                "alertedFriendly",
                errors,
                out HashSet<Character.Faction>? alertedFriendly);
            if (!friendlyValid || !aggravatedValid || !alertedValid)
            {
                continue;
            }

            FactionData factionData = new(
                friendly,
                aggravatedFriendly,
                alertedFriendly);

            data[faction] = factionData;
        }

        snapshot = new FactionSnapshot(names, runtimeFactions, displayNames, data);
        return errors.Count == 0;
    }

    internal static bool TryGetFaction(string? name, out Character.Faction faction)
    {
        faction = default;
        string? normalized = NormalizeName(name);
        if (normalized == null)
        {
            return false;
        }

        lock (Sync)
        {
            if (ActiveSnapshot.NameToFaction.TryGetValue(normalized, out faction))
            {
                return true;
            }

            if (ActiveSnapshot.RuntimeNameToFaction.TryGetValue(normalized, out faction))
            {
                return true;
            }
        }

        if (int.TryParse(normalized, out int id))
        {
            lock (Sync)
            {
                Character.Faction numericFaction = (Character.Faction)id;
                if (ActiveSnapshot.FactionToName.ContainsKey(numericFaction) ||
                    ActiveSnapshot.RuntimeNameToFaction.ContainsValue(numericFaction))
                {
                    faction = numericFaction;
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool TryGetRegisteredFaction(
        string? name,
        out Character.Faction faction,
        out string canonicalName)
    {
        faction = default;
        canonicalName = "";
        string? normalized = NormalizeName(name);
        if (normalized == null)
        {
            return false;
        }

        lock (Sync)
        {
            if (ActiveSnapshot.NameToFaction.TryGetValue(normalized, out faction))
            {
                canonicalName = ActiveSnapshot.FactionToName[faction];
                return true;
            }

            if (int.TryParse(normalized, out int id) &&
                ActiveSnapshot.FactionToName.TryGetValue((Character.Faction)id, out canonicalName))
            {
                faction = (Character.Faction)id;
                return true;
            }
        }

        return false;
    }

    internal static string[] GetRegisteredFactionNames()
    {
        lock (Sync)
        {
            return ActiveSnapshot.FactionToName
                .OrderBy(entry => (int)entry.Key)
                .Select(entry => entry.Value)
                .ToArray();
        }
    }

    internal static string GetDisplayName(Character.Faction faction)
    {
        lock (Sync)
        {
            return ActiveSnapshot.FactionToName.TryGetValue(faction, out string name) ? name : faction.ToString();
        }
    }

    internal static void ApplyFactionToZdo(Character character, string configuredFaction)
    {
        ZDO? zdo = character.GetComponent<ZNetView>()?.GetZDO();
        if (zdo == null)
        {
            return;
        }

        zdo.Set(FactionHash, configuredFaction);
        zdo.Set(FactionHash, (int)character.m_faction);
    }

    internal static void SetupBaseAi(BaseAI baseAi)
    {
        ZDO? zdo = baseAi.m_nview?.GetZDO();
        if (zdo != null)
        {
            string configuredFaction = zdo.GetString(FactionHash, "");
            if (!string.IsNullOrWhiteSpace(configuredFaction) && TryGetFaction(configuredFaction, out Character.Faction namedFaction))
            {
                baseAi.m_character.m_faction = namedFaction;
            }
            else
            {
                int factionId = zdo.GetInt(FactionHash, 0);
                if (factionId != 0)
                {
                    baseAi.m_character.m_faction = (Character.Faction)factionId;
                }
            }
        }

        lock (Sync)
        {
            Character.Faction faction = baseAi.m_character.m_faction;
            if (ActiveSnapshot.FactionDataByFaction.TryGetValue(faction, out FactionData data))
            {
                baseAi.m_aggravatable = data.AggravatedFriendly != null;
            }
        }
    }

    internal static bool TryIsEnemy(Character a, Character b, out bool isEnemy)
    {
        isEnemy = false;
        if ((Object)(object)a == (Object)(object)b)
        {
            return true;
        }

        if (!a || !b)
        {
            return true;
        }

        Character.Faction sourceFaction = a.GetFaction();
        Character.Faction originalTargetFaction = b.GetFaction();
        FactionData? data;
        bool targetFactionManaged;
        lock (Sync)
        {
            ActiveSnapshot.FactionDataByFaction.TryGetValue(sourceFaction, out data);
            targetFactionManaged = ActiveSnapshot.FactionDataByFaction.ContainsKey(originalTargetFaction);
        }

        if (data == null || !targetFactionManaged)
        {
            return false;
        }

        bool aTamed = a.IsTamed();
        bool bTamed = b.IsTamed();
        if (aTamed && bTamed)
        {
            isEnemy = false;
            return true;
        }

        if (aTamed)
        {
            isEnemy = BaseAI.IsEnemy(b, a);
            return true;
        }

        BaseAI bAi = b.GetBaseAI();
        if (a.IsPlayer() && !bTamed && bAi && bAi.IsAggravatable())
        {
            isEnemy = BaseAI.IsEnemy(b, a);
            return true;
        }

        string group = a.GetGroup();
        if (group.Length > 0 && group == b.GetGroup())
        {
            isEnemy = false;
            return true;
        }

        Character.Faction targetFaction = b.GetFaction();
        if (bTamed)
        {
            targetFaction = Character.Faction.Players;
        }

        BaseAI aAi = a.GetBaseAI();
        if (data.AlertedFriendly != null && aAi && aAi.IsAlerted())
        {
            isEnemy = !data.AlertedFriendly.Contains(targetFaction);
        }
        else if (data.AggravatedFriendly != null && aAi && aAi.IsAggravated())
        {
            isEnemy = !data.AggravatedFriendly.Contains(targetFaction);
        }
        else
        {
            isEnemy = !data.Friendly.Contains(targetFaction);
        }

        return true;
    }

    // Dedicated servers need the same relationship policy without constructing Characters.
    // Keep live BaseAI.IsEnemy calls at the caller when both instances exist (including other mods' patches).
    internal static bool IsHostileFromSynchronizedState(ZDO a, Character prefabA, ZDO b, Character prefabB)
    {
        if (a.m_uid == b.m_uid) return false;
        Character.Faction factionA = GetSynchronizedFaction(a, prefabA);
        Character.Faction factionB = GetSynchronizedFaction(b, prefabB);
        bool tamedA = a.GetBool(ZDOVars.s_tamed, false), tamedB = b.GetBool(ZDOVars.s_tamed, false);
        if (tamedA && tamedB) return false;
        if (prefabA.m_group.Length > 0 && prefabA.m_group == prefabB.m_group) return false;
        BaseAI? aiA = prefabA.GetComponent<BaseAI>(), aiB = prefabB.GetComponent<BaseAI>();
        FactionData? dataA, dataB;

        lock (Sync)
        {
            ActiveSnapshot.FactionDataByFaction.TryGetValue(factionA, out dataA);
            ActiveSnapshot.FactionDataByFaction.TryGetValue(factionB, out dataB);
            if (dataA != null && dataB != null)
            {
                // Tamed sources and players facing aggravatable creatures defer to the other side.
                bool EnemyA() => IsSynchronizedFactionEnemy(a, dataA, tamedB ? Character.Faction.Players : factionB, aiA != null);
                bool EnemyB() => IsSynchronizedFactionEnemy(b, dataB, tamedA ? Character.Faction.Players : factionA, aiB != null);
                if (tamedA || prefabA.IsPlayer() && !tamedB && aiB != null && dataB.AggravatedFriendly != null) return EnemyB();
                if (tamedB || prefabB.IsPlayer() && !tamedA && aiA != null && dataA.AggravatedFriendly != null) return EnemyA();
                return EnemyA() || EnemyB();
            }
        }

        // Same fallback as BaseAI.IsEnemy when either faction is outside CM's configured map.
        bool aggravatedA = aiA != null && (dataA != null ? dataA.AggravatedFriendly != null : aiA.m_aggravatable) &&
            a.GetBool(ZDOVars.s_aggravated, false);
        bool aggravatedB = aiB != null && (dataB != null ? dataB.AggravatedFriendly != null : aiB.m_aggravatable) &&
            b.GetBool(ZDOVars.s_aggravated, false);
        if (tamedA || tamedB)
            return !(tamedA && factionB == Character.Faction.Players || tamedB && factionA == Character.Faction.Players ||
                     tamedA && factionB == Character.Faction.Dverger && !aggravatedB ||
                     tamedB && factionA == Character.Faction.Dverger && !aggravatedA);
        if (aggravatedA && factionB == Character.Faction.Players || aggravatedB && factionA == Character.Faction.Players)
            return true;
        return IsVanillaFactionEnemy(factionA, factionB) || IsVanillaFactionEnemy(factionB, factionA);
    }

    private static Character.Faction GetSynchronizedFaction(ZDO zdo, Character prefab)
    {
        string name = zdo.GetString(FactionHash, "");
        if (!string.IsNullOrWhiteSpace(name) && TryGetFaction(name, out Character.Faction faction)) return faction;
        int stored = zdo.GetInt(FactionHash, 0);
        return stored != 0 ? (Character.Faction)stored : prefab.m_faction;
    }

    private static bool IsSynchronizedFactionEnemy(ZDO zdo, FactionData data, Character.Faction target, bool hasAi)
    {
        HashSet<Character.Faction> friendly = hasAi && data.AlertedFriendly != null && zdo.GetBool(ZDOVars.s_alert, false)
            ? data.AlertedFriendly
            : hasAi && data.AggravatedFriendly != null && zdo.GetBool(ZDOVars.s_aggravated, false)
                ? data.AggravatedFriendly : data.Friendly;
        return !friendly.Contains(target);
    }

    private static bool IsVanillaFactionEnemy(Character.Faction source, Character.Faction target)
    {
        if (source == target) return false;
        return source switch
        {
            Character.Faction.AnimalsVeg or Character.Faction.PlayerSpawned => true,
            Character.Faction.Players => target != Character.Faction.Dverger,
            Character.Faction.ForestMonsters or Character.Faction.MistlandsMonsters or Character.Faction.DeepNorth =>
                target != Character.Faction.AnimalsVeg && target != Character.Faction.Boss,
            Character.Faction.Undead => target != Character.Faction.Demon && target != Character.Faction.Boss,
            Character.Faction.Demon => target != Character.Faction.Undead && target != Character.Faction.Boss,
            Character.Faction.MountainMonsters or Character.Faction.SeaMonsters or Character.Faction.PlainsMonsters =>
                target != Character.Faction.Boss,
            Character.Faction.Dverger => target != Character.Faction.AnimalsVeg && target != Character.Faction.Boss &&
                target != Character.Faction.Players,
            Character.Faction.Boss => target is Character.Faction.Players or Character.Faction.PlayerSpawned,
            Character.Faction.TrainingDummy => target == Character.Faction.Players,
            _ => false
        };
    }

    internal static string BuildDefaultOverrideYaml()
    {
        StringBuilder builder = new();
        builder.AppendLine("# CreatureManager faction configuration.");
        builder.AppendLine("# Vanilla faction relationships are listed below as comments, then repeated as active YAML.");
        builder.AppendLine("# Edit these entries or append custom factions. Custom ids should normally start at 100.");
        builder.AppendLine("# friendly/aggravatedFriendly/alertedFriendly accept faction names or [All].");
        builder.AppendLine("# Faction relationships are global live rules and immediately affect loaded creatures.");
        builder.AppendLine("# Give custom factions explicit stable ids; do not change an id while persisted creatures use it.");
        builder.AppendLine("#");
        foreach (FactionDefinition definition in DefaultFactionDefinitions())
        {
            AppendCommentedFaction(builder, definition);
        }

        builder.AppendLine();
        foreach (FactionDefinition definition in DefaultFactionDefinitions())
        {
            AppendFaction(builder, definition, commented: false);
        }

        builder.AppendLine();
        builder.AppendLine("# Custom example:");
        builder.AppendLine("# - faction: MyRaiders");
        builder.AppendLine("#   id: 100");
        builder.AppendLine("#   friendly: [MyRaiders, PlainsMonsters]");
        builder.AppendLine("#   aggravatedFriendly: [MyRaiders]");
        builder.AppendLine("#   alertedFriendly: [MyRaiders]");
        return builder.ToString();
    }

    private static void AppendCommentedFaction(StringBuilder builder, FactionDefinition definition)
    {
        AppendFaction(builder, definition, commented: true);
    }

    private static void AppendFaction(StringBuilder builder, FactionDefinition definition, bool commented)
    {
        string prefix = commented ? "# " : "";
        builder.Append(prefix).Append("- faction: ").AppendLine(definition.Faction);
        builder.Append(prefix).Append("  id: ").AppendLine(definition.Id?.ToString() ?? "null");
        builder.Append(prefix).Append("  friendly: ").AppendLine(FormatInlineList(definition.Friendly));
        if (definition.AggravatedFriendly != null)
        {
            builder.Append(prefix).Append("  aggravatedFriendly: ").AppendLine(FormatInlineList(definition.AggravatedFriendly));
        }

        if (definition.AlertedFriendly != null)
        {
            builder.Append(prefix).Append("  alertedFriendly: ").AppendLine(FormatInlineList(definition.AlertedFriendly));
        }
    }

    private static string FormatInlineList(List<string>? values)
    {
        return values == null || values.Count == 0 ? "[]" : $"[{string.Join(", ", values)}]";
    }

    private static bool TryResolveOptionalFactionSet(
        List<string>? values,
        Dictionary<string, Character.Faction> names,
        HashSet<Character.Faction> allFactions,
        string owner,
        string field,
        List<string> errors,
        out HashSet<Character.Faction>? factions)
    {
        if (values == null)
        {
            factions = null;
            return true;
        }

        bool valid = TryResolveFactionSet(values, names, allFactions, owner, field, errors, out HashSet<Character.Faction> resolved);
        factions = resolved;
        return valid;
    }

    private static bool TryResolveFactionSet(
        List<string>? values,
        Dictionary<string, Character.Faction> names,
        HashSet<Character.Faction> allFactions,
        string owner,
        string field,
        List<string> errors,
        out HashSet<Character.Faction> factions)
    {
        factions = new HashSet<Character.Faction>();
        if (values == null)
        {
            return true;
        }

        bool valid = true;
        for (int index = 0; index < values.Count; index++)
        {
            string? rawValue = values[index];
            string? value = NormalizeName(rawValue);
            if (value == null)
            {
                errors.Add($"faction '{owner}' field '{field}' contains an empty target at position {index + 1}.");
                valid = false;
                continue;
            }

            if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
            {
                factions.UnionWith(allFactions);
                continue;
            }

            if (names.TryGetValue(value, out Character.Faction faction))
            {
                factions.Add(faction);
            }
            else
            {
                errors.Add($"faction '{owner}' field '{field}' references unknown faction '{value}'.");
                valid = false;
            }
        }

        return valid;
    }

    private static Dictionary<string, Character.Faction> BuildRuntimeFactionMap()
    {
        Dictionary<string, Character.Faction> factions = new(StringComparer.OrdinalIgnoreCase);
        foreach (FieldInfo field in typeof(Character.Faction).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral || field.GetRawConstantValue() is not object rawValue)
            {
                continue;
            }

            factions[field.Name] = (Character.Faction)Convert.ToInt32(rawValue);
        }

        // Some mods append names and values through separate Enum Harmony patches without extending Enum.Parse.
        // Pair the two runtime arrays just as those mods do, while the declared fields above remain a safe fallback.
        string[] runtimeNames = Enum.GetNames(typeof(Character.Faction));
        Array runtimeValues = Enum.GetValues(typeof(Character.Faction));
        int pairedCount = Math.Min(runtimeNames.Length, runtimeValues.Length);
        for (int index = 0; index < pairedCount; index++)
        {
            string? name = NormalizeName(runtimeNames[index]);
            if (name == null || factions.ContainsKey(name) ||
                runtimeValues.GetValue(index) is not Character.Faction faction)
            {
                continue;
            }

            factions[name] = faction;
        }

        return factions;
    }

    private static string GetRuntimeNameForId(
        IReadOnlyDictionary<string, Character.Faction> runtimeFactions,
        int id)
    {
        return runtimeFactions
            .Where(entry => (int)entry.Value == id)
            .Select(entry => entry.Key)
            .FirstOrDefault() ?? "unknown";
    }

    private static int GetNextCustomId(HashSet<int> reservedIds, ref int nextCustomId)
    {
        while (reservedIds.Contains(nextCustomId))
        {
            nextCustomId++;
        }

        return nextCustomId++;
    }

    private static string? NormalizeName(string? value)
    {
        string trimmed = (value ?? "").Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static void RefreshLiveBaseAis()
    {
        try
        {
            foreach (BaseAI baseAi in BaseAI.GetAllInstances())
            {
                if (baseAi == null)
                {
                    continue;
                }

                try
                {
                    SetupBaseAi(baseAi);
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogWarning(
                        $"Failed to refresh live faction state for '{baseAi.name}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Failed to enumerate live creatures while refreshing faction state: {ex.Message}");
        }
    }

    private static List<FactionDefinition> DefaultFactionDefinitions()
    {
        List<string> bossFriendly = new()
        {
            "AnimalsVeg", "ForestMonsters", "Undead", "Demon", "MountainMonsters", "SeaMonsters",
            "PlainsMonsters", "Boss", "MistlandsMonsters", "Dverger", "TrainingDummy"
        };

        List<string> nonPlayerFriendly = new()
        {
            "AnimalsVeg", "ForestMonsters", "Undead", "Demon", "MountainMonsters", "SeaMonsters",
            "PlainsMonsters", "Boss", "MistlandsMonsters", "Dverger", "PlayerSpawned", "TrainingDummy"
        };

        return new List<FactionDefinition>
        {
            Create("Players", 0, "Players", "Dverger"),
            Create("AnimalsVeg", 1, "AnimalsVeg"),
            Create("ForestMonsters", 2, "ForestMonsters", "AnimalsVeg", "Boss"),
            Create("Undead", 3, "Undead", "Demon", "Boss"),
            Create("Demon", 4, "Demon", "Undead", "Boss"),
            Create("MountainMonsters", 5, "MountainMonsters", "Boss"),
            Create("SeaMonsters", 6, "SeaMonsters", "Boss"),
            Create("PlainsMonsters", 7, "PlainsMonsters", "Boss"),
            new() { Faction = "Boss", Id = 8, Friendly = bossFriendly },
            Create("MistlandsMonsters", 9, "MistlandsMonsters", "AnimalsVeg", "Boss"),
            new()
            {
                Faction = "Dverger",
                Id = 10,
                Friendly = new List<string> { "Dverger", "AnimalsVeg", "Boss", "Players" },
                AggravatedFriendly = new List<string> { "Dverger", "AnimalsVeg", "Boss" }
            },
            Create("PlayerSpawned", 11, "PlayerSpawned"),
            new() { Faction = "TrainingDummy", Id = 12, Friendly = nonPlayerFriendly }
        };
    }

    private static FactionDefinition Create(string name, int id, params string[] friendly)
    {
        return new FactionDefinition
        {
            Faction = name,
            Id = id,
            Friendly = friendly.ToList()
        };
    }

    private sealed class FactionData
    {
        public FactionData(
            HashSet<Character.Faction> friendly,
            HashSet<Character.Faction>? aggravatedFriendly,
            HashSet<Character.Faction>? alertedFriendly)
        {
            Friendly = friendly;
            AggravatedFriendly = aggravatedFriendly;
            AlertedFriendly = alertedFriendly;
        }

        public HashSet<Character.Faction> Friendly { get; }
        public HashSet<Character.Faction>? AggravatedFriendly { get; }
        public HashSet<Character.Faction>? AlertedFriendly { get; }
    }

    private sealed class FactionSnapshot
    {
        public FactionSnapshot(
            Dictionary<string, Character.Faction> nameToFaction,
            Dictionary<string, Character.Faction> runtimeNameToFaction,
            Dictionary<Character.Faction, string> factionToName,
            Dictionary<Character.Faction, FactionData> factionDataByFaction)
        {
            NameToFaction = nameToFaction;
            RuntimeNameToFaction = runtimeNameToFaction;
            FactionToName = factionToName;
            FactionDataByFaction = factionDataByFaction;
        }

        public Dictionary<string, Character.Faction> NameToFaction { get; }
        public Dictionary<string, Character.Faction> RuntimeNameToFaction { get; }
        public Dictionary<Character.Faction, string> FactionToName { get; }
        public Dictionary<Character.Faction, FactionData> FactionDataByFaction { get; }
    }
}

public static class CreatureManagerFactionApi
{
    public static bool TryResolve(string? name, out Character.Faction faction)
    {
        return CreatureFactionManager.TryGetRegisteredFaction(name, out faction, out _);
    }

    public static bool TryApply(Character? character, string? name)
    {
        if (character == null ||
            !CreatureFactionManager.TryGetRegisteredFaction(name, out Character.Faction faction, out string canonicalName))
        {
            return false;
        }

        character.m_faction = faction;
        CreatureFactionManager.ApplyFactionToZdo(character, canonicalName);

        BaseAI? baseAi = character.GetBaseAI();
        if (baseAi != null)
        {
            CreatureFactionManager.SetupBaseAi(baseAi);
        }

        return true;
    }

    public static string[] GetNames()
    {
        return CreatureFactionManager.GetRegisteredFactionNames();
    }
}
