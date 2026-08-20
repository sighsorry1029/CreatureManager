using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace CreatureManager;

internal static class CreatureKarmaManager
{
    internal enum KarmaAddResult
    {
        Added,
        Saturated,
        Unavailable
    }

    private enum KarmaRealm
    {
        Outdoor,
        Dungeon
    }

    private const string CountedDeathKey = "CreatureManager_KarmaDeathCounted";
    private const string EnforcerKey = "CreatureManager_KarmaEnforcer";
    private const string EnforcerSummonedKey = "CreatureManager_KarmaEnforcerSummoned";
    private const string EnforcerNameKey = "CreatureManager_KarmaEnforcerName";
    private const string EnforcerLevelBonusKey = "CreatureManager_KarmaEnforcerLevelBonus";
    private const string EnforcerIsBossKey = "CreatureManager_KarmaEnforcerIsBoss";
    private const string EnforcerBossHudKey = "CreatureManager_KarmaEnforcerBossHud";
    private const string EnforcerLootKey = "CreatureManager_KarmaEnforcerLoot";
    private const string EnforcerLootDroppedKey = "CreatureManager_KarmaEnforcerLootDropped";
    private const string EnforcerPresenceAnchorStoredKey = "CreatureManager_KarmaEnforcerPresenceAnchorStored";
    private const string EnforcerPresenceInteriorKey = "CreatureManager_KarmaEnforcerPresenceInterior";
    private const string EnforcerPresenceZoneXKey = "CreatureManager_KarmaEnforcerPresenceZoneX";
    private const string EnforcerPresenceZoneYKey = "CreatureManager_KarmaEnforcerPresenceZoneY";
    private const string PlayerDeathRpc = "CreatureManager_KarmaPlayerDeath";
    private const string CreatureDeathRpc = "CreatureManager_KarmaCreatureDeath";
    private const string BlockerObservationRpc = "CreatureManager_KarmaBlockerObservation";
    private const string CenterQuoteRpc = "CreatureManager_KarmaCenterQuote";
    private const string KarmaStatusRequestRpc = "CreatureManager_KarmaStatusRequest";
    private const string KarmaStatusResponseRpc = "CreatureManager_KarmaStatusResponse";
    private const float KarmaStatusRequestInterval = 1f;
    private const float CreatureDeathSyncTimeout = 2f;
    private const float CreatureDeathPositionTolerance = 8f;
    private const float ProcessedCreatureDeathRetention = 300f;
    private const int MaximumPendingCreatureDeaths = 512;
    private const int MaximumPendingCreatureDeathsPerPeer = 64;
    private const int MaximumEnforcerMinionEntries = 16;
    private const int MaximumEnforcerMinionsPerEntry = 16;
    private const int MaximumEnforcerMinionsPerCandidate = 16;
    private const int MaximumEnforcerLootEntries = 32;
    private const int MaximumEnforcerLootAmountPerEntry = 64;
    private const int MaximumEnforcerLootAmountPerCandidate = 64;
    private const int MaximumEnforcerPrefabNameLength = 128;
    private const int MaximumSerializedEnforcerLootLength = 8192;
    private const float SectorPruneInterval = 1f;
    private const float EnforcerAbandonmentCheckInterval = 1f;
    private const float EnforcerPresenceRange = 64f;
    private const float EnforcerPresenceRangeSquared = EnforcerPresenceRange * EnforcerPresenceRange;
    private const int MaximumSectorScansPerPass = 256;
    private const int MaximumSectorStates = 32768;
    private const float SectorCapacityWarningInterval = 30f;
    private const string EnforcerNameSuffix = "$cm_suffix_enforcer";
    private const string EnforcerMinionSuffix = "$cm_suffix_minion";
    private const int ZoneRadius = 1;
    private const int DungeonComponentPositionAttempts = 12;
    private const int DungeonBossRandomPositionAttempts = 10;
    private const int DungeonMinionPositionAttempts = 8;
    private const float DungeonBossRandomRadiusMin = 6f;
    private const float DungeonBossRandomRadiusMax = 12f;
    private const float DungeonComponentVerticalTolerance = 6f;
    private const float DungeonSpawnFloorTolerance = 4f;
    private const float DungeonSpawnClearanceInset = 0.05f;
    private static readonly HashSet<string> KarmaFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "thresholds", "decay", "gain", "prefabs"
    };
    private static readonly HashSet<string> EnforcerFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "settings", "checks", "modifiers"
    };
    private static readonly HashSet<string> EnforcerBiomeFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "enabled", "enforcers", "dungeonEnforcers"
    };
    private static readonly HashSet<string> EnforcerCandidateFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "summon", "settings", "weight", "loot", "modifiers", "location"
    };
    private static readonly string[] KarmaLevelQuotes =
    {
        "$cm_message_karma_level_01",
        "$cm_message_karma_level_02",
        "$cm_message_karma_level_03",
        "$cm_message_karma_level_04",
        "$cm_message_karma_level_05"
    };
    private static readonly string[] EnforcerSpawnQuotes =
    {
        "$cm_message_enforcer_spawn_01",
        "$cm_message_enforcer_spawn_02",
        "$cm_message_enforcer_spawn_03",
        "$cm_message_enforcer_spawn_04",
        "$cm_message_enforcer_spawn_05"
    };
    private static readonly string[] EnforcerDeathQuotes =
    {
        "$cm_message_enforcer_death_01",
        "$cm_message_enforcer_death_02",
        "$cm_message_enforcer_death_03",
        "$cm_message_enforcer_death_04",
        "$cm_message_enforcer_death_05"
    };
    private static readonly object Sync = new();
    private static readonly Dictionary<string, SectorState> Sectors = new(StringComparer.Ordinal);
    private static readonly Queue<string> SectorPruneQueue = new();
    private static readonly SectorState EmptySectorState = new();
    private static readonly HashSet<int> RuntimeSummonedCreatureIds = new();
    private static readonly Dictionary<int, ResolvedEnforcerSettings> RuntimeEnforcerSettings = new();
    private static readonly Dictionary<int, List<EnforcerLootDefinition>> RuntimeEnforcerLoot = new();
    private static readonly HashSet<ZDOID> TrackedEnforcerZdoIds = new();
    private static readonly Dictionary<ZDOID, float> EnforcerNoPlayerSince = new();
    private static readonly List<EnforcerPlayerPresence> EnforcerPlayerPresenceBuffer = new();
    private static readonly HashSet<ZDOID> EnforcerPlayerPresenceIds = new();
    private static readonly List<ZDOID> StaleTrackedEnforcerIds = new();
    private static readonly List<ZDOID> AbandonedEnforcerIds = new();
    private static readonly List<ZDO> EnforcerBootstrapScanBuffer = new();
    private static readonly List<string> EnforcerBootstrapPrefabNames = new();
    private static readonly HashSet<ZDOID> TrackedBossZdoIds = new();
    private static readonly HashSet<ZDOID> ReportedBlockerZdoIds = new();
    private static readonly Dictionary<ZDOID, bool> ObservedPlayerDeathStates = new();
    private static readonly HashSet<ZDOID> ServerPendingCreatureDeaths = new();
    private static readonly Dictionary<long, int> ServerPendingCreatureDeathCounts = new();
    private static readonly Dictionary<ZDOID, float> ServerProcessedCreatureDeaths = new();
    private static readonly List<ZDOID> StaleProcessedCreatureDeaths = new();
    private static uint CreatureDeathEpoch;
    private static KarmaSettings Settings = KarmaSettings.Default();
    private static ZRoutedRpc? RegisteredRoutedRpc;
    private static FieldInfo? ExpandWorldDataCurrentLocationField;
    private static bool ExpandWorldDataCurrentLocationFieldResolved;
    private static MethodInfo? ExpandWorldDataTryGetBiomeMethod;
    private static MethodInfo? ExpandWorldDataTryGetBiomeDisplayNameMethod;
    private static bool ExpandWorldDataBiomeMethodsResolved;
    private static float NextSummonCheckTime;
    private static float NextEnforcerAbandonmentCheckTime;
    private static int LastEnforcerAbandonmentDespawnSeconds = -1;
    private static bool EnforcerBootstrapScanPending = true;
    private static bool EnforcerBootstrapScanInitialized;
    private static int EnforcerBootstrapPrefabPosition;
    private static int EnforcerBootstrapZdoScanIndex;
    private static int EnforcerBootstrapRestoredCount;
    private static float NextSectorPruneTime;
    private static float NextSectorCapacityWarningTime;
    private static readonly Dictionary<string, List<Vector3>> DungeonComponentPositionCache = new(StringComparer.Ordinal);
    private static readonly List<ZDO> DungeonSpawnZdoBuffer = new();
    private static readonly List<Vector3> DungeonSpawnPath = new();
    private static readonly int DungeonSpawnStaticMask = LayerMask.GetMask(
        "Default",
        "static_solid",
        "Default_small",
        "piece",
        "terrain",
        "blocker",
        "vehicle");
    private static readonly int DungeonSpawnCollisionMask =
        DungeonSpawnStaticMask |
        LayerMask.GetMask(
            "character",
            "character_net",
            "character_noenv",
            "character_ghost");
    private static float NextKarmaStatusRequestTime;
    private static int NextKarmaStatusRequestId;
    private static int LastKarmaStatusResponseId = -1;
    private static float ClientKarmaStatusValue;
    private static int ClientKarmaStatusLevel;
    private static KarmaRealm ClientKarmaStatusRealm;
    private static bool ClientKarmaStatusValid;

    private sealed class CreatureDeathContext
    {
        internal ZDOID DeadId;
        internal string Prefab = "";
        internal Vector3 Position;
        internal int Level;
        internal bool Boss;
        internal bool Enforcer;
        internal bool KarmaSummoned;
        internal bool PlayerAttributedKill;
        internal ZDOID PlayerKillerId;
        internal CreatureModifierManager.DeathAttributionKind AttributionKind;
        internal float OmenChance;
        internal uint Epoch;
    }

    private enum EnforcerSummonFailure
    {
        None,
        FeatureDisabled,
        ServerUnavailable,
        KillerUnavailable,
        KillerChangedRealm,
        BiomeNotConfigured,
        BiomeDisabled,
        NoCandidates,
        ActiveEnforcerCap,
        ActiveBoss,
        Cooldown,
        ChanceRollFailed,
        NoEligibleCandidate,
        InvalidCandidate,
        NoSpawnPosition,
        SectorStateCapacity,
        SpawnFailed
    }

    private static bool IsKarmaSystemEnabled()
    {
        return GetKarmaSystemMode() != CreatureManagerPlugin.KarmaSystemMode.Off;
    }

    private static bool IsKarmaLevelEnabled()
    {
        CreatureManagerPlugin.KarmaSystemMode mode = GetKarmaSystemMode();
        return mode is CreatureManagerPlugin.KarmaSystemMode.KarmaLevelAndEnforcer or
            CreatureManagerPlugin.KarmaSystemMode.KarmaLevelOnly;
    }

    private static bool IsEnforcerEnabled()
    {
        CreatureManagerPlugin.KarmaSystemMode mode = GetKarmaSystemMode();
        return mode is CreatureManagerPlugin.KarmaSystemMode.KarmaLevelAndEnforcer or
            CreatureManagerPlugin.KarmaSystemMode.EnforcerOnly;
    }

    private static CreatureManagerPlugin.KarmaSystemMode GetKarmaSystemMode()
    {
        return CreatureManagerPlugin.KarmaMode?.Value ?? CreatureManagerPlugin.KarmaSystemMode.KarmaLevelAndEnforcer;
    }

    internal static bool RequiresAuthoritativeLevelBonus(ZDO zdo, bool isBoss)
    {
        if (zdo != null &&
            IsKarmaSystemEnabled() &&
            zdo.GetBool(EnforcerKey, false))
        {
            return true;
        }

        return (IsKarmaLevelEnabled() && Settings.Karma.Thresholds.Count > 0) ||
               (isBoss && IsKarmaSystemEnabled());
    }

    private static int GetMaximumEnforcersPerSector()
    {
        return Mathf.Max(1, CreatureManagerPlugin.MaximumEnforcersPerSector?.Value ?? 1);
    }

    private static int GetEnforcerAbandonedDespawnSeconds()
    {
        return Mathf.Clamp(CreatureManagerPlugin.EnforcerAbandonedDespawnSeconds?.Value ?? 120, 0, 1500);
    }

    private static bool ShouldBlockEnforcerWhileBossActive()
    {
        return CreatureManagerPlugin.BlockEnforcerWhileBossActive?.Value != CreatureManagerPlugin.Toggle.Off;
    }

    private static bool ShouldBlockKarmaGainWhileBossActive()
    {
        return CreatureManagerPlugin.BlockKarmaGainWhileBossActive?.Value == CreatureManagerPlugin.Toggle.On;
    }

    private static bool ShouldBlockKarmaGain(Vector3 position, ZDOID excludedCharacterId)
    {
        bool blockForBoss = ShouldBlockKarmaGainWhileBossActive();
        bool blockForEnforcer = CreatureManagerPlugin.BlockKarmaGainWhileEnforcerActive?.Value == CreatureManagerPlugin.Toggle.On;
        if (!blockForBoss && !blockForEnforcer)
        {
            return false;
        }

        GetEnforcerBlockerState(
            position,
            out int activeEnforcers,
            out bool hasNonEnforcerBoss,
            excludedCharacterId: excludedCharacterId);
        return (blockForBoss && hasNonEnforcerBoss) ||
               (blockForEnforcer && activeEnforcers > 0);
    }

    internal static void RegisterRpcs()
    {
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        if (ReferenceEquals(RegisteredRoutedRpc, ZRoutedRpc.instance))
        {
            return;
        }

        ZRoutedRpc.instance.Register<ZPackage>(PlayerDeathRpc, RPC_PlayerDeath);
        ZRoutedRpc.instance.Register<ZPackage>(CreatureDeathRpc, RPC_CreatureDeath);
        ZRoutedRpc.instance.Register<ZPackage>(BlockerObservationRpc, RPC_BlockerObservation);
        ZRoutedRpc.instance.Register<ZPackage>(CenterQuoteRpc, RPC_CenterQuote);
        ZRoutedRpc.instance.Register<ZPackage>(KarmaStatusRequestRpc, RPC_KarmaStatusRequest);
        ZRoutedRpc.instance.Register<ZPackage>(KarmaStatusResponseRpc, RPC_KarmaStatusResponse);
        RegisteredRoutedRpc = ZRoutedRpc.instance;
    }

    internal static void ForgetCharacter(Character character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        RuntimeSummonedCreatureIds.Remove(id);
        RuntimeEnforcerSettings.Remove(id);
        RuntimeEnforcerLoot.Remove(id);
        ZDOID characterId = character.GetZDOID();
        if (!characterId.IsNone())
        {
            ReportedBlockerZdoIds.Remove(characterId);
        }

    }

    internal static void ResetRuntimeState()
    {
        unchecked
        {
            CreatureDeathEpoch++;
        }

        lock (Sync)
        {
            Sectors.Clear();
            SectorPruneQueue.Clear();
            ObservedPlayerDeathStates.Clear();
        }

        RuntimeSummonedCreatureIds.Clear();
        RuntimeEnforcerSettings.Clear();
        RuntimeEnforcerLoot.Clear();
        TrackedEnforcerZdoIds.Clear();
        EnforcerNoPlayerSince.Clear();
        EnforcerPlayerPresenceBuffer.Clear();
        EnforcerPlayerPresenceIds.Clear();
        StaleTrackedEnforcerIds.Clear();
        AbandonedEnforcerIds.Clear();
        EnforcerBootstrapScanBuffer.Clear();
        EnforcerBootstrapPrefabNames.Clear();
        TrackedBossZdoIds.Clear();
        ReportedBlockerZdoIds.Clear();
        ServerPendingCreatureDeaths.Clear();
        ServerPendingCreatureDeathCounts.Clear();
        ServerProcessedCreatureDeaths.Clear();
        StaleProcessedCreatureDeaths.Clear();
        DungeonComponentPositionCache.Clear();
        DungeonSpawnZdoBuffer.Clear();
        DungeonSpawnPath.Clear();
        // Registration follows the ZRoutedRpc instance lifetime. It has no unregister API and Register uses Dictionary.Add.
        NextSummonCheckTime = 0f;
        NextEnforcerAbandonmentCheckTime = 0f;
        LastEnforcerAbandonmentDespawnSeconds = -1;
        EnforcerBootstrapScanPending = true;
        EnforcerBootstrapScanInitialized = false;
        EnforcerBootstrapPrefabPosition = 0;
        EnforcerBootstrapZdoScanIndex = 0;
        EnforcerBootstrapRestoredCount = 0;
        NextSectorPruneTime = 0f;
        NextSectorCapacityWarningTime = 0f;
        NextKarmaStatusRequestTime = 0f;
        NextKarmaStatusRequestId = 0;
        LastKarmaStatusResponseId = -1;
        ClientKarmaStatusValue = 0f;
        ClientKarmaStatusLevel = 0;
        ClientKarmaStatusRealm = KarmaRealm.Outdoor;
        ClientKarmaStatusValid = false;
    }

    internal static string BuildDefaultYaml()
    {
        return """
# Outdoor and dungeon Karma are stored separately; the rules below apply to both.

karma:
  thresholds: [60, 120, 180]             # +1, +2, ... level at each value; the last value caps gain. [] disables bonuses and the cap.
  decay: [15, 30, 100]                   # [delayMinutes, karmaPerMinute, karmaRemovedOnPlayerDeath].
  gain: [1, 25, 0.3, 0.15, 4]            # [kill, bossKill, killScalingPerExtraLevel, bossScalingPerExtraLevel, dungeonMultiplier].
  prefabs:                               # Base gain overrides; level and dungeon scaling still apply.
    Troll: 5
    Abomination: 5
    StoneGolem: 5
    GoblinBrute: 3
    Lox: 5
    Gjall: 5
    SeekerBrute: 5
    Morgen: 5
    Morgen_NonSleeping: 5
    FallenValkyrie: 8

Enforcer:
  settings: [40, 30, 2]                  # [requiredKarma, consumeKarma, levelBonus].
  checks: [50, 1200, 60, 24~48]          # [chance%, cooldownSeconds, intervalSeconds, outdoorRadiusMin~MaxMeters].
  modifiers:                             # Partial map; omitted/{} inherits levels.yml, [] clears fallback, and trailing tuple values may be omitted.
    # Offense: Enraged to Undodgeable
    enraged: 10, 0.15                    # chance%, outgoingDamageBonus.
    fire: 10, 0.2                        # chance%, addedFireDamage.
    frost: 10, 0.1                       # chance%, addedFrostDamage.
    lightning: 10, 0.1                   # chance%, addedLightningDamage.
    spirit: 10, 0.05                     # chance%, addedSpiritDoT.
    armorPiercing: 10, 0.3               # chance%, ignoredPlayerArmor.
    staggering: 10, 0.6                  # chance%, staggerBonus.
    undodgeable: 10, 0.25                # chance%, damageReduction.
    # Defense: Armored to Chameleon
    armored: 10, 0.3                     # chance%, damageReduction.
    deathward: 10, 0.2, 10, 3            # chance%, restoredMaxHealthRatio, cooldownSeconds, maxActivations.
    regenerating: 10, 0.005, 20          # chance%, maxHealthRatioPerSecond, healthPerSecondCap (0 = unlimited).
    reflection: 10, 0.1, 0.5             # chance%, actualMeleeDamageReflected, procChance.
    vortex: 10, 0.5                      # chance%, projectileIgnoreProc.
    adaptive: 10, 0.5                    # chance%, rememberedTypeDamageReduction.
    unflinching: 10                      # chance%.
    chameleon: 10, 10                    # chance%, immunitySwitchSeconds.
    # Affliction: Exposed to ToxicDeath
    exposed: 10, 0.2, 0.5, 5             # chance%, damageTaken, proc, duration.
    weakened: 10, 0.2, 0.5, 5            # chance%, outgoingDamageReduction, proc, duration.
    withered: 10, 0.5, 0.5, 5            # chance%, healingReduction, proc, duration.
    crippling: 10, 0.5, 0.5, 0.5, 5     # chance%, moveReduction, jumpReduction, proc, duration.
    disruptive: 10, 0.5, 0.5, 0.5, 5    # chance%, staminaRegenReduction, eitrRegenReduction, proc, duration.
    adrenalineDrain: 10, 0.5, 0.5, 0.5, 5 # chance%, currentAdrenalineRemoved, adrenalineGainReduction, procChance, duration.
    corrosive: 10, 0.5, 0.5, 5           # chance%, durabilityLossBonus, procChance, duration.
    toxicDeath: 10, 0.3, 4, blob_aoe     # chance%, maxHealthDamage, radius, triggerEffect.
    # Special: Swift to Blamer
    swift: 10, 0.4                       # chance%, movementSpeedBonus.
    attackSpeed: 10, 0.3                 # chance%, attackSpeedBonus.
    vampiric: 10, 0.3                    # chance%, actualDirectDamageHealing.
    reaping: 10, 0.05, 20, 0.1, 2, 0.01, 0.2, 0.05, 0.5 # chance%, heal/base, healMaxActivations, maxHealth/base, maxHealthCap, damagePerKill, damageCap, scalePerKill, scaleCap.
    blink: 10, 6, 16, fx_Adrenaline1    # chance%, cooldown, maxRange, startEffect.
    omen: 10, 0.5                        # chance%, forcedEnforcerChance.
    juggernaut: 10, 150, 5               # chance%, minimumPushForce, cooldownSeconds.
    blamer: 0, 1, 60, 0.75               # chance%, karmaPerSecond, maxKarmaGain, fleeHealthRatio (0 maxKarmaGain = unlimited).

# Use top-level Global as the fallback table for unmatched biomes.
BlackForest:
  enabled: true
  enforcers:                             # Outdoor candidates.
    - summon: [Greydwarf_Elite, Greydwarf:2, Greydwarf_Shaman] # [enforcerPrefab, minionPrefab[:count], ...]
      settings: [40, 30, 1]             # [requiredKarma, consumeKarma, levelBonus]; omit to inherit Enforcer.settings.
      weight: 1                          # Relative selection weight; default 1.
      loot: [TrophyGreydwarfBrute:1, Amber:3] # Guaranteed extras as itemPrefab:amount; normal drops remain.
      modifiers:                         # Partial map; omitted/{} inherits Enforcer.modifiers, while [] clears fallback.
        staggering: 30, 0.6              # chance%, staggerBonus.
        deathward: 30, 0.2, 10, 3        # chance%, restored max-health ratio, cooldown seconds, max activations.
        toxicDeath: 30, 0.3, 4, blob_aoe
        juggernaut: 30, 150, 5
    - summon: [Bjorn]
      settings: [50, 40, 1]
      weight: 3
      loot: [TrophyBjorn:1, Amber:3]
      modifiers:
        lightning: 10, 0.1
        deathward: 20, 0.2, 10, 3
        disruptive: 10, 0.5, 0.5, 0.5, 5
        blink: 10, 6, 16, fx_Adrenaline1
  dungeonEnforcers:                      # Matching location > unrestricted dungeon > outdoor fallback.
    - summon: [Skeleton_Poison, Skeleton:2]
      # location: MountainCave02          # Optional; quote names containing ':'.
      weight: 2
      loot: [TrophySkeletonPoison:1, TrophySkeleton:1, Amber:3]
    - summon: [Ghost, Skeleton]
      loot: [TrophyGhost:1, Amber:3]

Swamp:
  enabled: true
  enforcers:
    - summon: [Wraith, Ghost:2]
      loot: [TrophyWraith:1, AmberPearl:2]
  dungeonEnforcers:
    - summon: [Draugr_Elite, Draugr, Draugr_Ranged]
      loot: [TrophyDraugrElite:1, AmberPearl:2]

Mountain:
  enabled: true
  enforcers:
    - summon: [Fenring, Wolf:2]
      weight: 2
      loot: [TrophyFenring:1, AmberPearl:2]
    - summon: [Hatchling, Hatchling:2]
      loot: [TrophyHatchling:1, AmberPearl:2]
  dungeonEnforcers:
    - summon: [Fenring_Cultist, Ulv:2]
      loot: [TrophyCultist:1, AmberPearl:2]

Plains:
  enabled: true
  enforcers:
    - summon: [GoblinBrute, GoblinShaman, GoblinArcher, Goblin:2]
      loot: [TrophyGoblinBrute:1, Ruby:2]
    - summon: [Deathsquito, Deathsquito:4]
      loot: [TrophyDeathsquito:1, Ruby:2]

Mistlands:
  enabled: true
  enforcers:
    - summon: [Gjall, Tick:4]
      loot: [TrophyGjall:1, Ruby:2]
  dungeonEnforcers:
    - summon: [SeekerBrute, Seeker:2]
      loot: [TrophySeekerBrute:1, Ruby:2]

AshLands:
  enabled: true
  enforcers:
    - summon: [Charred_Mage, Charred_Archer, Charred_Melee]
      weight: 3
      loot: [TrophyCharredMelee:1, TrophyCharredMage:1, TrophyCharredArcher:1, SilverNecklace:2]
    - summon: [FallenValkyrie, Volture:2]
      loot: [TrophyFallenValkyrie:1, SilverNecklace:2]
    - summon: [Morgen_NonSleeping, Charred_Twitcher:2]
      loot: [TrophyMorgen:1, SilverNecklace:2]

""";
    }

    internal static bool TryParseYaml(string yaml, string source, out ParsedConfiguration parsed)
    {
        parsed = null!;
        try
        {
            KarmaSettings loaded = string.IsNullOrWhiteSpace(yaml) ? KarmaSettings.Default() : ReadSettings(yaml, source);
            parsed = new ParsedConfiguration(() =>
            {
                Settings = loaded;
                ResetEnforcerBootstrapScan();
            });
            return true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to load Karma configuration from {source}; existing Karma settings were kept: {ex.Message}");
            return false;
        }
    }

    internal static void CommitParsedConfiguration(ParsedConfiguration parsed)
    {
        if (parsed == null)
        {
            throw new ArgumentNullException(nameof(parsed));
        }

        parsed.Commit();
    }

    internal static void RecordDeath(Character character)
    {
        if (!IsKarmaSystemEnabled() || character == null)
        {
            return;
        }

        if (character.IsPlayer())
        {
            RecordPlayerDeath(character);
        }
    }

    internal static void RecordDeath(
        Character character,
        CreatureModifierManager.FinalDeathAttribution attribution)
    {
        if (!IsKarmaSystemEnabled() ||
            character == null ||
            character.IsPlayer() ||
            character.IsTamed() ||
            !attribution.HasSource)
        {
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null || zdo.GetBool(CountedDeathKey, false))
        {
            return;
        }

        Vector3 deathPosition = character.transform.position;
        if (!IsFinite(deathPosition))
        {
            return;
        }

        if (ZNet.instance == null || ZNet.instance.IsServer())
        {
            if (TryBuildCreatureDeathContext(
                    zdo,
                    character,
                    attribution.Source,
                    attribution.Kind,
                    deathPosition,
                    out CreatureDeathContext context))
            {
                ProcessAuthorizedCreatureDeath(context, zdo, validatedDestroyedZdo: false);
            }

            return;
        }

        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        ZPackage package = new();
        package.Write(zdo.m_uid);
        package.Write(attribution.Source);
        package.Write((int)attribution.Kind);
        package.Write(deathPosition);
        ZRoutedRpc.instance.InvokeRoutedRPC(
            ZRoutedRpc.instance.GetServerPeerID(),
            CreatureDeathRpc,
            package);
    }

    private static void RecordPlayerDeath(Character player)
    {
        if (player is not Player valheimPlayer)
        {
            return;
        }

        if (ZNet.instance != null && ZNet.instance.IsServer())
        {
            ZNetView? nview = valheimPlayer.m_nview;
            ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo != null && IsPlayerCharacterZdo(zdo))
            {
                ObservePlayerDeathState(zdo);
            }

            return;
        }

        SendPlayerDeathKarmaClear();
    }

    private static void SendPlayerDeathKarmaClear()
    {
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        ZRoutedRpc.instance.InvokeRoutedRPC(
            ZRoutedRpc.instance.GetServerPeerID(),
            PlayerDeathRpc,
            new ZPackage());
    }

    private static void RPC_PlayerDeath(long sender, ZPackage package)
    {
        if (!IsKarmaSystemEnabled() ||
            ZNet.instance == null ||
            !ZNet.instance.IsServer())
        {
            return;
        }

        try
        {
            if (!TryGetPeerPlayerZdo(sender, out _, out ZDO zdo))
            {
                return;
            }

            ObservePlayerDeathState(zdo);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to process Karma player death RPC: {ex.Message}");
        }
    }

    private static void RPC_CreatureDeath(long sender, ZPackage package)
    {
        if (!IsKarmaSystemEnabled() ||
            ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZDOMan.instance == null ||
            ZNetScene.instance == null)
        {
            return;
        }

        ZDOID pendingDeathId = ZDOID.None;
        try
        {
            ZDOID deadId = package.ReadZDOID();
            ZDOID sourceId = package.ReadZDOID();
            int kindValue = package.ReadInt();
            Vector3 reportedPosition = package.ReadVector3();
            if (deadId == ZDOID.None ||
                sourceId == ZDOID.None ||
                sourceId == deadId ||
                !Enum.IsDefined(typeof(CreatureModifierManager.DeathAttributionKind), kindValue) ||
                kindValue == (int)CreatureModifierManager.DeathAttributionKind.None ||
                !IsFinite(reportedPosition))
            {
                return;
            }

            ZDO deadZdo = ZDOMan.instance.GetZDO(deadId);
            if (deadZdo == null ||
                deadZdo.GetOwner() != sender ||
                deadZdo.GetBool(CountedDeathKey, false) ||
                !IsFinite(deadZdo.GetPosition()) ||
                (deadZdo.GetPosition() - reportedPosition).sqrMagnitude >
                CreatureDeathPositionTolerance * CreatureDeathPositionTolerance ||
                ServerProcessedCreatureDeaths.ContainsKey(deadId) ||
                ServerPendingCreatureDeaths.Contains(deadId) ||
                ServerPendingCreatureDeaths.Count >= MaximumPendingCreatureDeaths ||
                (ServerPendingCreatureDeathCounts.TryGetValue(sender, out int senderPending) &&
                 senderPending >= MaximumPendingCreatureDeathsPerPeer))
            {
                return;
            }

            ServerPendingCreatureDeaths.Add(deadId);
            ServerPendingCreatureDeathCounts[sender] = senderPending + 1;
            pendingDeathId = deadId;

            Character? deadCharacter = TryFindCharacter(deadId);
            if (!TryBuildCreatureDeathContext(
                    deadZdo,
                    deadCharacter,
                    sourceId,
                    (CreatureModifierManager.DeathAttributionKind)kindValue,
                    reportedPosition,
                    out CreatureDeathContext context))
            {
                ReleasePendingCreatureDeath(deadId, sender);
                pendingDeathId = ZDOID.None;
                return;
            }

            ZDOMan authority = ZDOMan.instance;
            ZNet.instance.StartCoroutine(
                AuthorizeCreatureDeathAfterSync(sender, reportedPosition, context, authority));
            pendingDeathId = ZDOID.None;
        }
        catch (Exception ex)
        {
            if (pendingDeathId != ZDOID.None)
            {
                ReleasePendingCreatureDeath(pendingDeathId, sender);
            }

            CreatureManagerPlugin.Log.LogDebug(
                $"Failed to process Karma creature death RPC: {ex.Message}");
        }
    }

    private static IEnumerator AuthorizeCreatureDeathAfterSync(
        long sender,
        Vector3 reportedPosition,
        CreatureDeathContext context,
        ZDOMan authority)
    {
        float deadline = Time.realtimeSinceStartup + CreatureDeathSyncTimeout;
        try
        {
            while (Time.realtimeSinceStartup <= deadline)
            {
                if (!ReferenceEquals(ZDOMan.instance, authority) ||
                    context.Epoch != CreatureDeathEpoch)
                {
                    yield break;
                }

                ZDO deadZdo = authority.GetZDO(context.DeadId);
                if (deadZdo == null)
                {
                    // The routed report was accepted only while this sender still owned a live,
                    // matching ZDO. Within the same ZDOMan lifetime, disappearance is the vanilla
                    // DestroyZDO confirmation and may arrive before the final health sync.
                    ProcessAuthorizedCreatureDeath(context, null, validatedDestroyedZdo: true);
                    yield break;
                }

                if (deadZdo.GetOwner() != sender ||
                    deadZdo.GetBool(CountedDeathKey, false) ||
                    !IsFinite(deadZdo.GetPosition()) ||
                    (deadZdo.GetPosition() - reportedPosition).sqrMagnitude >
                    CreatureDeathPositionTolerance * CreatureDeathPositionTolerance)
                {
                    yield break;
                }

                bool observedDead = deadZdo.GetBool(ZDOVars.s_dead, false) ||
                                    deadZdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity) <= 0f;
                Character? deadCharacter = TryFindCharacter(context.DeadId);
                if (!observedDead && deadCharacter != null)
                {
                    observedDead = deadCharacter.IsDead() || deadCharacter.GetHealth() <= 0f;
                }

                if (observedDead)
                {
                    context.Position = deadZdo.GetPosition();
                    ProcessAuthorizedCreatureDeath(context, deadZdo, validatedDestroyedZdo: false);
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            ReleasePendingCreatureDeath(context.DeadId, sender);
        }
    }

    private static bool TryBuildCreatureDeathContext(
        ZDO deadZdo,
        Character? deadCharacter,
        ZDOID sourceId,
        CreatureModifierManager.DeathAttributionKind attributionKind,
        Vector3 reportedPosition,
        out CreatureDeathContext context)
    {
        context = null!;
        if (deadZdo == null ||
            sourceId == ZDOID.None ||
            sourceId == deadZdo.m_uid ||
            attributionKind == CreatureModifierManager.DeathAttributionKind.None ||
            !IsFinite(reportedPosition) ||
            deadZdo.GetBool(ZDOVars.s_tamed, deadCharacter != null && deadCharacter.IsTamed()) ||
            ZNetScene.instance == null ||
            !TryValidatePlayerSideDeathSource(sourceId, out bool sourceIsPlayer))
        {
            return false;
        }

        GameObject prefabObject = ZNetScene.instance.GetPrefab(deadZdo.GetPrefab());
        Character? prefabCharacter = prefabObject != null ? prefabObject.GetComponent<Character>() : null;
        if (prefabCharacter == null || prefabCharacter.IsPlayer())
        {
            return false;
        }

        bool boss = deadCharacter != null ? deadCharacter.IsBoss() : prefabCharacter.IsBoss();
        bool enforcer = deadZdo.GetBool(EnforcerKey, false);
        bool playerAttributedKill = sourceIsPlayer &&
                                    attributionKind is CreatureModifierManager.DeathAttributionKind.Direct or
                                        CreatureModifierManager.DeathAttributionKind.Delayed;
        float omenChance = 0f;
        if (playerAttributedKill &&
            CreatureLevelManager.AllowsModifierEffects(deadZdo, boss, enforcer))
        {
            CreatureModifierManager.TryGetOmenEnforcerChance(deadZdo, out omenChance);
        }

        context = new CreatureDeathContext
        {
            DeadId = deadZdo.m_uid,
            Prefab = Utils.GetPrefabName(prefabObject),
            Position = deadZdo.GetPosition(),
            Level = Mathf.Max(1, deadZdo.GetInt(ZDOVars.s_level, deadCharacter?.GetLevel() ?? 1)),
            Boss = boss,
            Enforcer = enforcer,
            KarmaSummoned = deadZdo.GetBool(EnforcerSummonedKey, false) ||
                            (deadCharacter != null && IsRuntimeSummonedCreature(deadCharacter)),
            PlayerAttributedKill = playerAttributedKill,
            PlayerKillerId = playerAttributedKill ? sourceId : ZDOID.None,
            AttributionKind = attributionKind,
            OmenChance = omenChance,
            Epoch = CreatureDeathEpoch
        };
        return context.DeadId != ZDOID.None && IsFinite(context.Position);
    }

    private static void ProcessAuthorizedCreatureDeath(
        CreatureDeathContext context,
        ZDO? deadZdo,
        bool validatedDestroyedZdo)
    {
        if (!IsKarmaSystemEnabled() ||
            context == null ||
            context.DeadId == ZDOID.None ||
            context.Epoch != CreatureDeathEpoch ||
            (deadZdo == null && !validatedDestroyedZdo) ||
            (deadZdo != null && deadZdo.m_uid != context.DeadId))
        {
            return;
        }

        PruneProcessedCreatureDeaths();
        if ((deadZdo != null && deadZdo.GetBool(CountedDeathKey, false)) ||
            ServerProcessedCreatureDeaths.ContainsKey(context.DeadId))
        {
            return;
        }

        ServerProcessedCreatureDeaths[context.DeadId] =
            Time.realtimeSinceStartup + ProcessedCreatureDeathRetention;
        deadZdo?.Set(CountedDeathKey, true);
        if (context.Enforcer)
        {
            BroadcastRegionalCenterQuote(EnforcerDeathQuotes, context.Position);
        }

        if (context.KarmaSummoned)
        {
            return;
        }

        if (!ShouldBlockKarmaGain(context.Position, context.DeadId))
        {
            float amount = GetKillKarma(
                context.Prefab,
                context.Boss,
                context.Level,
                IsLikelyDungeonPosition(context.Position));
            if (amount > 0f)
            {
                AddKarma(context.Position, amount);
            }
        }

        if (context.PlayerAttributedKill &&
            IsEnforcerEnabled() &&
            context.OmenChance > 0f &&
            UnityEngine.Random.Range(0f, 1f) < context.OmenChance)
        {
            bool summoned = TryForceEnforcerSummonNear(
                context.PlayerKillerId,
                context.DeadId,
                GetKarmaRealm(context.Position),
                out EnforcerSummonFailure failure);
            string failureSuffix = summoned ? "" : $" reason={failure}";
            CreatureManagerPlugin.Log.LogInfo(
                $"Karma Omen triggered by {context.Prefab}: attribution={context.AttributionKind} " +
                $"chance={context.OmenChance:P0} summoned={summoned}{failureSuffix}");
        }
    }

    private static bool TryValidatePlayerSideDeathSource(ZDOID sourceId, out bool sourceIsPlayer)
    {
        sourceIsPlayer = false;
        if (sourceId == ZDOID.None || ZNetScene.instance == null)
        {
            return false;
        }

        if (Player.m_localPlayer != null && Player.m_localPlayer.GetZDOID() == sourceId)
        {
            sourceIsPlayer = true;
            return true;
        }

        if (ZNet.instance != null)
        {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer != null && peer.IsReady() && peer.m_characterID == sourceId)
                {
                    sourceIsPlayer = true;
                    return true;
                }
            }
        }

        Character? sourceCharacter = TryFindCharacter(sourceId);
        if (sourceCharacter != null)
        {
            if (sourceCharacter.IsPlayer())
            {
                return false;
            }

            return sourceCharacter.IsTamed() ||
                   sourceCharacter.GetFaction() == Character.Faction.PlayerSpawned;
        }

        if (ZDOMan.instance == null)
        {
            return false;
        }

        ZDO sourceZdo = ZDOMan.instance.GetZDO(sourceId);
        if (sourceZdo == null)
        {
            return false;
        }

        if (sourceZdo.GetBool(ZDOVars.s_tamed, false))
        {
            return true;
        }

        GameObject sourcePrefab = ZNetScene.instance.GetPrefab(sourceZdo.GetPrefab());
        Character? sourcePrefabCharacter = sourcePrefab != null ? sourcePrefab.GetComponent<Character>() : null;
        return sourcePrefabCharacter != null &&
               !sourcePrefabCharacter.IsPlayer() &&
               sourcePrefabCharacter.GetFaction() == Character.Faction.PlayerSpawned;
    }

    private static Character? TryFindCharacter(ZDOID id)
    {
        if (id == ZDOID.None || ZNetScene.instance == null)
        {
            return null;
        }

        GameObject instance = ZNetScene.instance.FindInstance(id);
        return instance != null ? instance.GetComponent<Character>() : null;
    }

    private static void PruneProcessedCreatureDeaths()
    {
        float now = Time.realtimeSinceStartup;
        StaleProcessedCreatureDeaths.Clear();
        foreach (KeyValuePair<ZDOID, float> entry in ServerProcessedCreatureDeaths)
        {
            if (entry.Value <= now)
            {
                StaleProcessedCreatureDeaths.Add(entry.Key);
            }
        }

        foreach (ZDOID id in StaleProcessedCreatureDeaths)
        {
            ServerProcessedCreatureDeaths.Remove(id);
        }

        StaleProcessedCreatureDeaths.Clear();
    }

    private static void ReleasePendingCreatureDeath(ZDOID deadId, long sender)
    {
        if (deadId == ZDOID.None || !ServerPendingCreatureDeaths.Remove(deadId))
        {
            return;
        }

        if (!ServerPendingCreatureDeathCounts.TryGetValue(sender, out int count) || count <= 1)
        {
            ServerPendingCreatureDeathCounts.Remove(sender);
            return;
        }

        ServerPendingCreatureDeathCounts[sender] = count - 1;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static void RequestKarmaStatus()
    {
        if (ZRoutedRpc.instance == null || ZNet.instance == null || ZNet.instance.IsServer() || Time.time < NextKarmaStatusRequestTime)
        {
            return;
        }

        NextKarmaStatusRequestTime = Time.time + KarmaStatusRequestInterval;
        int requestId = ++NextKarmaStatusRequestId;
        ZPackage package = new();
        package.Write(requestId);
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), KarmaStatusRequestRpc, package);
    }

    private static void RPC_KarmaStatusRequest(long sender, ZPackage package)
    {
        if (!IsKarmaSystemEnabled() ||
            ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZRoutedRpc.instance == null)
        {
            return;
        }

        try
        {
            int requestId = package.ReadInt();
            if (!TryGetPeerPlayerZdo(sender, out _, out ZDO playerZdo))
            {
                return;
            }

            Vector3 position = playerZdo.GetPosition();
            if (!IsFinite(position))
            {
                return;
            }

            float karma = GetKarma(position);
            ZPackage response = new();
            response.Write(requestId);
            response.Write(karma);
            response.Write(IsKarmaLevelEnabled() ? GetSectorLevelBonus(karma) : 0);
            response.Write((int)GetKarmaRealm(position));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, KarmaStatusResponseRpc, response);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to process Karma status request: {ex.Message}");
        }
    }

    private static void RPC_KarmaStatusResponse(long sender, ZPackage package)
    {
        if (ZRoutedRpc.instance == null ||
            sender != ZRoutedRpc.instance.GetServerPeerID())
        {
            return;
        }

        try
        {
            int requestId = package.ReadInt();
            float karma = package.ReadSingle();
            int level = package.ReadInt();
            int realmValue = package.ReadInt();
            if (requestId < LastKarmaStatusResponseId)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(KarmaRealm), realmValue))
            {
                return;
            }

            LastKarmaStatusResponseId = requestId;
            KarmaRealm realm = (KarmaRealm)realmValue;
            Player? localPlayer = Player.m_localPlayer;
            if (localPlayer != null && GetKarmaRealm(localPlayer.transform.position) != realm)
            {
                ClientKarmaStatusValid = false;
                NextKarmaStatusRequestTime = 0f;
                return;
            }

            ClientKarmaStatusValue = Mathf.Max(0f, karma);
            ClientKarmaStatusLevel = Mathf.Max(0, level);
            ClientKarmaStatusRealm = realm;
            ClientKarmaStatusValid = true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to process Karma status response: {ex.Message}");
        }
    }

    private static bool TryGetPeerPlayerZdo(long sender, out ZNetPeer peer, out ZDO zdo)
    {
        peer = null!;
        zdo = null!;
        if (ZNet.instance == null || ZDOMan.instance == null)
        {
            return false;
        }

        peer = ZNet.instance.GetPeer(sender);
        if (peer == null || !peer.IsReady() || peer.m_characterID.IsNone())
        {
            peer = null!;
            return false;
        }

        zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
        if (zdo == null || zdo.GetOwner() != peer.m_uid || !IsPlayerCharacterZdo(zdo))
        {
            peer = null!;
            zdo = null!;
            return false;
        }

        return true;
    }

    private static bool IsPlayerCharacterZdo(ZDO zdo)
    {
        if (zdo == null || ZNetScene.instance == null)
        {
            return false;
        }

        GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
        return prefab != null && prefab.GetComponent<Player>() != null;
    }

    private static void ObservePlayerDeathState(ZDO zdo)
    {
        float health = zdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity);
        bool dead = zdo.GetBool(ZDOVars.s_dead, false) &&
                    !float.IsNaN(health) &&
                    !float.IsInfinity(health) &&
                    health <= 0f;
        bool newDeath = false;
        lock (Sync)
        {
            if (!dead)
            {
                ObservedPlayerDeathStates[zdo.m_uid] = false;
                return;
            }

            if (!ObservedPlayerDeathStates.TryGetValue(zdo.m_uid, out bool alreadyProcessed) || !alreadyProcessed)
            {
                ObservedPlayerDeathStates[zdo.m_uid] = true;
                newDeath = true;
            }
        }

        if (newDeath && IsKarmaSystemEnabled() && Settings.Karma.PlayerDeathClearKarma > 0f)
        {
            ApplyPlayerDeathKarmaClear(zdo.GetPosition());
        }
    }

    private static void RefreshObservedPlayerDeathTransitions()
    {
        if (ZNet.instance == null || ZDOMan.instance == null || ZNetScene.instance == null)
        {
            return;
        }

        HashSet<ZDOID> activeCharacterIds = new();
        ZDOID localCharacterId = ZNet.instance.LocalPlayerCharacterID;
        if (!localCharacterId.IsNone())
        {
            activeCharacterIds.Add(localCharacterId);
            ZDO localZdo = ZDOMan.instance.GetZDO(localCharacterId);
            if (localZdo != null && IsPlayerCharacterZdo(localZdo))
            {
                ObservePlayerDeathState(localZdo);
            }
        }

        foreach (ZNetPeer peer in ZNet.instance.GetConnectedPeers())
        {
            if (peer == null || !peer.IsReady() || peer.m_characterID.IsNone())
            {
                continue;
            }

            activeCharacterIds.Add(peer.m_characterID);
            if (TryGetPeerPlayerZdo(peer.m_uid, out _, out ZDO peerZdo))
            {
                ObservePlayerDeathState(peerZdo);
            }
        }

        lock (Sync)
        {
            foreach (ZDOID characterId in ObservedPlayerDeathStates.Keys
                         .Where(characterId => !activeCharacterIds.Contains(characterId))
                         .ToList())
            {
                ObservedPlayerDeathStates.Remove(characterId);
            }
        }
    }

    private static void ApplyPlayerDeathKarmaClear(Vector3 position)
    {
        lock (Sync)
        {
            ReduceKarmaUnsafe(position, Settings.Karma.PlayerDeathClearKarma, Time.time);
        }
    }

    internal static void UpdateSummons()
    {
        bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
        if (isServer)
        {
            float serverNow = Time.time;
            RefreshObservedPlayerDeathTransitions();
            PruneSectorStates(serverNow);
        }

        if (!IsEnforcerEnabled())
        {
            EnforcerNoPlayerSince.Clear();
            LastEnforcerAbandonmentDespawnSeconds = -1;
            if (!EnforcerBootstrapScanPending || EnforcerBootstrapScanInitialized)
            {
                ResetEnforcerBootstrapScan();
            }

            return;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer() || ZNetScene.instance == null)
        {
            return;
        }

        float now = Time.time;
        if (EnforcerBootstrapScanPending)
        {
            if (AdvanceEnforcerBootstrapScan())
            {
                EnforcerBootstrapScanPending = false;
            }

            if (EnforcerBootstrapScanPending)
            {
                return;
            }
        }

        UpdateAbandonedEnforcers(now);
        if (now < NextSummonCheckTime)
        {
            return;
        }

        NextSummonCheckTime = now + Mathf.Max(1f, Settings.Enforcer.CheckInterval);
        DungeonComponentPositionCache.Clear();
        List<ConnectedPlayerContext> players = GetConnectedAlivePlayerContexts();

        foreach ((ConnectedPlayerContext representative, Vector3 centerPosition, HashSet<string> regionZoneKeys) in BuildSummonCheckRegions(players))
        {
            bool summoned = TrySummonForPlayer(
                representative,
                now,
                out EnforcerSummonFailure failure,
                regionPosition: centerPosition,
                regionZoneKeys: regionZoneKeys);
            if (!summoned)
            {
                CreatureManagerPlugin.Log.LogDebug(
                    $"Karma Enforcer periodic check skipped for player {representative.CharacterId} " +
                    $"(peer {representative.PeerUid}): reason={failure}.");
            }
        }
    }

    private static void UpdateAbandonedEnforcers(float now)
    {
        if (now < NextEnforcerAbandonmentCheckTime)
        {
            return;
        }

        NextEnforcerAbandonmentCheckTime = now + EnforcerAbandonmentCheckInterval;
        int despawnSeconds = GetEnforcerAbandonedDespawnSeconds();
        if (despawnSeconds != LastEnforcerAbandonmentDespawnSeconds)
        {
            EnforcerNoPlayerSince.Clear();
            LastEnforcerAbandonmentDespawnSeconds = despawnSeconds;
        }

        if (despawnSeconds <= 0)
        {
            EnforcerNoPlayerSince.Clear();
            return;
        }

        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZDOMan.instance == null ||
            ZoneSystem.instance == null)
        {
            return;
        }

        if (!CollectEnforcerPlayerPresences())
        {
            EnforcerNoPlayerSince.Clear();
            return;
        }

        StaleTrackedEnforcerIds.Clear();
        AbandonedEnforcerIds.Clear();

        foreach (ZDOID enforcerId in TrackedEnforcerZdoIds)
        {
            ZDO enforcerZdo = ZDOMan.instance.GetZDO(enforcerId);
            if (!IsTrackedCharacterZdoAlive(enforcerZdo) ||
                !enforcerZdo.GetBool(EnforcerKey, false) ||
                enforcerZdo.GetBool(ZDOVars.s_tamed, false))
            {
                StaleTrackedEnforcerIds.Add(enforcerId);
                continue;
            }

            if (HasLivingPlayerInEnforcerRange(enforcerZdo))
            {
                EnforcerNoPlayerSince.Remove(enforcerId);
                continue;
            }

            if (!EnforcerNoPlayerSince.TryGetValue(enforcerId, out float noPlayerSince))
            {
                EnforcerNoPlayerSince[enforcerId] = now;
                continue;
            }

            if (now - noPlayerSince >= despawnSeconds)
            {
                AbandonedEnforcerIds.Add(enforcerId);
            }
        }

        foreach (ZDOID staleId in StaleTrackedEnforcerIds)
        {
            TrackedEnforcerZdoIds.Remove(staleId);
            EnforcerNoPlayerSince.Remove(staleId);
        }

        foreach (ZDOID abandonedId in AbandonedEnforcerIds)
        {
            ZDO enforcerZdo = ZDOMan.instance.GetZDO(abandonedId);
            if (!IsTrackedCharacterZdoAlive(enforcerZdo) ||
                !enforcerZdo.GetBool(EnforcerKey, false) ||
                enforcerZdo.GetBool(ZDOVars.s_tamed, false) ||
                HasLivingPlayerInEnforcerRange(enforcerZdo))
            {
                EnforcerNoPlayerSince.Remove(abandonedId);
                continue;
            }

            DespawnAbandonedEnforcer(enforcerZdo);
        }

        StaleTrackedEnforcerIds.Clear();
        AbandonedEnforcerIds.Clear();
        EnforcerPlayerPresenceBuffer.Clear();
        EnforcerPlayerPresenceIds.Clear();
    }

    private static bool CollectEnforcerPlayerPresences()
    {
        EnforcerPlayerPresenceBuffer.Clear();
        EnforcerPlayerPresenceIds.Clear();
        if (ZNet.instance == null)
        {
            return false;
        }

        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer != null && localPlayer.gameObject != null && !localPlayer.IsDead())
        {
            ZDOID localPlayerId = localPlayer.GetZDOID();
            if (localPlayerId.IsNone())
            {
                localPlayerId = ZNet.instance.LocalPlayerCharacterID;
            }

            TryAddEnforcerPlayerPresence(localPlayerId, localPlayer.transform.position);
        }

        List<ZNetPeer>? peers = ZNet.instance.GetPeers();
        if (peers == null)
        {
            return false;
        }

        foreach (ZNetPeer peer in peers)
        {
            if (TryGetLivingPeerPresence(peer, out ZDOID playerId, out Vector3 position))
            {
                TryAddEnforcerPlayerPresence(playerId, position);
            }
        }

        return true;
    }

    private static bool TryGetLivingPeerPresence(
        ZNetPeer peer,
        out ZDOID playerId,
        out Vector3 position)
    {
        playerId = ZDOID.None;
        position = Vector3.zero;
        if (peer == null ||
            peer.m_uid == 0L ||
            !peer.IsReady() ||
            peer.m_characterID.IsNone())
        {
            return false;
        }

        playerId = peer.m_characterID;
        GameObject? instance = ZNetScene.instance?.FindInstance(playerId);
        Player? loadedPlayer = instance != null ? instance.GetComponent<Player>() : null;
        if (loadedPlayer != null)
        {
            if (loadedPlayer.IsDead())
            {
                return false;
            }

            position = loadedPlayer.transform.position;
            return IsFinite(position);
        }

        ZDO? playerZdo = ZDOMan.instance?.GetZDO(playerId);
        if (playerZdo != null)
        {
            float health = playerZdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity);
            bool dead = playerZdo.GetBool(ZDOVars.s_dead, false) ||
                        (!float.IsNaN(health) && !float.IsInfinity(health) && health <= 0f);
            if (dead)
            {
                return false;
            }

            if (!float.IsNaN(health))
            {
                position = playerZdo.GetPosition();
                if (IsFinite(position))
                {
                    return true;
                }
            }
        }

        position = peer.GetRefPos();
        return IsFinite(position);
    }

    private static void TryAddEnforcerPlayerPresence(ZDOID playerId, Vector3 position)
    {
        if (playerId.IsNone() ||
            !IsFinite(position) ||
            !EnforcerPlayerPresenceIds.Add(playerId))
        {
            return;
        }

        EnforcerPlayerPresenceBuffer.Add(new EnforcerPlayerPresence(
            position,
            ZoneSystem.GetZone(position),
            Character.InInterior(position)));
    }

    private static bool HasLivingPlayerInEnforcerRange(ZDO enforcerZdo)
    {
        Vector3 enforcerPosition = enforcerZdo.GetPosition();
        if (!IsFinite(enforcerPosition))
        {
            return false;
        }

        bool anchorStored = enforcerZdo.GetBool(EnforcerPresenceAnchorStoredKey, false);
        bool enforcerInterior = anchorStored
            ? enforcerZdo.GetBool(EnforcerPresenceInteriorKey, false)
            : Character.InInterior(enforcerPosition);
        Vector2i currentZone = ZoneSystem.GetZone(enforcerPosition);
        Vector2i enforcerZone = anchorStored && enforcerInterior
            ? new Vector2i(
                enforcerZdo.GetInt(EnforcerPresenceZoneXKey, currentZone.x),
                enforcerZdo.GetInt(EnforcerPresenceZoneYKey, currentZone.y))
            : currentZone;

        foreach (EnforcerPlayerPresence player in EnforcerPlayerPresenceBuffer)
        {
            if (player.Interior != enforcerInterior)
            {
                continue;
            }

            if (enforcerInterior &&
                (player.Zone.x != enforcerZone.x || player.Zone.y != enforcerZone.y))
            {
                continue;
            }

            float deltaX = player.Position.x - enforcerPosition.x;
            float deltaZ = player.Position.z - enforcerPosition.z;
            if (deltaX * deltaX + deltaZ * deltaZ <= EnforcerPresenceRangeSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetEnforcerBootstrapScan()
    {
        EnforcerBootstrapScanPending = true;
        EnforcerBootstrapScanInitialized = false;
        EnforcerBootstrapPrefabNames.Clear();
        EnforcerBootstrapScanBuffer.Clear();
        EnforcerBootstrapPrefabPosition = 0;
        EnforcerBootstrapZdoScanIndex = 0;
        EnforcerBootstrapRestoredCount = 0;
    }

    private static bool AdvanceEnforcerBootstrapScan()
    {
        if (ZDOMan.instance == null || ZoneSystem.instance == null)
        {
            return false;
        }

        EnsureEnforcerBootstrapScanInitialized();
        if (EnforcerBootstrapPrefabPosition >= EnforcerBootstrapPrefabNames.Count)
        {
            CompleteEnforcerBootstrapScan();
            return true;
        }

        string prefab = EnforcerBootstrapPrefabNames[EnforcerBootstrapPrefabPosition];
        bool prefabComplete = ZDOMan.instance.GetAllZDOsWithPrefabIterative(
            prefab,
            EnforcerBootstrapScanBuffer,
            ref EnforcerBootstrapZdoScanIndex);
        if (!prefabComplete)
        {
            return false;
        }

        RegisterEnforcerBootstrapResults();
        EnforcerBootstrapPrefabPosition++;
        EnforcerBootstrapZdoScanIndex = 0;
        EnforcerBootstrapScanBuffer.Clear();
        if (EnforcerBootstrapPrefabPosition < EnforcerBootstrapPrefabNames.Count)
        {
            return false;
        }

        CompleteEnforcerBootstrapScan();
        return true;
    }

    private static void EnsureEnforcerBootstrapScanInitialized()
    {
        if (EnforcerBootstrapScanInitialized)
        {
            return;
        }

        HashSet<string> candidatePrefabs = new(StringComparer.Ordinal);
        foreach (EnforcerBiomeDefinition biome in Settings.Enforcer.Biomes.Values)
        {
            IEnumerable<EnforcerCandidateDefinition> candidates = biome.Outdoor
                .Concat(biome.Dungeon)
                .Concat(biome.DungeonByLocation.Values.SelectMany(entries => entries));
            foreach (EnforcerCandidateDefinition candidate in candidates)
            {
                string prefab = candidate.Summon.Boss.Trim();
                if (prefab.Length > 0)
                {
                    candidatePrefabs.Add(prefab);
                }
            }
        }

        EnforcerBootstrapPrefabNames.AddRange(
            candidatePrefabs.OrderBy(static prefab => prefab, StringComparer.Ordinal));
        EnforcerBootstrapScanInitialized = true;
    }

    private static void RegisterEnforcerBootstrapResults()
    {
        foreach (ZDO zdo in EnforcerBootstrapScanBuffer)
        {
            if (zdo == null ||
                zdo.m_uid.IsNone() ||
                !zdo.GetBool(EnforcerKey, false) ||
                !IsTrackedCharacterZdoAlive(zdo) ||
                zdo.GetBool(ZDOVars.s_tamed, false))
            {
                continue;
            }

            bool alreadyTracked = TrackedEnforcerZdoIds.Contains(zdo.m_uid);
            TrackPotentialBlockerZdo(zdo, isBoss: false);
            if (!alreadyTracked)
            {
                EnforcerBootstrapRestoredCount++;
            }

            StoreEnforcerPresenceAnchor(zdo);
        }
    }

    private static void CompleteEnforcerBootstrapScan()
    {
        int restored = EnforcerBootstrapRestoredCount;
        EnforcerBootstrapScanInitialized = false;
        EnforcerBootstrapPrefabNames.Clear();
        EnforcerBootstrapScanBuffer.Clear();
        EnforcerBootstrapPrefabPosition = 0;
        EnforcerBootstrapZdoScanIndex = 0;
        EnforcerBootstrapRestoredCount = 0;

        if (restored > 0)
        {
            CreatureManagerPlugin.Log.LogInfo(
                $"Restored tracking for {restored} persisted Karma Enforcer(s) from server ZDO data.");
        }
    }

    private static void DespawnAbandonedEnforcer(ZDO enforcerZdo)
    {
        if (enforcerZdo == null ||
            enforcerZdo.m_uid.IsNone() ||
            ZDOMan.instance == null ||
            !IsTrackedCharacterZdoAlive(enforcerZdo) ||
            !enforcerZdo.GetBool(EnforcerKey, false) ||
            enforcerZdo.GetBool(ZDOVars.s_tamed, false))
        {
            return;
        }

        ZDOID enforcerId = enforcerZdo.m_uid;
        string displayName = enforcerZdo.GetString(EnforcerNameKey, "Enforcer");
        TrackedEnforcerZdoIds.Remove(enforcerId);
        EnforcerNoPlayerSince.Remove(enforcerId);
        TrackedBossZdoIds.Remove(enforcerId);
        ReportedBlockerZdoIds.Remove(enforcerId);
        enforcerZdo.SetOwner(ZDOMan.instance.m_sessionID);
        enforcerZdo.Set(CountedDeathKey, true);
        enforcerZdo.Set(EnforcerSummonedKey, false);
        enforcerZdo.Set(EnforcerKey, false);
        ClearActiveBossCountBeforeDespawn(enforcerZdo);
        ZDOMan.instance.DestroyZDO(enforcerZdo);

        CreatureManagerPlugin.Log.LogInfo(
            $"Removed abandoned Karma Enforcer '{displayName}' after " +
            $"{GetEnforcerAbandonedDespawnSeconds()}s without a living player within {EnforcerPresenceRange:0}m.");
    }

    private static void ClearActiveBossCountBeforeDespawn(ZDO zdo)
    {
        if (ZoneSystem.instance == null || !zdo.GetBool("bosscount", false))
        {
            return;
        }

        ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out float activeBossCount);
        ZoneSystem.instance.SetGlobalKey(GlobalKeys.activeBosses, Mathf.Max(0f, activeBossCount - 1f));
        zdo.Set("bosscount", false);
    }

    private static List<ConnectedPlayerContext> GetConnectedAlivePlayerContexts()
    {
        List<ConnectedPlayerContext> players = new();
        if (ZNet.instance == null || ZDOMan.instance == null || ZNetScene.instance == null)
        {
            return players;
        }

        HashSet<ZDOID> addedCharacterIds = new();
        foreach (ZNetPeer peer in ZNet.instance.GetConnectedPeers())
        {
            if (TryCreateConnectedPlayerContext(peer, out ConnectedPlayerContext player) &&
                addedCharacterIds.Add(player.CharacterId))
            {
                players.Add(player);
            }
        }

        Player? localPlayer = Player.m_localPlayer;
        ZNetView? localNview = localPlayer != null ? localPlayer.m_nview : null;
        ZDO? localZdo = localNview != null && localNview.IsValid() ? localNview.GetZDO() : null;
        if (localPlayer != null &&
            !localPlayer.IsDead() &&
            localZdo != null &&
            IsPlayerCharacterZdo(localZdo) &&
            TryCreateConnectedPlayerContext(localZdo.GetOwner(), localZdo, out ConnectedPlayerContext localContext) &&
            addedCharacterIds.Add(localContext.CharacterId))
        {
            players.Add(localContext);
        }

        return players;
    }

    private static bool TryCreateConnectedPlayerContext(
        ZNetPeer peer,
        out ConnectedPlayerContext player)
    {
        player = null!;
        if (peer == null ||
            !peer.IsReady() ||
            peer.m_characterID.IsNone() ||
            ZDOMan.instance == null)
        {
            return false;
        }

        ZDO zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
        return zdo != null &&
               zdo.GetOwner() == peer.m_uid &&
               IsPlayerCharacterZdo(zdo) &&
               TryCreateConnectedPlayerContext(peer.m_uid, zdo, out player);
    }

    private static bool TryCreateConnectedPlayerContext(
        long peerUid,
        ZDO playerZdo,
        out ConnectedPlayerContext player)
    {
        player = null!;
        if (playerZdo == null ||
            playerZdo.m_uid.IsNone() ||
            !IsPlayerCharacterZdo(playerZdo))
        {
            return false;
        }

        Vector3 position = playerZdo.GetPosition();
        float health = playerZdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity);
        bool dead = playerZdo.GetBool(ZDOVars.s_dead, false) ||
                    (!float.IsNaN(health) && !float.IsInfinity(health) && health <= 0f);
        if (dead || !IsFinite(position) || float.IsNaN(health))
        {
            return false;
        }

        player = new ConnectedPlayerContext(peerUid, playerZdo.m_uid, position);
        return true;
    }

    private static List<(ConnectedPlayerContext Representative, Vector3 CenterPosition, HashSet<string> RegionZoneKeys)> BuildSummonCheckRegions(
        IReadOnlyList<ConnectedPlayerContext> players)
    {
        Dictionary<(int X, int Y, KarmaRealm Realm), List<ConnectedPlayerContext>> playersByCenterZone = new();
        foreach (ConnectedPlayerContext player in players)
        {
            Vector2i centerZone = ZoneSystem.GetZone(player.Position);
            (int X, int Y, KarmaRealm Realm) key =
                (centerZone.x, centerZone.y, GetKarmaRealm(player.Position));
            if (!playersByCenterZone.TryGetValue(key, out List<ConnectedPlayerContext> zonePlayers))
            {
                zonePlayers = new List<ConnectedPlayerContext>();
                playersByCenterZone[key] = zonePlayers;
            }

            zonePlayers.Add(player);
        }

        List<SummonCheckWindow> windows = new();
        foreach (KeyValuePair<(int X, int Y, KarmaRealm Realm), List<ConnectedPlayerContext>> entry in playersByCenterZone)
        {
            Vector2i centerZone = new(entry.Key.X, entry.Key.Y);
            Vector3 centerPosition = ZoneSystem.GetZonePos(centerZone);
            centerPosition.y = entry.Value[0].Position.y;
            float karma = GetKarma(centerPosition);
            List<ConnectedPlayerContext> eligiblePlayers = players
                .Where(player => IsInKarmaNeighborhood(player.Position, centerZone, entry.Key.Realm))
                .Where(player => HasEligibleEnforcerCandidate(player, karma))
                .ToList();
            windows.Add(new SummonCheckWindow(
                centerZone,
                centerPosition,
                karma,
                eligiblePlayers,
                new HashSet<string>(GetSectorKeys(centerPosition), StringComparer.Ordinal)));
        }

        bool[] connected = new bool[windows.Count];
        List<(ConnectedPlayerContext Representative, Vector3 CenterPosition, HashSet<string> RegionZoneKeys)> regions = new();
        for (int windowIndex = 0; windowIndex < windows.Count; windowIndex++)
        {
            if (connected[windowIndex])
            {
                continue;
            }

            List<SummonCheckWindow> component = new();
            Stack<int> pending = new();
            pending.Push(windowIndex);
            connected[windowIndex] = true;
            while (pending.Count > 0)
            {
                SummonCheckWindow current = windows[pending.Pop()];
                component.Add(current);
                for (int candidateIndex = 0; candidateIndex < windows.Count; candidateIndex++)
                {
                    if (connected[candidateIndex] || !current.ZoneKeys.Overlaps(windows[candidateIndex].ZoneKeys))
                    {
                        continue;
                    }

                    connected[candidateIndex] = true;
                    pending.Push(candidateIndex);
                }
            }

            SummonCheckWindow? anchor = component
                .Where(window => window.EligiblePlayers.Count > 0)
                .OrderByDescending(window => window.Karma)
                .ThenByDescending(window => window.EligiblePlayers.Count)
                .ThenBy(window => window.CenterZone.x)
                .ThenBy(window => window.CenterZone.y)
                .FirstOrDefault();
            if (anchor == null)
            {
                continue;
            }

            ConnectedPlayerContext representative = anchor.EligiblePlayers[UnityEngine.Random.Range(0, anchor.EligiblePlayers.Count)];
            Vector3 centerPosition = anchor.CenterPosition;
            centerPosition.y = representative.Position.y;
            HashSet<string> regionZoneKeys = new(StringComparer.Ordinal);
            foreach (SummonCheckWindow window in component)
            {
                regionZoneKeys.UnionWith(window.ZoneKeys);
            }

            regions.Add((representative, centerPosition, regionZoneKeys));
        }

        return regions;
    }

    private static bool HasEligibleEnforcerCandidate(ConnectedPlayerContext player, float karma)
    {
        Vector3 position = player.Position;
        Heightmap.Biome biome = GetBiome(position);
        bool dungeonSummon = IsLikelyDungeonPosition(position);
        if (!TryGetEnforcerBiomeDefinition(biome, out EnforcerBiomeDefinition biomeDefinition) || !biomeDefinition.Enabled)
        {
            return false;
        }

        string dungeonLocation = dungeonSummon && TryGetDungeonLocationPrefabName(position, out string resolvedDungeonLocation)
            ? resolvedDungeonLocation
            : "";
        ResolvedEnforcerSettings baseline = ResolvedEnforcerSettings.FromGlobal(Settings.Enforcer);
        foreach (EnforcerCandidateDefinition candidate in biomeDefinition.GetCandidates(dungeonSummon, dungeonLocation))
        {
            if (candidate.Weight <= 0f || candidate.Summon.Boss.Length == 0)
            {
                continue;
            }

            ResolvedEnforcerSettings settings = ResolveEnforcerSettings(candidate.Override, baseline);
            if (karma >= settings.RequiredKarma)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryForceEnforcerSummonNear(
        ZDOID killerId,
        ZDOID excludedDeadId,
        KarmaRealm triggerRealm,
        out EnforcerSummonFailure failure)
    {
        failure = EnforcerSummonFailure.None;
        if (!IsEnforcerEnabled())
        {
            failure = EnforcerSummonFailure.FeatureDisabled;
            return false;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer() || ZNetScene.instance == null)
        {
            failure = EnforcerSummonFailure.ServerUnavailable;
            return false;
        }

        if (!TryFindConnectedAlivePlayer(killerId, out ConnectedPlayerContext player))
        {
            failure = EnforcerSummonFailure.KillerUnavailable;
            return false;
        }

        if (GetKarmaRealm(player.Position) != triggerRealm)
        {
            failure = EnforcerSummonFailure.KillerChangedRealm;
            return false;
        }

        if (IsLikelyDungeonPosition(player.Position))
        {
            DungeonComponentPositionCache.Clear();
        }

        bool ignoreCooldown =
            CreatureManagerPlugin.BlockOmenEnforcerDuringCooldown?.Value == CreatureManagerPlugin.Toggle.Off;

        return TrySummonForPlayer(
            player,
            Time.time,
            out failure,
            ignoreCooldown: ignoreCooldown,
            ignoreChance: true,
            ignoreRequiredKarma: true,
            excludedCharacterId: excludedDeadId);
    }

    private static bool TryFindConnectedAlivePlayer(
        ZDOID playerId,
        out ConnectedPlayerContext player)
    {
        player = null!;
        if (playerId == ZDOID.None)
        {
            return false;
        }

        foreach (ConnectedPlayerContext candidate in GetConnectedAlivePlayerContexts())
        {
            if (candidate.CharacterId == playerId)
            {
                player = candidate;
                return true;
            }
        }

        return false;
    }

    internal static int GetLevelBonus(Character character)
    {
        if (!IsKarmaSystemEnabled() || character == null || character.IsPlayer())
        {
            return 0;
        }

        if (TryGetZdo(character, out ZDO characterZdo))
        {
            TrackPotentialBlockerZdo(characterZdo, character.IsBoss());
        }

        float karma = GetKarma(character.transform.position);
        int bonus = IsKarmaLevelEnabled() ? GetSectorLevelBonus(karma) : 0;
        if (IsEnforcer(character))
        {
            bonus += GetStoredEnforcerLevelBonus(character);
        }

        return Mathf.Max(0, bonus);
    }

    internal static void ObservePotentialBlocker(Character character)
    {
        if (!IsKarmaSystemEnabled() ||
            character == null ||
            character.IsPlayer() ||
            !TryGetZdo(character, out ZDO zdo))
        {
            return;
        }

        bool isEnforcer = zdo.GetBool(EnforcerKey, false);
        bool isBoss = character.IsBoss();
        if (!isEnforcer && !isBoss)
        {
            return;
        }

        if (ZNet.instance == null || ZNet.instance.IsServer())
        {
            TrackPotentialBlockerZdo(zdo, isBoss);
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            ReportedBlockerZdoIds.Contains(zdo.m_uid))
        {
            return;
        }

        RegisterRpcs();
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        try
        {
            ZPackage package = new();
            package.Write(zdo.m_uid);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.instance.GetServerPeerID(),
                BlockerObservationRpc,
                package);
            ReportedBlockerZdoIds.Add(zdo.m_uid);
        }
        catch
        {
            // Character/Humanoid Start can retry while this instance remains available.
        }
    }

    private static void RPC_BlockerObservation(long sender, ZPackage package)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            !IsKarmaSystemEnabled() ||
            ZDOMan.instance == null ||
            ZNetScene.instance == null)
        {
            return;
        }

        try
        {
            ZDOID characterId = package.ReadZDOID();
            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            ZDO zdo = characterId != ZDOID.None ? ZDOMan.instance.GetZDO(characterId) : null!;
            GameObject? prefab = zdo != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
            Character? prefabCharacter = prefab != null ? prefab.GetComponent<Character>() : null;
            if (peer == null ||
                !peer.IsReady() ||
                zdo == null ||
                zdo.GetOwner() != sender ||
                prefabCharacter == null ||
                prefabCharacter.IsPlayer())
            {
                return;
            }

            bool isEnforcer = zdo.GetBool(EnforcerKey, false);
            bool isBoss = prefabCharacter.IsBoss();
            if (isEnforcer || isBoss)
            {
                TrackPotentialBlockerZdo(zdo, isBoss);
            }
        }
        catch
        {
            // Ignore malformed or stale one-way observations.
        }
    }

    internal static void TrackPotentialBlockerZdo(ZDO zdo, bool isBoss)
    {
        if (zdo == null || zdo.m_uid.IsNone())
        {
            return;
        }

        if (zdo.GetBool(EnforcerKey, false))
        {
            TrackedEnforcerZdoIds.Add(zdo.m_uid);
            TrackedBossZdoIds.Remove(zdo.m_uid);
        }
        else if (isBoss)
        {
            TrackedBossZdoIds.Add(zdo.m_uid);
        }
    }

    internal static int GetAuthoritativeLevelBonus(ZDO zdo)
    {
        if (!IsKarmaSystemEnabled() || zdo == null)
        {
            return 0;
        }

        float karma = GetKarma(zdo.GetPosition());
        int bonus = IsKarmaLevelEnabled() ? GetSectorLevelBonus(karma) : 0;
        if (zdo.GetBool(EnforcerKey, false))
        {
            bonus += Mathf.Max(0, zdo.GetInt(EnforcerLevelBonusKey, Settings.Enforcer.LevelBonus));
        }

        return Mathf.Max(0, bonus);
    }

    private static int GetStoredEnforcerLevelBonus(Character character)
    {
        if (TryGetRuntimeEnforcerSettings(character, out ResolvedEnforcerSettings runtimeSettings))
        {
            return Mathf.Max(0, runtimeSettings.LevelBonus);
        }

        if (TryGetZdo(character, out ZDO zdo))
        {
            return Mathf.Max(0, zdo.GetInt(EnforcerLevelBonusKey, Settings.Enforcer.LevelBonus));
        }

        return Mathf.Max(0, Settings.Enforcer.LevelBonus);
    }

    internal static bool TryGetEnforcerModifierDefinitions(
        Character character,
        out Dictionary<string, ModifierDefinition> modifiers,
        out bool fallbackBlocked)
    {
        modifiers = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
        fallbackBlocked = false;
        if (!IsKarmaSystemEnabled() || !IsEnforcer(character))
        {
            return false;
        }

        if (TryGetRuntimeEnforcerSettings(character, out ResolvedEnforcerSettings runtimeSettings))
        {
            modifiers = runtimeSettings.Modifiers;
            fallbackBlocked = runtimeSettings.ModifiersCleared;
            return true;
        }

        modifiers = Settings.Enforcer.Modifiers;
        fallbackBlocked = Settings.Enforcer.ModifiersCleared;
        return true;
    }

    internal static bool TryGetDisplayName(Character character, out string displayName)
    {
        displayName = "";
        if (character == null)
        {
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        displayName = CreatureLocalization.LocalizeText(nview.GetZDO()?.GetString(EnforcerNameKey, "") ?? "");
        return displayName.Length > 0;
    }

    internal static string GetDebugLine(Vector3 position)
    {
        SectorState state = GetBestState(position, out string key);
        int bonus = IsKarmaLevelEnabled() ? GetSectorLevelBonus(state.Karma) : 0;
        string realm = GetKarmaRealm(position) == KarmaRealm.Dungeon ? "dungeon" : "outdoor";
        return $"Karma realm={realm} zone={key} neighborhood=3x3 karma={state.Karma:0.#} bonus={bonus} activeEnforcers={GetActiveEnforcerCountInSector(position)}/{GetMaximumEnforcersPerSector()} enforcerCooldown={GetRemainingEnforcerCooldown(position):0}s";
    }

    internal static string GetMinimapStatus(Vector3 position)
    {
        if (!IsKarmaSystemEnabled())
        {
            return "";
        }

        bool karmaLevelEnabled = IsKarmaLevelEnabled();
        bool showValue = CreatureManagerPlugin.ShowKarmaValueOnMinimap?.Value == CreatureManagerPlugin.Toggle.On;
        if (!karmaLevelEnabled && !showValue)
        {
            return "";
        }

        float karma;
        int bonus;
        if (ZNet.instance != null && !ZNet.instance.IsServer())
        {
            KarmaRealm localRealm = GetKarmaRealm(position);
            if (ClientKarmaStatusValid && ClientKarmaStatusRealm != localRealm)
            {
                ClientKarmaStatusValid = false;
                NextKarmaStatusRequestTime = 0f;
            }

            RequestKarmaStatus();
            if (!ClientKarmaStatusValid || ClientKarmaStatusRealm != localRealm)
            {
                return "";
            }

            karma = ClientKarmaStatusValue;
            bonus = ClientKarmaStatusLevel;
        }
        else
        {
            karma = GetKarma(position);
            bonus = GetSectorLevelBonus(karma);
        }
        int displayedKarma = Mathf.FloorToInt(Mathf.Max(0f, karma));
        if (!karmaLevelEnabled)
        {
            return CreatureLocalization.Format(
                "cm_karma_value",
                $"Karma ({displayedKarma})",
                ("karma", displayedKarma.ToString(CultureInfo.InvariantCulture)));
        }

        if (showValue)
        {
            return CreatureLocalization.Format(
                "cm_karma_level_value",
                $"Karma Lv. {bonus} ({displayedKarma})",
                ("level", bonus.ToString(CultureInfo.InvariantCulture)),
                ("karma", displayedKarma.ToString(CultureInfo.InvariantCulture)));
        }

        return CreatureLocalization.Format(
            "cm_karma_level",
            $"Karma Lv. {bonus}",
            ("level", bonus.ToString(CultureInfo.InvariantCulture)));
    }

    internal static void SetDebugKarma(Vector3 position, float value)
    {
        float now = Time.time;
        lock (Sync)
        {
            string[] keys = GetSectorKeys(position).ToArray();
            if (!TryEnsureSectorStatesUnsafe(keys))
            {
                return;
            }

            foreach (string key in keys)
            {
                SectorState state = Sectors[key];
                state.Karma = Mathf.Max(0f, value);
                state.LastKarmaTime = now;
            }
        }
    }

    internal static bool IsEnforcer(Character character)
    {
        if (TryGetRuntimeEnforcerSettings(character, out _))
        {
            return true;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        return nview.GetZDO()?.GetBool(EnforcerKey, false) == true;
    }

    internal static bool IsKarmaSummonedCreature(Character character)
    {
        if (character == null)
        {
            return false;
        }

        return (TryGetZdo(character, out ZDO zdo) && zdo.GetBool(EnforcerSummonedKey, false)) ||
               IsRuntimeSummonedCreature(character);
    }

    internal static KarmaAddResult TryAddBlamerKarma(Vector3 position, float amount)
    {
        if (!IsKarmaSystemEnabled() || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return KarmaAddResult.Unavailable;
        }

        if (ZNet.instance != null && !ZNet.instance.IsServer())
        {
            return KarmaAddResult.Unavailable;
        }

        return AddKarma(position, amount);
    }

    internal static bool IsBossHudOnly(Character character)
    {
        if (character == null)
        {
            return false;
        }

        if (TryGetRuntimeEnforcerSettings(character, out ResolvedEnforcerSettings settings))
        {
            return settings.BossHud && !settings.IsBoss;
        }

        return TryGetZdo(character, out ZDO zdo) &&
               zdo.GetBool(EnforcerBossHudKey, false) &&
               !zdo.GetBool(EnforcerIsBossKey, character.m_boss);
    }

    private static bool IsRuntimeSummonedCreature(Character character)
    {
        if (character == null || !RuntimeSummonedCreatureIds.Contains(character.GetInstanceID()))
        {
            return false;
        }

        TryStoreRuntimeSummonedZdo(character);
        return true;
    }

    private static bool TryGetRuntimeEnforcerSettings(Character character, out ResolvedEnforcerSettings settings)
    {
        settings = null!;
        if (character == null || !RuntimeEnforcerSettings.TryGetValue(character.GetInstanceID(), out settings))
        {
            return false;
        }

        TryStoreRuntimeEnforcerZdo(character, settings);
        return true;
    }

    private static void MarkRuntimeSummonedCreature(Character character)
    {
        if (character == null)
        {
            return;
        }

        RuntimeSummonedCreatureIds.Add(character.GetInstanceID());
        TryStoreRuntimeSummonedZdo(character);
    }

    private static void MarkRuntimeEnforcer(
        Character character,
        ResolvedEnforcerSettings settings,
        IReadOnlyList<EnforcerLootDefinition>? loot)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        RuntimeEnforcerSettings[id] = settings.Clone();
        RuntimeEnforcerLoot[id] = loot?.Select(CloneEnforcerLoot).ToList() ?? new List<EnforcerLootDefinition>();
        TryStoreRuntimeEnforcerZdo(character, settings);
    }

    private static void TryStoreRuntimeSummonedZdo(Character character)
    {
        if (TryGetZdo(character, out ZDO zdo))
        {
            zdo.Set(EnforcerSummonedKey, true);
        }
    }

    private static void TryStoreRuntimeEnforcerZdo(Character character, ResolvedEnforcerSettings settings)
    {
        if (!TryGetZdo(character, out ZDO zdo))
        {
            return;
        }

        TrackedEnforcerZdoIds.Add(zdo.m_uid);
        TrackedBossZdoIds.Remove(zdo.m_uid);
        zdo.Set(EnforcerKey, true);
        zdo.Set(EnforcerSummonedKey, true);
        zdo.Set(EnforcerLevelBonusKey, Mathf.Max(0, settings.LevelBonus));
        zdo.Set(EnforcerIsBossKey, settings.IsBoss);
        zdo.Set(EnforcerBossHudKey, settings.BossHud);
        StoreEnforcerPresenceAnchor(zdo);

        if (RuntimeEnforcerLoot.TryGetValue(character.GetInstanceID(), out List<EnforcerLootDefinition> loot) &&
            loot.Count > 0 &&
            string.IsNullOrEmpty(zdo.GetString(EnforcerLootKey, "")))
        {
            StoreEnforcerLoot(zdo, loot);
        }

        if (!string.IsNullOrWhiteSpace(character.m_name) && string.IsNullOrWhiteSpace(zdo.GetString(EnforcerNameKey, "")))
        {
            zdo.Set(EnforcerNameKey, character.m_name);
        }
    }

    private static void StoreEnforcerPresenceAnchor(ZDO zdo)
    {
        if (zdo.GetBool(EnforcerPresenceAnchorStoredKey, false))
        {
            return;
        }

        Vector3 position = zdo.GetPosition();
        bool interior = Character.InInterior(position);
        zdo.Set(EnforcerPresenceInteriorKey, interior);
        if (interior)
        {
            Vector2i zone = ZoneSystem.GetZone(position);
            zdo.Set(EnforcerPresenceZoneXKey, zone.x);
            zdo.Set(EnforcerPresenceZoneYKey, zone.y);
        }

        zdo.Set(EnforcerPresenceAnchorStoredKey, true);
    }

    private static bool TryGetZdo(Character character, out ZDO zdo)
    {
        zdo = null!;
        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        zdo = nview.GetZDO();
        return zdo != null;
    }

    private static bool TrySummonForPlayer(
        ConnectedPlayerContext player,
        float now,
        out EnforcerSummonFailure failure,
        bool ignoreCooldown = false,
        bool ignoreChance = false,
        bool ignoreRequiredKarma = false,
        Vector3? regionPosition = null,
        HashSet<string>? regionZoneKeys = null,
        ZDOID excludedCharacterId = default)
    {
        failure = EnforcerSummonFailure.None;
        if (player == null ||
            player.CharacterId.IsNone() ||
            !IsFinite(player.Position))
        {
            failure = EnforcerSummonFailure.KillerUnavailable;
            return false;
        }

        Vector3 playerPosition = player.Position;
        Vector3 statePosition = regionPosition ?? playerPosition;
        Heightmap.Biome biome = GetBiome(playerPosition);
        bool dungeonSummon = IsLikelyDungeonPosition(playerPosition);
        if (!TryGetEnforcerBiomeDefinition(biome, out EnforcerBiomeDefinition biomeDefinition))
        {
            failure = EnforcerSummonFailure.BiomeNotConfigured;
            return false;
        }

        string dungeonLocation = dungeonSummon && TryGetDungeonLocationPrefabName(playerPosition, out string resolvedDungeonLocation)
            ? resolvedDungeonLocation
            : "";
        List<EnforcerCandidateDefinition> candidates = biomeDefinition.GetCandidates(dungeonSummon, dungeonLocation);
        if (!biomeDefinition.Enabled)
        {
            failure = EnforcerSummonFailure.BiomeDisabled;
            return false;
        }

        if (candidates.Count == 0)
        {
            failure = EnforcerSummonFailure.NoCandidates;
            return false;
        }

        ResolvedEnforcerSettings biomeSettings = ResolvedEnforcerSettings.FromGlobal(Settings.Enforcer);
        EnforcerSummonFailure blockerFailure = GetEnforcerBlockerFailure(
            statePosition,
            regionZoneKeys,
            excludedCharacterId);
        if (blockerFailure != EnforcerSummonFailure.None)
        {
            failure = blockerFailure;
            return false;
        }

        string sectorKey;
        float karma;
        lock (Sync)
        {
            SectorState state = GetBestStateUnsafe(statePosition, out sectorKey);
            karma = state.Karma;

            if (!ignoreCooldown && GetRemainingEnforcerCooldownUnsafe(statePosition, now, biomeSettings, regionZoneKeys) > 0f)
            {
                failure = EnforcerSummonFailure.Cooldown;
                return false;
            }
        }

        if (!ignoreChance && UnityEngine.Random.Range(0f, 100f) >= Mathf.Clamp(biomeSettings.Chance, 0f, 100f))
        {
            failure = EnforcerSummonFailure.ChanceRollFailed;
            return false;
        }

        if (!TrySelectEnforcerCandidate(candidates, biomeSettings, karma, ignoreRequiredKarma, out EnforcerCandidateDefinition candidate, out ResolvedEnforcerSettings resolvedSettings))
        {
            failure = EnforcerSummonFailure.NoEligibleCandidate;
            return false;
        }

        EnforcerSummonSet summon = candidate.Summon;
        if (summon.Boss.Length == 0)
        {
            failure = EnforcerSummonFailure.InvalidCandidate;
            return false;
        }

        if (!TryGetCreaturePrefab(summon.Boss, out GameObject bossPrefab))
        {
            failure = EnforcerSummonFailure.SpawnFailed;
            return false;
        }

        if (!TryFindSummonPosition(bossPrefab, playerPosition, resolvedSettings, out Vector3 spawnPosition))
        {
            failure = EnforcerSummonFailure.NoSpawnPosition;
            CreatureManagerPlugin.Log.LogDebug($"Karma Enforcer summon skipped: no spawn position near {playerPosition}.");
            return false;
        }

        lock (Sync)
        {
            if (!TryEnsureSectorStatesUnsafe(GetSectorKeys(statePosition).ToArray()))
            {
                failure = EnforcerSummonFailure.SectorStateCapacity;
                return false;
            }
        }

        if (!TrySpawnCreature(summon.Boss, bossPrefab, spawnPosition, playerPosition, markEnforcer: true, EnforcerNameSuffix, resolvedSettings, candidate.Loot, out Character boss))
        {
            failure = EnforcerSummonFailure.SpawnFailed;
            return false;
        }

        foreach (EnforcerMinionDefinition minion in summon.Minions)
        {
            if (!TryGetCreaturePrefab(minion.Prefab, out GameObject minionPrefab))
            {
                continue;
            }

            int skippedMinions = 0;
            for (int i = 0; i < minion.Count; i++)
            {
                Vector3 minionPosition;
                if (dungeonSummon)
                {
                    if (!TryFindDungeonMinionPosition(
                            minionPrefab,
                            boss,
                            playerPosition,
                            out minionPosition))
                    {
                        skippedMinions++;
                        continue;
                    }
                }
                else
                {
                    Vector2 offset = GetRandomHorizontalOffset(0f, 3f);
                    minionPosition = spawnPosition + new Vector3(offset.x, 0f, offset.y);
                }

                TrySpawnCreature(
                    minion.Prefab,
                    minionPrefab,
                    minionPosition,
                    playerPosition,
                    markEnforcer: false,
                    EnforcerMinionSuffix,
                    resolvedSettings,
                    null,
                    out _);
            }

            if (skippedMinions > 0)
            {
                CreatureManagerPlugin.Log.LogDebug(
                    $"Karma Enforcer minion '{minion.Prefab}' skipped {skippedMinions}/{minion.Count}: " +
                    "no safe dungeon spawn position.");
            }
        }

        float remainingKarma;
        lock (Sync)
        {
            remainingKarma = ApplyEnforcerCostUnsafe(statePosition, now, resolvedSettings);
        }

        CreatureManagerPlugin.Log.LogInfo($"Karma Enforcer summoned: {GetPrefabName(boss)} zone={sectorKey} karma={karma:0.#}->{remainingKarma:0.#} forced={ignoreCooldown || ignoreChance || ignoreRequiredKarma}");
        BroadcastRegionalCenterQuote(EnforcerSpawnQuotes, statePosition, regionZoneKeys);

        return true;
    }

    private static Vector2 GetRandomHorizontalOffset(float minRadius, float maxRadius)
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        float radius = UnityEngine.Random.Range(
            Mathf.Max(0f, minRadius),
            Mathf.Max(minRadius, maxRadius));
        return direction * radius;
    }

    private static ResolvedEnforcerSettings ResolveEnforcerSettings(
        EnforcerOverrideSettings? candidateOverride,
        ResolvedEnforcerSettings baseline)
    {
        ResolvedEnforcerSettings settings = baseline.Clone();
        ApplyCandidateOverride(settings, candidateOverride);
        return settings;
    }

    private static void ApplyCandidateOverride(ResolvedEnforcerSettings settings, EnforcerOverrideSettings? overrides)
    {
        if (overrides == null)
        {
            return;
        }

        if (overrides.RequiredKarma.HasValue) settings.RequiredKarma = Mathf.Max(0f, overrides.RequiredKarma.Value);
        if (overrides.ConsumeKarma.HasValue) settings.ConsumeKarma = Mathf.Max(0f, overrides.ConsumeKarma.Value);
        if (overrides.LevelBonus.HasValue) settings.LevelBonus = Mathf.Max(0, overrides.LevelBonus.Value);
        if (overrides.ModifiersCleared)
        {
            settings.Modifiers.Clear();
            settings.ModifiersCleared = true;
        }

        if (overrides.Modifiers != null)
        {
            MergeModifierOverrides(settings.Modifiers, overrides.Modifiers);
        }
    }

    private static bool TrySelectEnforcerCandidate(
        List<EnforcerCandidateDefinition> candidates,
        ResolvedEnforcerSettings biomeSettings,
        float karma,
        bool ignoreRequiredKarma,
        out EnforcerCandidateDefinition selected,
        out ResolvedEnforcerSettings resolvedSettings)
    {
        selected = new EnforcerCandidateDefinition();
        resolvedSettings = biomeSettings;
        List<(EnforcerCandidateDefinition Candidate, ResolvedEnforcerSettings Settings)> eligible = new();
        foreach (EnforcerCandidateDefinition candidate in candidates)
        {
            if (candidate.Weight <= 0f || candidate.Summon.Boss.Length == 0)
            {
                continue;
            }

            ResolvedEnforcerSettings candidateSettings = ResolveEnforcerSettings(candidate.Override, biomeSettings);
            if (ignoreRequiredKarma || karma >= candidateSettings.RequiredKarma)
            {
                eligible.Add((candidate, candidateSettings));
            }
        }

        if (eligible.Count == 0)
        {
            return false;
        }

        float totalWeight = eligible.Sum(entry => Mathf.Max(0f, entry.Candidate.Weight));
        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        foreach ((EnforcerCandidateDefinition candidate, ResolvedEnforcerSettings settings) in eligible)
        {
            roll -= Mathf.Max(0f, candidate.Weight);
            if (roll <= 0f)
            {
                selected = candidate;
                resolvedSettings = settings;
                return true;
            }
        }

        (selected, resolvedSettings) = eligible[eligible.Count - 1];
        return true;
    }

    private static bool TryGetEnforcerBiomeDefinition(Heightmap.Biome biome, out EnforcerBiomeDefinition summon)
    {
        foreach (string key in GetBiomeLookupKeys(biome))
        {
            if (Settings.Enforcer.Biomes.TryGetValue(key, out summon))
            {
                return true;
            }
        }

        foreach (KeyValuePair<string, EnforcerBiomeDefinition> entry in Settings.Enforcer.Biomes)
        {
            if (IsGlobalBiomeKey(entry.Key))
            {
                continue;
            }

            if (TryResolveBiomeName(entry.Key, out Heightmap.Biome configuredBiome) &&
                (biome & configuredBiome) != 0)
            {
                summon = entry.Value;
                return true;
            }
        }

        if (Settings.Enforcer.Biomes.TryGetValue("global", out summon))
        {
            return true;
        }

        summon = new EnforcerBiomeDefinition();
        return false;
    }

    private static IEnumerable<string> GetBiomeLookupKeys(Heightmap.Biome biome)
    {
        yield return NormalizeBiomeName(biome.ToString());
        yield return NormalizeBiomeName(((int)biome).ToString(CultureInfo.InvariantCulture));
        if (TryGetBiomeDisplayName(biome, out string displayName))
        {
            yield return NormalizeBiomeName(displayName);
            yield return displayName.Trim();
        }
    }

    private static bool IsGlobalBiomeKey(string key)
    {
        return string.Equals(key, "global", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetActiveEnforcerCountInSector(Vector3 position)
    {
        GetEnforcerBlockerState(position, out int activeEnforcers, out _);
        return activeEnforcers;
    }

    private static void GetEnforcerBlockerState(
        Vector3 position,
        out int activeEnforcers,
        out bool hasNonEnforcerBoss,
        Character? excludedCharacter = null,
        HashSet<string>? regionZoneKeys = null,
        ZDOID excludedCharacterId = default)
    {
        activeEnforcers = 0;
        hasNonEnforcerBoss = false;
        Vector2i centerZone = ZoneSystem.GetZone(position);
        KarmaRealm centerRealm = GetKarmaRealm(position);
        HashSet<ZDOID> observedCharacterIds = new();
        foreach (Character character in Character.GetAllCharacters())
        {
            if (character == null ||
                ReferenceEquals(character, excludedCharacter) ||
                (excludedCharacterId != ZDOID.None && character.GetZDOID() == excludedCharacterId) ||
                character.IsDead())
            {
                continue;
            }

            ZDOID characterId = character.GetZDOID();
            if (!characterId.IsNone())
            {
                observedCharacterIds.Add(characterId);
            }

            bool enforcer = IsEnforcer(character);
            bool nonEnforcerBoss = !enforcer && character.IsBoss();
            if (enforcer && !characterId.IsNone())
            {
                TrackedEnforcerZdoIds.Add(characterId);
                TrackedBossZdoIds.Remove(characterId);
            }
            else if (nonEnforcerBoss && !characterId.IsNone())
            {
                TrackedBossZdoIds.Add(characterId);
            }

            if (!IsInEnforcerCheckRegion(character.transform.position, centerZone, centerRealm, regionZoneKeys))
            {
                continue;
            }

            if (enforcer)
            {
                activeEnforcers++;
            }
            else if (nonEnforcerBoss)
            {
                hasNonEnforcerBoss = true;
            }
        }

        CountTrackedBlockerZdos(
            centerZone,
            centerRealm,
            regionZoneKeys,
            excludedCharacterId,
            observedCharacterIds,
            ref activeEnforcers,
            ref hasNonEnforcerBoss);
    }

    private static void CountTrackedBlockerZdos(
        Vector2i centerZone,
        KarmaRealm centerRealm,
        HashSet<string>? regionZoneKeys,
        ZDOID excludedCharacterId,
        HashSet<ZDOID> observedCharacterIds,
        ref int activeEnforcers,
        ref bool hasNonEnforcerBoss)
    {
        if (ZDOMan.instance == null)
        {
            return;
        }

        foreach (ZDOID trackedId in TrackedEnforcerZdoIds.ToList())
        {
            if (trackedId == excludedCharacterId || observedCharacterIds.Contains(trackedId))
            {
                continue;
            }

            ZDO trackedZdo = ZDOMan.instance.GetZDO(trackedId);
            if (!IsTrackedCharacterZdoAlive(trackedZdo) ||
                !trackedZdo.GetBool(EnforcerKey, false))
            {
                TrackedEnforcerZdoIds.Remove(trackedId);
                EnforcerNoPlayerSince.Remove(trackedId);
                continue;
            }

            Vector3 trackedPosition = trackedZdo.GetPosition();
            if (IsFinite(trackedPosition) &&
                IsInEnforcerCheckRegion(trackedPosition, centerZone, centerRealm, regionZoneKeys))
            {
                activeEnforcers++;
            }
        }

        if ((!ShouldBlockEnforcerWhileBossActive() && !ShouldBlockKarmaGainWhileBossActive()) ||
            hasNonEnforcerBoss)
        {
            return;
        }

        foreach (ZDOID trackedId in TrackedBossZdoIds.ToList())
        {
            if (trackedId == excludedCharacterId || observedCharacterIds.Contains(trackedId))
            {
                continue;
            }

            ZDO trackedZdo = ZDOMan.instance.GetZDO(trackedId);
            if (!IsTrackedCharacterZdoAlive(trackedZdo))
            {
                TrackedBossZdoIds.Remove(trackedId);
                continue;
            }

            Vector3 trackedPosition = trackedZdo.GetPosition();
            if (IsFinite(trackedPosition) &&
                IsInEnforcerCheckRegion(trackedPosition, centerZone, centerRealm, regionZoneKeys))
            {
                hasNonEnforcerBoss = true;
                return;
            }
        }
    }

    private static bool IsTrackedCharacterZdoAlive(ZDO zdo)
    {
        if (zdo == null || zdo.GetBool(ZDOVars.s_dead, false))
        {
            return false;
        }

        float health = zdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity);
        return !float.IsNaN(health) &&
               (float.IsInfinity(health) || health > 0f);
    }

    private static bool IsInKarmaNeighborhood(
        Vector3 position,
        Vector2i centerZone,
        KarmaRealm centerRealm)
    {
        if (GetKarmaRealm(position) != centerRealm)
        {
            return false;
        }

        Vector2i zone = ZoneSystem.GetZone(position);
        return Math.Abs(zone.x - centerZone.x) <= ZoneRadius &&
               Math.Abs(zone.y - centerZone.y) <= ZoneRadius;
    }

    private static bool IsInEnforcerCheckRegion(
        Vector3 position,
        Vector2i centerZone,
        KarmaRealm centerRealm,
        HashSet<string>? regionZoneKeys)
    {
        return regionZoneKeys != null
            ? regionZoneKeys.Contains(GetSectorKey(position))
            : IsInKarmaNeighborhood(position, centerZone, centerRealm);
    }

    private static EnforcerSummonFailure GetEnforcerBlockerFailure(
        Vector3 position,
        HashSet<string>? regionZoneKeys,
        ZDOID excludedCharacterId)
    {
        GetEnforcerBlockerState(
            position,
            out int activeEnforcers,
            out bool hasNonEnforcerBoss,
            regionZoneKeys: regionZoneKeys,
            excludedCharacterId: excludedCharacterId);
        if (activeEnforcers >= GetMaximumEnforcersPerSector())
        {
            return EnforcerSummonFailure.ActiveEnforcerCap;
        }

        return ShouldBlockEnforcerWhileBossActive() && hasNonEnforcerBoss
            ? EnforcerSummonFailure.ActiveBoss
            : EnforcerSummonFailure.None;
    }

    private static bool TryFindSummonPosition(
        GameObject prefab,
        Vector3 playerPosition,
        ResolvedEnforcerSettings settings,
        out Vector3 position)
    {
        if (IsLikelyDungeonPosition(playerPosition))
        {
            if (ZoneSystem.instance == null || !ZoneSystem.instance.IsZoneLoaded(playerPosition))
            {
                return TryFindDungeonZdoAnchorPosition(
                    playerPosition,
                    Settings.Enforcer.DungeonSpawnerSearchRadius,
                    out position);
            }

            if (TryFindValidDungeonComponentPosition<CreatureSpawner>(
                    prefab,
                    playerPosition,
                    Settings.Enforcer.DungeonSpawnerSearchRadius,
                    out position))
            {
                return true;
            }

            if (TryFindValidDungeonComponentPosition<SpawnArea>(
                    prefab,
                    playerPosition,
                    Settings.Enforcer.DungeonSpawnerSearchRadius,
                    out position))
            {
                return true;
            }

            for (int attempt = 0; attempt < DungeonBossRandomPositionAttempts; attempt++)
            {
                Vector2 offset = GetRandomHorizontalOffset(
                    DungeonBossRandomRadiusMin,
                    DungeonBossRandomRadiusMax);
                Vector3 dungeonCandidate = playerPosition + new Vector3(offset.x, 0f, offset.y);
                if (TryValidateDungeonSpawnPosition(
                        prefab,
                        playerPosition,
                        playerPosition,
                        dungeonCandidate,
                        out position))
                {
                    return true;
                }
            }

            position = playerPosition;
            return false;
        }

        float minRadius = Mathf.Max(2f, settings.SpawnRadiusMin);
        float maxRadius = Mathf.Max(minRadius, settings.SpawnRadiusMax);
        Vector2 outdoorOffset = GetRandomHorizontalOffset(minRadius, maxRadius);
        Vector3 candidate = playerPosition + new Vector3(outdoorOffset.x, 0f, outdoorOffset.y);
        float groundHeight = candidate.y;
        if (ZoneSystem.instance != null)
        {
            ZoneSystem.instance.GetGroundHeight(candidate + Vector3.up * 100f, out groundHeight);
            candidate.y = groundHeight + 0.5f;
        }
        else if (WorldGenerator.instance != null)
        {
            candidate.y = WorldGenerator.instance.GetHeight(candidate.x, candidate.z) + 0.5f;
        }

        position = candidate;
        return true;
    }

    private static bool TryFindDungeonZdoAnchorPosition(
        Vector3 origin,
        float radius,
        out Vector3 position)
    {
        position = origin;
        if (ZDOMan.instance == null || ZNetScene.instance == null)
        {
            return false;
        }

        float searchRadius = Mathf.Max(0f, radius);
        float searchRadiusSquared = searchRadius * searchRadius;
        float bestDistanceSquared = float.PositiveInfinity;
        bool found = false;
        DungeonSpawnZdoBuffer.Clear();
        try
        {
            Vector2i originZone = ZoneSystem.GetZone(origin);
            ZDOMan.instance.FindSectorObjects(originZone, 1, 0, DungeonSpawnZdoBuffer);
            foreach (ZDO zdo in DungeonSpawnZdoBuffer)
            {
                if (zdo == null)
                {
                    continue;
                }

                Vector3 candidate = zdo.GetPosition();
                float deltaX = candidate.x - origin.x;
                float deltaZ = candidate.z - origin.z;
                float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
                if (!IsFinite(candidate) ||
                    !IsLikelyDungeonPosition(candidate) ||
                    Mathf.Abs(candidate.y - origin.y) > DungeonComponentVerticalTolerance ||
                    distanceSquared > searchRadiusSquared ||
                    distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                GameObject? anchorPrefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (anchorPrefab == null ||
                    (anchorPrefab.GetComponent<CreatureSpawner>() == null &&
                     anchorPrefab.GetComponent<SpawnArea>() == null))
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                position = candidate;
                found = true;
            }

            return found;
        }
        catch (Exception exception)
        {
            CreatureManagerPlugin.Log.LogDebug(
                $"Dungeon Enforcer ZDO anchor lookup failed near {origin}: {exception.Message}");
            position = origin;
            return false;
        }
        finally
        {
            DungeonSpawnZdoBuffer.Clear();
        }
    }

    private static bool TryFindValidDungeonComponentPosition<T>(
        GameObject prefab,
        Vector3 origin,
        float radius,
        out Vector3 position)
        where T : Component
    {
        Vector2i originZone = ZoneSystem.GetZone(origin);
        List<Vector3> candidates = GetCachedComponentPositions<T>(originZone);
        float searchRadius = Mathf.Max(0f, radius);
        foreach (Vector3 candidate in candidates
                     .Where(candidate =>
                         IsLikelyDungeonPosition(candidate) &&
                         Utils.DistanceXZ(candidate, origin) <= searchRadius &&
                         Mathf.Abs(candidate.y - origin.y) <= DungeonComponentVerticalTolerance)
                     .OrderBy(candidate => (candidate - origin).sqrMagnitude)
                     .Take(DungeonComponentPositionAttempts))
        {
            if (TryValidateDungeonSpawnPosition(
                    prefab,
                    origin,
                    origin,
                    candidate,
                    out position))
            {
                return true;
            }
        }

        position = origin;
        return false;
    }

    private static List<Vector3> GetCachedComponentPositions<T>(Vector2i zone) where T : Component
    {
        string key = $"{typeof(T).FullName}:{zone.x},{zone.y}";
        if (DungeonComponentPositionCache.TryGetValue(key, out List<Vector3> positions))
        {
            return positions;
        }

        positions = new List<Vector3>();
        foreach (T component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (component != null && component.gameObject.activeInHierarchy && IsSameZone(component.transform.position, zone))
            {
                positions.Add(component.transform.position);
            }
        }

        DungeonComponentPositionCache[key] = positions;
        return positions;
    }

    private static bool IsSameZone(Vector3 position, Vector2i zone)
    {
        Vector2i other = ZoneSystem.GetZone(position);
        return other.x == zone.x && other.y == zone.y;
    }

    private static bool TryFindDungeonMinionPosition(
        GameObject prefab,
        Character boss,
        Vector3 targetPosition,
        out Vector3 position)
    {
        Vector3 bossPosition = boss.transform.position;
        float minionRadius = GetPrefabCapsuleRadius(prefab);
        float minRadius = Mathf.Max(1f, boss.GetRadius() + minionRadius + 0.25f);
        float maxRadius = minRadius + 1f;
        for (int attempt = 0; attempt < DungeonMinionPositionAttempts; attempt++)
        {
            Vector2 offset = GetRandomHorizontalOffset(minRadius, maxRadius);
            Vector3 candidate = bossPosition + new Vector3(offset.x, 0f, offset.y);
            if (TryValidateDungeonSpawnPosition(
                    prefab,
                    bossPosition,
                    targetPosition,
                    candidate,
                    out position))
            {
                return true;
            }
        }

        position = bossPosition;
        return false;
    }

    private static bool TryValidateDungeonSpawnPosition(
        GameObject prefab,
        Vector3 pathOrigin,
        Vector3 facingTarget,
        Vector3 candidate,
        out Vector3 position)
    {
        position = candidate;
        if (ZoneSystem.instance == null ||
            !IsFinite(pathOrigin) ||
            !IsFinite(candidate) ||
            !IsLikelyDungeonPosition(candidate) ||
            !ZoneSystem.instance.GetSolidHeight(candidate, out float floorHeight, 1) ||
            Mathf.Abs(floorHeight - pathOrigin.y) > DungeonSpawnFloorTolerance)
        {
            return false;
        }

        candidate.y = floorHeight;
        Quaternion candidateRotation = GetSpawnRotation(candidate, facingTarget);
        if (!HasDungeonSpawnClearance(prefab, candidate, candidateRotation))
        {
            return false;
        }

        BaseAI? baseAI = prefab.GetComponent<BaseAI>();
        Pathfinding.AgentType agentType = baseAI != null
            ? baseAI.m_pathAgentType
            : Pathfinding.AgentType.Humanoid;

        try
        {
            Vector3 resolvedPosition = candidate;
            bool hasFullPath = false;
            DungeonSpawnPath.Clear();
            if (Pathfinding.instance != null &&
                Pathfinding.instance.GetPath(
                    pathOrigin,
                    candidate,
                    DungeonSpawnPath,
                    agentType,
                    requireFullPath: true,
                    cleanup: false,
                    havePath: true) &&
                DungeonSpawnPath.Count > 0)
            {
                Vector3 pathStart = DungeonSpawnPath[0];
                Vector3 pathEnd = DungeonSpawnPath[DungeonSpawnPath.Count - 1];
                hasFullPath =
                    Vector3.Distance(pathStart, pathOrigin) <= 2f &&
                    Vector3.Distance(pathEnd, candidate) <= 1.25f &&
                    IsLikelyDungeonPosition(pathEnd) &&
                    Mathf.Abs(pathEnd.y - pathOrigin.y) <= DungeonSpawnFloorTolerance + 1.25f;
                if (hasFullPath)
                {
                    resolvedPosition = pathEnd;
                }
            }

            if (!hasFullPath &&
                Physics.Linecast(
                    pathOrigin + Vector3.up * 0.8f,
                    candidate + Vector3.up * 0.8f,
                    DungeonSpawnStaticMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Quaternion rotation = GetSpawnRotation(resolvedPosition, facingTarget);
            if (!HasDungeonSpawnClearance(prefab, resolvedPosition, rotation))
            {
                return false;
            }

            position = resolvedPosition;
            return true;
        }
        catch (Exception exception)
        {
            CreatureManagerPlugin.Log.LogDebug(
                $"Dungeon summon position validation failed for '{prefab.name}': {exception.Message}");
            return false;
        }
        finally
        {
            DungeonSpawnPath.Clear();
        }
    }

    private static bool HasDungeonSpawnClearance(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        CapsuleCollider? capsule = prefab.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            Vector3 bottom = position + Vector3.up * 0.45f;
            Vector3 top = position + Vector3.up * 1.35f;
            return !Physics.CheckCapsule(
                bottom,
                top,
                0.35f,
                DungeonSpawnCollisionMask,
                QueryTriggerInteraction.Ignore);
        }

        Vector3 scale = prefab.transform.lossyScale;
        int direction = Mathf.Clamp(capsule.direction, 0, 2);
        float axisScale = Mathf.Abs(GetVectorAxis(scale, direction));
        int radiusAxisA = direction == 0 ? 1 : 0;
        int radiusAxisB = direction == 2 ? 1 : 2;
        float radiusScale = Mathf.Max(
            Mathf.Abs(GetVectorAxis(scale, radiusAxisA)),
            Mathf.Abs(GetVectorAxis(scale, radiusAxisB)));
        float worldRadius = Mathf.Max(0.05f, capsule.radius * radiusScale);
        float worldHeight = Mathf.Max(capsule.height * axisScale, worldRadius * 2f);
        float checkRadius = Mathf.Max(0.05f, worldRadius - DungeonSpawnClearanceInset);
        float checkHeight = Mathf.Max(checkRadius * 2f, worldHeight - DungeonSpawnClearanceInset * 2f);
        float segmentHalf = Mathf.Max(0f, checkHeight * 0.5f - checkRadius);
        Vector3 localAxis = direction switch
        {
            0 => Vector3.right,
            2 => Vector3.forward,
            _ => Vector3.up
        };
        Vector3 worldAxis = rotation * localAxis;
        Vector3 center = position +
                         rotation * Vector3.Scale(capsule.center, scale) +
                         Vector3.up * DungeonSpawnClearanceInset;
        Vector3 point0 = center - worldAxis * segmentHalf;
        Vector3 point1 = center + worldAxis * segmentHalf;
        return !Physics.CheckCapsule(
            point0,
            point1,
            checkRadius,
            DungeonSpawnCollisionMask,
            QueryTriggerInteraction.Ignore);
    }

    private static float GetPrefabCapsuleRadius(GameObject prefab)
    {
        CapsuleCollider? capsule = prefab.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            return 0.4f;
        }

        Vector3 scale = prefab.transform.lossyScale;
        int direction = Mathf.Clamp(capsule.direction, 0, 2);
        int radiusAxisA = direction == 0 ? 1 : 0;
        int radiusAxisB = direction == 2 ? 1 : 2;
        return Mathf.Max(
            0.1f,
            capsule.radius * Mathf.Max(
                Mathf.Abs(GetVectorAxis(scale, radiusAxisA)),
                Mathf.Abs(GetVectorAxis(scale, radiusAxisB))));
    }

    private static float GetVectorAxis(Vector3 vector, int axis)
    {
        return axis switch
        {
            0 => vector.x,
            2 => vector.z,
            _ => vector.y
        };
    }

    private static Quaternion GetSpawnRotation(Vector3 position, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(direction.normalized)
            : Quaternion.identity;
    }

    private static bool TryGetCreaturePrefab(string prefabName, out GameObject prefab)
    {
        prefab = null!;
        if (ZNetScene.instance == null)
        {
            return false;
        }

        prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Karma Enforcer summon skipped: missing prefab '{prefabName}'.");
            return false;
        }

        if (prefab.GetComponent<Character>() == null || CreaturePrefabRegistry.IsPlayerPrefab(prefab))
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Karma Enforcer summon skipped: prefab '{prefabName}' is not a supported non-player Character.");
            prefab = null!;
            return false;
        }

        return true;
    }

    private static bool TrySpawnCreature(
        string prefabName,
        GameObject prefab,
        Vector3 position,
        Vector3 targetPosition,
        bool markEnforcer,
        string nameSuffix,
        ResolvedEnforcerSettings settings,
        IReadOnlyList<EnforcerLootDefinition>? loot,
        out Character character)
    {
        character = null!;
        Quaternion rotation = GetSpawnRotation(position, targetPosition);

        GameObject? spawned = null;
        try
        {
            spawned = UnityEngine.Object.Instantiate(prefab, position, rotation);
            character = spawned.GetComponent<Character>();
            if (character == null || character.IsPlayer())
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Karma Enforcer summon failed: instantiated prefab '{prefabName}' is not a supported non-player Character.");
                CleanupFailedSummon(spawned, character);
                character = null!;
                return false;
            }

            MarkRuntimeSummonedCreature(character);

            ZNetView? nview = character.m_nview;
            ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;

            if (markEnforcer)
            {
                MarkSummonedEnforcer(character, zdo, nameSuffix, settings, loot);
            }
            else
            {
                MarkSummonedCreatureName(character, zdo, nameSuffix);
            }

            ApplyHuntPlayer(character);

            CreatureManagerCharacterLifecycle.ApplyLevelAndModifiers(character);
            return true;
        }
        catch (Exception ex)
        {
            CleanupFailedSummon(spawned, character);
            character = null!;
            CreatureManagerPlugin.Log.LogWarning($"Karma Enforcer summon failed for '{prefabName}': {ex.Message}");
            return false;
        }
    }

    private static void CleanupFailedSummon(GameObject? spawned, Character? character)
    {
        if (character != null)
        {
            ForgetCharacter(character);
        }

        if (spawned == null)
        {
            return;
        }

        try
        {
            ZNetView? nview = spawned.GetComponent<ZNetView>();
            if (ZNetScene.instance != null && nview != null && nview.IsValid() && nview.IsOwner())
            {
                ZNetScene.instance.Destroy(spawned);
            }
            else
            {
                UnityEngine.Object.Destroy(spawned);
            }
        }
        catch
        {
            UnityEngine.Object.Destroy(spawned);
        }
    }

    internal static void DropStoredEnforcerLoot(Character character)
    {
        try
        {
            DropStoredEnforcerLootCore(character);
        }
        catch (Exception ex)
        {
            // Loot compatibility must never interrupt Character.OnDeath or vanilla drops.
            CreatureManagerPlugin.Log.LogWarning($"Failed to drop Karma Enforcer loot: {ex.Message}");
        }
    }

    private static void DropStoredEnforcerLootCore(Character character)
    {
        if (character == null || character.IsPlayer())
        {
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null ||
            !zdo.GetBool(EnforcerKey, false) ||
            zdo.GetBool(EnforcerLootDroppedKey, false))
        {
            return;
        }

        string serializedLoot = zdo.GetString(EnforcerLootKey, "");
        if (string.IsNullOrEmpty(serializedLoot) &&
            RuntimeEnforcerLoot.TryGetValue(character.GetInstanceID(), out List<EnforcerLootDefinition> runtimeLoot) &&
            runtimeLoot.Count > 0)
        {
            StoreEnforcerLoot(zdo, runtimeLoot);
            serializedLoot = zdo.GetString(EnforcerLootKey, "");
        }

        if (string.IsNullOrEmpty(serializedLoot))
        {
            return;
        }

        List<EnforcerLootDefinition> rewards = DeserializeEnforcerLoot(serializedLoot);
        if (rewards.Count == 0)
        {
            zdo.Set(EnforcerLootDroppedKey, true);
            return;
        }

        ZNetScene? scene = ZNetScene.instance;
        if (scene == null)
        {
            return;
        }

        List<KeyValuePair<GameObject, int>> drops = new(rewards.Count);
        foreach (EnforcerLootDefinition reward in rewards)
        {
            GameObject itemPrefab = scene.GetPrefab(reward.Prefab);
            if (itemPrefab == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Karma Enforcer loot skipped: missing prefab '{reward.Prefab}'.");
                continue;
            }

            if (itemPrefab.GetComponent<ItemDrop>() == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Karma Enforcer loot skipped: prefab '{reward.Prefab}' is not an item prefab.");
                continue;
            }

            drops.Add(new KeyValuePair<GameObject, int>(itemPrefab, reward.Amount));
        }

        if (drops.Count == 0)
        {
            zdo.Set(EnforcerLootDroppedKey, true);
            return;
        }

        CharacterDrop? dropTable = character.GetComponent<CharacterDrop>();
        Vector3 centerPosition = character.GetCenterPoint();
        if (dropTable != null)
        {
            centerPosition += dropTable.transform.TransformVector(dropTable.m_spawnOffset);
        }

        if (!IsFinite(centerPosition))
        {
            centerPosition = character.transform.position;
        }

        if (!IsFinite(centerPosition))
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Karma Enforcer loot skipped for '{GetPrefabName(character)}': invalid death position.");
            return;
        }

        // Mark before spawning. If another mod throws after partially creating the items,
        // retrying this death callback would duplicate an unknown subset of the reward.
        zdo.Set(EnforcerLootDroppedKey, true);
        CharacterDrop.DropItems(drops, centerPosition, 0.5f);
    }

    private static void StoreEnforcerLoot(ZDO zdo, IReadOnlyList<EnforcerLootDefinition>? loot)
    {
        string value = loot == null || loot.Count == 0
            ? ""
            : string.Join("\n", loot.Select(entry => $"{entry.Prefab}:{entry.Amount.ToString(CultureInfo.InvariantCulture)}"));
        zdo.Set(EnforcerLootKey, value);
    }

    private static List<EnforcerLootDefinition> DeserializeEnforcerLoot(string value)
    {
        List<EnforcerLootDefinition> loot = new();
        if (string.IsNullOrEmpty(value))
        {
            return loot;
        }

        if (value.Length > MaximumSerializedEnforcerLootLength)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Stored Enforcer loot was ignored because it exceeds the {MaximumSerializedEnforcerLootLength}-character safety limit.");
            return loot;
        }

        int totalAmount = 0;
        foreach (string token in value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (loot.Count >= MaximumEnforcerLootEntries ||
                totalAmount >= MaximumEnforcerLootAmountPerCandidate)
            {
                break;
            }

            int separator = token.LastIndexOf(':');
            if (separator <= 0 || separator >= token.Length - 1 ||
                separator > MaximumEnforcerPrefabNameLength ||
                !int.TryParse(token.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) ||
                amount <= 0)
            {
                continue;
            }

            amount = Math.Min(
                Math.Min(amount, MaximumEnforcerLootAmountPerEntry),
                MaximumEnforcerLootAmountPerCandidate - totalAmount);
            if (amount <= 0)
            {
                break;
            }

            string prefab = token.Substring(0, separator).Trim();
            if (prefab.Length == 0 || prefab.Length > MaximumEnforcerPrefabNameLength)
            {
                continue;
            }

            loot.Add(new EnforcerLootDefinition
            {
                Prefab = prefab,
                Amount = amount
            });
            totalAmount += amount;
        }

        return loot;
    }

    private static EnforcerLootDefinition CloneEnforcerLoot(EnforcerLootDefinition source)
    {
        return new EnforcerLootDefinition
        {
            Prefab = source.Prefab,
            Amount = source.Amount
        };
    }

    private static void MarkSummonedEnforcer(
        Character character,
        ZDO? zdo,
        string nameSuffix,
        ResolvedEnforcerSettings settings,
        IReadOnlyList<EnforcerLootDefinition>? loot)
    {
        ResolvedEnforcerSettings appliedSettings = settings.Clone();
        appliedSettings.IsBoss = character.m_boss || settings.IsBoss;
        character.m_boss = appliedSettings.IsBoss;
        MarkRuntimeEnforcer(character, appliedSettings, loot);
        MarkSummonedCreatureName(character, zdo, nameSuffix);
    }

    private static void MarkSummonedCreatureName(Character character, ZDO? zdo, string nameSuffix)
    {
        string prefab = GetPrefabName(character);
        string displayName = BuildSummonedName(character, prefab, nameSuffix);
        if (displayName.Length == 0)
        {
            return;
        }

        character.m_name = displayName;
        zdo?.Set(EnforcerNameKey, displayName);
    }

    private static string BuildSummonedName(Character character, string prefab, string nameSuffix)
    {
        string suffix = nameSuffix.Trim();
        string baseName = character.m_name.Trim();
        if (baseName.Length == 0)
        {
            baseName = prefab;
        }

        return suffix.Length == 0 ? baseName : $"{baseName} {suffix}";
    }

    private static void ApplyHuntPlayer(Character character)
    {
        BaseAI? baseAI = character.GetBaseAI();
        if (baseAI == null)
        {
            return;
        }

        baseAI.SetHuntPlayer(true);
        baseAI.SetAlerted(true);
        if (baseAI is MonsterAI monsterAI)
        {
            monsterAI.m_enableHuntPlayer = true;
        }
    }

    private static bool IsLikelyDungeonPosition(Vector3 position)
    {
        return Character.InInterior(position);
    }

    private static KarmaRealm GetKarmaRealm(Vector3 position)
    {
        // Keep the Karma ledger aligned with the existing dungeon gain and Enforcer-table boundary.
        return IsLikelyDungeonPosition(position)
            ? KarmaRealm.Dungeon
            : KarmaRealm.Outdoor;
    }

    private static bool TryGetDungeonLocationPrefabName(Vector3 position, out string locationPrefab)
    {
        if (TryGetZoneLocationPrefabName(position, out locationPrefab))
        {
            return true;
        }

        try
        {
            Location? zoneLocation = Location.GetZoneLocation(position);
            if (TryGetLocationPrefabName(zoneLocation, out locationPrefab))
            {
                return true;
            }

            Location? location = Location.GetLocation(position);
            if (TryGetLocationPrefabName(location, out locationPrefab))
            {
                return true;
            }
        }
        catch
        {
            locationPrefab = "";
        }

        return false;
    }

    private static bool TryGetLocationPrefabName(Location? location, out string prefabName)
    {
        prefabName = "";
        if (location == null)
        {
            return false;
        }

        if (TryGetZoneLocationPrefabName(location.transform.position, out prefabName))
        {
            return true;
        }

        prefabName = TrimCloneSuffix(location.gameObject.name).Trim();
        return prefabName.Length > 0;
    }

    private static bool TryGetZoneLocationPrefabName(Vector3 position, out string prefabName)
    {
        prefabName = "";
        if (ZoneSystem.instance == null)
        {
            return false;
        }

        Vector2i zone = ZoneSystem.GetZone(position);
        if (!ZoneSystem.instance.m_locationInstances.TryGetValue(zone, out ZoneSystem.LocationInstance locationInstance))
        {
            return false;
        }

        float radius = Mathf.Max(locationInstance.m_location.m_exteriorRadius, locationInstance.m_location.m_interiorRadius);
        if (radius > 0f && Utils.DistanceXZ(locationInstance.m_position, position) > radius)
        {
            return false;
        }

        string candidate = GetLocationSpawnContextPrefabName(locationInstance.m_location);
        if (candidate.Length == 0)
        {
            return false;
        }

        prefabName = candidate;
        return true;
    }

    private static string GetLocationSpawnContextPrefabName(ZoneSystem.ZoneLocation? location)
    {
        string locationPrefab = GetZoneLocationPrefabName(location);
        if (!locationPrefab.Contains(':') &&
            TryGetExpandWorldDataCurrentLocationPrefabName(out string currentLocationPrefab) &&
            currentLocationPrefab.Contains(':') &&
            string.Equals(GetExpandWorldDataBaseLocationName(currentLocationPrefab), locationPrefab, StringComparison.OrdinalIgnoreCase))
        {
            return currentLocationPrefab;
        }

        return locationPrefab;
    }

    private static string GetZoneLocationPrefabName(ZoneSystem.ZoneLocation? location)
    {
        return (location?.m_prefabName ?? "").Trim();
    }

    private static bool TryGetExpandWorldDataCurrentLocationPrefabName(out string locationPrefab)
    {
        locationPrefab = "";
        if (!ExpandWorldDataCurrentLocationFieldResolved)
        {
            Type? locationSpawningType = FindLoadedType("ExpandWorldData.LocationSpawning", "ExpandWorldData");
            ExpandWorldDataCurrentLocationField = locationSpawningType?.GetField("CurrentLocation", BindingFlags.Public | BindingFlags.Static);
            ExpandWorldDataCurrentLocationFieldResolved = true;
        }

        try
        {
            if (ExpandWorldDataCurrentLocationField?.GetValue(null) is not ZoneSystem.ZoneLocation currentLocation)
            {
                return false;
            }

            locationPrefab = GetZoneLocationPrefabName(currentLocation);
            return locationPrefab.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static Type? FindLoadedType(string typeName, string assemblyName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AssemblyName name;
            try
            {
                name = assembly.GetName();
            }
            catch
            {
                continue;
            }

            if (!string.Equals(name.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Type? type = assembly.GetType(typeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    internal static bool TryResolveBiomeName(string key, out Heightmap.Biome biome)
    {
        string trimmed = (key ?? "").Trim();
        if (trimmed.Length == 0)
        {
            biome = Heightmap.Biome.None;
            return false;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericBiome))
        {
            biome = (Heightmap.Biome)numericBiome;
            return numericBiome != 0;
        }

        if (Enum.TryParse(trimmed, ignoreCase: true, out biome))
        {
            return true;
        }

        if (TryResolveExpandWorldDataBiome(trimmed, out biome))
        {
            return true;
        }

        biome = Heightmap.Biome.None;
        return false;
    }

    private static bool TryResolveExpandWorldDataBiome(string key, out Heightmap.Biome biome)
    {
        EnsureExpandWorldDataBiomeMethods();
        if (ExpandWorldDataTryGetBiomeMethod == null)
        {
            biome = Heightmap.Biome.None;
            return false;
        }

        try
        {
            object[] args = { key, Heightmap.Biome.None };
            if (ExpandWorldDataTryGetBiomeMethod.Invoke(null, args) is bool matched &&
                matched &&
                args[1] is Heightmap.Biome resolvedBiome)
            {
                biome = resolvedBiome;
                return true;
            }
        }
        catch
        {
            // Expand World Data is optional.
        }

        biome = Heightmap.Biome.None;
        return false;
    }

    internal static bool TryGetBiomeDisplayName(Heightmap.Biome biome, out string displayName)
    {
        displayName = "";
        EnsureExpandWorldDataBiomeMethods();
        if (ExpandWorldDataTryGetBiomeDisplayNameMethod == null)
        {
            return false;
        }

        try
        {
            object?[] args = { biome, null };
            if (ExpandWorldDataTryGetBiomeDisplayNameMethod.Invoke(null, args) is bool matched &&
                matched &&
                args[1] is string resolvedName &&
                !string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
                return true;
            }
        }
        catch
        {
            // Expand World Data is optional.
        }

        return false;
    }

    private static void EnsureExpandWorldDataBiomeMethods()
    {
        if (ExpandWorldDataBiomeMethodsResolved)
        {
            return;
        }

        Type? biomeManagerType = FindLoadedType("ExpandWorldData.BiomeManager", "ExpandWorldData");
        ExpandWorldDataTryGetBiomeMethod = biomeManagerType?.GetMethod(
            "TryGetBiome",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(Heightmap.Biome).MakeByRefType() },
            null);
        ExpandWorldDataTryGetBiomeDisplayNameMethod = biomeManagerType?.GetMethod(
            "TryGetDisplayName",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Heightmap.Biome), typeof(string).MakeByRefType() },
            null);
        ExpandWorldDataBiomeMethodsResolved = true;
    }

    private static string GetExpandWorldDataBaseLocationName(string? locationPrefab)
    {
        string normalized = (locationPrefab ?? "").Trim();
        int separatorIndex = normalized.IndexOf(':');
        return separatorIndex > 0 ? normalized.Substring(0, separatorIndex).Trim() : normalized;
    }

    private static string TrimCloneSuffix(string name)
    {
        const string cloneSuffix = "(Clone)";
        return name.EndsWith(cloneSuffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - cloneSuffix.Length)
            : name;
    }

    private static float GetRemainingEnforcerCooldown(Vector3 position)
    {
        lock (Sync)
        {
            return GetRemainingEnforcerCooldownUnsafe(position, Time.time);
        }
    }

    private static float GetRemainingEnforcerCooldownUnsafe(Vector3 position, float now)
    {
        return GetRemainingEnforcerCooldownUnsafe(position, now, ResolvedEnforcerSettings.FromGlobal(Settings.Enforcer));
    }

    private static float GetRemainingEnforcerCooldownUnsafe(
        Vector3 position,
        float now,
        ResolvedEnforcerSettings settings,
        HashSet<string>? regionZoneKeys = null)
    {
        if (settings.Cooldown <= 0f)
        {
            return 0f;
        }

        float remaining = 0f;
        IEnumerable<string> keys = regionZoneKeys != null ? regionZoneKeys : GetSectorKeys(position);
        foreach (string key in keys)
        {
            if (Sectors.TryGetValue(key, out SectorState state))
            {
                remaining = Mathf.Max(remaining, GetRemainingEnforcerCooldown(state, now, settings));
            }
        }

        return remaining;
    }

    private static float GetRemainingEnforcerCooldown(SectorState state, float now, ResolvedEnforcerSettings settings)
    {
        if (settings.Cooldown <= 0f || state.LastEnforcerTime <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, settings.Cooldown - (now - state.LastEnforcerTime));
    }

    private static float ApplyEnforcerCostUnsafe(Vector3 position, float now, ResolvedEnforcerSettings settings)
    {
        float remainingKarma = ReduceKarmaUnsafe(position, settings.ConsumeKarma, now);
        RefreshEnforcerCooldownUnsafe(position, now);
        return remainingKarma;
    }

    private static void RefreshEnforcerCooldownUnsafe(Vector3 position, float now)
    {
        foreach (string key in GetSectorKeys(position))
        {
            if (Sectors.TryGetValue(key, out SectorState state))
            {
                state.LastEnforcerTime = now;
            }
        }
    }

    private static float GetKillKarma(string prefab, bool boss, int level, bool dungeon)
    {
        float amount = Settings.Karma.Prefabs.TryGetValue(prefab, out float prefabValue)
            ? prefabValue
            : boss ? Settings.Karma.BossKill : Settings.Karma.Kill;
        float scaling = boss ? Settings.Karma.BossKarmaScaling : Settings.Karma.KarmaScaling;
        amount *= Mathf.Max(0f, 1f + Mathf.Max(0f, scaling) * (Mathf.Max(1, level) - 1));
        if (dungeon)
        {
            amount *= Mathf.Max(0f, Settings.Karma.DungeonMultiplier);
        }

        return amount;
    }

    private static KarmaAddResult AddKarma(Vector3 position, float amount)
    {
        float now = Time.time;
        bool levelIncreased = false;
        bool karmaIncreased = false;
        bool gainCapReached = false;
        lock (Sync)
        {
            string[] keys = GetSectorKeys(position).ToArray();
            if (!TryEnsureSectorStatesUnsafe(keys))
            {
                return KarmaAddResult.Unavailable;
            }

            List<float> thresholds = Settings.Karma.Thresholds;
            bool hasGainCap = thresholds.Count > 0;
            float gainCap = hasGainCap ? thresholds[thresholds.Count - 1] : 0f;
            bool everySectorAtGainCap = hasGainCap;
            int previousBonus = IsKarmaLevelEnabled() ? GetSectorLevelBonus(GetBestStateUnsafe(position, out _).Karma) : 0;
            float updatedKarma = 0f;
            foreach (string key in keys)
            {
                SectorState state = Sectors[key];
                ApplyDecayUnsafe(state, now);
                float previousKarma = Mathf.Max(0f, state.Karma);
                if (!hasGainCap || previousKarma < gainCap)
                {
                    everySectorAtGainCap = false;
                    state.Karma = Mathf.Max(0f, previousKarma + amount);
                    if (hasGainCap)
                    {
                        state.Karma = Mathf.Min(gainCap, state.Karma);
                    }

                    if (state.Karma > previousKarma)
                    {
                        state.LastKarmaTime = now;
                        karmaIncreased = true;
                    }
                }

                updatedKarma = Mathf.Max(updatedKarma, state.Karma);
            }

            levelIncreased = IsKarmaLevelEnabled() && GetSectorLevelBonus(updatedKarma) > previousBonus;
            gainCapReached = everySectorAtGainCap;
        }

        if (levelIncreased)
        {
            BroadcastRegionalCenterQuote(KarmaLevelQuotes, position);
        }

        if (karmaIncreased)
        {
            return KarmaAddResult.Added;
        }

        return gainCapReached
            ? KarmaAddResult.Saturated
            : KarmaAddResult.Unavailable;
    }

    private static float ReduceKarmaUnsafe(Vector3 position, float amount, float now)
    {
        float remainingKarma = 0f;
        foreach (string key in GetSectorKeys(position))
        {
            if (!Sectors.TryGetValue(key, out SectorState state))
            {
                continue;
            }

            ApplyDecayUnsafe(state, now);
            if (amount > 0f)
            {
                state.Karma = Mathf.Max(0f, state.Karma - amount);
            }

            remainingKarma = Mathf.Max(remainingKarma, state.Karma);
        }

        return remainingKarma;
    }

    private static void PruneSectorStates(float now)
    {
        if (now < NextSectorPruneTime)
        {
            return;
        }

        NextSectorPruneTime = now + SectorPruneInterval;
        float cooldown = Mathf.Max(0f, Settings.Enforcer.Cooldown);
        lock (Sync)
        {
            int scanCount = Math.Min(MaximumSectorScansPerPass, SectorPruneQueue.Count);
            for (int index = 0; index < scanCount; index++)
            {
                string key = SectorPruneQueue.Dequeue();
                if (!Sectors.TryGetValue(key, out SectorState state))
                {
                    continue;
                }

                ApplyDecayUnsafe(state, now);
                bool cooldownExpired = state.LastEnforcerTime <= 0f ||
                                       cooldown <= 0f ||
                                       now - state.LastEnforcerTime >= cooldown;
                if (state.Karma <= 0f && cooldownExpired)
                {
                    Sectors.Remove(key);
                    continue;
                }

                SectorPruneQueue.Enqueue(key);
            }
        }
    }

    private static float GetKarma(Vector3 position)
    {
        lock (Sync)
        {
            return GetBestStateUnsafe(position, out _).Karma;
        }
    }

    private static SectorState GetBestState(Vector3 position, out string key)
    {
        lock (Sync)
        {
            return GetBestStateUnsafe(position, out key).Clone();
        }
    }

    private static SectorState GetBestStateUnsafe(Vector3 position, out string key)
    {
        float now = Time.time;
        key = "";
        SectorState bestState = null!;
        bool hasBest = false;
        foreach (string sectorKey in GetSectorKeys(position))
        {
            if (!Sectors.TryGetValue(sectorKey, out SectorState state))
            {
                continue;
            }

            ApplyDecayUnsafe(state, now);
            if (!hasBest || state.Karma > bestState.Karma)
            {
                key = sectorKey;
                bestState = state;
                hasBest = true;
            }
        }

        if (hasBest)
        {
            return bestState;
        }

        key = GetSectorKey(position);
        return EmptySectorState;
    }

    private static void ApplyDecayUnsafe(SectorState state, float now)
    {
        if (!IsKarmaSystemEnabled())
        {
            return;
        }

        if (state.Karma <= 0f)
        {
            state.Karma = 0f;
            state.LastKarmaTime = now;
            return;
        }

        float decayPerMinute = Settings.Karma.DecayPerMinute;
        if (decayPerMinute <= 0f)
        {
            return;
        }

        if (state.LastKarmaTime <= 0f)
        {
            state.LastKarmaTime = now;
            return;
        }

        float graceSeconds = Mathf.Max(0f, Settings.Karma.DecayAfterMinutes) * 60f;
        float elapsed = Mathf.Max(0f, now - state.LastKarmaTime);
        if (elapsed <= graceSeconds)
        {
            return;
        }

        float decay = (elapsed - graceSeconds) / 60f * decayPerMinute;
        state.Karma = Mathf.Max(0f, state.Karma - decay);
        state.LastKarmaTime = state.Karma <= 0f ? now : now - graceSeconds;
    }

    private static bool TryEnsureSectorStatesUnsafe(IReadOnlyCollection<string> keys)
    {
        HashSet<string> requiredKeys = new(keys, StringComparer.Ordinal);
        string[] missingKeys = requiredKeys
            .Where(key => !Sectors.ContainsKey(key))
            .ToArray();
        if (Sectors.Count + missingKeys.Length > MaximumSectorStates)
        {
            float warningTime = Time.unscaledTime;
            if (warningTime >= NextSectorCapacityWarningTime)
            {
                NextSectorCapacityWarningTime = warningTime + SectorCapacityWarningInterval;
                CreatureManagerPlugin.Log.LogWarning(
                    $"Karma sector state limit ({MaximumSectorStates}) reached; preserving active Karma/cooldowns and rejecting new regional state until the bounded background pruner reclaims capacity.");
            }

            return false;
        }

        foreach (string key in missingKeys)
        {
            SectorState state = new();
            Sectors[key] = state;
            SectorPruneQueue.Enqueue(key);
        }

        return true;
    }

    private static int GetSectorLevelBonus(float karma)
    {
        List<float> thresholds = Settings.Karma.Thresholds;
        int bonus = 0;
        for (int i = 0; i < thresholds.Count; i++)
        {
            if (karma >= thresholds[i])
            {
                bonus++;
            }
        }

        return Mathf.Max(0, bonus);
    }

    private static string GetSectorKey(Vector3 position)
    {
        Vector2i zone = ZoneSystem.GetZone(position);
        return GetSectorKey(zone, GetKarmaRealm(position));
    }

    private static string GetSectorKey(Vector2i zone, KarmaRealm realm)
    {
        string prefix = realm == KarmaRealm.Dungeon ? "D" : "O";
        return $"{prefix}:{zone.x},{zone.y}";
    }

    private static IEnumerable<string> GetSectorKeys(Vector3 position)
    {
        Vector2i zone = ZoneSystem.GetZone(position);
        KarmaRealm realm = GetKarmaRealm(position);
        yield return GetSectorKey(zone, realm);

        int radius = ZoneRadius;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                yield return GetSectorKey(new Vector2i(zone.x + x, zone.y + y), realm);
            }
        }
    }

    private static Heightmap.Biome GetBiome(Vector3 position)
    {
        if (WorldGenerator.instance == null)
        {
            return Heightmap.Biome.None;
        }

        try
        {
            return WorldGenerator.instance.GetBiome(position);
        }
        catch
        {
            return Heightmap.Biome.None;
        }
    }

    private static string GetPrefabName(Character character)
    {
        return Utils.GetPrefabName(((Component)character).gameObject);
    }

    private static void BroadcastRegionalCenterQuote(
        IReadOnlyList<string> quotes,
        Vector3 position,
        HashSet<string>? regionZoneKeys = null)
    {
        if (quotes.Count == 0 || !IsFinite(position))
        {
            return;
        }

        string message = quotes[UnityEngine.Random.Range(0, quotes.Count)];
        HashSet<string> targetZoneKeys = regionZoneKeys ??
                                         new HashSet<string>(GetSectorKeys(position), StringComparer.Ordinal);
        if (ZNet.instance != null &&
            ZNet.instance.IsServer())
        {
            Player? localPlayer = Player.m_localPlayer;
            ZDO? localPlayerZdo = localPlayer?.m_nview != null && localPlayer.m_nview.IsValid()
                ? localPlayer.m_nview.GetZDO()
                : null;
            ZDOID localCharacterId = localPlayerZdo?.m_uid ?? ZDOID.None;
            HashSet<long> notifiedPeerUids = new();

            foreach (ConnectedPlayerContext player in GetConnectedAlivePlayerContexts())
            {
                if (!targetZoneKeys.Contains(GetSectorKey(player.Position)) ||
                    !notifiedPeerUids.Add(player.PeerUid))
                {
                    continue;
                }

                if (localPlayer != null && player.CharacterId == localCharacterId)
                {
                    ShowLocalCenterQuote(message);
                    continue;
                }

                if (ZRoutedRpc.instance == null)
                {
                    continue;
                }

                try
                {
                    ZPackage package = new();
                    package.Write(message);
                    ZRoutedRpc.instance.InvokeRoutedRPC(
                        player.PeerUid,
                        CenterQuoteRpc,
                        package);
                }
                catch (Exception exception)
                {
                    CreatureManagerPlugin.Log.LogDebug(
                        $"Could not send regional Karma quote to peer {player.PeerUid}: {exception.Message}");
                }
            }

            return;
        }

        Player? fallbackPlayer = Player.m_localPlayer;
        if (fallbackPlayer != null &&
            !fallbackPlayer.IsDead() &&
            targetZoneKeys.Contains(GetSectorKey(fallbackPlayer.transform.position)))
        {
            ShowLocalCenterQuote(message);
        }
    }

    private static void ShowLocalCenterQuote(string message)
    {
        if (message.Length > 0 && Player.m_localPlayer != null)
        {
            ((Character)Player.m_localPlayer).Message(
                MessageHud.MessageType.Center,
                message,
                0,
                null);
        }
    }

    private static void RPC_CenterQuote(long sender, ZPackage package)
    {
        if (ZRoutedRpc.instance == null ||
            sender != ZRoutedRpc.instance.GetServerPeerID())
        {
            return;
        }

        try
        {
            string message = package.ReadString();
            ShowLocalCenterQuote(message);
        }
        catch
        {
            // Ignore malformed or stale quote messages.
        }
    }

    private static KarmaSettings ReadSettings(string yaml, string source)
    {
        KarmaSettings settings = KarmaSettings.Default();
        YamlStream stream = new();
        using StringReader reader = new(yaml);
        stream.Load(reader);
        if (stream.Documents.Count == 0)
        {
            return settings;
        }

        if (stream.Documents.Count == 1 &&
            stream.Documents[0].RootNode is YamlSequenceNode emptySequence &&
            emptySequence.Children.Count == 0)
        {
            return settings;
        }

        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new FormatException($"Karma YAML from {source} must contain one top-level mapping.");
        }

        CreatureYaml.ValidateUniqueMappingKeys(root, source, "root");

        if (TryGetNode(root, "karma", out YamlNode karmaNode))
        {
            if (karmaNode is not YamlMappingNode karmaMap)
            {
                throw new FormatException($"Karma YAML from {source} karma must be a mapping.");
            }

            ValidateKnownFields(karmaMap, KarmaFields, source, "karma");
            settings.Karma.Thresholds = ReadFloatSequence(karmaMap, "thresholds", settings.Karma.Thresholds, source, "karma.thresholds")
                .Where(value => value >= 0f)
                .OrderBy(value => value)
                .ToList();
            if (TryReadExactFloatTuple(karmaMap, "decay", 3, source, "karma.decay", out List<float> decay))
            {
                settings.Karma.DecayAfterMinutes = Mathf.Max(0f, decay[0]);
                settings.Karma.DecayPerMinute = Mathf.Max(0f, decay[1]);
                settings.Karma.PlayerDeathClearKarma = Mathf.Max(0f, decay[2]);
            }

            ApplyKarmaGainTuple(karmaMap, settings.Karma, source);
            if (TryGetNode(karmaMap, "prefabs", out YamlNode prefabsNode))
            {
                if (prefabsNode is not YamlMappingNode prefabsMap)
                {
                    throw new FormatException($"Karma YAML from {source} karma.prefabs must be a mapping.");
                }
                settings.Karma.Prefabs = ReadFloatMap(prefabsMap, source, "karma.prefabs");
            }
        }

        if (TryGetExactNode(root, "Enforcer", out YamlNode enforcerNode))
        {
            if (enforcerNode is not YamlMappingNode enforcerMap)
            {
                throw new FormatException($"Karma YAML from {source} Enforcer must be a mapping.");
            }

            ValidateKnownFields(enforcerMap, EnforcerFields, source, "Enforcer");
            ApplyEnforcerSettingsTuple(enforcerMap, settings.Enforcer, source);
            ApplyEnforcerChecksTuple(enforcerMap, settings.Enforcer, source, "Enforcer");
            if (TryReadModifierBlock(enforcerMap, source, "Enforcer.modifiers", out Dictionary<string, ModifierDefinition>? modifiers, out bool modifiersCleared))
            {
                settings.Enforcer.Modifiers = modifiers ?? new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
                settings.Enforcer.ModifiersCleared = modifiersCleared;
            }
        }
        else if (TryGetNode(root, "enforcer", out _))
        {
            throw new FormatException($"Karma YAML from {source} uses unsupported top-level block 'enforcer'. Use 'Enforcer'.");
        }

        settings.Enforcer.Biomes = ReadEnforcerBiomes(root, source);
        return settings;
    }

    private static void ApplyKarmaGainTuple(YamlMappingNode map, KarmaGainSettings settings, string source)
    {
        if (!TryReadExactFloatTuple(map, "gain", 5, source, "karma.gain", out List<float> values))
        {
            return;
        }

        settings.Kill = Mathf.Max(0f, values[0]);
        settings.BossKill = Mathf.Max(0f, values[1]);
        settings.KarmaScaling = Mathf.Max(0f, values[2]);
        settings.BossKarmaScaling = Mathf.Max(0f, values[3]);
        settings.DungeonMultiplier = Mathf.Max(0f, values[4]);
    }

    private static bool TryGetExactNode(YamlMappingNode node, string field, out YamlNode value)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> entry in node.Children)
        {
            if (string.Equals(GetScalar(entry.Key), field, StringComparison.Ordinal))
            {
                value = entry.Value;
                return true;
            }
        }

        value = new YamlScalarNode("");
        return false;
    }

    private static void ApplyEnforcerSettingsTuple(YamlMappingNode map, EnforcerSettings settings, string source)
    {
        if (!TryReadExactFloatTuple(map, "settings", 3, source, "Enforcer.settings", out List<float> values))
        {
            return;
        }

        settings.RequiredKarma = Mathf.Max(0f, values[0]);
        settings.ConsumeKarma = Mathf.Max(0f, values[1]);
        settings.LevelBonus = Mathf.Max(0, Mathf.RoundToInt(values[2]));
    }

    private static void ApplyEnforcerChecksTuple(YamlMappingNode map, EnforcerSettings settings, string source, string label)
    {
        if (!TryGetNode(map, "checks", out YamlNode node))
        {
            return;
        }

        if (!TryReadStringSequence(node, out List<string> values))
        {
            throw new FormatException($"Karma YAML from {source} {label}.checks must be a YAML list of non-empty scalar values.");
        }

        if (values.Count != 4)
        {
            throw new FormatException($"Karma YAML from {source} {label}.checks must contain exactly 4 values: [chance, cooldown, checkInterval, spawnRadius].");
        }

        if (!TryParseFiniteFloat(values[0], out float chance) ||
            !TryParseFiniteFloat(values[1], out float cooldown) ||
            !TryParseFiniteFloat(values[2], out float checkInterval))
        {
            throw new FormatException($"Karma YAML from {source} {label}.checks first three values must be finite numbers.");
        }

        if (!TryParseFloatRange(values[3], source, $"{label}.checks[4]", out float minRadius, out float maxRadius))
        {
            return;
        }

        settings.Chance = Mathf.Clamp(chance, 0f, 100f);
        settings.Cooldown = Mathf.Max(0f, cooldown);
        settings.CheckInterval = Mathf.Max(0f, checkInterval);
        settings.SpawnRadiusMin = minRadius;
        settings.SpawnRadiusMax = maxRadius;
    }

    private static EnforcerOverrideSettings ReadEnforcerSettingsOverride(YamlMappingNode map, string source, string label)
    {
        EnforcerOverrideSettings settings = new();
        if (!TryReadExactFloatTuple(map, "settings", 3, source, $"{label}.settings", out List<float> values))
        {
            return settings;
        }

        settings.RequiredKarma = Mathf.Max(0f, values[0]);
        settings.ConsumeKarma = Mathf.Max(0f, values[1]);
        settings.LevelBonus = Mathf.Max(0, Mathf.RoundToInt(values[2]));
        return settings;
    }

    private static bool TryReadExactFloatTuple(
        YamlMappingNode map,
        string field,
        int expectedCount,
        string source,
        string label,
        out List<float> values)
    {
        values = new List<float>();
        if (!TryGetNode(map, field, out YamlNode node))
        {
            return false;
        }

        if (!TryReadStringSequence(node, out List<string> tokens))
        {
            throw new FormatException($"Karma YAML from {source} {label} must be a YAML list of non-empty scalar values.");
        }

        if (tokens.Count != expectedCount)
        {
            throw new FormatException($"Karma YAML from {source} {label} must contain exactly {expectedCount} values.");
        }

        foreach (string token in tokens)
        {
            if (!TryParseFiniteFloat(token, out float value))
            {
                throw new FormatException($"Karma YAML from {source} {label} has invalid number '{token}'.");
            }

            values.Add(value);
        }

        return true;
    }

    private static void ValidateKnownFields(
        YamlMappingNode map,
        HashSet<string> allowedFields,
        string source,
        string label)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> entry in map.Children)
        {
            string field = GetScalar(entry.Key);
            if (!allowedFields.Contains(field))
            {
                throw new FormatException($"Karma YAML from {source} {label} has unknown field '{field}'.");
            }
        }
    }

    private static bool ReadBool(YamlMappingNode node, string field, bool fallback, string source, string label)
    {
        if (!TryGetNode(node, field, out YamlNode value))
        {
            return fallback;
        }

        string text = GetScalar(value);
        if (bool.TryParse(text, out bool parsed))
        {
            return parsed;
        }

        throw new FormatException($"Karma YAML from {source} {label}.{field} must be true or false.");
    }

    private static float ReadFloat(YamlMappingNode node, string field, float fallback)
    {
        if (!TryGetNode(node, field, out YamlNode value))
        {
            return fallback;
        }

        if (!TryParseFiniteFloat(GetScalar(value), out float parsed))
        {
            throw new FormatException($"Karma YAML field '{field}' must be a finite number.");
        }

        return parsed;
    }

    private static bool TryParseFloatRange(string text, string source, string label, out float min, out float max)
    {
        min = 0f;
        max = 0f;
        string[] parts = text.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !TryParseFiniteFloat(parts[0].Trim(), out min) ||
            !TryParseFiniteFloat(parts[1].Trim(), out max))
        {
            throw new FormatException($"Karma YAML from {source} {label} must use finite 'min~max' values, for example '24~48'.");
        }

        min = Mathf.Max(0f, min);
        max = Mathf.Max(min, max);
        return true;
    }

    private static List<float> ReadFloatSequence(
        YamlMappingNode node,
        string field,
        List<float> fallback,
        string source,
        string label)
    {
        if (!TryGetNode(node, field, out YamlNode value))
        {
            return fallback.ToList();
        }

        if (!TryReadStringSequence(value, out List<string> tokens))
        {
            throw new FormatException($"Karma YAML from {source} {label} must be a YAML list of non-empty scalar values.");
        }

        List<float> values = new(tokens.Count);
        foreach (string token in tokens)
        {
            if (!TryParseFiniteFloat(token, out float parsed))
            {
                throw new FormatException($"Karma YAML from {source} {label} has invalid number '{token}'.");
            }

            values.Add(parsed);
        }

        return values;
    }

    private static Dictionary<string, float> ReadFloatMap(YamlMappingNode map, string source, string label)
    {
        Dictionary<string, float> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in map.Children)
        {
            string key = GetScalar(entry.Key);
            if (key.Length > 0 && TryParseFiniteFloat(GetScalar(entry.Value), out float value))
            {
                values[key] = value;
            }
            else
            {
                throw new FormatException($"Karma YAML from {source} {label}.{key} must have a non-empty key and finite number value.");
            }
        }

        return values;
    }

    private static bool TryReadModifierBlock(
        YamlMappingNode owner,
        string source,
        string label,
        out Dictionary<string, ModifierDefinition>? modifiers,
        out bool cleared)
    {
        modifiers = null;
        cleared = false;
        if (!TryGetNode(owner, "modifiers", out YamlNode node))
        {
            return false;
        }

        if (!CreatureYaml.TryReadModifierBlock(
            node,
            source,
            label,
            CreatureYaml.ModifierYamlContext.Karma,
            out modifiers,
            out cleared))
        {
            throw new FormatException($"Karma YAML from {source} {label} is invalid.");
        }

        return true;
    }

    private static Dictionary<string, ModifierDefinition> CloneModifiers(Dictionary<string, ModifierDefinition> source)
    {
        Dictionary<string, ModifierDefinition> clone = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, ModifierDefinition> entry in source)
        {
            clone[entry.Key] = entry.Value.Clone();
        }

        return clone;
    }

    private static void MergeModifierOverrides(
        Dictionary<string, ModifierDefinition> target,
        Dictionary<string, ModifierDefinition> overrides)
    {
        foreach (KeyValuePair<string, ModifierDefinition> entry in overrides)
        {
            if (!target.TryGetValue(entry.Key, out ModifierDefinition current))
            {
                target[entry.Key] = entry.Value.Clone();
                continue;
            }

            current.OverlayFrom(entry.Value);
        }
    }

    private static Dictionary<string, EnforcerBiomeDefinition> ReadEnforcerBiomes(YamlMappingNode root, string source)
    {
        Dictionary<string, EnforcerBiomeDefinition> biomes = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in root.Children)
        {
            string key = GetScalar(entry.Key);
            if (string.Equals(key, "karma", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "enforcer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key.Length == 0 || entry.Value is not YamlMappingNode map)
            {
                throw new FormatException($"Karma YAML from {source} top-level biome blocks must be named mappings.");
            }

            EnforcerBiomeDefinition definition = ReadEnforcerBiome(map, source, key);
            if (definition.HasContent)
            {
                RegisterEnforcerBiomeDefinition(biomes, key, definition);
            }
        }

        return biomes;
    }

    private static void RegisterEnforcerBiomeDefinition(Dictionary<string, EnforcerBiomeDefinition> biomes, string key, EnforcerBiomeDefinition definition)
    {
        string trimmed = (key ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        biomes[trimmed] = definition;
        biomes[NormalizeBiomeName(trimmed)] = definition;
        if (!TryResolveBiomeName(trimmed, out Heightmap.Biome biome))
        {
            return;
        }

        biomes[NormalizeBiomeName(biome.ToString())] = definition;
        biomes[NormalizeBiomeName(((int)biome).ToString(CultureInfo.InvariantCulture))] = definition;
        if (TryGetBiomeDisplayName(biome, out string displayName))
        {
            biomes[displayName.Trim()] = definition;
            biomes[NormalizeBiomeName(displayName)] = definition;
        }
    }

    private static EnforcerBiomeDefinition ReadEnforcerBiome(YamlMappingNode map, string source, string biome)
    {
        EnforcerBiomeDefinition definition = new();
        ValidateKnownFields(map, EnforcerBiomeFields, source, biome);
        definition.Enabled = ReadBool(map, "enabled", true, source, biome);
        if (TryGetNode(map, "enforcers", out YamlNode enforcersNode))
        {
            definition.Outdoor = ReadEnforcerCandidates(enforcersNode, source, $"{biome}.enforcers");
        }

        if (TryGetNode(map, "dungeonEnforcers", out YamlNode dungeonEnforcersNode))
        {
            ReadDungeonEnforcerCandidates(definition, dungeonEnforcersNode, source, $"{biome}.dungeonEnforcers");
        }

        return definition;
    }

    private static void ReadDungeonEnforcerCandidates(EnforcerBiomeDefinition definition, YamlNode node, string source, string label)
    {
        if (node is not YamlSequenceNode)
        {
            throw new FormatException($"Karma YAML from {source} {label} must be a list. Use location: LocationPrefab on individual entries for location-specific dungeon summons.");
        }

        AddDungeonEnforcerCandidates(definition, ReadEnforcerCandidates(node, source, label));
    }

    private static void AddDungeonEnforcerCandidates(EnforcerBiomeDefinition definition, List<EnforcerCandidateDefinition> candidates)
    {
        foreach (IGrouping<string, EnforcerCandidateDefinition> group in candidates.GroupBy(candidate => (candidate.Location ?? "").Trim(), StringComparer.OrdinalIgnoreCase))
        {
            List<EnforcerCandidateDefinition> groupCandidates = group.ToList();
            foreach (EnforcerCandidateDefinition candidate in groupCandidates)
            {
                candidate.Location = "";
            }

            if (group.Key.Length == 0)
            {
                definition.Dungeon.AddRange(groupCandidates);
            }
            else
            {
                AddDungeonLocationCandidates(definition, group.Key, groupCandidates);
            }
        }
    }

    private static void AddDungeonLocationCandidates(EnforcerBiomeDefinition definition, string location, List<EnforcerCandidateDefinition> candidates)
    {
        if (!definition.DungeonByLocation.TryGetValue(location, out List<EnforcerCandidateDefinition> existing))
        {
            definition.DungeonByLocation[location] = candidates;
            return;
        }

        existing.AddRange(candidates);
    }

    private static List<EnforcerCandidateDefinition> ReadEnforcerCandidates(YamlNode node, string source, string label)
    {
        List<EnforcerCandidateDefinition> candidates = new();
        if (node is not YamlSequenceNode sequence)
        {
            throw new FormatException($"Karma YAML from {source} {label} must be a list.");
        }

        int index = 0;
        foreach (YamlNode child in sequence.Children)
        {
            index++;
            EnforcerCandidateDefinition candidate = ReadEnforcerCandidate(child, source, $"{label}[{index}]");
            if (candidate.Summon.Boss.Length > 0)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private static EnforcerCandidateDefinition ReadEnforcerCandidate(YamlNode node, string source, string label)
    {
        if (node is not YamlMappingNode map)
        {
            throw new FormatException($"Karma YAML from {source} {label} must be a mapping with summon: [BossPrefab, MinionPrefab[:count], ...].");
        }

        ValidateKnownFields(map, EnforcerCandidateFields, source, label);
        if (!TryGetNode(map, "summon", out YamlNode summonNode))
        {
            throw new FormatException($"Karma YAML from {source} {label} must include summon: [BossPrefab, MinionPrefab[:count], ...].");
        }

        if (!TryReadStringSequence(summonNode, out List<string> summonValues))
        {
            throw new FormatException($"Karma YAML from {source} {label}.summon must be a YAML list of non-empty prefab values.");
        }
        if (summonValues.Count == 0)
        {
            throw new FormatException($"Karma YAML from {source} {label}.summon must include a boss prefab.");
        }

        string location = "";
        if (TryGetNode(map, "location", out YamlNode locationNode))
        {
            if (locationNode is not YamlScalarNode)
            {
                throw new FormatException($"Karma YAML from {source} {label}.location must be a scalar prefab name.");
            }
            location = GetScalar(locationNode);
        }

        return new EnforcerCandidateDefinition
        {
            Summon = ReadSummonSet(summonValues, source, label),
            Weight = Mathf.Max(0f, ReadFloat(map, "weight", 1f)),
            Loot = ReadEnforcerLoot(map, source, label),
            Location = location,
            Override = ReadEnforcerCandidateOverride(map, source, label)
        };
    }

    private static List<EnforcerLootDefinition> ReadEnforcerLoot(YamlMappingNode map, string source, string label)
    {
        List<EnforcerLootDefinition> loot = new();
        if (!TryGetNode(map, "loot", out YamlNode node))
        {
            return loot;
        }

        if (!TryReadStringSequence(node, out List<string> values))
        {
            throw new FormatException($"Karma YAML from {source} {label}.loot must be a YAML list of itemPrefab:amount values without a space after the colon.");
        }

        if (values.Count > MaximumEnforcerLootEntries)
        {
            throw new FormatException(
                $"Karma YAML from {source} {label}.loot has {values.Count} entries; the maximum is {MaximumEnforcerLootEntries}.");
        }

        long totalAmount = 0;
        foreach (string value in values)
        {
            string text = value.Trim();
            int separator = text.LastIndexOf(':');
            if (separator <= 0 || separator >= text.Length - 1 ||
                !int.TryParse(text.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) ||
                amount <= 0)
            {
                throw new FormatException($"Karma YAML from {source} {label}.loot has invalid value '{value}'. Use itemPrefab:amount with a positive integer amount.");
            }

            string prefab = text.Substring(0, separator).Trim();
            if (prefab.Length == 0 || prefab.Length > MaximumEnforcerPrefabNameLength)
            {
                throw new FormatException(
                    $"Karma YAML from {source} {label}.loot item prefab in '{value}' must contain from 1 to {MaximumEnforcerPrefabNameLength} characters.");
            }

            if (amount > MaximumEnforcerLootAmountPerEntry)
            {
                throw new FormatException(
                    $"Karma YAML from {source} {label}.loot value '{value}' exceeds the per-item maximum of {MaximumEnforcerLootAmountPerEntry}.");
            }

            totalAmount += amount;
            if (totalAmount > MaximumEnforcerLootAmountPerCandidate)
            {
                throw new FormatException(
                    $"Karma YAML from {source} {label}.loot grants {totalAmount} total items; the maximum is {MaximumEnforcerLootAmountPerCandidate}.");
            }

            loot.Add(new EnforcerLootDefinition
            {
                Prefab = prefab,
                Amount = amount
            });
        }

        return loot;
    }

    private static EnforcerOverrideSettings ReadEnforcerCandidateOverride(YamlMappingNode map, string source, string label)
    {
        EnforcerOverrideSettings settings = ReadEnforcerSettingsOverride(map, source, label);
        if (TryReadModifierBlock(map, source, $"{label}.modifiers", out Dictionary<string, ModifierDefinition>? modifiers, out bool modifiersCleared))
        {
            settings.Modifiers = modifiers;
            settings.ModifiersCleared = modifiersCleared;
        }

        return settings;
    }

    private static EnforcerSummonSet ReadSummonSet(List<string> values, string source, string label)
    {
        if (values.Count == 0)
        {
            return new EnforcerSummonSet();
        }

        string boss = values[0].Trim();
        if (boss.Length == 0 || boss.Length > MaximumEnforcerPrefabNameLength)
        {
            throw new FormatException(
                $"Karma YAML from {source} {label}.summon boss prefab must contain from 1 to {MaximumEnforcerPrefabNameLength} characters.");
        }

        if (values.Count - 1 > MaximumEnforcerMinionEntries)
        {
            throw new FormatException(
                $"Karma YAML from {source} {label}.summon has {values.Count - 1} minion entries; the maximum is {MaximumEnforcerMinionEntries}.");
        }

        List<EnforcerMinionDefinition> minions = new();
        int totalMinions = 0;
        foreach (string value in values.Skip(1))
        {
            EnforcerMinionDefinition minion = ParseMinionDefinition(value, source, label);
            if (minion.Prefab.Length == 0 || minion.Count <= 0)
            {
                continue;
            }

            totalMinions += minion.Count;
            if (totalMinions > MaximumEnforcerMinionsPerCandidate)
            {
                throw new FormatException(
                    $"Karma YAML from {source} {label}.summon requests {totalMinions} total minions; the maximum is {MaximumEnforcerMinionsPerCandidate}.");
            }

            minions.Add(minion);
        }

        return new EnforcerSummonSet
        {
            Boss = boss,
            Minions = minions
        };
    }

    private static EnforcerMinionDefinition ParseMinionDefinition(string value, string source, string label)
    {
        string text = value.Trim();
        if (text.Length > MaximumEnforcerPrefabNameLength + 16)
        {
            throw new FormatException(
                $"Karma YAML from {source} {label}.summon minion entry exceeds the supported prefab/count length.");
        }

        int count = 1;
        int separator = text.LastIndexOf(':');
        if (separator > 0 && separator < text.Length - 1 &&
            int.TryParse(text.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCount))
        {
            if (parsedCount < 1)
            {
                throw new FormatException($"Karma YAML from {source} {label}.summon minion '{value}' must use a count of 1 or greater.");
            }

            if (parsedCount > MaximumEnforcerMinionsPerEntry)
            {
                throw new FormatException(
                    $"Karma YAML from {source} {label}.summon minion '{value}' exceeds the per-entry maximum of {MaximumEnforcerMinionsPerEntry}.");
            }

            text = text.Substring(0, separator).Trim();
            count = parsedCount;
        }

        if (text.Length == 0 || text.Length > MaximumEnforcerPrefabNameLength)
        {
            throw new FormatException(
                $"Karma YAML from {source} {label}.summon minion prefab must contain from 1 to {MaximumEnforcerPrefabNameLength} characters.");
        }

        return new EnforcerMinionDefinition
        {
            Prefab = text,
            Count = count
        };
    }

    private static bool TryReadStringSequence(YamlNode node, out List<string> values)
    {
        values = new List<string>();
        if (node is not YamlSequenceNode sequence)
        {
            return false;
        }

        foreach (YamlNode child in sequence.Children)
        {
            if (child is not YamlScalarNode)
            {
                values.Clear();
                return false;
            }

            string value = GetScalar(child);
            if (value.Length == 0)
            {
                values.Clear();
                return false;
            }

            values.Add(value);
        }

        return true;
    }

    private static bool TryGetNode(YamlMappingNode node, string field, out YamlNode value)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> entry in node.Children)
        {
            if (string.Equals(GetScalar(entry.Key), field, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = new YamlScalarNode("");
        return false;
    }

    private static string GetScalar(YamlNode node)
    {
        return node is YamlScalarNode scalar ? (scalar.Value ?? "").Trim() : "";
    }

    private static bool TryParseFiniteFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
               !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string NormalizeBiomeName(string value)
    {
        return new string((value ?? "")
            .Where(character => !char.IsWhiteSpace(character) && character != '_' && character != '-')
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private sealed class SummonCheckWindow
    {
        internal readonly Vector2i CenterZone;
        internal readonly Vector3 CenterPosition;
        internal readonly float Karma;
        internal readonly List<ConnectedPlayerContext> EligiblePlayers;
        internal readonly HashSet<string> ZoneKeys;

        internal SummonCheckWindow(
            Vector2i centerZone,
            Vector3 centerPosition,
            float karma,
            List<ConnectedPlayerContext> eligiblePlayers,
            HashSet<string> zoneKeys)
        {
            CenterZone = centerZone;
            CenterPosition = centerPosition;
            Karma = karma;
            EligiblePlayers = eligiblePlayers;
            ZoneKeys = zoneKeys;
        }
    }

    private sealed class ConnectedPlayerContext
    {
        internal readonly long PeerUid;
        internal readonly ZDOID CharacterId;
        internal readonly Vector3 Position;

        internal ConnectedPlayerContext(long peerUid, ZDOID characterId, Vector3 position)
        {
            PeerUid = peerUid;
            CharacterId = characterId;
            Position = position;
        }
    }

    private readonly struct EnforcerPlayerPresence
    {
        internal readonly Vector3 Position;
        internal readonly Vector2i Zone;
        internal readonly bool Interior;

        internal EnforcerPlayerPresence(Vector3 position, Vector2i zone, bool interior)
        {
            Position = position;
            Zone = zone;
            Interior = interior;
        }
    }

    private sealed class SectorState
    {
        internal float Karma;
        internal float LastKarmaTime;
        internal float LastEnforcerTime = -999999f;

        internal SectorState Clone()
        {
            return new SectorState
            {
                Karma = Karma,
                LastKarmaTime = LastKarmaTime,
                LastEnforcerTime = LastEnforcerTime
            };
        }
    }

    internal sealed class ParsedConfiguration
    {
        private readonly Action _commit;

        internal ParsedConfiguration(Action commit)
        {
            _commit = commit;
        }

        internal void Commit()
        {
            _commit();
        }
    }

    private sealed class KarmaSettings
    {
        internal KarmaGainSettings Karma = new();
        internal EnforcerSettings Enforcer = new();

        internal static KarmaSettings Default()
        {
            return new KarmaSettings();
        }
    }

    private sealed class KarmaGainSettings
    {
        internal List<float> Thresholds = new() { 60f, 120f, 180f };
        internal float DecayAfterMinutes = 15f;
        internal float DecayPerMinute = 30f;
        internal float PlayerDeathClearKarma = 100f;
        internal float Kill = 1f;
        internal float BossKill = 25f;
        internal float KarmaScaling = 0.3f;
        internal float BossKarmaScaling = 0.15f;
        internal float DungeonMultiplier = 4f;
        internal Dictionary<string, float> Prefabs = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class EnforcerSettings
    {
        internal float RequiredKarma = 40f;
        internal float ConsumeKarma = 30f;
        internal float Chance = 50f;
        internal float Cooldown = 1200f;
        internal float CheckInterval = 60f;
        internal float SpawnRadiusMin = 24f;
        internal float SpawnRadiusMax = 48f;
        internal float DungeonSpawnerSearchRadius = 32f;
        internal int LevelBonus = 2;
        internal Dictionary<string, ModifierDefinition> Modifiers = new(StringComparer.OrdinalIgnoreCase);
        internal bool ModifiersCleared;
        internal Dictionary<string, EnforcerBiomeDefinition> Biomes = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ResolvedEnforcerSettings
    {
        internal float RequiredKarma;
        internal float ConsumeKarma;
        internal float Chance;
        internal float Cooldown;
        internal bool IsBoss;
        internal bool BossHud;
        internal float SpawnRadiusMin;
        internal float SpawnRadiusMax;
        internal int LevelBonus;
        internal Dictionary<string, ModifierDefinition> Modifiers = new(StringComparer.OrdinalIgnoreCase);
        internal bool ModifiersCleared;

        internal static ResolvedEnforcerSettings FromGlobal(EnforcerSettings settings)
        {
            return new ResolvedEnforcerSettings
            {
                RequiredKarma = Mathf.Max(0f, settings.RequiredKarma),
                ConsumeKarma = Mathf.Max(0f, settings.ConsumeKarma),
                Chance = Mathf.Clamp(settings.Chance, 0f, 100f),
                Cooldown = Mathf.Max(0f, settings.Cooldown),
                IsBoss = false,
                BossHud = true,
                SpawnRadiusMin = Mathf.Max(0f, settings.SpawnRadiusMin),
                SpawnRadiusMax = Mathf.Max(settings.SpawnRadiusMin, settings.SpawnRadiusMax),
                LevelBonus = Mathf.Max(0, settings.LevelBonus),
                Modifiers = CloneModifiers(settings.Modifiers),
                ModifiersCleared = settings.ModifiersCleared
            };
        }

        internal ResolvedEnforcerSettings Clone()
        {
            return new ResolvedEnforcerSettings
            {
                RequiredKarma = RequiredKarma,
                ConsumeKarma = ConsumeKarma,
                Chance = Chance,
                Cooldown = Cooldown,
                IsBoss = IsBoss,
                BossHud = BossHud,
                SpawnRadiusMin = SpawnRadiusMin,
                SpawnRadiusMax = SpawnRadiusMax,
                LevelBonus = LevelBonus,
                Modifiers = CloneModifiers(Modifiers),
                ModifiersCleared = ModifiersCleared
            };
        }
    }

    private sealed class EnforcerOverrideSettings
    {
        internal float? RequiredKarma;
        internal float? ConsumeKarma;
        internal int? LevelBonus;
        internal Dictionary<string, ModifierDefinition>? Modifiers;
        internal bool ModifiersCleared;
    }

    private sealed class EnforcerBiomeDefinition
    {
        internal bool Enabled = true;
        internal List<EnforcerCandidateDefinition> Outdoor = new();
        internal List<EnforcerCandidateDefinition> Dungeon = new();
        internal Dictionary<string, List<EnforcerCandidateDefinition>> DungeonByLocation = new(StringComparer.OrdinalIgnoreCase);

        internal bool HasContent => !Enabled || Outdoor.Count > 0 || Dungeon.Count > 0 || DungeonByLocation.Count > 0;

        internal List<EnforcerCandidateDefinition> GetCandidates(bool dungeonSummon, string dungeonLocation)
        {
            if (!dungeonSummon)
            {
                return Outdoor;
            }

            string location = (dungeonLocation ?? "").Trim();
            if (location.Length > 0)
            {
                if (DungeonByLocation.TryGetValue(location, out List<EnforcerCandidateDefinition> exact) && exact.Count > 0)
                {
                    return exact;
                }

                string baseLocation = GetExpandWorldDataBaseLocationName(location);
                if (!string.Equals(baseLocation, location, StringComparison.OrdinalIgnoreCase) &&
                    DungeonByLocation.TryGetValue(baseLocation, out List<EnforcerCandidateDefinition> baseCandidates) &&
                    baseCandidates.Count > 0)
                {
                    return baseCandidates;
                }
            }

            return Dungeon.Count > 0 ? Dungeon : Outdoor;
        }
    }

    private sealed class EnforcerCandidateDefinition
    {
        internal EnforcerSummonSet Summon = new();
        internal float Weight = 1f;
        internal List<EnforcerLootDefinition> Loot = new();
        internal string Location = "";
        internal EnforcerOverrideSettings Override = new();
    }

    private sealed class EnforcerLootDefinition
    {
        internal string Prefab = "";
        internal int Amount;
    }

    private sealed class EnforcerSummonSet
    {
        internal string Boss = "";
        internal List<EnforcerMinionDefinition> Minions = new();
    }

    private sealed class EnforcerMinionDefinition
    {
        internal string Prefab = "";
        internal int Count = 1;
    }
}
