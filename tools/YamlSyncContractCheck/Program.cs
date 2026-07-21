using CreatureManager;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

ISerializer serializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
    .Build();
IDeserializer deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

try
{
    RoundTrip(
        new CreatureDefinition
        {
            Prefab = "Troll",
            Enabled = true,
            Scale = 1.25f,
            Humanoid = new HumanoidDefinition
            {
                RandomHair = new List<string> { "NPC_White_Hair0", "NPC_White_Hair1" }
            }
        },
        definition =>
        {
            Require(definition.Prefab == "Troll", "Creature prefab was not preserved.");
            Require(definition.Enabled == true, "Creature enabled value was not preserved.");
            Require(definition.Scale == 1.25f, "Creature scale was not preserved.");
            Require(
                definition.Humanoid?.RandomHair is ["NPC_White_Hair0", "NPC_White_Hair1"],
                "Creature randomHair values were not preserved.");
        });

    RoundTrip(
        new CreatureDefinition
        {
            Prefab = "Dverger",
            Appearance = new AppearanceDefinition
            {
                Hair = "",
                HairColor = "#112233",
                ModelIndex = 1
            }
        },
        definition =>
        {
            Require(definition.Appearance?.Hair == "", "Appearance hair clear was not preserved.");
            Require(definition.Appearance?.Beard == null, "Omitted appearance beard did not remain inherited.");
            Require(definition.Appearance?.HairColor == "#112233", "Appearance hairColor was not preserved.");
            Require(definition.Appearance?.SkinColor == null, "Omitted appearance skinColor did not remain inherited.");
            Require(definition.Appearance?.ModelIndex == 1, "Appearance modelIndex was not preserved.");
        });

    RoundTrip(
        new AiDefinition
        {
            Ai = "Aggressive",
            Enabled = false,
            MonsterAI = new MonsterAiDefinition { AlertRange = 40f }
        },
        definition =>
        {
            Require(definition.Ai == "Aggressive", "AI name was not preserved.");
            Require(definition.Enabled == false, "AI enabled value was not preserved.");
            Require(definition.MonsterAI?.AlertRange == 40f, "AI settings were not preserved.");
        });

    RoundTrip(
        new AttackDefinition
        {
            Prefab = "blob_attack_aoe",
            Enabled = true,
            Damage = new AttackDamageDefinition { Poison = 25f }
        },
        definition =>
        {
            Require(definition.Prefab == "blob_attack_aoe", "Attack prefab was not preserved.");
            Require(definition.Enabled == true, "Attack enabled value was not preserved.");
            Require(definition.Damage?.Poison == 25f, "Attack damage was not preserved.");
        });

    RoundTrip(
        new ProjectileDefinition
        {
            Prefab = "BombBlob_Tar_projectile",
            Enabled = true,
            UsedByAttacks = new List<string> { "BombBlob_Tar" },
            Projectile = new ProjectileComponentDefinition { SpawnOnHit = "BlobTar" },
            SpawnAbility = new SpawnAbilityDefinition { SpawnPrefabs = new List<string> { "Mistile" } }
        },
        definition =>
        {
            Require(definition.Prefab == "BombBlob_Tar_projectile", "Projectile prefab was not preserved.");
            Require(definition.Enabled == true, "Projectile enabled value was not preserved.");
            Require(definition.UsedByAttacks is ["BombBlob_Tar"], "Projectile usedByAttacks values were not preserved.");
            Require(definition.Projectile?.SpawnOnHitSpecified == true, "Projectile spawnOnHit presence was not preserved.");
            Require(definition.Projectile?.SpawnOnHit == "BlobTar", "Projectile spawnOnHit value was not preserved.");
            Require(definition.SpawnAbility?.SpawnPrefabsSpecified == true, "SpawnAbility spawnPrefabs presence was not preserved.");
            Require(definition.SpawnAbility?.SpawnPrefabs is ["Mistile"], "SpawnAbility spawnPrefabs values were not preserved.");
        });

    RoundTrip(
        new FactionDefinition
        {
            Faction = "Forest",
            Id = 42,
            Friendly = new List<string> { "Animals" }
        },
        definition =>
        {
            Require(definition.Faction == "Forest", "Faction name was not preserved.");
            Require(definition.Id == 42, "Faction id was not preserved.");
            Require(definition.Friendly is ["Animals"], "Faction relationships were not preserved.");
        });

    RoundTrip(
        new LevelDefinition
        {
            Target = "Boss",
            Damage = 1.5f,
            Modifiers = new Dictionary<string, ModifierDefinition>
            {
                ["enraged"] = new ModifierDefinition { Chance = 20f, Power = 0.15f }
            },
            ModifiersCleared = true,
            IsPreset = true
        },
        definition =>
        {
            Require(definition.Target == "Boss", "Level target was not preserved.");
            Require(definition.Damage == 1.5f, "Level damage was not preserved.");
            Require(definition.ModifiersCleared, "Level terminal-clear bookkeeping was not preserved.");
            Require(definition.IsPreset, "Level preset bookkeeping was not preserved.");
            Require(definition.Modifiers?.ContainsKey("ENRAGED") == true, "Level modifier comparer was not preserved.");
            Require(definition.Modifiers!["enraged"].Power == 0.15f, "Level modifier settings were not preserved.");
        });

    const string nestedLevelYaml = """
        Troll:
          Meadows:
            damage: 1.5
        """;
    Require(
        CreatureYaml.TryReadLevelDefinitions(nestedLevelYaml, "nested level contract", out List<LevelDefinition> nestedLevels),
        "Nested biome level syntax was rejected.");
    Require(
        nestedLevels is [{ Target: "Troll", Biome: "Meadows", Damage: 1.5f }],
        "Nested biome level syntax did not normalize to the expected definition.");

    const string invalidScalarLevelYaml = """
        Troll:
          damage: not-a-number
          health: 2
        """;
    Require(
        !CreatureYaml.TryReadLevelDefinitions(invalidScalarLevelYaml, "invalid scalar contract", out _),
        "An invalid level scalar was partially accepted.");

    const string multiDocumentLevelYaml = """
        Troll:
          damage: 1
        ---
        Troll:
          damage: 2
        """;
    Require(
        !CreatureYaml.TryReadLevelDefinitions(multiDocumentLevelYaml, "multiple document contract", out _),
        "Additional level YAML documents were silently ignored.");

    const string duplicateCaseLevelYaml = """
        Troll:
          damage: 1
          Damage: 2
        """;
    Require(
        !CreatureYaml.TryReadLevelDefinitions(duplicateCaseLevelYaml, "duplicate key contract", out _),
        "Case-insensitive duplicate level keys were accepted.");

    const string numericAttackEnumYaml = """
        - prefab: test_attack
          attack: [999, swing]
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<AttackDefinition>(numericAttackEnumYaml, "numeric attack enum contract", out _),
        "An undefined numeric attack enum was accepted.");

    const string positionalProjectileYaml = """
        - prefab: test_attack
          projectile: [test_projectile, '', '', 2]
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<AttackDefinition>(positionalProjectileYaml, "projectile position contract", out _),
        "Empty projectile tuple positions were collapsed and reinterpreted.");

    const string numericDamageModifierYaml = """
        - prefab: Troll
          character:
            damageModifiers:
              fire: 999
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<CreatureDefinition>(numericDamageModifierYaml, "numeric damage modifier contract", out _),
        "An undefined numeric damage modifier enum was accepted.");

    const string randomHairYaml = """
        - prefab: Vincent
          humanoid:
            randomArmor: []
            randomHair: [NPC_White_Hair0, NPC_White_Hair1]
        """;
    Require(
        CreatureYaml.TryReadDefinitions<CreatureDefinition>(randomHairYaml, "random hair contract", out List<CreatureDefinition> randomHairDefinitions),
        "A valid humanoid.randomHair list was rejected.");
    Require(
        randomHairDefinitions is [{ Humanoid.RandomHair: ["NPC_White_Hair0", "NPC_White_Hair1"] }],
        "humanoid.randomHair did not parse to the expected values.");

    const string partialAppearanceYaml = """
        - prefab: partial_appearance
          appearance:
            skinColor: '#A1B2C3'
        """;
    Require(
        CreatureYaml.TryReadDefinitions<CreatureDefinition>(partialAppearanceYaml, "partial appearance contract", out List<CreatureDefinition> partialAppearanceDefinitions),
        "A partial appearance mapping was rejected.");
    Require(
        partialAppearanceDefinitions is [{ Appearance.SkinColor: "#A1B2C3", Appearance.Hair: null, Appearance.ModelIndex: null }],
        "A partial appearance mapping did not preserve inherited fields.");

    const string clearAppearanceYaml = """
        - prefab: clear_appearance
          appearance:
            hair: ''
            beard: '   '
        """;
    Require(
        CreatureYaml.TryReadDefinitions<CreatureDefinition>(clearAppearanceYaml, "appearance clear contract", out List<CreatureDefinition> clearAppearanceDefinitions),
        "Explicit appearance hair/beard clears were rejected.");
    Require(
        clearAppearanceDefinitions is [{ Appearance.Hair: "", Appearance.Beard: "" }],
        "Appearance hair/beard clears were not normalized to empty strings.");

    const string emptyAppearanceYaml = """
        - prefab: empty_appearance
          appearance: {}
        """;
    Require(
        CreatureYaml.TryReadDefinitions<CreatureDefinition>(emptyAppearanceYaml, "empty appearance contract", out List<CreatureDefinition> emptyAppearanceDefinitions),
        "An empty appearance mapping was rejected.");
    Require(
        emptyAppearanceDefinitions is [{ Appearance: null }],
        "An empty appearance mapping was not normalized to a no-op.");

    const string invalidAppearanceColorYaml = """
        - prefab: invalid_appearance_color
          appearance:
            hairColor: '112233'
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<CreatureDefinition>(invalidAppearanceColorYaml, "invalid appearance color contract", out _),
        "An appearance color without the required #RRGGBB format was accepted.");

    const string negativeAppearanceModelYaml = """
        - prefab: invalid_appearance_model
          appearance:
            modelIndex: -1
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<CreatureDefinition>(negativeAppearanceModelYaml, "negative appearance model contract", out _),
        "A negative appearance.modelIndex was accepted.");

    const string legacyAppearanceTupleYaml = """
        - prefab: legacy_appearance_tuple
          appearance: 'Hair, Beard, #FFFFFF, #FFFFFF, 0'
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<CreatureDefinition>(legacyAppearanceTupleYaml, "legacy appearance tuple contract", out _),
        "The legacy scalar appearance tuple was accepted.");

    Require(
        CreatureYaml.TryParseAppearanceColor("#00A1FF", out _),
        "A valid #RRGGBB appearance color could not be parsed for runtime use.");
    Require(
        !CreatureYaml.TryParseAppearanceColor("00A1FF", out _),
        "The runtime appearance color parser accepted a value without '#'.");

    const string explicitNullProjectileYaml = """
        - prefab: BombBlob_Tar_projectile
          projectile:
            spawnOnHit: null
        """;
    Require(
        CreatureYaml.TryReadDefinitions<ProjectileDefinition>(explicitNullProjectileYaml, "explicit null projectile contract", out List<ProjectileDefinition> explicitNullProjectileDefinitions),
        "An explicit null projectile.spawnOnHit was rejected.");
    Require(
        explicitNullProjectileDefinitions is [{ Projectile.SpawnOnHitSpecified: true, Projectile.SpawnOnHit: null }],
        "Explicit null projectile.spawnOnHit did not retain field presence.");
    string explicitNullProjectileBundle = serializer.Serialize(explicitNullProjectileDefinitions);
    Require(
        explicitNullProjectileBundle.Contains("spawnOnHit:", StringComparison.Ordinal) &&
        !explicitNullProjectileBundle.Contains("serializedSpawnOnHit:", StringComparison.OrdinalIgnoreCase),
        "Explicit null projectile.spawnOnHit was omitted or leaked its DTO surrogate name during serialization.");
    Require(
        CreatureYaml.TryReadDefinitions<ProjectileDefinition>(explicitNullProjectileBundle, "serialized explicit null projectile contract", out List<ProjectileDefinition> roundTrippedExplicitNullProjectileDefinitions),
        "Serialized explicit null projectile.spawnOnHit could not be read.");
    Require(
        roundTrippedExplicitNullProjectileDefinitions is [{ Projectile.SpawnOnHitSpecified: true, Projectile.SpawnOnHit: null }],
        "Explicit null projectile.spawnOnHit was not preserved through bundle serialization.");

    const string emptyProjectileBlockYaml = """
        - prefab: empty_projectile_block
          projectile: {}
        """;
    Require(
        CreatureYaml.TryReadDefinitions<ProjectileDefinition>(emptyProjectileBlockYaml, "empty projectile block contract", out List<ProjectileDefinition> emptyProjectileBlockDefinitions),
        "An empty projectile block was rejected.");
    Require(
        emptyProjectileBlockDefinitions is [{ Projectile: null }],
        "An empty projectile block was not normalized to a no-op.");

    const string emptySpawnOnHitYaml = """
        - prefab: invalid_empty_spawn_on_hit
          projectile:
            spawnOnHit: ''
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<ProjectileDefinition>(emptySpawnOnHitYaml, "empty spawnOnHit contract", out _),
        "An empty projectile.spawnOnHit prefab name was accepted.");

    const string emptySpawnPrefabsYaml = """
        - prefab: invalid_empty_spawn_prefabs
          spawnAbility:
            spawnPrefabs: []
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<ProjectileDefinition>(emptySpawnPrefabsYaml, "empty spawnPrefabs contract", out _),
        "An empty spawnAbility.spawnPrefabs list was accepted.");

    const string nullSpawnPrefabsYaml = """
        - prefab: invalid_null_spawn_prefabs
          spawnAbility:
            spawnPrefabs: null
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<ProjectileDefinition>(nullSpawnPrefabsYaml, "null spawnPrefabs contract", out _),
        "A null spawnAbility.spawnPrefabs value was accepted.");

    const string weightedSpawnPrefabsYaml = """
        - prefab: weighted_spawn_prefabs
          spawnAbility:
            spawnPrefabs: [Skeleton:10, Blob]
        """;
    Require(
        CreatureYaml.TryReadDefinitions<ProjectileDefinition>(weightedSpawnPrefabsYaml, "weighted spawnPrefabs contract", out List<ProjectileDefinition> weightedSpawnPrefabDefinitions),
        "Valid weighted spawnAbility.spawnPrefabs entries were rejected.");
    Require(
        weightedSpawnPrefabDefinitions is [{ SpawnAbility.SpawnPrefabs: ["Skeleton:10", "Blob"] }],
        "Weighted spawnAbility.spawnPrefabs scalar values were not preserved by the DTO.");
    Require(
        CreatureYaml.TryParseSpawnPrefabEntry("Skeleton:10", out string weightedPrefabName, out int weightedPrefabWeight, out _) &&
        weightedPrefabName == "Skeleton" && weightedPrefabWeight == 10,
        "A weighted spawn prefab entry did not parse to the expected name and weight.");
    Require(
        CreatureYaml.TryParseSpawnPrefabEntry("Blob", out string defaultWeightPrefabName, out int defaultPrefabWeight, out _) &&
        defaultWeightPrefabName == "Blob" && defaultPrefabWeight == 1,
        "An unweighted spawn prefab entry did not default to weight 1.");
    Require(
        CreatureYaml.TryParseSpawnPrefabEntry("namespace:Skeleton:10", out string colonPrefabName, out int colonPrefabWeight, out _) &&
        colonPrefabName == "namespace:Skeleton" && colonPrefabWeight == 10,
        "The weighted spawn prefab parser did not split only the final colon.");

    string[] invalidWeightedSpawnPrefabEntries =
    {
        "",
        ":10",
        "Skeleton:",
        "Skeleton:0",
        "Skeleton:-1",
        "Skeleton:1.5",
        "Skeleton:many",
        $"Skeleton:{CreatureYaml.MaxSpawnPrefabWeight + 1}"
    };
    foreach (string invalidEntry in invalidWeightedSpawnPrefabEntries)
    {
        Require(
            !CreatureYaml.TryParseSpawnPrefabEntry(invalidEntry, out _, out _, out _),
            $"Invalid weighted spawn prefab entry '{invalidEntry}' was accepted.");
    }

    Require(
        !CreatureYaml.TryParseSpawnPrefabEntries(
            new[]
            {
                $"Skeleton:{CreatureYaml.MaxSpawnPrefabWeight}",
                $"Blob:{CreatureYaml.MaxSpawnPrefabWeight}",
                $"Greydwarf:{CreatureYaml.MaxSpawnPrefabWeight}",
                $"Troll:{CreatureYaml.MaxSpawnPrefabWeight}",
                "Boar:97"
            },
            out _,
            out _),
        "A spawn prefab list exceeding the expanded-entry safety limit was accepted.");

    const string unknownProjectileFieldYaml = """
        - prefab: invalid_unknown_projectile_field
          projectile:
            spawnPrefab: BlobTar
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<ProjectileDefinition>(unknownProjectileFieldYaml, "unknown projectile field contract", out _),
        "An unknown projectile field was accepted.");

    const string ignoredUsedByAttacksYaml = """
        - prefab: ignored_reference_metadata
          usedByAttacks: ['', '   ']
          projectile: {}
        """;
    Require(
        CreatureYaml.TryReadDefinitions<ProjectileDefinition>(ignoredUsedByAttacksYaml, "ignored usedByAttacks contract", out _),
        "Reference-only usedByAttacks metadata affected projectile override validity.");

    const string referenceOnlyProjectileTypeYaml = """
        - prefab: invalid_reference_type
          type: [Projectile]
        """;
    Require(
        !CreatureYaml.TryReadDefinitions<ProjectileDefinition>(referenceOnlyProjectileTypeYaml, "reference-only projectile type contract", out _),
        "Reference-only projectile type metadata was accepted by the override schema.");

    const string localizationYaml = """
        cm_rootwitch_name: Root Witch
        $cm_rootwitch_title: "Witch of Roots"
        """;
    Require(
        CreatureYaml.TryReadLocalizationMap(localizationYaml, "localization contract", out Dictionary<string, string> localizationMap),
        "A valid flat localization mapping was rejected.");
    Require(
        localizationMap.Count == 2 &&
        localizationMap["cm_rootwitch_name"] == "Root Witch" &&
        localizationMap["cm_rootwitch_title"] == "Witch of Roots",
        "Localization tokens or their optional leading '$' were not normalized correctly.");

    Require(
        CreatureYaml.TryReadLocalizationMap("# comment-only localization\n", "empty localization contract", out Dictionary<string, string> emptyLocalizationMap) &&
        emptyLocalizationMap.Count == 0,
        "A comment-only localization file was not accepted as an empty mapping.");

    string[] invalidLocalizationMaps =
    {
        "'$': invalid",
        "cm_null:",
        "cm_empty: ''",
        "cm_nested:\n  text: invalid",
        "cm_list: [invalid]",
        "$cm_duplicate: first\ncm_duplicate: second",
        "cm_bad-token: invalid",
        "cm_first: first\n---\ncm_second: second"
    };
    foreach (string invalidLocalizationMap in invalidLocalizationMaps)
    {
        Require(
            !CreatureYaml.TryReadLocalizationMap(invalidLocalizationMap, "invalid localization contract", out _),
            $"Invalid localization YAML was accepted: {invalidLocalizationMap.Replace("\n", " | ")}.");
    }

    ServerLocalizationPayload localizationPayload = new()
    {
        Version = CreatureYaml.ServerLocalizationPayloadVersion,
        Languages = new Dictionary<string, Dictionary<string, string>>
        {
            ["English"] = new()
            {
                ["cm_rootwitch_name"] = "Root Witch",
                ["cm_english_only"] = "English fallback"
            },
            ["Korean"] = new()
            {
                ["cm_rootwitch_name"] = "뿌리 마녀"
            }
        }
    };
    Require(
        CreatureYaml.TryNormalizeLocalizationPayload(localizationPayload, "localization payload contract", out ServerLocalizationPayload normalizedLocalizationPayload),
        "A valid localization payload was rejected.");
    Dictionary<string, string> koreanLocalization =
        CreatureYaml.BuildLocalizationForLanguage(normalizedLocalizationPayload, "Korean");
    Require(
        koreanLocalization["cm_rootwitch_name"] == "뿌리 마녀" &&
        koreanLocalization["cm_english_only"] == "English fallback",
        "Selected-language localization did not override English while retaining its fallback entries.");

    string serializedLocalizationPayload = serializer.Serialize(normalizedLocalizationPayload);
    ServerLocalizationPayload? deserializedLocalizationPayload =
        deserializer.Deserialize<ServerLocalizationPayload>(serializedLocalizationPayload);
    Require(
        CreatureYaml.TryNormalizeLocalizationPayload(deserializedLocalizationPayload, "localization payload round-trip", out ServerLocalizationPayload roundTrippedLocalizationPayload) &&
        CreatureYaml.BuildLocalizationForLanguage(roundTrippedLocalizationPayload, "Korean")["cm_rootwitch_name"] == "뿌리 마녀",
        "Localization payload did not survive synchronized YAML serialization.");

    ServerLocalizationPayload invalidLocalizationVersion = new()
    {
        Version = CreatureYaml.ServerLocalizationPayloadVersion + 1,
        Languages = new Dictionary<string, Dictionary<string, string>>()
    };
    Require(
        !CreatureYaml.TryNormalizeLocalizationPayload(invalidLocalizationVersion, "invalid localization version", out _),
        "An unsupported localization payload version was accepted.");

    ServerLocalizationPayload duplicateLocalizationLanguages = new()
    {
        Version = CreatureYaml.ServerLocalizationPayloadVersion,
        Languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["English"] = new(),
            ["english"] = new()
        }
    };
    Require(
        !CreatureYaml.TryNormalizeLocalizationPayload(duplicateLocalizationLanguages, "duplicate localization languages", out _),
        "Case-insensitive duplicate localization language names were accepted.");

    string defaultsDirectory = Path.Combine(AppContext.BaseDirectory, "defaults");
    string creatureSamplePath = Path.Combine(defaultsDirectory, "creatures.sample.yml");
    string attackSamplePath = Path.Combine(defaultsDirectory, "attacks.sample.yml");
    foreach (string samplePath in new[] { creatureSamplePath, attackSamplePath })
    {
        Require(File.Exists(samplePath), $"Default sample file was not copied to the contract-check output: {samplePath}");
    }

    string creatureSampleYaml = File.ReadAllText(creatureSamplePath);
    Require(
        CreatureYaml.TryReadDefinitions<CreatureDefinition>(creatureSampleYaml, "default creature sample contract", out List<CreatureDefinition> creatureSamples),
        "The generated creature sample YAML was rejected by the creature schema.");
    string[] expectedCreatureSamplePrefabs = { "vitrfell", "bonebeard", "vincent", "rootwitch" };
    Require(
        creatureSamples.Count == expectedCreatureSamplePrefabs.Length &&
        new HashSet<string>(creatureSamples.Select(definition => definition.Prefab ?? ""), StringComparer.Ordinal)
            .SetEquals(expectedCreatureSamplePrefabs),
        "The generated creature sample must contain exactly vitrfell, bonebeard, vincent, and rootwitch once each.");

    string attackSampleYaml = File.ReadAllText(attackSamplePath);
    Require(
        CreatureYaml.TryReadDefinitions<AttackDefinition>(attackSampleYaml, "default attack sample contract", out List<AttackDefinition> attackSamples),
        "The generated attack sample YAML was rejected by the attack schema.");
    string[] expectedAttackSamplePrefabs =
    {
        "boar_base_attack_boss",
        "troll_throw_bonebeard",
        "Fader_Spin_bonebeard",
        "attack_sword_dualcombo_bonebeard",
        "attack_sword_left180_bonebeard",
        "attack_sword_leftlong_bonebeard",
        "attack_sword_right180_bonebeard",
        "attack_sword_slash_bonebeard",
        "attack_bow_vincent1",
        "attack_bow_vincent2",
        "attack_bow_vincent3",
        "attack_bow_vincent4",
        "attack_bow_vincent5",
        "attack_bow_stab_vincent",
        "attack_kick_bow_vincent",
        "Eikthyr_stomp_rootwitch",
        "attack_cast_enemy_rootwitch",
        "GoblinShaman_attack_fireball_rootwitch",
        "gd_king_shoot_rootwitch",
        "attack_cast_protect_rootwitch"
    };
    Require(
        attackSamples.Count == expectedAttackSamplePrefabs.Length &&
        new HashSet<string>(attackSamples.Select(definition => definition.Prefab ?? ""), StringComparer.Ordinal)
            .SetEquals(expectedAttackSamplePrefabs),
        "The generated attack sample does not contain the exact expected set of 20 attack prefabs.");

    string[] expectedSampleLocalizationTokens = { "rootwitch", "vincent", "bonebeard", "vitrfell" };
    const string englishSampleYaml = """
        $rootwitch: Root Witch
        $vincent: Vincent
        $bonebeard: Bonebeard
        $vitrfell: Vitrfell
        """;
    Require(
        englishSampleYaml.Split('\n').Count(line => line.TrimStart().StartsWith("$", StringComparison.Ordinal)) == 4,
        "The default English localization sample must spell all four mappings with one leading '$'.");
    Require(
        CreatureYaml.TryReadLocalizationMap(englishSampleYaml, "default English localization contract", out Dictionary<string, string> englishSampleTokens),
        "The default English localization YAML was rejected.");
    Require(
        englishSampleTokens.Count == expectedSampleLocalizationTokens.Length &&
        new HashSet<string>(englishSampleTokens.Keys, StringComparer.Ordinal).SetEquals(expectedSampleLocalizationTokens) &&
        englishSampleTokens["rootwitch"] == "Root Witch" &&
        englishSampleTokens["vincent"] == "Vincent" &&
        englishSampleTokens["bonebeard"] == "Bonebeard" &&
        englishSampleTokens["vitrfell"] == "Vitrfell",
        "The default English localization sample did not normalize the exact four '$' tokens and values.");

    const string koreanSampleYaml =
        "$rootwitch: \uBFCC\uB9AC \uB9C8\uB140\n" +
        "$vincent: \uBE48\uC13C\uD2B8\n" +
        "$bonebeard: \uBCF8\uBE44\uC5B4\uB4DC\n" +
        "$vitrfell: \uBE44\uD2B8\uB974\uD3A0\n";
    Require(
        koreanSampleYaml.Split('\n').Count(line => line.TrimStart().StartsWith("$", StringComparison.Ordinal)) == 4,
        "The default Korean localization sample must spell all four mappings with one leading '$'.");
    Require(
        CreatureYaml.TryReadLocalizationMap(koreanSampleYaml, "default Korean localization contract", out Dictionary<string, string> koreanSampleTokens),
        "The default Korean localization YAML was rejected.");
    Require(
        koreanSampleTokens.Count == expectedSampleLocalizationTokens.Length &&
        new HashSet<string>(koreanSampleTokens.Keys, StringComparer.Ordinal).SetEquals(expectedSampleLocalizationTokens) &&
        koreanSampleTokens["rootwitch"] == "\uBFCC\uB9AC \uB9C8\uB140" &&
        koreanSampleTokens["vincent"] == "\uBE48\uC13C\uD2B8" &&
        koreanSampleTokens["bonebeard"] == "\uBCF8\uBE44\uC5B4\uB4DC" &&
        koreanSampleTokens["vitrfell"] == "\uBE44\uD2B8\uB974\uD3A0",
        "The default Korean localization sample did not normalize the exact four '$' tokens and values.");

    Console.WriteLine("YAML sync DTO and strict parser contract checks passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"YAML sync DTO contract check failed: {exception.Message}");
    return 1;
}

void RoundTrip<T>(T value, Action<T> verify)
{
    string yaml = serializer.Serialize(new List<T> { value });
    Require(!yaml.Contains("isEnabled:", StringComparison.OrdinalIgnoreCase), $"{typeof(T).Name} leaked computed isEnabled into synced YAML.");

    List<T>? values = deserializer.Deserialize<List<T>>(yaml);
    Require(values is { Count: 1 }, $"{typeof(T).Name} did not round-trip as one definition.");
    verify(values[0]);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
