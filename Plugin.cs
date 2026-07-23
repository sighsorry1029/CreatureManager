using System;
using System.IO;
using System.Reflection;
using System.Timers;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace CreatureManager;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.creaturelevelcontrol")]
[BepInIncompatibility("MidnightsFX.StarLevelSystem")]
[BepInIncompatibility("RustyMods.MonsterDB")]
[BepInIncompatibility("warpalicious.MonsterModifiers")]
public class CreatureManagerPlugin : BaseUnityPlugin
{
    internal const string ModName = "CreatureManager";
    internal const string ModVersion = "1.0.3";
    internal const string Author = "sighsorry";
    internal const string ModGUID = $"{Author}.{ModName}";
    private static readonly string ConfigFileName = $"{ModGUID}.cfg";
    private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    private readonly Harmony _harmony = new(ModGUID);
    public static ManualLogSource Log { get; private set; } = null!;
    internal static readonly ConfigSync ConfigSync = new(ModGUID)
    {
        DisplayName = ModName,
        CurrentVersion = ModVersion,
        MinimumRequiredVersion = ModVersion,
        ModRequired = true
    };
    private FileSystemWatcher? _watcher;
    private readonly object _reloadLock = new();
    private DateTime _lastConfigReloadTime;
    private const long ReloadDelayTicks = TimeSpan.TicksPerSecond;
    private const float RuntimeMaintenanceInterval = 0.5f;
    private float _nextRuntimeMaintenanceTime;

    public enum Toggle
    {
        On = 1,
        Off = 0
    }

    public enum LevelBiomePreset
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        VeryHard = 3
    }

    public enum KarmaSystemMode
    {
        Off = 0,
        KarmaLevelAndEnforcer = 1,
        KarmaLevelOnly = 2,
        EnforcerOnly = 3
    }

    public enum ModifierIconLayout
    {
        FixedCategorySlots = 0,
        RightPacked = 1
    }

    public void Awake()
    {
        Log = Logger;
        bool saveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        try
        {
            LocalizationManager.Localizer.Load(_harmony);

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, Ordered("If on, the configuration is locked and can be changed by server admins only.", 100));
            NormalCreatureNameplateRange = config("1 - General", "Normal Creature Nameplate Range", 30f, Ordered("Distance in meters for normal creature nameplates and health bars. Vanilla is 10. Boss HUD range is not changed by this option.", 90, new AcceptableValueRange<float>(10f, 50f)), synchronizedSetting: false);
            ShowSneakHoverResistances = config("1 - General", "Show Sneak Hover Resistances", Toggle.On, Ordered("If on, sneaking while hovering a non-tamed creature shows non-Normal and non-Ignore damage modifiers under its nameplate. Uses Normal Creature Nameplate Range.", 80), synchronizedSetting: false);
            ModifierHudIconLayout = config("1 - General", "Modifier HUD Icon Layout", ModifierIconLayout.FixedCategorySlots, Ordered("FixedCategorySlots keeps the first Offense, Defense, Affliction, and Special icon in its category slot; forced same-category extras fill unused slots so none are hidden. RightPacked removes category gaps and packs every visible icon against the right edge of creature and boss HUDs.", 70), synchronizedSetting: false);
            GenerateSampleTextures = config("1 - General", "Generate Sample Textures", Toggle.On, Ordered("If on, bundled sample PNGs are created in CreatureManager/textures when missing. Existing files are never overwritten or deleted; Off stops automatic creation but does not disable existing textures.", 60), synchronizedSetting: false);
            EnableLevelSystem = config("2 - Levels", "Enable Level System", Toggle.On, Ordered("Master switch for CreatureManager level rules, level damage/health scaling, distance scaling, modifiers, and level visuals.", 100));
            BiomeLevelPreset = config("2 - Levels", "Biome Level Preset", LevelBiomePreset.Easy, Ordered("Built-in level weights for vanilla biome names when Enable Level System is On. levels.yml lists the biome distributions for every preset; copy or uncomment the desired preset's biome blocks to tune its difficulty as explicit overrides. Explicit biome, group, and prefab rules override this preset; Global remains the fallback for non-boss creatures and Enforcers.", 90));
            BossesFollowBiomeLevelPreset = config("2 - Levels", "Bosses Follow Biome Level Preset", Toggle.On, Ordered("If on, regular bosses can use the built-in Biome Level Preset as a level fallback when no Boss, group, or prefab level rule applies. Other omitted boss fields never fall back to Global.", 85));
            ApplyLevelScaleToSaddleableCreatures = config("2 - Levels", "Apply Level Scale To Saddle-able Creatures", Toggle.On, Ordered("If off, levels.yml scalePerLevel is not applied to creatures that can use a saddle.", 70));
            EnableGlobalModifiers = config("2 - Levels", "Global Modifiers", Toggle.On, Ordered("Master switch for modifier rolls and effects on non-boss creatures. Karma Enforcers are controlled separately by Enforcer Modifiers.", 69));
            EnableBossModifiers = config("2 - Levels", "Boss Modifiers", Toggle.On, Ordered("Master switch for modifier rolls and effects on regular boss creatures. Karma Enforcers are controlled separately by Enforcer Modifiers.", 68));
            EnableEnforcerModifiers = config("2 - Levels", "Enforcer Modifiers", Toggle.On, Ordered("Master switch for modifier rolls, effects, and modifier HUD icons on Karma Enforcers. This does not disable Enforcer summoning, level bonuses, or loot settings.", 67));
            KarmaMode = config("3 - Karma", "Karma System Mode", KarmaSystemMode.KarmaLevelAndEnforcer, Ordered("Off disables Karma runtime processing without deleting stored values. KarmaLevelAndEnforcer enables both features, KarmaLevelOnly disables Enforcer summons, and EnforcerOnly tracks Karma for summons without adding Karma levels to normal spawns.", 100));
            MaximumEnforcersPerSector = config("3 - Karma", "Maximum Enforcers Per Sector", 1, Ordered("Maximum active Enforcers allowed in the same fixed 3x3 Karma neighborhood.", 90, new AcceptableValueRange<int>(1, 20)));
            BlockEnforcerWhileBossActive = config("3 - Karma", "Block Enforcer While Boss Is Active", Toggle.On, Ordered("If on, Enforcer summons are blocked while a non-Enforcer boss is active in the same fixed 3x3 Karma neighborhood.", 80));
            BlockKarmaGainWhileBossActive = config("3 - Karma", "Block Karma Gain While Boss Is Active", Toggle.On, Ordered("If on, creature kills do not add Karma while a non-Enforcer boss is alive in the same fixed 3x3 Karma neighborhood. Killing the last active boss can still award Karma.", 75));
            BlockKarmaGainWhileEnforcerActive = config("3 - Karma", "Block Karma Gain While Enforcer Is Active", Toggle.On, Ordered("If on, creature kills do not add Karma while an Enforcer is alive in the same fixed 3x3 Karma neighborhood.", 74));
            BlockOmenEnforcerDuringCooldown = config("3 - Karma", "Block Omen Enforcer During Cooldown", Toggle.On, Ordered("If on, Omen cannot summon an Enforcer while the target Karma region's Enforcer cooldown is active. Omen still bypasses the normal summon chance and required Karma.", 73));
            ShowKarmaValueOnMinimap = config("3 - Karma", "Show Karma Value On Minimap", Toggle.On, Ordered("If on, the minimap Karma label includes the current Karma value, for example 'Karma Lv. 2 (137)'.", 70), synchronizedSetting: false);
            MultiplayerHealthIncreasePerPlayer = config("4 - Multiplayer Difficulty", "HP Increase Per Player In Multiplayer (%)", 30f, Ordered("Extra creature effective health per nearby player after the first. Vanilla is 30%. This does not increase max health directly; vanilla applies it by reducing damage taken, while floating damage text generally shows the pre-scaling damage.", 100, new AcceptableValueRange<float>(0f, 200f)));
            MultiplayerDamageIncreasePerPlayer = config("4 - Multiplayer Difficulty", "DMG Increase Per Player In Multiplayer (%)", 4f, Ordered("Extra creature damage per nearby player after the first. Vanilla is 4%.", 90, new AcceptableValueRange<float>(0f, 200f)));
            MultiplayerMaximumPlayerCount = config("4 - Multiplayer Difficulty", "Maximum Player Count For Multiplayer Scaling", 5, Ordered("Maximum nearby player count used by vanilla multiplayer difficulty scaling. Vanilla is 5.", 80, new AcceptableValueRange<int>(1, 25)));
            EnableLevelSystem.SettingChanged += ReloadLevelConfiguration;
            BiomeLevelPreset.SettingChanged += ReloadLevelConfiguration;
            NormalCreatureNameplateRange.SettingChanged += ApplyRuntimeConfigValues;
            GenerateSampleTextures.SettingChanged += ApplySampleTextureSetting;
            MultiplayerHealthIncreasePerPlayer.SettingChanged += ApplyRuntimeConfigValues;
            MultiplayerDamageIncreasePerPlayer.SettingChanged += ApplyRuntimeConfigValues;
            MultiplayerMaximumPlayerCount.SettingChanged += ApplyRuntimeConfigValues;
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            CreatureManagerFeedLikeGrandmaPokeballReleasePatch.ApplyIfAvailable(_harmony);
            CreatureServerLocalization.Initialize(ConfigSync);
            CreatureDomainManager.Initialize(ConfigSync);
            SetupWatcher();

            Config.Save();
            CreatureModifierManager.RegisterRpcs();
        }
        finally
        {
            Config.SaveOnConfigSet = saveOnSet;
        }
    }

    private static void ApplyRuntimeConfigValues(object sender, EventArgs args)
    {
        CreatureGameSettings.ApplyAll();
    }

    private static void ApplySampleTextureSetting(object sender, EventArgs args)
    {
        CreatureDomainManager.EnsureDefaultTextures();
    }

    private static void ReloadLevelConfiguration(object sender, EventArgs args)
    {
        CreatureDomainManager.RequestConfigurationReload();
    }

    private void Update()
    {
        CreatureServerLocalization.Update();
        CreatureDomainManager.Update();
        if (Time.time < _nextRuntimeMaintenanceTime)
        {
            return;
        }

        _nextRuntimeMaintenanceTime = Time.time + RuntimeMaintenanceInterval;
        CreatureManagerFeedLikeGrandmaPokeballReleasePatch.ApplyIfAvailable(_harmony, lateAttempt: true);
        CreatureKarmaManager.RegisterRpcs();
        CreatureModifierManager.RegisterRpcs();
        CreatureKarmaManager.UpdateSummons();
    }

    private void OnDestroy()
    {
        if (EnableLevelSystem != null) EnableLevelSystem.SettingChanged -= ReloadLevelConfiguration;
        if (BiomeLevelPreset != null) BiomeLevelPreset.SettingChanged -= ReloadLevelConfiguration;
        if (NormalCreatureNameplateRange != null) NormalCreatureNameplateRange.SettingChanged -= ApplyRuntimeConfigValues;
        if (GenerateSampleTextures != null) GenerateSampleTextures.SettingChanged -= ApplySampleTextureSetting;
        if (MultiplayerHealthIncreasePerPlayer != null) MultiplayerHealthIncreasePerPlayer.SettingChanged -= ApplyRuntimeConfigValues;
        if (MultiplayerDamageIncreasePerPlayer != null) MultiplayerDamageIncreasePerPlayer.SettingChanged -= ApplyRuntimeConfigValues;
        if (MultiplayerMaximumPlayerCount != null) MultiplayerMaximumPlayerCount.SettingChanged -= ApplyRuntimeConfigValues;
        SaveWithRespectToConfigSet();
        CreatureServerLocalization.Dispose();
        CreatureDomainManager.Dispose();
        CreatureManagerSpawnLifecycle.ResetRuntimeState();
        CreatureKarmaManager.ResetRuntimeState();
        CreatureModifierManager.ResetRuntimeState();
        CreatureLevelManager.ResetRuntimeState();
        _watcher?.Dispose();
        _watcher = null;
        _harmony.UnpatchSelf();
        CreatureManagerFeedLikeGrandmaPokeballReleasePatch.Reset();
        LocalizationManager.Localizer.Unload();
    }

    private void SetupWatcher()
    {
        _watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName);
        _watcher.Changed += ReadConfigValues;
        _watcher.Created += ReadConfigValues;
        _watcher.Renamed += ReadConfigValues;
        _watcher.IncludeSubdirectories = true;
        _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        _watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        DateTime now = DateTime.Now;
        long time = now.Ticks - _lastConfigReloadTime.Ticks;
        if (time < ReloadDelayTicks)
        {
            return;
        }

        lock (_reloadLock)
        {
            if (!File.Exists(ConfigFileFullPath))
            {
                Log.LogWarning("Config file does not exist. Skipping reload.");
                return;
            }

            try
            {
                Log.LogDebug("Reloading configuration...");
                SaveWithRespectToConfigSet(true);
                Log.LogInfo("Configuration reload complete.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error reloading configuration: {ex.Message}");
            }
        }

        _lastConfigReloadTime = now;
    }

    private void SaveWithRespectToConfigSet(bool reload = false)
    {
        bool originalSaveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        try
        {
            if (reload)
            {
                Config.Reload();
            }

            Config.Save();
        }
        finally
        {
            Config.SaveOnConfigSet = originalSaveOnSet;
        }
    }
    #region ConfigOptions

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    internal static ConfigEntry<float> NormalCreatureNameplateRange = null!;
    internal static ConfigEntry<Toggle> ShowSneakHoverResistances = null!;
    internal static ConfigEntry<ModifierIconLayout> ModifierHudIconLayout = null!;
    internal static ConfigEntry<Toggle> GenerateSampleTextures = null!;
    internal static ConfigEntry<Toggle> EnableLevelSystem = null!;
    internal static ConfigEntry<Toggle> ApplyLevelScaleToSaddleableCreatures = null!;
    internal static ConfigEntry<LevelBiomePreset> BiomeLevelPreset = null!;
    internal static ConfigEntry<Toggle> BossesFollowBiomeLevelPreset = null!;
    internal static ConfigEntry<Toggle> EnableGlobalModifiers = null!;
    internal static ConfigEntry<Toggle> EnableBossModifiers = null!;
    internal static ConfigEntry<Toggle> EnableEnforcerModifiers = null!;
    internal static ConfigEntry<KarmaSystemMode> KarmaMode = null!;
    internal static ConfigEntry<int> MaximumEnforcersPerSector = null!;
    internal static ConfigEntry<Toggle> BlockEnforcerWhileBossActive = null!;
    internal static ConfigEntry<Toggle> BlockKarmaGainWhileBossActive = null!;
    internal static ConfigEntry<Toggle> BlockKarmaGainWhileEnforcerActive = null!;
    internal static ConfigEntry<Toggle> BlockOmenEnforcerDuringCooldown = null!;
    internal static ConfigEntry<Toggle> ShowKarmaValueOnMinimap = null!;
    internal static ConfigEntry<float> MultiplayerHealthIncreasePerPlayer = null!;
    internal static ConfigEntry<float> MultiplayerDamageIncreasePerPlayer = null!;
    internal static ConfigEntry<int> MultiplayerMaximumPlayerCount = null!;

    private static ConfigDescription Ordered(string description, int order, AcceptableValueBase? acceptableValues = null)
    {
        return new ConfigDescription(description, acceptableValues, new object[]
        {
            new ConfigurationManagerAttributes
            {
                Order = order
            }
        });
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order = null!;
    }

    #endregion
}
