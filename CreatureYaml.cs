using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CreatureManager;

internal sealed class ServerLocalizationPayload
{
    public int Version { get; set; }
    public Dictionary<string, Dictionary<string, string>>? Languages { get; set; }
}

internal static class CreatureYaml
{
    internal const int MaxSpawnPrefabWeight = 1000;
    internal const int MaxExpandedSpawnPrefabCount = 4096;
    internal const int ServerLocalizationPayloadVersion = 1;
    internal const int MaxLocalizationLanguageCount = 32;
    internal const int MaxLocalizationTokensPerLanguage = 8192;
    internal const int MaxLocalizationTokenLength = 128;
    internal const int MaxLocalizationTextLength = 4096;

    internal enum ModifierYamlContext
    {
        Level,
        Karma
    }

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    private static readonly HashSet<string> LevelFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "level",
        "damage",
        "damagePerLevel",
        "health",
        "healthPerLevel",
        "scalePerLevel",
        "distanceScaling",
        "modifiers",
        "modifierDistanceScaling"
    };

    internal static bool TryReadDefinitions<T>(string yaml, string source, out List<T> definitions)
    {
        definitions = new List<T>();
        if (string.IsNullOrWhiteSpace(yaml) || IsCommentOnlyYaml(yaml))
        {
            return true;
        }

        try
        {
            ValidateDefinitionDocument(yaml, source);
            definitions = Deserializer.Deserialize<List<T>>(yaml) ?? new List<T>();
            return ValidateDefinitions(definitions, source);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read YAML from {source}: {ex.Message}");
            return false;
        }
    }

    internal static bool ValidateDefinitions<T>(IReadOnlyList<T> definitions, string source)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            object? definition = definitions[index];
            string path = $"{source}[{index + 1}]";
            if (definition == null)
            {
                return Invalid(path, "definition must be a mapping, not null.");
            }

            bool valid = definition switch
            {
                FactionDefinition faction => ValidateFactionDefinition(faction, path),
                LevelDefinition level => ValidateNormalizedLevelDefinition(level, path),
                AiDefinition ai => ValidateAiDefinition(ai, path),
                AttackDefinition attack => ValidateAttackDefinition(attack, path),
                ProjectileDefinition projectile => ValidateProjectileDefinition(projectile, path),
                CreatureDefinition creature => ValidateCreatureDefinition(creature, path),
                _ => true
            };
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateFactionDefinition(FactionDefinition definition, string path)
    {
        if (string.IsNullOrWhiteSpace(definition.Faction))
        {
            return Invalid(path, "faction must be a non-empty name.");
        }

        return ValidateNameList(definition.Friendly, path, "friendly") &&
               ValidateNameList(definition.AggravatedFriendly, path, "aggravatedFriendly") &&
               ValidateNameList(definition.AlertedFriendly, path, "alertedFriendly");
    }

    private static bool ValidateAiDefinition(AiDefinition definition, string path)
    {
        if (string.IsNullOrWhiteSpace(definition.Ai))
        {
            return Invalid(path, "ai must be a non-empty name.");
        }

        BaseAiDefinition? baseAi = definition.BaseAI;
        if (baseAi != null &&
            (!ValidateStringTuple(baseAi.Senses, 4, path, "baseAI.senses", "float", "float", "float", "bool") ||
             !ValidateFloatTuple(baseAi.IdleSound, 2, path, "baseAI.idleSound") ||
             !ValidateStringTuple(baseAi.Movement, 4, path, "baseAI.movement", "bool", "name", "float", "bool") ||
             !ValidateStringTuple(baseAi.Serpent, 2, path, "baseAI.serpent", "bool", "float") ||
             !ValidateFloatTuple(baseAi.RandomMove, 4, path, "baseAI.randomMove") ||
             !ValidateStringTuple(baseAi.Flight, 10, path, "baseAI.flight", "bool", "float", "float", "float", "float", "float", "float", "float", "float", "float") ||
             !ValidateStringTuple(baseAi.Avoid, 6, path, "baseAI.avoid", "bool", "bool", "bool", "bool", "bool", "bool") ||
             !ValidateFloatTuple(baseAi.Flee, 3, path, "baseAI.flee") ||
             !ValidateStringTuple(baseAi.Aggressive, 2, path, "baseAI.aggressive", "bool", "bool") ||
             !ValidateStringTuple(baseAi.Messages, 3, path, "baseAI.messages", "text", "text", "text")))
        {
            return false;
        }

        MonsterAiDefinition? monsterAi = definition.MonsterAI;
        return monsterAi == null ||
               IsFinite(monsterAi.AlertRange, path, "monsterAI.alertRange") &&
               ValidateStringTuple(monsterAi.Hunt, 3, path, "monsterAI.hunt", "bool", "bool", "int") &&
               ValidateFloatTuple(monsterAi.Chase, 4, path, "monsterAI.chase") &&
               ValidateFloatTuple(monsterAi.Circle, 3, path, "monsterAI.circle") &&
               ValidateStringTuple(monsterAi.HurtFlee, 7, path, "monsterAI.hurtFlee", "bool", "float", "float", "bool", "float", "float", "bool") &&
               ValidateStringTuple(monsterAi.Charge, 2, path, "monsterAI.charge", "bool", "bool") &&
               ValidateStringTuple(monsterAi.Sleep, 7, path, "monsterAI.sleep", "bool", "float", "bool", "float", "float", "float", "float");
    }

    private static bool ValidateAttackDefinition(AttackDefinition definition, string path)
    {
        if (string.IsNullOrWhiteSpace(definition.Prefab))
        {
            return Invalid(path, "prefab must be a non-empty name.");
        }

        AttackDamageDefinition? damage = definition.Damage;
        if (damage != null && !AllFinite(path, "damage", damage.Damage, damage.Blunt, damage.Slash, damage.Pierce,
                damage.Chop, damage.Pickaxe, damage.Fire, damage.Frost, damage.Lightning, damage.Poison,
                damage.Spirit, damage.AttackForce))
        {
            return false;
        }

        if (definition.Attack != null)
        {
            string[] values = CleanStringTuple(definition.Attack);
            if (values.Length != 2 || values.Any(value => value.Length == 0) ||
                !Enum.TryParse(values[0], true, out Attack.AttackType attackType) ||
                !Enum.IsDefined(typeof(Attack.AttackType), attackType))
            {
                return Invalid(path, "attack must be [type, animation] with a valid attack type and two non-empty values.");
            }
        }

        if (definition.StatusEffect != null)
        {
            string[] values = CleanStringTuple(definition.StatusEffect);
            if (values.Length != 2 || values[0].Length == 0 || !TryParseFiniteFloat(values[1], out _))
            {
                return Invalid(path, "statusEffect must be [effect, chance] with a finite numeric chance.");
            }
        }

        if (definition.Projectile != null)
        {
            string[] values = CleanStringTuple(definition.Projectile);
            if (values.Length is < 1 or > 4 ||
                values[0].Length == 0 ||
                values.Length >= 2 && !TryParseFiniteFloat(values[1], out _) ||
                values.Length >= 3 && !TryParseFiniteFloat(values[2], out _) ||
                values.Length >= 4 && !int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return Invalid(path, "projectile must be [prefab[, velocity[, accuracy[, count]]]] with valid numeric values.");
            }
        }

        return ValidateFloatTuple(definition.Ai, 4, path, "ai");
    }

    private static bool ValidateProjectileDefinition(ProjectileDefinition definition, string path)
    {
        if (string.IsNullOrWhiteSpace(definition.Prefab))
        {
            return Invalid(path, "prefab must be a non-empty name.");
        }

        ProjectileComponentDefinition? projectile = definition.Projectile;
        if (projectile != null)
        {
            if (!projectile.HasSpecifiedFields)
            {
                definition.Projectile = null;
            }
            else if (projectile.SpawnOnHit != null && string.IsNullOrWhiteSpace(projectile.SpawnOnHit))
            {
                return Invalid(path, "projectile.spawnOnHit must be a non-empty prefab name or null.");
            }
        }

        SpawnAbilityDefinition? spawnAbility = definition.SpawnAbility;
        if (spawnAbility?.SpawnPrefabsSpecified == true)
        {
            if (!TryParseSpawnPrefabEntries(spawnAbility.SpawnPrefabs, out _, out string error))
            {
                return Invalid(path, $"spawnAbility.spawnPrefabs is invalid: {error}");
            }
        }

        return true;
    }

    private static bool ValidateCreatureDefinition(CreatureDefinition definition, string path)
    {
        if (string.IsNullOrWhiteSpace(definition.Prefab))
        {
            return Invalid(path, "prefab must be a non-empty name.");
        }

        if (!IsFinite(definition.Scale, path, "scale") ||
            !ValidateNameList(definition.AvailableAttackAnimations, path, "availableAttackAnimations"))
        {
            return false;
        }

        CharacterDefinition? character = definition.Character;
        if (character != null)
        {
            if (character.Boss != null && !TryParseBossTuple(character.Boss, out _, out _, out _, out string bossError))
                return Invalid(path, $"character.boss is invalid: {bossError}");
            if (character.Health != null && !TryParseHealthTuple(character.Health, out _, out _, out string healthError))
                return Invalid(path, $"character.health is invalid: {healthError}");
            if (character.Speed != null && !TryParseFloatTuple(character.Speed, 7, "speed", out _, out string speedError))
                return Invalid(path, $"character.speed is invalid: {speedError}");
            if (character.Jump != null && !TryParseFloatTuple(character.Jump, 5, "jump", out _, out string jumpError))
                return Invalid(path, $"character.jump is invalid: {jumpError}");
            if (character.Swim != null && !TryParseBoolFloatTuple(character.Swim, 4, "swim", out _, out _, out string swimError))
                return Invalid(path, $"character.swim is invalid: {swimError}");
            if (character.Flight != null && !TryParseBoolFloatTuple(character.Flight, 3, "flight", out _, out _, out string flightError))
                return Invalid(path, $"character.flight is invalid: {flightError}");
            if (!ValidateDamageModifiers(character.DamageModifiers, path))
                return false;
        }

        HumanoidDefinition? humanoid = definition.Humanoid;
        if (humanoid != null &&
            (!ValidateNameList(humanoid.DefaultItems, path, "humanoid.defaultItems") ||
             !ValidateNameList(humanoid.RandomWeapon, path, "humanoid.randomWeapon") ||
             !ValidateNameList(humanoid.RandomArmor, path, "humanoid.randomArmor") ||
             !ValidateNameList(humanoid.RandomHair, path, "humanoid.randomHair") ||
             !ValidateNameList(humanoid.RandomShield, path, "humanoid.randomShield") ||
             !ValidateRandomItems(humanoid.RandomItems, path) ||
             !ValidateRandomSets(humanoid.RandomSets, path)))
        {
            return false;
        }

        AppearanceDefinition? appearance = definition.Appearance;
        if (appearance != null)
        {
            if (!appearance.HasSpecifiedFields)
            {
                definition.Appearance = null;
            }
            else
            {
                if (appearance.HairColor != null && !TryParseAppearanceColor(appearance.HairColor, out _))
                    return Invalid(path, "appearance.hairColor must use #RRGGBB format.");
                if (appearance.SkinColor != null && !TryParseAppearanceColor(appearance.SkinColor, out _))
                    return Invalid(path, "appearance.skinColor must use #RRGGBB format.");
                if (appearance.ModelIndex is < 0)
                    return Invalid(path, "appearance.modelIndex must be 0 or greater.");
            }
        }

        return ValidateTextureOverrides(definition.Textures, path);
    }

    private static bool ValidateDamageModifiers(DamageModifiersDefinition? definition, string path)
    {
        if (definition == null)
        {
            return true;
        }

        string?[] values =
        {
            definition.Blunt, definition.Slash, definition.Pierce, definition.Chop, definition.Pickaxe,
            definition.Fire, definition.Frost, definition.Lightning, definition.Poison, definition.Spirit
        };
        foreach (string? value in values)
        {
            if (value != null &&
                (!Enum.TryParse(value, true, out HitData.DamageModifier modifier) ||
                 !Enum.IsDefined(typeof(HitData.DamageModifier), modifier)))
            {
                return Invalid(path, $"character.damageModifiers has unknown value '{value}'.");
            }
        }

        return true;
    }

    private static bool ValidateRandomItems(List<string>? values, string path)
    {
        if (values == null) return true;
        foreach (string value in values)
        {
            if (!TryParseRandomItemTuple(value, out _, out _, out string error))
                return Invalid(path, $"humanoid.randomItems is invalid: {error}");
        }
        return true;
    }

    private static bool ValidateRandomSets(List<string>? values, string path)
    {
        if (values == null) return true;
        foreach (string value in values)
        {
            if (!TryParseRandomSetTuple(value, out _, out _, out string error))
                return Invalid(path, $"humanoid.randomSets is invalid: {error}");
        }
        return true;
    }

    private static bool ValidateTextureOverrides(List<string>? values, string path)
    {
        if (values == null) return true;
        foreach (string value in values)
        {
            string[] tokens = (value ?? "").Split(':');
            if (tokens.Length != 3 || string.IsNullOrWhiteSpace(tokens[0]) || string.IsNullOrWhiteSpace(tokens[2]) ||
                !int.TryParse(tokens[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return Invalid(path, $"textures entry '{value}' must be rendererName:materialIndex:textureName.");
            }
        }
        return true;
    }

    private static bool ValidateNameList(List<string>? values, string path, string field)
    {
        if (values == null) return true;
        return values.All(value => !string.IsNullOrWhiteSpace(value)) || Invalid(path, $"{field} must contain only non-empty names.");
    }

    private static bool ValidateStringTuple(List<string>? values, int count, string path, string field, params string[] types)
    {
        if (values == null) return true;
        if (values.Count != count || types.Length != count)
            return Invalid(path, $"{field} must contain exactly {count} values.");

        for (int index = 0; index < count; index++)
        {
            string value = values[index]?.Trim() ?? "";
            bool valid = types[index] switch
            {
                "bool" => TryParseFlexibleBool(value, out _),
                "float" => TryParseFiniteFloat(value, out _),
                "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                "name" => value.Length > 0,
                "text" => values[index] != null,
                _ => false
            };
            if (!valid)
                return Invalid(path, $"{field}[{index + 1}] must be a valid {types[index]} value.");
        }

        return true;
    }

    private static bool ValidateFloatTuple(List<float>? values, int count, string path, string field)
    {
        if (values == null) return true;
        if (values.Count != count)
            return Invalid(path, $"{field} must contain exactly {count} values.");
        return values.All(IsFinite) || Invalid(path, $"{field} must contain only finite numbers.");
    }

    private static bool AllFinite(string path, string field, params float?[] values)
    {
        return values.All(value => !value.HasValue || IsFinite(value.Value)) || Invalid(path, $"{field} must contain only finite numbers.");
    }

    private static bool IsFinite(float? value, string path, string field)
    {
        return !value.HasValue || IsFinite(value.Value) || Invalid(path, $"{field} must be a finite number.");
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool TryParseFiniteFloat(string value, out float parsed)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && IsFinite(parsed);
    }

    private static bool TryParseFlexibleBool(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed)) return true;
        switch (value.Trim().ToLowerInvariant())
        {
            case "1": case "yes": case "y": case "on": parsed = true; return true;
            case "0": case "no": case "n": case "off": parsed = false; return true;
            default: parsed = false; return false;
        }
    }

    private static string[] CleanStringTuple(IEnumerable<string> values)
    {
        return values.Select(value => value?.Trim() ?? "").ToArray();
    }

    private static bool Invalid(string path, string message)
    {
        CreatureManagerPlugin.Log.LogError($"Failed to read YAML from {path}: {message}");
        return false;
    }

    internal static bool TryReadLevelDefinitions(string yaml, string source, out List<LevelDefinition> definitions)
    {
        definitions = new List<LevelDefinition>();
        if (string.IsNullOrWhiteSpace(yaml) || IsCommentOnlyYaml(yaml))
        {
            return true;
        }

        try
        {
            YamlStream stream = new();
            using StringReader reader = new(yaml);
            stream.Load(reader);
            if (stream.Documents.Count == 0)
            {
                return true;
            }

            if (stream.Documents.Count != 1)
            {
                throw new FormatException($"Level YAML from {source} must contain exactly one YAML document.");
            }

            YamlNode rootNode = stream.Documents[0].RootNode;
            if (rootNode is YamlSequenceNode emptySequence && emptySequence.Children.Count == 0)
            {
                return true;
            }

            if (rootNode is not YamlMappingNode root)
            {
                CreatureManagerPlugin.Log.LogError($"Failed to read level YAML from {source}: expected a top-level mapping or an empty sequence.");
                return false;
            }

            ValidateUniqueMappingKeys(root, source, "root");

            Dictionary<string, List<string>> groups = ReadLevelGroups(root, source);
            List<LevelDefinition> nestedDefinitions = ReadNestedLevelDefinitions(root, groups, source, out HashSet<string> targetsWithNestedDefinitions);
            definitions = ReadTopLevelLevelDefinitions(root, groups, source, targetsWithNestedDefinitions);
            definitions.AddRange(nestedDefinitions);
            return ValidateDefinitions(definitions, source);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read level YAML from {source}: {ex.Message}");
            definitions.Clear();
            return false;
        }
    }

    private static List<LevelDefinition> ReadTopLevelLevelDefinitions(YamlMappingNode root, Dictionary<string, List<string>> groups, string source, HashSet<string> targetsWithNestedDefinitions)
    {
        List<LevelDefinition> definitions = new();
        foreach (KeyValuePair<YamlNode, YamlNode> entry in root.Children)
        {
            string target = GetYamlScalar(entry.Key);
            if (target.Length == 0)
            {
                throw new FormatException($"Level YAML from {source} has an empty top-level block name.");
            }

            if (string.Equals(target, "groups", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsUnsupportedLevelTarget(target))
            {
                throw new FormatException($"Level YAML from {source} uses unsupported top-level block '{target}'. Use 'Boss'.");
            }

            if (entry.Value is not YamlMappingNode targetMap)
            {
                throw new FormatException($"Level YAML from {source} '{target}' must be a mapping.");
            }

            LevelDefinition rule = ReadLevelDefinition(targetMap, source, target, allowNestedDefinitions: true);
            rule.Target = target;
            if (groups.TryGetValue(target, out List<string>? prefabs) && prefabs != null)
            {
                rule.Prefabs = prefabs;
            }

            bool warnNoEffect = !targetsWithNestedDefinitions.Contains(target);
            if (ValidateLevelDefinition(rule, source, target, warnNoEffect))
            {
                definitions.Add(rule);
            }
        }

        return definitions;
    }

    private static Dictionary<string, List<string>> ReadLevelGroups(YamlMappingNode root, string source)
    {
        Dictionary<string, List<string>> groups = new(StringComparer.OrdinalIgnoreCase);
        YamlMappingNode? groupMap = null;
        foreach (KeyValuePair<YamlNode, YamlNode> rootEntry in root.Children)
        {
            if (!string.Equals(GetYamlScalar(rootEntry.Key), "groups", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            groupMap = rootEntry.Value as YamlMappingNode;
            if (groupMap == null)
            {
                throw new FormatException($"Level YAML from {source} 'groups' must be a mapping.");
            }

            break;
        }

        if (groupMap == null)
        {
            return groups;
        }

        foreach (KeyValuePair<YamlNode, YamlNode> entry in groupMap.Children)
        {
            string group = GetYamlScalar(entry.Key);
            if (group.Length == 0)
            {
                throw new FormatException($"Level YAML from {source} has an empty group name.");
            }

            if (entry.Value is not YamlSequenceNode sequence)
            {
                throw new FormatException($"Level YAML from {source} group '{group}' must be a prefab list.");
            }

            List<string> prefabs = new();
            foreach (YamlNode child in sequence.Children)
            {
                string prefab = GetYamlScalar(child);
                if (child is not YamlScalarNode || string.IsNullOrWhiteSpace(prefab))
                {
                    throw new FormatException($"Level YAML from {source} group '{group}' must contain only non-empty prefab names.");
                }

                if (!prefabs.Contains(prefab, StringComparer.OrdinalIgnoreCase))
                {
                    prefabs.Add(prefab);
                }
            }
            if (prefabs.Count == 0)
            {
                throw new FormatException($"Level YAML from {source} group '{group}' has no prefab names.");
            }

            groups[group] = prefabs;
        }

        return groups;
    }

    private static List<LevelDefinition> ReadNestedLevelDefinitions(YamlMappingNode root, Dictionary<string, List<string>> groups, string source, out HashSet<string> targetsWithNestedDefinitions)
    {
        List<LevelDefinition> definitions = new();
        targetsWithNestedDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<YamlNode, YamlNode> targetEntry in root.Children)
        {
            string target = GetYamlScalar(targetEntry.Key);
            if (target.Length == 0 || string.Equals(target, "groups", StringComparison.OrdinalIgnoreCase) ||
                IsUnsupportedLevelTarget(target) ||
                targetEntry.Value is not YamlMappingNode targetMap)
            {
                continue;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> nestedEntry in targetMap.Children)
            {
                string biome = GetYamlScalar(nestedEntry.Key);
                if (biome.Length == 0 || LevelFieldNames.Contains(biome) || nestedEntry.Value is not YamlMappingNode nestedMap)
                {
                    continue;
                }

                targetsWithNestedDefinitions.Add(target);
                string label = $"{target}.{biome}";
                LevelDefinition rule = ReadLevelDefinition(nestedMap, source, label);
                rule.Target = target;
                rule.Biome = biome;
                if (groups.TryGetValue(target, out List<string>? prefabs) && prefabs != null)
                {
                    rule.Prefabs = prefabs;
                }

                if (ValidateLevelDefinition(rule, source, label, warnNoEffect: true))
                {
                    definitions.Add(rule);
                }
            }
        }

        return definitions;
    }

    private static bool IsUnsupportedLevelTarget(string target)
    {
        return string.Equals(target, "Bosses", StringComparison.OrdinalIgnoreCase);
    }

    internal static void ValidateUniqueMappingKeys(YamlNode node, string source, string path)
    {
        if (node is YamlMappingNode mapping)
        {
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
            {
                string key = GetYamlScalar(entry.Key);
                if (!keys.Add(key))
                {
                    throw new FormatException($"YAML from {source} {path} has duplicate key '{key}' when compared case-insensitively.");
                }

                ValidateUniqueMappingKeys(entry.Value, source, $"{path}.{key}");
            }

            return;
        }

        if (node is YamlSequenceNode sequence)
        {
            for (int index = 0; index < sequence.Children.Count; index++)
            {
                ValidateUniqueMappingKeys(sequence.Children[index], source, $"{path}[{index + 1}]");
            }
        }
    }

    internal static void ValidateUniqueMappingKeysInDocument(string yaml, string source)
    {
        YamlNode root = LoadSingleDocumentRoot(yaml, source);
        ValidateUniqueMappingKeys(root, source, "root");
    }

    private static void ValidateDefinitionDocument(string yaml, string source)
    {
        YamlNode root = LoadSingleDocumentRoot(yaml, source);
        if (root is not YamlSequenceNode)
        {
            throw new FormatException(
                $"YAML from {source} must have a sequence root. Use [] for an intentionally empty definition file.");
        }

        ValidateUniqueMappingKeys(root, source, "root");
    }

    private static YamlNode LoadSingleDocumentRoot(string yaml, string source)
    {
        YamlStream stream = new();
        using StringReader reader = new(yaml);
        stream.Load(reader);
        if (stream.Documents.Count != 1)
        {
            throw new FormatException($"YAML from {source} must contain exactly one YAML document.");
        }

        return stream.Documents[0].RootNode;
    }

    private static LevelDefinition ReadLevelDefinition(
        YamlMappingNode node,
        string source,
        string label,
        bool allowNestedDefinitions = false)
    {
        LevelDefinition rule = new();
        foreach (KeyValuePair<YamlNode, YamlNode> entry in node.Children)
        {
            string field = GetYamlScalar(entry.Key);
            switch (field.ToLowerInvariant())
            {
                case "level":
                    rule.Level = ReadFloatList(entry.Value, source, label, "level");
                    break;
                case "damage":
                    rule.Damage = ReadFloat(entry.Value, source, label, "damage");
                    break;
                case "damageperlevel":
                    rule.DamagePerLevel = ReadFloat(entry.Value, source, label, "damagePerLevel");
                    break;
                case "health":
                    rule.Health = ReadFloat(entry.Value, source, label, "health");
                    break;
                case "healthperlevel":
                    rule.HealthPerLevel = ReadFloat(entry.Value, source, label, "healthPerLevel");
                    break;
                case "scaleperlevel":
                    rule.ScalePerLevel = ReadFloat(entry.Value, source, label, "scalePerLevel");
                    break;
                case "distancescaling":
                    rule.DistanceScaling = ReadFloatList(entry.Value, source, label, "distanceScaling");
                    break;
                case "modifierdistancescaling":
                    rule.ModifierDistanceScaling = ReadFloatList(entry.Value, source, label, "modifierDistanceScaling");
                    break;
                case "modifiers":
                    if (!TryReadModifierBlock(
                            entry.Value,
                            source,
                            label,
                            ModifierYamlContext.Level,
                            out Dictionary<string, ModifierDefinition>? modifiers,
                            out bool modifiersCleared))
                    {
                        throw new FormatException($"Level YAML from {source} '{label}'.modifiers is invalid.");
                    }
                    rule.Modifiers = modifiers;
                    rule.ModifiersCleared = modifiersCleared;
                    break;
                default:
                    if (allowNestedDefinitions && entry.Value is YamlMappingNode)
                    {
                        break;
                    }

                    throw new FormatException($"Level YAML from {source} '{label}' has unknown field '{field}'.");
            }
        }

        return rule;
    }

    private static List<float>? ReadFloatList(YamlNode node, string source, string label, string field)
    {
        if (node is not YamlSequenceNode sequence)
        {
            throw new FormatException($"Level YAML from {source} '{label}'.{field} must be a YAML list.");
        }

        List<float> values = new();
        foreach (YamlNode item in sequence.Children)
        {
            float? value = ReadFloat(item, source, label, field);
            if (!value.HasValue)
            {
                throw new FormatException($"Level YAML from {source} '{label}'.{field} contains an invalid number.");
            }

            values.Add(value.Value);
        }

        return values;
    }

    private static float? ReadFloat(YamlNode node, string source, string label, string field)
    {
        string text = GetYamlScalar(node);
        if (TryParseFiniteFloat(text, out float value))
        {
            return value;
        }

        throw new FormatException($"Level YAML from {source} '{label}'.{field} has invalid number '{text}'.");
    }

    internal static bool TryReadModifierBlock(
        YamlNode node,
        string source,
        string label,
        ModifierYamlContext context,
        out Dictionary<string, ModifierDefinition>? modifiers,
        out bool modifiersCleared)
    {
        modifiers = null;
        modifiersCleared = false;
        string blockPath = GetModifierBlockPath(source, label, context);
        if (node is YamlSequenceNode sequence)
        {
            if (sequence.Children.Count == 0)
            {
                modifiers = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
                modifiersCleared = true;
                return true;
            }

            CreatureManagerPlugin.Log.LogError($"{blockPath} must be a mapping ({{}} keeps fallback) or an empty list [] to block all lower modifier fallback.");
            return false;
        }

        if (node is not YamlMappingNode mapping)
        {
            CreatureManagerPlugin.Log.LogError($"{blockPath} must be a mapping ({{}} keeps fallback) or an empty list [] to block all lower modifier fallback.");
            return false;
        }

        modifiers = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            string modifier = GetYamlScalar(entry.Key);
            if (context == ModifierYamlContext.Karma)
            {
                modifier = modifier.ToLowerInvariant();
            }

            if (modifier.Length == 0)
            {
                CreatureManagerPlugin.Log.LogError(context == ModifierYamlContext.Level
                    ? $"{blockPath} has an empty modifier name."
                    : $"{blockPath} has unknown modifier ''.");
                return false;
            }

            if (!CreatureModifierCatalog.IsKnown(modifier))
            {
                CreatureManagerPlugin.Log.LogError($"{blockPath} has unknown modifier '{modifier}'.");
                return false;
            }

            if (!TryReadModifierDefinition(entry.Value, source, label, modifier, context, out ModifierDefinition definition))
            {
                return false;
            }

            modifiers[modifier] = definition;
        }

        return true;
    }

    private static bool TryReadModifierDefinition(
        YamlNode node,
        string source,
        string label,
        string modifier,
        ModifierYamlContext context,
        out ModifierDefinition definition)
    {
        definition = null!;
        bool isBlink = modifier.Equals("blink", StringComparison.OrdinalIgnoreCase);
        bool isKnockback = modifier.Equals("juggernaut", StringComparison.OrdinalIgnoreCase);
        bool isChanceOnly = modifier.Equals("unflinching", StringComparison.OrdinalIgnoreCase);
        bool isUndodgeable = modifier.Equals("undodgeable", StringComparison.OrdinalIgnoreCase);
        bool isChameleon = modifier.Equals("chameleon", StringComparison.OrdinalIgnoreCase);
        bool isBlamer = modifier.Equals("blamer", StringComparison.OrdinalIgnoreCase);
        bool isDeathward = modifier.Equals("deathward", StringComparison.OrdinalIgnoreCase);
        bool isReflection = modifier.Equals("reflection", StringComparison.OrdinalIgnoreCase);
        bool isReaping = modifier.Equals("reaping", StringComparison.OrdinalIgnoreCase);
        bool isStandardAffliction = modifier.Equals("exposed", StringComparison.OrdinalIgnoreCase) ||
                                    modifier.Equals("weakened", StringComparison.OrdinalIgnoreCase) ||
                                    modifier.Equals("withered", StringComparison.OrdinalIgnoreCase) ||
                                    modifier.Equals("corrosive", StringComparison.OrdinalIgnoreCase);
        bool isSplitAffliction = modifier.Equals("crippling", StringComparison.OrdinalIgnoreCase) ||
                                 modifier.Equals("disruptive", StringComparison.OrdinalIgnoreCase);
        bool isAdrenalineDrain = modifier.Equals("adrenalinedrain", StringComparison.OrdinalIgnoreCase);
        bool isToxicDeath = modifier.Equals("toxicdeath", StringComparison.OrdinalIgnoreCase);
        string format = GetModifierTupleFormat(
            isBlink,
            isKnockback,
            isChanceOnly,
            isUndodgeable,
            isChameleon,
            isBlamer,
            isDeathward,
            isReflection,
            isReaping,
            isStandardAffliction,
            isSplitAffliction,
            isAdrenalineDrain,
            isToxicDeath);
        string modifierPath = $"{GetModifierBlockPath(source, label, context)}.{modifier}";
        string text = GetYamlScalar(node);
        if (text.Length == 0)
        {
            CreatureManagerPlugin.Log.LogWarning(context == ModifierYamlContext.Level
                ? $"{modifierPath} must be '{format}'."
                : $"{modifierPath} must be {format}.");
            return false;
        }

        string[] tokens = SplitTuple(text);
        int maxValues = isBlink ? 4 : 2;
        int exactCount = isUndodgeable ? 2 :
            isChanceOnly ? 1 :
            isKnockback ? 3 :
            isDeathward ? 4 :
            isChameleon ? 2 :
            isBlamer ? 4 :
            isReflection ? 3 :
            isReaping ? 9 :
            isStandardAffliction ? 4 :
            isSplitAffliction ? 5 :
            isAdrenalineDrain ? 5 :
            isToxicDeath ? 4 : 0;
        bool invalidCount = exactCount > 0
            ? tokens.Length != exactCount
            : tokens.Length < 1 || tokens.Length > maxValues;
        if (invalidCount)
        {
            if (context == ModifierYamlContext.Level)
            {
                string expected = exactCount > 0
                    ? exactCount.ToString(CultureInfo.InvariantCulture)
                    : isBlink ? "1 to 4" : "1 or 2";
                CreatureManagerPlugin.Log.LogWarning(
                    $"{modifierPath} expected {expected} tuple values but got {tokens.Length}. Use modifierDistanceScaling for distance-based chance scaling.");
            }
            else
            {
                CreatureManagerPlugin.Log.LogWarning($"{modifierPath} must be {format}.");
            }

            return false;
        }

        int numericCount = isBlink || isToxicDeath || isDeathward ? Math.Min(tokens.Length, 3) : tokens.Length;
        float[] values = new float[numericCount];
        for (int index = 0; index < numericCount; index++)
        {
            if (TryParseFiniteFloat(tokens[index], out values[index]))
            {
                continue;
            }

            CreatureManagerPlugin.Log.LogError(
                $"{modifierPath} has invalid number '{tokens[index]}' at position {index + 1}.");
            return false;
        }

        if (isUndodgeable && (float.IsNaN(values[1]) || float.IsInfinity(values[1])))
        {
            CreatureManagerPlugin.Log.LogWarning($"{modifierPath} damageReduction must be a finite number.");
            return false;
        }

        int deathwardMaxActivations = 0;
        if (isDeathward &&
            (!int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out deathwardMaxActivations) ||
             deathwardMaxActivations < 1))
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} maxActivations must be an integer of 1 or greater.");
            return false;
        }

        int reapingHealMaxActivations = 0;
        if (isReaping &&
            (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out reapingHealMaxActivations) ||
             reapingHealMaxActivations < 1))
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} healMaxActivations must be an integer of 1 or greater.");
            return false;
        }

        definition = new ModifierDefinition
        {
            Chance = values[0]
        };
        if (values.Length >= 2)
        {
            if (isBlink)
            {
                definition.Cooldown = values[1];
            }
            else
            {
                definition.Power = values[1];
            }
        }

        if (isDeathward)
        {
            definition.Cooldown = values[2];
            definition.MaxActivations = deathwardMaxActivations;
        }

        if (isKnockback)
        {
            definition.Cooldown = values[2];
        }

        if (isBlamer)
        {
            definition.MaxKarmaGain = values[2];
            definition.FleeHealthRatio = UnityEngine.Mathf.Clamp01(values[3]);
        }

        if (isBlink && values.Length >= 3)
        {
            definition.MaxRange = values[2];
        }

        if (isBlink && tokens.Length >= 4)
        {
            definition.StartEffect = tokens[3];
        }

        if (isReaping)
        {
            definition.ReapingHealMaxActivations = reapingHealMaxActivations;
            definition.ReapingMaxHealthPerKill = values[3];
            definition.ReapingMaxHealthCap = values[4];
            definition.ReapingDamagePerKill = values[5];
            definition.ReapingDamageCap = values[6];
            definition.ReapingScalePerKill = values[7];
            definition.ReapingScaleCap = values[8];
        }

        if (isStandardAffliction)
        {
            definition.ProcChance = values[2];
            definition.Duration = values[3];
        }

        if (isSplitAffliction || isAdrenalineDrain)
        {
            definition.SecondaryPower = values[2];
            definition.ProcChance = values[3];
            definition.Duration = values[4];
        }

        if (isReflection)
        {
            definition.ProcChance = values[2];
        }

        if (isToxicDeath)
        {
            definition.Radius = values[2];
            definition.TriggerEffect = tokens[3];
        }

        if (context == ModifierYamlContext.Level)
        {
            ValidateLevelModifier(definition, modifier, modifierPath);
        }
        else
        {
            ValidateAndNormalizeKarmaModifier(definition, modifier, modifierPath);
        }

        return true;
    }

    private static string GetModifierBlockPath(string source, string label, ModifierYamlContext context)
    {
        return context == ModifierYamlContext.Level
            ? $"Level YAML from {source} '{label}'.modifiers"
            : $"Karma YAML from {source} {label}";
    }

    private static string GetModifierTupleFormat(
        bool isBlink,
        bool isKnockback,
        bool isChanceOnly,
        bool isUndodgeable,
        bool isChameleon,
        bool isBlamer,
        bool isDeathward,
        bool isReflection,
        bool isReaping,
        bool isStandardAffliction,
        bool isSplitAffliction,
        bool isAdrenalineDrain,
        bool isToxicDeath)
    {
        if (isBlink) return "chance[, cooldown[, maxRange[, startEffect]]]";
        if (isUndodgeable) return "chance, damageReduction";
        if (isChanceOnly) return "chance";
        if (isKnockback) return "chance, minimumPushForce, cooldown";
        if (isDeathward) return "chance, restoreHealth, cooldown, maxActivations";
        if (isChameleon) return "chance, immunitySwitchSeconds";
        if (isBlamer) return "chance, karmaPerSecond, maxKarmaGain, fleeHealthRatio";
        if (isReaping) return "chance, healPerKill, healMaxActivations, maxHealthPerKill, maxHealthCap, damagePerKill, damageCap, scalePerKill, scaleCap";
        if (isStandardAffliction) return "chance, power, procChance, duration";
        if (isSplitAffliction) return "chance, primaryPower, secondaryPower, procChance, duration";
        if (isAdrenalineDrain) return "chance, currentAdrenalineRemoved, adrenalineGainReduction, procChance, duration";
        if (isReflection) return "chance, reflectedDamage, procChance";
        if (isToxicDeath) return "chance, maxHealthDamage, radius, triggerEffect";
        return "chance[, power]";
    }

    private static void ValidateAndNormalizeKarmaModifier(
        ModifierDefinition definition,
        string modifier,
        string modifierPath)
    {
        if (modifier.Equals("undodgeable", StringComparison.OrdinalIgnoreCase) &&
            definition.Power is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} damageReduction must be from 0 to 1 and will be clamped at runtime.");
        }

        definition.Chance = UnityEngine.Mathf.Clamp(definition.Chance!.Value, 0f, 100f);
        if (definition.Cooldown.HasValue) definition.Cooldown = UnityEngine.Mathf.Max(0f, definition.Cooldown.Value);
        if (definition.MaxRange.HasValue) definition.MaxRange = UnityEngine.Mathf.Max(0f, definition.MaxRange.Value);
        if (definition.ProcChance.HasValue) definition.ProcChance = UnityEngine.Mathf.Clamp01(definition.ProcChance.Value);
        if (definition.Duration.HasValue) definition.Duration = UnityEngine.Mathf.Max(0.1f, definition.Duration.Value);
        if (definition.SecondaryPower.HasValue) definition.SecondaryPower = UnityEngine.Mathf.Clamp01(definition.SecondaryPower.Value);
        if (definition.Radius.HasValue) definition.Radius = UnityEngine.Mathf.Max(0f, definition.Radius.Value);
        if (definition.MaxKarmaGain.HasValue) definition.MaxKarmaGain = UnityEngine.Mathf.Max(0f, definition.MaxKarmaGain.Value);
        if (definition.FleeHealthRatio.HasValue) definition.FleeHealthRatio = UnityEngine.Mathf.Clamp01(definition.FleeHealthRatio.Value);
        if (definition.ReapingMaxHealthPerKill.HasValue) definition.ReapingMaxHealthPerKill = UnityEngine.Mathf.Max(0f, definition.ReapingMaxHealthPerKill.Value);
        if (definition.ReapingMaxHealthCap.HasValue) definition.ReapingMaxHealthCap = UnityEngine.Mathf.Max(0f, definition.ReapingMaxHealthCap.Value);
        if (definition.ReapingDamagePerKill.HasValue) definition.ReapingDamagePerKill = UnityEngine.Mathf.Max(0f, definition.ReapingDamagePerKill.Value);
        if (definition.ReapingDamageCap.HasValue) definition.ReapingDamageCap = UnityEngine.Mathf.Max(0f, definition.ReapingDamageCap.Value);
        if (definition.ReapingScalePerKill.HasValue) definition.ReapingScalePerKill = UnityEngine.Mathf.Max(0f, definition.ReapingScalePerKill.Value);
        if (definition.ReapingScaleCap.HasValue) definition.ReapingScaleCap = UnityEngine.Mathf.Max(0f, definition.ReapingScaleCap.Value);
    }

    private static void ValidateLevelModifier(
        ModifierDefinition definition,
        string modifier,
        string modifierPath)
    {
        if (definition.Chance is < 0f or > 100f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} chance must be from 0 to 100 and will be clamped at runtime.");
        }

        if (definition.Power.HasValue &&
            !modifier.Equals("juggernaut", StringComparison.OrdinalIgnoreCase) &&
            !modifier.Equals("chameleon", StringComparison.OrdinalIgnoreCase) &&
            definition.Power.Value is < 0f or > 1f)
        {
            string valueName = modifier.Equals("undodgeable", StringComparison.OrdinalIgnoreCase)
                ? "damageReduction"
                : "power";
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} {valueName} must be from 0 to 1 and will be clamped at runtime.");
        }

        if (modifier.Equals("chameleon", StringComparison.OrdinalIgnoreCase) && definition.Power.HasValue)
        {
            float interval = definition.Power.Value;
            string chameleonPath = GetCanonicalModifierPath(modifierPath, modifier, "chameleon");
            if (float.IsNaN(interval) || float.IsInfinity(interval))
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"{chameleonPath} immunitySwitchSeconds must be a finite number and will use the default interval.");
            }
            else if (interval <= 0f)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"{chameleonPath} immunitySwitchSeconds must be greater than 0 and will use at least 0.1 seconds.");
            }
        }

        if (modifier.Equals("juggernaut", StringComparison.OrdinalIgnoreCase) && definition.Power < 0f)
        {
            string juggernautPath = GetCanonicalModifierPath(modifierPath, modifier, "juggernaut");
            CreatureManagerPlugin.Log.LogWarning(
                $"{juggernautPath} minimumPushForce must be 0 or greater and will be clamped at runtime.");
        }

        if (definition.SecondaryPower.HasValue && definition.SecondaryPower.Value is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} secondary power must be from 0 to 1 and will be clamped at runtime.");
        }

        if (definition.ProcChance.HasValue && definition.ProcChance.Value is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} procChance must be from 0 to 1 and will be clamped at runtime.");
        }

        if (definition.Duration.HasValue && definition.Duration.Value <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} duration must be greater than 0 and will use at least 0.1 seconds.");
        }

        if (definition.Radius.HasValue && definition.Radius.Value < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} radius must be 0 or greater and will be clamped at runtime.");
        }

        if (definition.Cooldown.HasValue && definition.Cooldown.Value < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} cooldown must be 0 or greater and will be clamped at runtime.");
        }

        if (definition.MaxActivations.HasValue && definition.MaxActivations.Value < 1)
        {
            CreatureManagerPlugin.Log.LogWarning($"{modifierPath} maxActivations must be 1 or greater.");
        }

        if (definition.MaxRange.HasValue && definition.MaxRange.Value <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} maxRange must be greater than 0 and will disable Blink AI range expansion.");
        }

        if (modifier.Equals("reaping", StringComparison.OrdinalIgnoreCase) &&
            (definition.ReapingMaxHealthPerKill < 0f ||
             definition.ReapingMaxHealthCap < 0f ||
             definition.ReapingDamagePerKill < 0f ||
             definition.ReapingDamageCap < 0f ||
             definition.ReapingScalePerKill < 0f ||
             definition.ReapingScaleCap < 0f))
        {
            string reapingPath = GetCanonicalModifierPath(modifierPath, modifier, "reaping");
            CreatureManagerPlugin.Log.LogWarning(
                $"{reapingPath} gain and cap values must be 0 or greater and will be clamped at runtime.");
        }
    }

    private static string GetCanonicalModifierPath(string modifierPath, string modifier, string canonicalModifier)
    {
        return modifierPath.Substring(0, modifierPath.Length - modifier.Length) + canonicalModifier;
    }

    private static string GetYamlScalar(YamlNode node)
    {
        return node is YamlScalarNode scalar ? (scalar.Value ?? "").Trim() : "";
    }

    private static bool ValidateNormalizedLevelDefinition(LevelDefinition rule, string path)
    {
        if (string.IsNullOrWhiteSpace(rule.Target))
        {
            return Invalid(path, "target must be a non-empty name.");
        }

        if (rule.Prefabs != null && !ValidateNameList(rule.Prefabs, path, "prefabs"))
        {
            return false;
        }

        if (rule.ModifiersCleared && rule.Modifiers is { Count: > 0 })
        {
            return Invalid(path, "modifiersCleared cannot be combined with modifier entries.");
        }

        if (rule.Modifiers != null)
        {
            foreach (KeyValuePair<string, ModifierDefinition> entry in rule.Modifiers)
            {
                if (!CreatureModifierCatalog.IsKnown(entry.Key))
                {
                    return Invalid(path, $"modifiers has unknown modifier '{entry.Key}'.");
                }

                if (!ValidateNormalizedModifier(entry.Key, entry.Value, path))
                {
                    return false;
                }
            }
        }

        try
        {
            if (!ValidateLevelDefinition(rule, path, rule.Target!, warnNoEffect: true))
            {
                return Invalid(path, "level definition has no effect fields.");
            }
        }
        catch (Exception ex)
        {
            return Invalid(path, ex.Message);
        }

        return true;
    }

    private static bool ValidateNormalizedModifier(string modifier, ModifierDefinition? definition, string path)
    {
        if (definition == null || !definition.Chance.HasValue || !IsFinite(definition.Chance.Value))
        {
            return Invalid(path, $"modifiers.{modifier}.chance must be a finite number.");
        }

        float?[] floats =
        {
            definition.Power, definition.Cooldown, definition.MaxRange, definition.ProcChance,
            definition.Duration, definition.SecondaryPower, definition.Radius, definition.MaxKarmaGain,
            definition.FleeHealthRatio, definition.ReapingMaxHealthPerKill, definition.ReapingMaxHealthCap,
            definition.ReapingDamagePerKill, definition.ReapingDamageCap, definition.ReapingScalePerKill,
            definition.ReapingScaleCap
        };
        if (floats.Any(value => value.HasValue && !IsFinite(value.Value)))
        {
            return Invalid(path, $"modifiers.{modifier} must contain only finite numbers.");
        }

        if (definition.MaxActivations is < 1 || definition.ReapingHealMaxActivations is < 1)
        {
            return Invalid(path, $"modifiers.{modifier} activation counts must be integers of 1 or greater.");
        }

        ValidateLevelModifier(definition, modifier, $"{path}.modifiers.{modifier}");
        return true;
    }

    private static bool ValidateLevelDefinition(LevelDefinition rule, string source, string label, bool warnNoEffect)
    {
        if (rule.Level is { Count: 0 })
        {
            throw new FormatException($"Level YAML from {source} '{label}'.level must not be empty.");
        }

        if (rule.Level != null && rule.Level.Any(value => !IsFinite(value)))
            throw new FormatException($"Level YAML from {source} '{label}'.level must contain only finite numbers.");

        if (rule.Health.HasValue && (!IsFinite(rule.Health.Value) || rule.Health.Value <= 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.health must be a finite number greater than 0.");
        }

        if (rule.Damage.HasValue && (!IsFinite(rule.Damage.Value) || rule.Damage.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.damage must be a finite number of 0 or greater.");
        }

        if (rule.DamagePerLevel.HasValue && (!IsFinite(rule.DamagePerLevel.Value) || rule.DamagePerLevel.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.damagePerLevel must be a finite number of 0 or greater.");
        }

        if (rule.HealthPerLevel.HasValue && (!IsFinite(rule.HealthPerLevel.Value) || rule.HealthPerLevel.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.healthPerLevel must be a finite number of 0 or greater.");
        }

        if (rule.ScalePerLevel.HasValue && (!IsFinite(rule.ScalePerLevel.Value) || rule.ScalePerLevel.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.scalePerLevel must be a finite number of 0 or greater.");
        }

        ValidateDistanceScaling(rule.DistanceScaling, source, label);
        ValidateModifierDistanceScaling(rule.ModifierDistanceScaling, source, label);
        if (rule.Level == null && !rule.Health.HasValue && !rule.HealthPerLevel.HasValue &&
            !rule.Damage.HasValue && !rule.DamagePerLevel.HasValue && !rule.ScalePerLevel.HasValue &&
            !HasDistanceScalingEffect(rule.DistanceScaling) &&
            !HasModifierDistanceScalingValue(rule.ModifierDistanceScaling) &&
            !rule.ModifiersCleared &&
            (rule.Modifiers == null || rule.Modifiers.Count == 0))
        {
            if (warnNoEffect && rule.Modifiers == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}' has no effect fields and will be ignored.");
            }

            return false;
        }

        return true;
    }

    private static bool HasDistanceScalingEffect(List<float>? scaling)
    {
        return scaling is { Count: >= 2 } && (scaling[0] > 0f || scaling[1] > 0f);
    }

    private static void ValidateDistanceScaling(List<float>? scaling, string source, string label)
    {
        if (scaling == null)
        {
            return;
        }

        if (scaling.Count is < 2 or > 4)
        {
            throw new FormatException($"Level YAML from {source} '{label}'.distanceScaling must be [damage, health[, interval[, maxSteps]]].");
        }

        if (scaling.Any(value => !IsFinite(value)))
            throw new FormatException($"Level YAML from {source} '{label}'.distanceScaling must contain only finite numbers.");

        if (scaling[0] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling damage must be 0 or greater.");
            scaling[0] = 0f;
        }

        if (scaling[1] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling health must be 0 or greater.");
            scaling[1] = 0f;
        }

        if (scaling.Count >= 3 && scaling[2] <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling interval must be greater than 0 and will use 1000.");
            scaling[2] = 1000f;
        }

        if (scaling.Count >= 4 && scaling[3] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling maxSteps must be 0 or greater and will be ignored.");
            scaling[3] = 0f;
        }
    }

    private static bool HasModifierDistanceScalingValue(List<float>? scaling)
    {
        return scaling is { Count: 3 };
    }

    private static void ValidateModifierDistanceScaling(List<float>? scaling, string source, string label)
    {
        if (scaling == null)
        {
            return;
        }

        if (scaling.Count != 3)
        {
            throw new FormatException($"Level YAML from {source} '{label}'.modifierDistanceScaling must be [chancePerStep, stepDistance, maxSteps].");
        }

        if (scaling.Any(value => !IsFinite(value)))
            throw new FormatException($"Level YAML from {source} '{label}'.modifierDistanceScaling must contain only finite numbers.");

        if (scaling[0] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.modifierDistanceScaling chancePerStep must be 0 or greater.");
            scaling[0] = 0f;
        }

        if (scaling[1] <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.modifierDistanceScaling stepDistance must be greater than 0 and will use 1000.");
            scaling[1] = 1000f;
        }

        if (scaling[2] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.modifierDistanceScaling maxSteps must be 0 or greater and will be ignored.");
            scaling[2] = 0f;
        }
    }

    private static bool IsCommentOnlyYaml(string yaml)
    {
        foreach (string line in yaml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryReadLocalizationMap(
        string yaml,
        string source,
        out Dictionary<string, string> translations)
    {
        translations = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(yaml) || IsCommentOnlyYaml(yaml))
        {
            return true;
        }

        try
        {
            YamlStream stream = new();
            using StringReader reader = new(yaml);
            stream.Load(reader);
            if (stream.Documents.Count == 0)
            {
                return true;
            }

            if (stream.Documents.Count != 1)
            {
                throw new FormatException($"Localization YAML from {source} must contain exactly one YAML document.");
            }

            if (stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                throw new FormatException($"Localization YAML from {source} must be a flat token-to-text mapping.");
            }

            ValidateUniqueMappingKeys(root, source, "root");
            if (root.Children.Count > MaxLocalizationTokensPerLanguage)
            {
                throw new FormatException(
                    $"Localization YAML from {source} contains {root.Children.Count} tokens; the limit is {MaxLocalizationTokensPerLanguage}.");
            }

            HashSet<string> normalizedKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<YamlNode, YamlNode> entry in root.Children)
            {
                if (entry.Key is not YamlScalarNode keyNode)
                {
                    throw new FormatException($"Localization YAML from {source} has a non-scalar token key.");
                }

                if (!TryNormalizeLocalizationToken(keyNode.Value, out string token, out string tokenError))
                {
                    throw new FormatException($"Localization YAML from {source} has an invalid token: {tokenError}");
                }

                if (!normalizedKeys.Add(token))
                {
                    throw new FormatException(
                        $"Localization YAML from {source} has duplicate normalized token '{token}' when compared case-insensitively.");
                }

                if (entry.Value is not YamlScalarNode valueNode || string.IsNullOrEmpty(valueNode.Value))
                {
                    throw new FormatException(
                        $"Localization YAML from {source} token '{token}' must have a non-empty scalar string value; null, mappings, and lists are not supported.");
                }

                string text = valueNode.Value!;
                if (text.Length > MaxLocalizationTextLength)
                {
                    throw new FormatException(
                        $"Localization YAML from {source} token '{token}' exceeds the {MaxLocalizationTextLength}-character text limit.");
                }

                translations[token] = text;
            }

            return true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read localization YAML from {source}: {ex.Message}");
            translations.Clear();
            return false;
        }
    }

    internal static bool TryNormalizeLocalizationPayload(
        ServerLocalizationPayload? payload,
        string source,
        out ServerLocalizationPayload normalized)
    {
        normalized = new ServerLocalizationPayload
        {
            Version = ServerLocalizationPayloadVersion,
            Languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        };

        if (payload == null || payload.Version != ServerLocalizationPayloadVersion || payload.Languages == null)
        {
            string actualVersion = payload == null ? "missing" : payload.Version.ToString(CultureInfo.InvariantCulture);
            return Invalid(source,
                $"localization payload must have version {ServerLocalizationPayloadVersion} and a languages mapping; received version {actualVersion}.");
        }

        if (payload.Languages.Count > MaxLocalizationLanguageCount)
        {
            return Invalid(source,
                $"localization payload contains {payload.Languages.Count} languages; the limit is {MaxLocalizationLanguageCount}.");
        }

        HashSet<string> languageNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Dictionary<string, string>> languageEntry in payload.Languages)
        {
            string language = languageEntry.Key?.Trim() ?? "";
            if (language.Length == 0 || language.Length > 64)
            {
                return Invalid(source, "localization language names must contain from 1 to 64 characters.");
            }

            if (!languageNames.Add(language))
            {
                return Invalid(source,
                    $"localization payload has duplicate language '{language}' when compared case-insensitively.");
            }

            Dictionary<string, string>? sourceMap = languageEntry.Value;
            if (sourceMap == null || sourceMap.Count > MaxLocalizationTokensPerLanguage)
            {
                string count = sourceMap == null ? "null" : sourceMap.Count.ToString(CultureInfo.InvariantCulture);
                return Invalid(source,
                    $"localization language '{language}' has {count} tokens; it must be a mapping with at most {MaxLocalizationTokensPerLanguage} entries.");
            }

            Dictionary<string, string> languageMap = new(StringComparer.Ordinal);
            HashSet<string> normalizedKeys = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> translation in sourceMap)
            {
                if (!TryNormalizeLocalizationToken(translation.Key, out string token, out string tokenError))
                {
                    return Invalid(source,
                        $"localization language '{language}' has an invalid token: {tokenError}");
                }

                if (!normalizedKeys.Add(token))
                {
                    return Invalid(source,
                        $"localization language '{language}' has duplicate normalized token '{token}' when compared case-insensitively.");
                }

                if (string.IsNullOrEmpty(translation.Value) || translation.Value.Length > MaxLocalizationTextLength)
                {
                    return Invalid(source,
                        $"localization language '{language}' token '{token}' must be non-empty and no longer than {MaxLocalizationTextLength} characters.");
                }

                languageMap[token] = translation.Value;
            }

            normalized.Languages[language] = languageMap;
        }

        return true;
    }

    internal static Dictionary<string, string> BuildLocalizationForLanguage(
        ServerLocalizationPayload payload,
        string? language)
    {
        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, string>> languages = payload.Languages ??
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string selectedLanguage = language?.Trim() ?? "";
        if (selectedLanguage.Length == 0)
        {
            selectedLanguage = "English";
        }

        if (languages.TryGetValue("English", out Dictionary<string, string>? english))
        {
            foreach (KeyValuePair<string, string> translation in english)
            {
                translations[translation.Key] = translation.Value;
            }
        }

        if (!selectedLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) &&
            languages.TryGetValue(selectedLanguage, out Dictionary<string, string>? selected))
        {
            foreach (KeyValuePair<string, string> translation in selected)
            {
                translations[translation.Key] = translation.Value;
            }
        }

        return translations;
    }

    internal static bool TryNormalizeLocalizationToken(string? value, out string token, out string error)
    {
        token = value?.Trim() ?? "";
        error = "";
        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            token = token.Substring(1);
        }

        if (token.Length == 0 || token.Length > MaxLocalizationTokenLength)
        {
            error = $"token names must contain from 1 to {MaxLocalizationTokenLength} characters after an optional leading '$'.";
            return false;
        }

        const string tokenTerminators = " (){}[]+-!?/\\&%,.:-=<>\r\n\t";
        if (token.IndexOf('$') >= 0 || token.Any(character => char.IsControl(character) || tokenTerminators.IndexOf(character) >= 0))
        {
            error = $"token '{token}' contains a character that terminates Valheim localization tokens.";
            return false;
        }

        return true;
    }

    internal static bool TryParseFloatTuple(string line, int expectedCount, string label, out float[] values, out string error)
    {
        values = Array.Empty<float>();
        error = "";
        string[] tokens = SplitTuple(line);
        if (tokens.Length != expectedCount)
        {
            error = $"{label} tuple expected {expectedCount} values but got {tokens.Length}.";
            return false;
        }

        float[] parsed = new float[expectedCount];
        for (int index = 0; index < tokens.Length; index++)
        {
            if (!TryParseFiniteFloat(tokens[index], out parsed[index]))
            {
                error = $"{label} tuple has invalid number '{tokens[index]}' at position {index + 1}.";
                return false;
            }
        }

        values = parsed;
        return true;
    }

    internal static bool TryParseBoolFloatTuple(string line, int floatCount, string label, out bool boolValue, out float[] values, out string error)
    {
        boolValue = false;
        values = Array.Empty<float>();
        error = "";

        string[] tokens = SplitTuple(line);
        if (tokens.Length != floatCount + 1)
        {
            error = $"{label} tuple expected {floatCount + 1} values but got {tokens.Length}.";
            return false;
        }

        if (!TryParseBool(tokens[0], out boolValue))
        {
            error = $"{label} tuple has invalid boolean '{tokens[0]}' at position 1.";
            return false;
        }

        float[] parsed = new float[floatCount];
        for (int index = 0; index < floatCount; index++)
        {
            string token = tokens[index + 1];
            if (!TryParseFiniteFloat(token, out parsed[index]))
            {
                error = $"{label} tuple has invalid number '{token}' at position {index + 2}.";
                return false;
            }
        }

        values = parsed;
        return true;
    }

    internal static bool TryParseBossTuple(string line, out bool boss, out bool dontHideBossHud, out string? bossEvent, out string error)
    {
        boss = false;
        dontHideBossHud = false;
        bossEvent = null;
        error = "";

        string[] tokens = SplitTuplePreserveEmpty(line);
        if (tokens.Length is < 1 or > 3)
        {
            error = $"boss tuple expected 'boss[, dontHideBossHud[, bossEvent]]' but got {tokens.Length} values.";
            return false;
        }

        if (!TryParseBool(tokens[0], out boss))
        {
            error = $"boss tuple has invalid boolean '{tokens[0]}' at position 1.";
            return false;
        }

        if (tokens.Length >= 2 && !TryParseBool(tokens[1], out dontHideBossHud))
        {
            error = $"boss tuple has invalid boolean '{tokens[1]}' at position 2.";
            return false;
        }

        if (tokens.Length >= 3)
        {
            bossEvent = tokens[2];
        }

        return true;
    }

    internal static bool TryParseHealthTuple(string line, out float health, out float? regenAllHPTime, out string error)
    {
        health = 0f;
        regenAllHPTime = null;
        error = "";

        string[] tokens = SplitTuple(line);
        if (tokens.Length is not (1 or 2))
        {
            error = $"health tuple expected 'health[, regenAllHPTime]' but got {tokens.Length} values.";
            return false;
        }

        if (!TryParseFiniteFloat(tokens[0], out health))
        {
            error = $"health tuple has invalid health '{tokens[0]}'.";
            return false;
        }

        if (tokens.Length == 2)
        {
            if (!TryParseFiniteFloat(tokens[1], out float regen))
            {
                error = $"health tuple has invalid regenAllHPTime '{tokens[1]}'.";
                return false;
            }

            regenAllHPTime = regen;
        }

        return true;
    }

    internal static bool TryParseRandomItemTuple(string line, out string prefabName, out float chance, out string error)
    {
        prefabName = "";
        chance = 0.5f;
        error = "";

        string[] tokens = SplitTuple(line);
        if (tokens.Length is not (1 or 2))
        {
            error = $"randomItems tuple expected 'prefab[, chance]' but got {tokens.Length} values.";
            return false;
        }

        prefabName = tokens[0];
        if (tokens.Length == 1)
        {
            return true;
        }

        if (!TryParseFiniteFloat(tokens[1], out chance))
        {
            error = $"randomItems tuple has invalid chance '{tokens[1]}'.";
            return false;
        }

        chance = Math.Max(0f, Math.Min(1f, chance));
        return true;
    }

    internal static bool TryParseRandomSetTuple(string line, out string setName, out string[] itemNames, out string error)
    {
        setName = "";
        itemNames = Array.Empty<string>();
        error = "";

        string[] tokens = SplitTuple(line);
        if (tokens.Length < 2)
        {
            error = $"randomSets tuple expected 'setName, itemPrefab...' but got {tokens.Length} values.";
            return false;
        }

        setName = tokens[0];
        itemNames = tokens.Skip(1).ToArray();
        return true;
    }

    internal static bool TryParseSpawnPrefabEntry(
        string? value,
        out string prefabName,
        out int weight,
        out string error)
    {
        prefabName = "";
        weight = 1;
        error = "";

        string entry = value?.Trim() ?? "";
        if (entry.Length == 0)
        {
            error = $"entry must use Prefab[:weight] with a non-empty prefab name; omitted weight defaults to 1 and weight must be from 1 to {MaxSpawnPrefabWeight}.";
            return false;
        }

        int separator = entry.LastIndexOf(':');
        if (separator < 0)
        {
            prefabName = entry;
            return true;
        }

        prefabName = entry.Substring(0, separator).Trim();
        string weightToken = entry.Substring(separator + 1).Trim();
        if (prefabName.Length == 0)
        {
            error = $"entry '{entry}' must use Prefab[:weight] with a non-empty prefab name; omitted weight defaults to 1 and weight must be from 1 to {MaxSpawnPrefabWeight}.";
            return false;
        }

        if (!int.TryParse(weightToken, NumberStyles.None, CultureInfo.InvariantCulture, out weight) ||
            weight is < 1 or > MaxSpawnPrefabWeight)
        {
            error = $"entry '{entry}' must use Prefab[:weight], where omitted weight defaults to 1 and an explicit weight is an integer from 1 to {MaxSpawnPrefabWeight}.";
            return false;
        }

        return true;
    }

    internal static bool TryParseSpawnPrefabEntries(
        IReadOnlyList<string>? values,
        out List<(string PrefabName, int Weight)> entries,
        out string error)
    {
        entries = new List<(string PrefabName, int Weight)>();
        error = "";
        if (values is not { Count: > 0 })
        {
            error = $"value must be a non-empty Prefab[:weight] list; omitted weight defaults to 1 and weight must be from 1 to {MaxSpawnPrefabWeight}.";
            return false;
        }

        int expandedTotal = 0;
        foreach (string? value in values)
        {
            if (!TryParseSpawnPrefabEntry(value, out string prefabName, out int weight, out error))
            {
                entries.Clear();
                return false;
            }

            if (expandedTotal > MaxExpandedSpawnPrefabCount - weight)
            {
                entries.Clear();
                error = $"expanded Prefab[:weight] total must not exceed {MaxExpandedSpawnPrefabCount}; each omitted weight defaults to 1 and each explicit weight must be from 1 to {MaxSpawnPrefabWeight}.";
                return false;
            }

            expandedTotal += weight;
            entries.Add((prefabName, weight));
        }

        return true;
    }

    private static string[] SplitTuple(string line)
    {
        return line
            .Split(',')
            .Select(CleanTupleToken)
            .Where(token => token.Length > 0)
            .ToArray();
    }

    private static string[] SplitTuplePreserveEmpty(string line)
    {
        return line
            .Split(',')
            .Select(CleanTupleToken)
            .ToArray();
    }

    private static string CleanTupleToken(string token)
    {
        string cleaned = token.Trim();
        if (cleaned.Length >= 2 && cleaned[0] == '\'' && cleaned[cleaned.Length - 1] == '\'')
        {
            return cleaned.Substring(1, cleaned.Length - 2).Replace("''", "'");
        }

        if (cleaned.Length >= 2 && cleaned[0] == '"' && cleaned[cleaned.Length - 1] == '"')
        {
            return cleaned.Substring(1, cleaned.Length - 2).Replace("\\\"", "\"");
        }

        return cleaned;
    }

    internal static bool TryParseAppearanceColor(string value, out UnityEngine.Vector3 color)
    {
        color = UnityEngine.Vector3.one;
        if (value == null ||
            value.Length != 7 ||
            value[0] != '#' ||
            !int.TryParse(value.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) ||
            !int.TryParse(value.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) ||
            !int.TryParse(value.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
        {
            return false;
        }

        color = new UnityEngine.Vector3(r / 255f, g / 255f, b / 255f);
        return true;
    }

    private static bool TryParseBool(string token, out bool value)
    {
        return bool.TryParse(token, out value);
    }
}
