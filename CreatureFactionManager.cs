using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CreatureManager;

internal static class CreatureFactionManager
{
    private const int CustomFactionStartId = 100;
    private static readonly int FactionHash = StringExtensionMethods.GetStableHashCode("faction");
    private static readonly object Sync = new();
    private static Dictionary<string, Character.Faction> NameToFaction = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<Character.Faction, string> FactionToName = new();
    private static Dictionary<Character.Faction, FactionData> FactionDataByFaction = new();
    private static HashSet<Character.Faction> Aggravatable = new();

    static CreatureFactionManager()
    {
        Load(DefaultFactionDefinitions());
    }

    internal static void Load(List<FactionDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            definitions = DefaultFactionDefinitions();
        }

        Dictionary<string, Character.Faction> names = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<Character.Faction, string> displayNames = new();
        int nextCustomId = CustomFactionStartId;

        foreach (FactionDefinition definition in definitions)
        {
            string? name = NormalizeName(definition.Faction);
            if (name == null)
            {
                continue;
            }

            int id = definition.Id ?? GetDefaultId(name, ref nextCustomId);
            Character.Faction faction = (Character.Faction)id;
            if (names.ContainsKey(name))
            {
                CreatureManagerPlugin.Log.LogWarning($"Duplicate faction name '{name}' skipped.");
                continue;
            }

            if (displayNames.ContainsKey(faction))
            {
                CreatureManagerPlugin.Log.LogWarning($"Duplicate faction id '{id}' for '{name}' skipped.");
                continue;
            }

            names[name] = faction;
            displayNames[faction] = name;
        }

        Dictionary<Character.Faction, FactionData> data = new();
        HashSet<Character.Faction> aggravatable = new();
        foreach (FactionDefinition definition in definitions)
        {
            string? name = NormalizeName(definition.Faction);
            if (name == null || !names.TryGetValue(name, out Character.Faction faction))
            {
                continue;
            }

            FactionData factionData = new(
                ResolveFactionSet(definition.Friendly, names, displayNames.Keys),
                definition.AggravatedFriendly == null ? null : ResolveFactionSet(definition.AggravatedFriendly, names, displayNames.Keys),
                definition.AlertedFriendly == null ? null : ResolveFactionSet(definition.AlertedFriendly, names, displayNames.Keys));

            data[faction] = factionData;
            if (factionData.AggravatedFriendly != null)
            {
                aggravatable.Add(faction);
            }
        }

        lock (Sync)
        {
            NameToFaction = names;
            FactionToName = displayNames;
            FactionDataByFaction = data;
            Aggravatable = aggravatable;
        }

        RefreshLiveBaseAis();
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
            if (NameToFaction.TryGetValue(normalized, out faction))
            {
                return true;
            }
        }

        if (int.TryParse(normalized, out int id))
        {
            faction = (Character.Faction)id;
            return true;
        }

        return Enum.TryParse(normalized, true, out faction);
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
            if (NameToFaction.TryGetValue(normalized, out faction))
            {
                canonicalName = FactionToName[faction];
                return true;
            }

            if (int.TryParse(normalized, out int id) &&
                FactionToName.TryGetValue((Character.Faction)id, out canonicalName))
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
            return FactionToName
                .OrderBy(entry => (int)entry.Key)
                .Select(entry => entry.Value)
                .ToArray();
        }
    }

    internal static string GetDisplayName(Character.Faction faction)
    {
        lock (Sync)
        {
            return FactionToName.TryGetValue(faction, out string name) ? name : faction.ToString();
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
            baseAi.m_aggravatable = Aggravatable.Contains(baseAi.m_character.m_faction);
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

        FactionData? data;
        lock (Sync)
        {
            FactionDataByFaction.TryGetValue(a.GetFaction(), out data);
        }

        if (data == null)
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

    internal static string BuildDefaultOverrideYaml()
    {
        StringBuilder builder = new();
        builder.AppendLine("# CreatureManager faction configuration.");
        builder.AppendLine("# Vanilla faction relationships are listed below as comments, then repeated as active YAML.");
        builder.AppendLine("# Edit these entries or append custom factions. Custom ids should normally start at 100.");
        builder.AppendLine("# friendly/aggravatedFriendly/alertedFriendly accept faction names or [All].");
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

    private static HashSet<Character.Faction> ResolveFactionSet(
        List<string>? values,
        Dictionary<string, Character.Faction> names,
        IEnumerable<Character.Faction> allFactions)
    {
        HashSet<Character.Faction> factions = new();
        if (values == null)
        {
            return factions;
        }

        foreach (string rawValue in values)
        {
            string? value = NormalizeName(rawValue);
            if (value == null)
            {
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
                CreatureManagerPlugin.Log.LogWarning($"Unknown faction relationship target '{value}'.");
            }
        }

        return factions;
    }

    private static int GetDefaultId(string name, ref int nextCustomId)
    {
        if (Enum.TryParse(name, true, out Character.Faction faction))
        {
            return (int)faction;
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
        foreach (BaseAI baseAi in BaseAI.GetAllInstances())
        {
            if (baseAi != null)
            {
                SetupBaseAi(baseAi);
            }
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
