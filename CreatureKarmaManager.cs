using System;
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
    private const string CountedDeathKey = "CreatureManager_KarmaDeathCounted";
    private const string EnforcerKey = "CreatureManager_KarmaEnforcer";
    private const string EnforcerSummonedKey = "CreatureManager_KarmaEnforcerSummoned";
    private const string EnforcerNameKey = "CreatureManager_KarmaEnforcerName";
    private const string EnforcerLevelBonusKey = "CreatureManager_KarmaEnforcerLevelBonus";
    private const string EnforcerIsBossKey = "CreatureManager_KarmaEnforcerIsBoss";
    private const string EnforcerBossHudKey = "CreatureManager_KarmaEnforcerBossHud";
    private const string EnforcerLootKey = "CreatureManager_KarmaEnforcerLoot";
    private const string PlayerDeathRpc = "CreatureManager_KarmaPlayerDeath";
    private const string KarmaStatusRequestRpc = "CreatureManager_KarmaStatusRequest";
    private const string KarmaStatusResponseRpc = "CreatureManager_KarmaStatusResponse";
    private const float KarmaStatusRequestInterval = 1f;
    private const string EnforcerNameSuffix = "$cm_suffix_enforcer";
    private const string EnforcerMinionSuffix = "$cm_suffix_minion";
    private const int ZoneRadius = 1;
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
    private static readonly SectorState EmptySectorState = new();
    private static readonly HashSet<int> RuntimeSummonedCreatureIds = new();
    private static readonly Dictionary<int, ResolvedEnforcerSettings> RuntimeEnforcerSettings = new();
    private static readonly Dictionary<int, List<EnforcerLootDefinition>> RuntimeEnforcerLoot = new();
    private static readonly HashSet<int> AppliedEnforcerLootIds = new();
    private static readonly Dictionary<ZDOID, bool> ObservedPlayerDeathStates = new();
    private static KarmaSettings Settings = KarmaSettings.Default();
    private static ZRoutedRpc? RegisteredRoutedRpc;
    private static FieldInfo? ExpandWorldDataCurrentLocationField;
    private static bool ExpandWorldDataCurrentLocationFieldResolved;
    private static MethodInfo? ExpandWorldDataTryGetBiomeMethod;
    private static MethodInfo? ExpandWorldDataTryGetBiomeDisplayNameMethod;
    private static bool ExpandWorldDataBiomeMethodsResolved;
    private static float NextSummonCheckTime;
    private static readonly Dictionary<string, List<Vector3>> DungeonComponentPositionCache = new(StringComparer.Ordinal);
    private static float NextKarmaStatusRequestTime;
    private static int NextKarmaStatusRequestId;
    private static int LastKarmaStatusResponseId = -1;
    private static float ClientKarmaStatusValue;
    private static int ClientKarmaStatusLevel;
    private static bool ClientKarmaStatusValid;

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

    private static int GetMaximumEnforcersPerSector()
    {
        return Mathf.Max(1, CreatureManagerPlugin.MaximumEnforcersPerSector?.Value ?? 1);
    }

    private static bool ShouldBlockEnforcerWhileBossActive()
    {
        return CreatureManagerPlugin.BlockEnforcerWhileBossActive?.Value != CreatureManagerPlugin.Toggle.Off;
    }

    private static bool ShouldBlockKarmaGain(Character killedCharacter)
    {
        bool blockForBoss = CreatureManagerPlugin.BlockKarmaGainWhileBossActive?.Value == CreatureManagerPlugin.Toggle.On;
        bool blockForEnforcer = CreatureManagerPlugin.BlockKarmaGainWhileEnforcerActive?.Value == CreatureManagerPlugin.Toggle.On;
        if (!blockForBoss && !blockForEnforcer)
        {
            return false;
        }

        GetEnforcerBlockerState(
            killedCharacter.transform.position,
            out int activeEnforcers,
            out bool hasNonEnforcerBoss,
            killedCharacter);
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
        AppliedEnforcerLootIds.Remove(id);
    }

    internal static void ResetRuntimeState()
    {
        lock (Sync)
        {
            Sectors.Clear();
            ObservedPlayerDeathStates.Clear();
        }

        RuntimeSummonedCreatureIds.Clear();
        RuntimeEnforcerSettings.Clear();
        RuntimeEnforcerLoot.Clear();
        AppliedEnforcerLootIds.Clear();
        DungeonComponentPositionCache.Clear();
        // Registration follows the ZRoutedRpc instance lifetime. It has no unregister API and Register uses Dictionary.Add.
        NextSummonCheckTime = 0f;
        NextKarmaStatusRequestTime = 0f;
        NextKarmaStatusRequestId = 0;
        LastKarmaStatusResponseId = -1;
        ClientKarmaStatusValue = 0f;
        ClientKarmaStatusLevel = 0;
        ClientKarmaStatusValid = false;
    }

    internal static string BuildDefaultYaml()
    {
        return """
# CreatureManager Karma configuration.
# Karma uses a sliding 3x3 vanilla zone neighborhood. Kill creatures in a neighborhood to raise its Karma.
# Higher Karma can add level bonuses to future spawns in that neighborhood.
# Enforcer summons a boss-style non-boss creature with minions in a high-Karma neighborhood.
# Overlapping player-centered 3x3 windows join transitively into one connected check region.
# Each connected region rolls once; its highest-Karma eligible window supplies the table and spawn location.
# Blockers and cooldowns scan the full region, while Karma consumption and cooldown writes use that anchor window.
# Feature mode, Enforcer cap, summon blocking, and Karma-gain blocking are configured in BepInEx section '3 - Karma'.
# In any modifiers field, omission or {} keeps normal inheritance/fallback; [] is a terminal clear that blocks every lower modifier source.
# A mapping overrides listed values only. Candidates inherit Enforcer.modifiers, and Enforcer.modifiers inherits omitted values from levels.yml.

karma:
  thresholds: [60, 120, 180, 240, 300]   # Karma values reached for +1, +2, +3... level bonus.
  decay: [15, 30, 100]                   # [afterMinutes, karmaPerMinute, playerDeathClearKarma].
  gain: [1, 25, 0.3, 0.15, 4]            # [kill, bossKill, karmaScaling, bossKarmaScaling, dungeonMultiplier].
  prefabs:                               # Per-prefab Karma gain overrides.
    Troll: 5                             # This prefab grants this exact Karma amount before dungeonMultiplier.
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
  checks: [50, 1200, 60, 24~48]          # [chance% per connected-region check, cooldownSeconds, checkIntervalSeconds, outdoorSpawnRadius].
  modifiers:                             # Partial Enforcer table. Omitted values inherit levels.yml; [] blocks that fallback.
    # Offense: Enraged to Undodgeable
    enraged: 10, 0.15                    # chance%, outgoingDamageBonus.
    fire: 10, 0.2                        # chance%, addedFireDamage.
    frost: 10, 0.1                       # chance%, addedFrostDamage.
    lightning: 10, 0.1                   # chance%, addedLightningDamage.
    spirit: 10, 0.05                     # chance%, addedSpiritDoT.
    armorPiercing: 10, 0.3               # chance%, ignoredPlayerArmor.
    staggering: 10, 0.6                  # chance%, staggerBonus.
    undodgeable: 10, 0.25                # chance%, damageReduction; attacks against players ignore dodge invulnerability.
    # Defense: Armored to Chameleon
    armored: 10, 0.3                     # chance%, damageReduction.
    deathward: 10, 0.2, 10, 3            # chance%, restoredHealth, cooldownSeconds, maxActivations.
    regenerating: 10, 0.005              # chance%, maxHealthRegenPerSecond.
    reflection: 10, 0.1, 0.5             # chance%, actualMeleeDamageReflected, procChance.
    vortex: 10, 0.5                      # chance%, projectileIgnoreProc.
    adaptive: 10, 0.5                    # chance%, rememberedTypeDamageReduction.
    unflinching: 10                      # chance%; prevents normal-hit and perfect-parry stagger.
    chameleon: 10, 10                    # chance%, immunitySwitchSeconds.
    # Affliction: Exposed to ToxicDeath
    exposed: 10, 0.2, 0.5, 5             # chance%, damageTaken, proc, duration.
    weakened: 10, 0.2, 0.5, 5            # chance%, outgoingDamageReduction, proc, duration.
    withered: 10, 0.5, 0.5, 5            # chance%, healingReduction, proc, duration.
    crippling: 10, 0.5, 0.5, 0.5, 5     # chance%, moveReduction, jumpReduction, proc, duration.
    disruptive: 10, 0.5, 0.5, 0.5, 5    # chance%, staminaRegenReduction, eitrRegenReduction, proc, duration.
    adrenalineDrain: 10, 0.5, 0.5, 0.5, 5 # chance%, currentAdrenalineRemoved, adrenalineGainReduction, procChance, duration.
    corrosive: 10, 0.5, 0.5, 5           # chance%, durabilityLossBonus, procChance, duration. Equipped armor, weapons, and shield only.
    toxicDeath: 10, 0.3, 4, blob_aoe     # chance%, maxHealthDamage, radius, triggerEffect.
    # Special: Swift to Blamer
    swift: 10, 0.4                       # chance%, movementSpeedBonus.
    attackSpeed: 10, 0.3                 # chance%, attackSpeedBonus.
    vampiric: 10, 0.3                    # chance%, actualDirectDamageHealing.
    reaping: 10, 0.05, 20, 0.1, 2, 0.01, 0.2, 0.05, 0.5 # chance%, heal/base, healMaxActivations, maxHealth/base, maxHealthCap, damagePerKill, damageCap, scalePerKill, scaleCap. New scale gains are disabled in dungeons.
    blink: 10, 6, 24, fx_Adrenaline1    # chance%, cooldown, maxRange, startEffect.
    omen: 10, 0.5                        # chance%, forcedEnforcerChance.
    juggernaut: 10, 150, 5               # chance%, minimumPushForce, cooldownSeconds.
    blamer: 0, 1, 60, 0.75               # chance%, karmaPerSecond, maxKarmaGain, fleeHealthRatio. 0 cap is unlimited.

# Biome-specific Enforcer tables. Global can be used as fallback.
# Enforcer and minion AI always hunts the player.
# Non-boss Enforcers use a boss-style HUD; original boss prefabs keep their boss gameplay flag.
BlackForest:
  enabled: true                          # If false, disables Enforcer summons for this biome.
  enforcers:                             # Candidate entries may use summon, settings, weight, loot, and modifiers.
    - summon: [Greydwarf_Elite, Greydwarf:2, Greydwarf_Shaman] # [enforcerPrefab, minionPrefab[:count], ...]
      settings: [40, 30, 1]             # Optional [requiredKarma, consumeKarma, levelBonus].
      weight: 1                          # Optional weighted random chance among eligible candidates.
      loot: [TrophyGreydwarfBrute:1, Amber:3] # Optional guaranteed bonus drops as itemPrefab:amount; original creature drops are retained.
      modifiers:                         # Optional partial map; omitted entries inherit Enforcer.modifiers. {} is the same as omitting this field.
        staggering: 30, 0.6              # chance%, staggerBonus.
        deathward: 30, 0.2, 10, 3        # chance%, restored max-health ratio, cooldown seconds, max activations.
        toxicDeath: 30, 0.3, 4, blob_aoe
        juggernaut: 30, 150, 5
    - summon: [Bjorn]
      settings: [50, 40, 1]
      weight: 3
      loot: [TrophyBjorn:1, Amber:3]
      modifiers:                         # Optional partial map; omitted entries inherit Enforcer.modifiers. {} is the same as omitting this field.
        lightning: 10, 0.1
        deathward: 20, 0.2, 10, 3
        disruptive: 10, 0.5, 0.5, 0.5, 5
        blink: 10, 6, 24, fx_Adrenaline1
  dungeonEnforcers:
    - summon: [Skeleton_Poison, Skeleton:2]
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

# Location-specific dungeon Enforcer entries.
# Use location: LocationPrefab to restrict a dungeon entry to that location.
# Quote Expand World Data clone names because the colon is part of the value.
#Mountain:
#  dungeonEnforcers:
#    - summon: [Fenring_Cultist, Ulv:2]
#      location: MountainCave02
#    - summon: [AlphaWolf]
#      location: SomeModLocation
#    - summon: [AlphaWolf]
#      location: "MountainCave02:cloned"
""";
    }

    internal static bool TryParseYaml(string yaml, string source, out ParsedConfiguration parsed)
    {
        parsed = null!;
        try
        {
            KarmaSettings loaded = string.IsNullOrWhiteSpace(yaml) ? KarmaSettings.Default() : ReadSettings(yaml, source);
            parsed = new ParsedConfiguration(() => Settings = loaded);
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
            return;
        }

        if (character.IsTamed())
        {
            return;
        }

        if (ZNet.instance != null && !ZNet.instance.IsServer())
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

        zdo.Set(CountedDeathKey, true);
        HitData? lastHit = character.m_lastHit;
        Character? finalAttacker = lastHit?.GetAttacker();
        bool directPlayerKill = finalAttacker != null && finalAttacker.IsPlayer();
        bool playerSideKill = directPlayerKill ||
                              (finalAttacker != null &&
                               (finalAttacker.IsTamed() || finalAttacker.GetFaction() == Character.Faction.PlayerSpawned));
        if (!playerSideKill)
        {
            return;
        }

        if (IsEnforcer(character))
        {
            BroadcastCenterQuote(EnforcerDeathQuotes);
        }

        if (zdo.GetBool(EnforcerSummonedKey, false) || IsRuntimeSummonedCreature(character))
        {
            return;
        }

        string prefab = GetPrefabName(character);
        if (!ShouldBlockKarmaGain(character))
        {
            float amount = GetKillKarma(prefab, character.IsBoss(), character.GetLevel(), IsLikelyDungeonPosition(character.transform.position));
            if (amount > 0f)
            {
                AddKarma(character.transform.position, amount);
            }
        }

        if (directPlayerKill &&
            IsEnforcerEnabled() &&
            CreatureModifierManager.TryRollOmenEnforcerTrigger(character, out float omenChance))
        {
            bool summoned = TryForceEnforcerSummonNear(character.transform.position);
            CreatureManagerPlugin.Log.LogInfo(
                $"Karma Omen triggered by {prefab}: chance={omenChance:P0} summoned={summoned}");
        }
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
            if (!TryGetPeerReferencePosition(sender, out Vector3 position))
            {
                return;
            }

            float karma = GetKarma(position);
            ZPackage response = new();
            response.Write(requestId);
            response.Write(karma);
            response.Write(IsKarmaLevelEnabled() ? GetSectorLevelBonus(karma) : 0);
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
            if (requestId < LastKarmaStatusResponseId)
            {
                return;
            }

            LastKarmaStatusResponseId = requestId;
            ClientKarmaStatusValue = Mathf.Max(0f, karma);
            ClientKarmaStatusLevel = Mathf.Max(0, level);
            ClientKarmaStatusValid = true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to process Karma status response: {ex.Message}");
        }
    }

    private static bool TryGetPeerReferencePosition(long sender, out Vector3 position)
    {
        position = Vector3.zero;
        if (ZNet.instance == null)
        {
            return false;
        }

        ZNetPeer peer = ZNet.instance.GetPeer(sender);
        if (peer == null)
        {
            return false;
        }

        position = peer.m_refPos;
        return true;
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
        if (ZNet.instance != null && ZNet.instance.IsServer())
        {
            RefreshObservedPlayerDeathTransitions();
        }

        if (!IsEnforcerEnabled())
        {
            return;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer() || ZNetScene.instance == null)
        {
            return;
        }

        float now = Time.time;
        if (now < NextSummonCheckTime)
        {
            return;
        }

        NextSummonCheckTime = now + Mathf.Max(1f, Settings.Enforcer.CheckInterval);
        DungeonComponentPositionCache.Clear();
        List<Character> players = Character.GetAllCharacters()
            .Where(character => character != null && character.IsPlayer() && !character.IsDead())
            .ToList();

        foreach ((Character representative, Vector3 centerPosition, HashSet<string> regionZoneKeys) in BuildSummonCheckRegions(players))
        {
            TrySummonForPlayer(
                representative,
                now,
                regionPosition: centerPosition,
                regionZoneKeys: regionZoneKeys);
        }
    }

    private static List<(Character Representative, Vector3 CenterPosition, HashSet<string> RegionZoneKeys)> BuildSummonCheckRegions(
        IReadOnlyList<Character> players)
    {
        Dictionary<(int X, int Y), List<Character>> playersByCenterZone = new();
        foreach (Character player in players)
        {
            Vector2i centerZone = ZoneSystem.GetZone(player.transform.position);
            (int X, int Y) key = (centerZone.x, centerZone.y);
            if (!playersByCenterZone.TryGetValue(key, out List<Character> zonePlayers))
            {
                zonePlayers = new List<Character>();
                playersByCenterZone[key] = zonePlayers;
            }

            zonePlayers.Add(player);
        }

        List<SummonCheckWindow> windows = new();
        foreach (KeyValuePair<(int X, int Y), List<Character>> entry in playersByCenterZone)
        {
            Vector2i centerZone = new(entry.Key.X, entry.Key.Y);
            Vector3 centerPosition = ZoneSystem.GetZonePos(centerZone);
            centerPosition.y = entry.Value[0].transform.position.y;
            float karma = GetKarma(centerPosition);
            List<Character> eligiblePlayers = players
                .Where(player => IsInKarmaNeighborhood(player.transform.position, centerZone))
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
        List<(Character Representative, Vector3 CenterPosition, HashSet<string> RegionZoneKeys)> regions = new();
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

            Character representative = anchor.EligiblePlayers[UnityEngine.Random.Range(0, anchor.EligiblePlayers.Count)];
            Vector3 centerPosition = anchor.CenterPosition;
            centerPosition.y = representative.transform.position.y;
            HashSet<string> regionZoneKeys = new(StringComparer.Ordinal);
            foreach (SummonCheckWindow window in component)
            {
                regionZoneKeys.UnionWith(window.ZoneKeys);
            }

            regions.Add((representative, centerPosition, regionZoneKeys));
        }

        return regions;
    }

    private static bool HasEligibleEnforcerCandidate(Character player, float karma)
    {
        Vector3 position = player.transform.position;
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

    internal static bool TryForceEnforcerSummonNear(Vector3 position)
    {
        if (!IsEnforcerEnabled())
        {
            return false;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer() || ZNetScene.instance == null)
        {
            return false;
        }

        if (!TryFindNearestAlivePlayer(position, out Character player))
        {
            return false;
        }

        return TrySummonForPlayer(
            player,
            Time.time,
            ignoreCooldown: true,
            ignoreChance: true,
            ignoreRequiredKarma: true);
    }

    private static bool TryFindNearestAlivePlayer(Vector3 position, out Character player)
    {
        player = null!;
        float nearestDistance = float.MaxValue;
        foreach (Character character in Character.GetAllCharacters())
        {
            if (character == null || !character.IsPlayer() || character.IsDead())
            {
                continue;
            }

            float distance = Utils.DistanceXZ(character.transform.position, position);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            player = character;
        }

        return player != null;
    }

    internal static int GetLevelBonus(Character character)
    {
        if (!IsKarmaSystemEnabled() || character == null || character.IsPlayer())
        {
            return 0;
        }

        float karma = GetKarma(character.transform.position);
        int bonus = IsKarmaLevelEnabled() ? GetSectorLevelBonus(karma) : 0;
        if (IsEnforcer(character))
        {
            bonus += GetStoredEnforcerLevelBonus(character);
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
        return $"Karma zone={key} neighborhood=3x3 karma={state.Karma:0.#} bonus={bonus} activeEnforcers={GetActiveEnforcerCountInSector(position)}/{GetMaximumEnforcersPerSector()} enforcerCooldown={GetRemainingEnforcerCooldown(position):0}s";
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
            RequestKarmaStatus();
            if (!ClientKarmaStatusValid)
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
            foreach (string key in GetSectorKeys(position))
            {
                SectorState state = GetStateUnsafe(key);
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

    internal static bool TryAddBlamerKarma(Vector3 position, float amount)
    {
        if (!IsKarmaSystemEnabled() || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return false;
        }

        if (ZNet.instance != null && !ZNet.instance.IsServer())
        {
            return false;
        }

        AddKarma(position, amount);
        return true;
    }

    internal static void RefreshStoredEnforcerLoot(Character character)
    {
        if (character == null || AppliedEnforcerLootIds.Contains(character.GetInstanceID()) || !TryGetZdo(character, out ZDO zdo))
        {
            return;
        }

        if (!zdo.GetBool(EnforcerKey, false))
        {
            return;
        }

        ApplyEnforcerLoot(character, DeserializeEnforcerLoot(zdo.GetString(EnforcerLootKey, "")));
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

        zdo.Set(EnforcerKey, true);
        zdo.Set(EnforcerSummonedKey, true);
        zdo.Set(EnforcerLevelBonusKey, Mathf.Max(0, settings.LevelBonus));
        zdo.Set(EnforcerIsBossKey, settings.IsBoss);
        zdo.Set(EnforcerBossHudKey, settings.BossHud);

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
        Character player,
        float now,
        bool ignoreCooldown = false,
        bool ignoreChance = false,
        bool ignoreRequiredKarma = false,
        Vector3? regionPosition = null,
        HashSet<string>? regionZoneKeys = null)
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 statePosition = regionPosition ?? playerPosition;
        Heightmap.Biome biome = GetBiome(playerPosition);
        bool dungeonSummon = IsLikelyDungeonPosition(playerPosition);
        if (!TryGetEnforcerBiomeDefinition(biome, out EnforcerBiomeDefinition biomeDefinition))
        {
            return false;
        }

        string dungeonLocation = dungeonSummon && TryGetDungeonLocationPrefabName(playerPosition, out string resolvedDungeonLocation)
            ? resolvedDungeonLocation
            : "";
        List<EnforcerCandidateDefinition> candidates = biomeDefinition.GetCandidates(dungeonSummon, dungeonLocation);
        if (!biomeDefinition.Enabled)
        {
            return false;
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        ResolvedEnforcerSettings biomeSettings = ResolvedEnforcerSettings.FromGlobal(Settings.Enforcer);
        if (ignoreCooldown
                ? HasEnforcerBlocker(statePosition, regionZoneKeys)
                : TryRefreshEnforcerCooldownForBlocker(statePosition, now, regionZoneKeys))
        {
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
                return false;
            }
        }

        if (!ignoreChance && UnityEngine.Random.Range(0f, 100f) >= Mathf.Clamp(biomeSettings.Chance, 0f, 100f))
        {
            return false;
        }

        if (!TrySelectEnforcerCandidate(candidates, biomeSettings, karma, ignoreRequiredKarma, out EnforcerCandidateDefinition candidate, out ResolvedEnforcerSettings resolvedSettings))
        {
            return false;
        }

        EnforcerSummonSet summon = candidate.Summon;
        if (summon.Boss.Length == 0)
        {
            return false;
        }

        if (!TryFindSummonPosition(player, resolvedSettings, out Vector3 spawnPosition))
        {
            CreatureManagerPlugin.Log.LogDebug($"Karma Enforcer summon skipped: no spawn position near {playerPosition}.");
            return false;
        }

        if (!TrySpawnCreature(summon.Boss, spawnPosition, playerPosition, markEnforcer: true, EnforcerNameSuffix, resolvedSettings, candidate.Loot, out Character boss))
        {
            return false;
        }

        foreach (EnforcerMinionDefinition minion in summon.Minions)
        {
            for (int i = 0; i < minion.Count; i++)
            {
                Vector2 offset = GetMinionOffset(dungeonSummon);
                Vector3 minionPosition = spawnPosition + new Vector3(offset.x, 0f, offset.y);
                TrySpawnCreature(minion.Prefab, minionPosition, playerPosition, markEnforcer: false, EnforcerMinionSuffix, resolvedSettings, null, out _);
            }
        }

        float remainingKarma;
        lock (Sync)
        {
            remainingKarma = ApplyEnforcerCostUnsafe(statePosition, now, resolvedSettings);
        }

        CreatureManagerPlugin.Log.LogInfo($"Karma Enforcer summoned: {GetPrefabName(boss)} zone={sectorKey} karma={karma:0.#}->{remainingKarma:0.#} forced={ignoreCooldown || ignoreChance || ignoreRequiredKarma}");
        BroadcastCenterQuote(EnforcerSpawnQuotes);

        return true;
    }

    private static Vector2 GetMinionOffset(bool dungeonSummon)
    {
        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        float radius = dungeonSummon ? UnityEngine.Random.Range(1f, 2f) : UnityEngine.Random.Range(0f, 3f);
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
        HashSet<string>? regionZoneKeys = null)
    {
        activeEnforcers = 0;
        hasNonEnforcerBoss = false;
        Vector2i centerZone = ZoneSystem.GetZone(position);
        foreach (Character character in Character.GetAllCharacters())
        {
            if (character == null ||
                ReferenceEquals(character, excludedCharacter) ||
                character.IsDead() ||
                !IsInEnforcerCheckRegion(character.transform.position, centerZone, regionZoneKeys))
            {
                continue;
            }

            if (IsEnforcer(character))
            {
                activeEnforcers++;
            }
            else if (character.IsBoss())
            {
                hasNonEnforcerBoss = true;
            }
        }
    }

    private static bool IsInKarmaNeighborhood(Vector3 position, Vector2i centerZone)
    {
        Vector2i zone = ZoneSystem.GetZone(position);
        return Math.Abs(zone.x - centerZone.x) <= ZoneRadius &&
               Math.Abs(zone.y - centerZone.y) <= ZoneRadius;
    }

    private static bool IsInEnforcerCheckRegion(
        Vector3 position,
        Vector2i centerZone,
        HashSet<string>? regionZoneKeys)
    {
        return regionZoneKeys != null
            ? regionZoneKeys.Contains(GetSectorKey(position))
            : IsInKarmaNeighborhood(position, centerZone);
    }

    private static bool TryRefreshEnforcerCooldownForBlocker(
        Vector3 position,
        float now,
        HashSet<string>? regionZoneKeys = null)
    {
        if (!HasEnforcerBlocker(position, regionZoneKeys))
        {
            return false;
        }

        lock (Sync)
        {
            RefreshEnforcerCooldownUnsafe(position, now);
        }

        return true;
    }

    private static bool HasEnforcerBlocker(Vector3 position, HashSet<string>? regionZoneKeys = null)
    {
        GetEnforcerBlockerState(
            position,
            out int activeEnforcers,
            out bool hasNonEnforcerBoss,
            regionZoneKeys: regionZoneKeys);
        bool blockedByCap = activeEnforcers >= GetMaximumEnforcersPerSector();
        bool blockedByBoss = ShouldBlockEnforcerWhileBossActive() && hasNonEnforcerBoss;
        return blockedByCap || blockedByBoss;
    }

    private static bool TryFindSummonPosition(Character player, ResolvedEnforcerSettings settings, out Vector3 position)
    {
        Vector3 playerPosition = player.transform.position;
        if (IsLikelyDungeonPosition(playerPosition))
        {
            if (TryFindNearestComponentPosition<CreatureSpawner>(playerPosition, Settings.Enforcer.DungeonSpawnerSearchRadius, out position))
            {
                return true;
            }

            if (TryFindNearestComponentPosition<SpawnArea>(playerPosition, Settings.Enforcer.DungeonSpawnerSearchRadius, out position))
            {
                return true;
            }

            Vector2 dungeonOffset = UnityEngine.Random.insideUnitCircle;
            if (dungeonOffset.sqrMagnitude < 0.01f)
            {
                dungeonOffset = Vector2.right;
            }

            dungeonOffset.Normalize();
            dungeonOffset *= UnityEngine.Random.Range(6f, 12f);
            position = playerPosition + new Vector3(dungeonOffset.x, 0f, dungeonOffset.y);
            return true;
        }

        float minRadius = Mathf.Max(2f, settings.SpawnRadiusMin);
        float maxRadius = Mathf.Max(minRadius, settings.SpawnRadiusMax);
        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        float radius = UnityEngine.Random.Range(minRadius, maxRadius);
        Vector3 candidate = playerPosition + new Vector3(direction.x * radius, 0f, direction.y * radius);
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

    private static bool TryFindNearestComponentPosition<T>(Vector3 origin, float radius, out Vector3 position) where T : Component
    {
        Vector2i originZone = ZoneSystem.GetZone(origin);
        List<Vector3> candidates = GetCachedComponentPositions<T>(originZone);
        float bestDistance = Mathf.Max(0f, radius);
        Vector3 best = origin;
        bool found = false;
        foreach (Vector3 candidate in candidates)
        {
            float distance = Utils.DistanceXZ(candidate, origin);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = candidate;
            found = true;
        }

        if (!found)
        {
            position = origin;
            return false;
        }

        position = best;
        return true;
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

    private static bool TrySpawnCreature(
        string prefabName,
        Vector3 position,
        Vector3 targetPosition,
        bool markEnforcer,
        string nameSuffix,
        ResolvedEnforcerSettings settings,
        IReadOnlyList<EnforcerLootDefinition>? loot,
        out Character character)
    {
        character = null!;
        if (ZNetScene.instance == null)
        {
            return false;
        }

        GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Karma Enforcer summon skipped: missing prefab '{prefabName}'.");
            return false;
        }

        if (prefab.GetComponent<Character>() == null || CreaturePrefabRegistry.IsPlayerPrefab(prefab))
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Karma Enforcer summon skipped: prefab '{prefabName}' is not a supported non-player Character.");
            return false;
        }

        Vector3 direction = targetPosition - position;
        direction.y = 0f;
        Quaternion rotation = direction.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(direction.normalized)
            : Quaternion.identity;

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
                ApplyEnforcerLoot(character, loot);
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

    private static void ApplyEnforcerLoot(Character character, IReadOnlyList<EnforcerLootDefinition>? loot)
    {
        if (!AppliedEnforcerLootIds.Add(character.GetInstanceID()) || loot == null || loot.Count == 0 || ZNetScene.instance == null)
        {
            return;
        }

        CharacterDrop dropTable = character.GetComponent<CharacterDrop>() ?? character.gameObject.AddComponent<CharacterDrop>();
        List<CharacterDrop.Drop> drops = new();
        if (dropTable.m_drops != null)
        {
            foreach (CharacterDrop.Drop drop in dropTable.m_drops)
            {
                if (drop == null)
                {
                    continue;
                }

                drops.Add(new CharacterDrop.Drop
                {
                    m_prefab = drop.m_prefab,
                    m_amountMin = drop.m_amountMin,
                    m_amountMax = drop.m_amountMax,
                    m_chance = drop.m_chance,
                    m_onePerPlayer = drop.m_onePerPlayer,
                    m_levelMultiplier = drop.m_levelMultiplier,
                    m_dontScale = drop.m_dontScale
                });
            }
        }

        foreach (EnforcerLootDefinition reward in loot)
        {
            GameObject itemPrefab = ZNetScene.instance.GetPrefab(reward.Prefab);
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

            int remaining = reward.Amount;
            while (remaining > 0)
            {
                int amount = Math.Min(100, remaining);
                drops.Add(new CharacterDrop.Drop
                {
                    m_prefab = itemPrefab,
                    m_amountMin = amount,
                    m_amountMax = amount,
                    m_chance = 1f,
                    m_onePerPlayer = false,
                    m_levelMultiplier = false,
                    m_dontScale = true
                });
                remaining -= amount;
            }
        }

        dropTable.m_drops = drops;
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
        foreach (string token in (value ?? "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = token.LastIndexOf(':');
            if (separator <= 0 || separator >= token.Length - 1 ||
                !int.TryParse(token.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) ||
                amount <= 0)
            {
                continue;
            }

            loot.Add(new EnforcerLootDefinition
            {
                Prefab = token.Substring(0, separator).Trim(),
                Amount = amount
            });
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
        return position.y >= 4500f;
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
        Type? direct = Type.GetType($"{typeName}, {assemblyName}");
        if (direct != null)
        {
            return direct;
        }

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
            SectorState state = GetStateUnsafe(key);
            state.LastEnforcerTime = now;
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

    private static void AddKarma(Vector3 position, float amount)
    {
        float now = Time.time;
        bool levelIncreased = false;
        lock (Sync)
        {
            int previousBonus = IsKarmaLevelEnabled() ? GetSectorLevelBonus(GetBestStateUnsafe(position, out _).Karma) : 0;
            float updatedKarma = 0f;
            foreach (string key in GetSectorKeys(position))
            {
                SectorState state = GetStateUnsafe(key);
                ApplyDecayUnsafe(state, now);
                state.Karma = Mathf.Max(0f, state.Karma + amount);
                state.LastKarmaTime = now;
                updatedKarma = Mathf.Max(updatedKarma, state.Karma);
            }

            levelIncreased = IsKarmaLevelEnabled() && GetSectorLevelBonus(updatedKarma) > previousBonus;
        }

        if (levelIncreased)
        {
            BroadcastCenterQuote(KarmaLevelQuotes);
        }
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

    private static SectorState GetStateUnsafe(string key)
    {
        if (!Sectors.TryGetValue(key, out SectorState state))
        {
            state = new SectorState();
            Sectors[key] = state;
        }

        return state;
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
        return GetSectorKey(zone);
    }

    private static string GetSectorKey(Vector2i zone)
    {
        return $"{zone.x},{zone.y}";
    }

    private static IEnumerable<string> GetSectorKeys(Vector3 position)
    {
        Vector2i zone = ZoneSystem.GetZone(position);
        yield return GetSectorKey(zone);

        int radius = ZoneRadius;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                yield return GetSectorKey(new Vector2i(zone.x + x, zone.y + y));
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

    private static void BroadcastCenterQuote(IReadOnlyList<string> quotes)
    {
        if (quotes.Count == 0)
        {
            return;
        }

        string message = quotes[UnityEngine.Random.Range(0, quotes.Count)];
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player != null)
            {
                ((Character)player).Message(MessageHud.MessageType.Center, message, 0, null);
            }
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
            Summon = ReadSummonSet(summonValues),
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
            if (prefab.Length == 0)
            {
                throw new FormatException($"Karma YAML from {source} {label}.loot has an empty item prefab in '{value}'.");
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

    private static EnforcerSummonSet ReadSummonSet(List<string> values)
    {
        if (values.Count == 0)
        {
            return new EnforcerSummonSet();
        }

        return new EnforcerSummonSet
        {
            Boss = values[0],
            Minions = values.Skip(1).Select(ParseMinionDefinition).Where(minion => minion.Prefab.Length > 0 && minion.Count > 0).ToList()
        };
    }

    private static EnforcerMinionDefinition ParseMinionDefinition(string value)
    {
        string text = value.Trim();
        int count = 1;
        int separator = text.LastIndexOf(':');
        if (separator > 0 && separator < text.Length - 1 &&
            int.TryParse(text.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCount))
        {
            if (parsedCount < 1)
            {
                throw new FormatException($"Enforcer minion '{value}' must use a count of 1 or greater.");
            }

            text = text.Substring(0, separator).Trim();
            count = parsedCount;
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
        internal readonly List<Character> EligiblePlayers;
        internal readonly HashSet<string> ZoneKeys;

        internal SummonCheckWindow(
            Vector2i centerZone,
            Vector3 centerPosition,
            float karma,
            List<Character> eligiblePlayers,
            HashSet<string> zoneKeys)
        {
            CenterZone = centerZone;
            CenterPosition = centerPosition;
            Karma = karma;
            EligiblePlayers = eligiblePlayers;
            ZoneKeys = zoneKeys;
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
        internal List<float> Thresholds = new() { 60f, 120f, 180f, 240f, 300f };
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
