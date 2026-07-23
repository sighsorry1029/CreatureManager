using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Object = UnityEngine.Object;

namespace CreatureManager;

internal static class CreatureDomainManager
{
    private const long ReloadDebounceTicks = TimeSpan.TicksPerSecond / 4;
    private const long SyncedApplyDebounceTicks = TimeSpan.TicksPerSecond / 10;
    private const int SyncedYamlBundleVersion = 4;
    private const string ReferenceLogicVersion = "2026-07-21-ragdoll-visual-v2";
    private const string MainTextureProperty = "_MainTex";
    private const string RagdollCloneSuffix = "_CreatureManagerRagdoll";
    private const string DefaultTextureResourcePrefix = "CreatureManager.defaults.textures.";
    private static readonly string[] DefaultTextureFileNames =
    {
        "boar2.png",
        "DarkBrood.png",
        "DarkSpider.png",
        "DarkSpiderSmall.png",
        "goblin2.png",
        "PolarFenring.png",
        "PolarLox.png",
        "PolarWolf.png",
        "StormFenring.png",
        "SvartalfarMage.png",
        "troll2.png"
    };
    private static readonly object Sync = new();
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static ConfigSync? ConfigSync;
    private static CustomSyncedValue<string>? SyncedYamlBundle;
    private static FileSystemWatcher? Watcher;
    private static DateTime PendingDiskReloadTime = DateTime.MaxValue;
    private static DateTime PendingSyncedApplyTime = DateTime.MaxValue;
    private static DateTime PendingTextureRefreshTime = DateTime.MaxValue;
    private static bool DiskReloadPending;
    private static bool SyncedApplyPending;
    private static bool TextureRefreshPending;
    private static bool SuppressSyncedApply;
    private static bool RemoteBundleReady;
    private static List<FactionDefinition> ActiveFactionDefinitions = new();
    private static List<LevelDefinition> ActiveLevelDefinitions = new();
    private static List<AiDefinition> ActiveAiDefinitions = new();
    private static List<AttackDefinition> ActiveAttackDefinitions = new();
    private static List<ProjectileDefinition> ActiveProjectileDefinitions = new();
    private static List<CreatureDefinition> ActiveDefinitions = new();
    private static readonly Dictionary<string, Vector3> EquipmentVisualScales = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, GameObject[]> RandomHairPrefabsByCreature = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CreatureAppearanceRuntimeState> AppearanceByCreature = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CreatureAppearanceRuntimeState> InheritedAppearanceByClone = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CreatureAppearanceRuntimeState> AppearanceByRagdollPrefab = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, GameObject> ManagedRagdollPrefabs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, GameObject> RagdollCloneSources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Vector3> OriginalRagdollScales = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RagdollScaleRuntimeState> RagdollScalesByPrefab = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RagdollScaleRuntimeState> InheritedRagdollScales = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RagdollTextureRuntimeState[]> RagdollTexturesByPrefab = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RagdollTextureRuntimeState[]> InheritedRagdollTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<int> AppliedRagdollTextureVisuals = new();
    private static readonly Dictionary<(int RendererId, int MaterialIndex), TextureMaterialOverride> TextureMaterialOverrides = new();
    private static readonly MaterialPropertyBlock RagdollTexturePropertyBlock = new();
    private static bool GameDataReady;

    private sealed class TextureMaterialOverride
    {
        internal Renderer Renderer = null!;
        internal int MaterialIndex;
        internal Material Original = null!;
        internal Material Generated = null!;
        internal bool Active;
    }

    private sealed class RagdollScaleRuntimeState
    {
        internal readonly Vector3 FinalScale;
        internal readonly Vector3 EquipmentScale;

        internal RagdollScaleRuntimeState(Vector3 finalScale, Vector3 equipmentScale)
        {
            FinalScale = finalScale;
            EquipmentScale = equipmentScale;
        }
    }

    private sealed class RagdollTextureRuntimeState
    {
        internal readonly string RendererName;
        internal readonly int MaterialIndex;
        internal readonly Texture Texture;

        internal RagdollTextureRuntimeState(string rendererName, int materialIndex, Texture texture)
        {
            RendererName = rendererName;
            MaterialIndex = materialIndex;
            Texture = texture;
        }
    }

    internal sealed class CreatureAppearanceRuntimeState
    {
        internal readonly string? Hair;
        internal readonly string? Beard;
        internal readonly Vector3? HairColor;
        internal readonly Vector3? SkinColor;
        internal readonly int? ModelIndex;

        internal CreatureAppearanceRuntimeState(
            string? hair,
            string? beard,
            Vector3? hairColor,
            Vector3? skinColor,
            int? modelIndex)
        {
            Hair = hair;
            Beard = beard;
            HairColor = hairColor;
            SkinColor = skinColor;
            ModelIndex = modelIndex;
        }

        internal bool HasSpecifiedFields =>
            Hair != null ||
            Beard != null ||
            HairColor.HasValue ||
            SkinColor.HasValue ||
            ModelIndex.HasValue;
    }

    private sealed class DefinitionSnapshot
    {
        internal List<FactionDefinition> Factions = new();
        internal List<LevelDefinition> Levels = new();
        internal List<AiDefinition> Ai = new();
        internal List<AttackDefinition> Attacks = new();
        internal List<ProjectileDefinition> Projectiles = new();
        internal List<CreatureDefinition> Creatures = new();
        internal string KarmaYaml = "";
    }

    private sealed class SyncedYamlBundleData
    {
        public int Version { get; set; }
        public List<FactionDefinition>? Factions { get; set; }
        public List<LevelDefinition>? Levels { get; set; }
        public List<AiDefinition>? Ai { get; set; }
        public List<AttackDefinition>? Attacks { get; set; }
        public List<ProjectileDefinition>? Projectiles { get; set; }
        public List<CreatureDefinition>? Creatures { get; set; }
        public string? Karma { get; set; }
    }

    internal static string ConfigDirectoryPath => Path.Combine(Paths.ConfigPath, CreatureManagerPlugin.ModName);
    internal static string CacheDirectoryPath => Path.Combine(ConfigDirectoryPath, "cache");
    internal static string TextureDirectoryPath => Path.Combine(ConfigDirectoryPath, "textures");
    internal static string FactionConfigurationPath => Path.Combine(ConfigDirectoryPath, "factions.yml");
    internal static string LevelConfigurationPath => Path.Combine(ConfigDirectoryPath, "levels.yml");
    internal static string KarmaConfigurationPath => Path.Combine(ConfigDirectoryPath, "karma.yml");
    internal static string AiConfigurationPath => Path.Combine(ConfigDirectoryPath, "ai.yml");
    internal static string AiReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "ai.reference.yml");
    internal static string AttackConfigurationPath => Path.Combine(ConfigDirectoryPath, "attacks.yml");
    internal static string AttackSampleConfigurationPath => Path.Combine(ConfigDirectoryPath, "attacks.sample.yml");
    internal static string AttackReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "attacks.reference.yml");
    internal static string ProjectileConfigurationPath => Path.Combine(ConfigDirectoryPath, "projectile.yml");
    internal static string ProjectileReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "projectile.reference.yml");
    internal static string CreatureLoadoutReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "creatureLoadout.reference.txt");
    internal static string TextureReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "textures.reference.txt");
    internal static string LevelVisualReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "levelVisual.reference.yml");
    internal static string CreatureConfigurationPath => Path.Combine(ConfigDirectoryPath, "creatures.yml");
    internal static string CreatureSampleConfigurationPath => Path.Combine(ConfigDirectoryPath, "creatures.sample.yml");
    internal static string ReferenceConfigurationPath => Path.Combine(ConfigDirectoryPath, "creatures.reference.yml");
    internal static string FullScaffoldConfigurationPath => Path.Combine(ConfigDirectoryPath, "creatures.full.yml");
    private static string ReferenceStatePath => Path.Combine(CacheDirectoryPath, ".reference-state.txt");

    internal static void Initialize(ConfigSync configSync)
    {
        ConfigSync = configSync;
        RemoteBundleReady = false;
        SyncedYamlBundle = new CustomSyncedValue<string>(configSync, "YamlBundle", "", 100);
        SyncedYamlBundle.ValueChanged += RequestSyncedYamlApply;
        configSync.SourceOfTruthChanged += OnSourceOfTruthChanged;

        EnsureDirectoriesAndDefaultFiles();
        if (configSync.IsSourceOfTruth)
        {
            LoadDefinitionsFromDisk(assignSyncedValue: true);
        }
        else
        {
            RequestSyncedYamlApply();
        }
        SetupWatcher();
        CreatureConsoleCommands.Register();
    }

    internal static void Dispose()
    {
        NotifyGameDataUnavailable();

        if (SyncedYamlBundle != null)
        {
            SyncedYamlBundle.ValueChanged -= RequestSyncedYamlApply;
            SyncedYamlBundle = null;
        }

        if (ConfigSync != null)
        {
            ConfigSync.SourceOfTruthChanged -= OnSourceOfTruthChanged;
            ConfigSync = null;
        }

        DiskReloadPending = false;
        SyncedApplyPending = false;
        TextureRefreshPending = false;
        PendingDiskReloadTime = DateTime.MaxValue;
        PendingSyncedApplyTime = DateTime.MaxValue;
        PendingTextureRefreshTime = DateTime.MaxValue;
        SuppressSyncedApply = false;
        RemoteBundleReady = false;
        Watcher?.Dispose();
        Watcher = null;
        lock (Sync)
        {
            ActiveFactionDefinitions = new List<FactionDefinition>();
            ActiveLevelDefinitions = new List<LevelDefinition>();
            ActiveAiDefinitions = new List<AiDefinition>();
            ActiveAttackDefinitions = new List<AttackDefinition>();
            ActiveProjectileDefinitions = new List<ProjectileDefinition>();
            ActiveDefinitions = new List<CreatureDefinition>();
        }

        CreatureTextureRegistry.Dispose();
    }

    internal static void RequestConfigurationReload()
    {
        if (ConfigSync?.IsSourceOfTruth == false)
        {
            return;
        }

        DiskReloadPending = true;
        PendingDiskReloadTime = DateTime.UtcNow.AddTicks(ReloadDebounceTicks);
    }

    internal static void Update()
    {
        if (!DiskReloadPending && !SyncedApplyPending && !TextureRefreshPending)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (DiskReloadPending && now >= PendingDiskReloadTime)
        {
            DiskReloadPending = false;
            PendingDiskReloadTime = DateTime.MaxValue;
            TextureRefreshPending = false;
            PendingTextureRefreshTime = DateTime.MaxValue;
            if (ConfigSync?.IsSourceOfTruth != false)
            {
                LoadDefinitionsFromDisk(assignSyncedValue: true);
            }
        }

        if (SyncedApplyPending && now >= PendingSyncedApplyTime)
        {
            SyncedApplyPending = false;
            PendingSyncedApplyTime = DateTime.MaxValue;
            TextureRefreshPending = false;
            PendingTextureRefreshTime = DateTime.MaxValue;
            ApplySyncedYaml();
        }

        if (TextureRefreshPending && !DiskReloadPending && !SyncedApplyPending && now >= PendingTextureRefreshTime)
        {
            TextureRefreshPending = false;
            PendingTextureRefreshTime = DateTime.MaxValue;
            ApplyDefinitionsToGameData();
        }
    }

    internal static void NotifyGameDataAvailable()
    {
        if (!HasGameDataInstances())
        {
            return;
        }

        GameDataReady = true;
        CreatureConsoleCommands.InvalidateSpawnAutocompleteOptions();
        CreatureAssetOwnerCatalog.InvalidateMappings();
        RefreshReferenceConfigurationFilesIfNeeded();
        if (ConfigSync?.IsSourceOfTruth == false && !RemoteBundleReady)
        {
            RequestSyncedYamlApply();
        }

        ApplyDefinitionsToGameData();
    }

    internal static void NotifyGameDataUnavailable()
    {
        CreaturePrefabBaseline.RestoreAllAndClear();
        DisposeTextureOverrides();
        EquipmentVisualScales.Clear();
        RandomHairPrefabsByCreature.Clear();
        AppearanceByCreature.Clear();
        InheritedAppearanceByClone.Clear();
        AppearanceByRagdollPrefab.Clear();
        ManagedRagdollPrefabs.Clear();
        RagdollCloneSources.Clear();
        OriginalRagdollScales.Clear();
        RagdollScalesByPrefab.Clear();
        InheritedRagdollScales.Clear();
        RagdollTexturesByPrefab.Clear();
        InheritedRagdollTextures.Clear();
        AppliedRagdollTextureVisuals.Clear();
        CreaturePrefabRegistry.ResetOwnedClones();
        CreatureConsoleCommands.InvalidateSpawnAutocompleteOptions();
        CreatureAssetOwnerCatalog.InvalidateMappings();
        GameDataReady = false;
        RemoteBundleReady = false;
        SyncedApplyPending = false;
        PendingSyncedApplyTime = DateTime.MaxValue;
        TextureRefreshPending = false;
        PendingTextureRefreshTime = DateTime.MaxValue;
    }

    internal static void LoadDefinitionsFromDisk(bool assignSyncedValue)
    {
        EnsureDirectoriesAndDefaultFiles();
        if (!TryBuildDiskSnapshot(out DefinitionSnapshot snapshot) ||
            !CreatureKarmaManager.TryParseYaml(snapshot.KarmaYaml, KarmaConfigurationPath, out CreatureKarmaManager.ParsedConfiguration karma))
        {
            CreatureManagerPlugin.Log.LogWarning("Keeping the complete last-known-good CreatureManager configuration because at least one local YAML domain is invalid.");
            return;
        }

        string? serializedBundle = null;
        if (assignSyncedValue)
        {
            if (!TrySerializeAndVerifyBundle(snapshot, out serializedBundle, out DefinitionSnapshot verifiedSnapshot))
            {
                CreatureManagerPlugin.Log.LogWarning("Keeping the complete last-known-good CreatureManager configuration because the synchronized YAML bundle did not round-trip.");
                return;
            }

            snapshot = verifiedSnapshot;
            if (!CreatureKarmaManager.TryParseYaml(snapshot.KarmaYaml, "local synchronized YAML bundle", out karma))
            {
                CreatureManagerPlugin.Log.LogWarning("Keeping the complete last-known-good CreatureManager configuration because Karma did not survive the synchronized YAML bundle round-trip.");
                return;
            }
        }

        if (serializedBundle != null)
        {
            SuppressSyncedApply = true;
            try
            {
                if (SyncedYamlBundle == null)
                {
                    CreatureManagerPlugin.Log.LogError("Cannot publish the synchronized YAML bundle because ServerSync is not initialized.");
                    return;
                }

                SyncedYamlBundle.AssignLocalValue(serializedBundle);
            }
            catch (Exception ex)
            {
                CreatureManagerPlugin.Log.LogError($"Failed to publish the synchronized YAML bundle; keeping the complete last-known-good configuration: {ex.Message}");
                return;
            }
            finally
            {
                SuppressSyncedApply = false;
            }

            SyncedApplyPending = false;
            PendingSyncedApplyTime = DateTime.MaxValue;
        }

        CreatureKarmaManager.CommitParsedConfiguration(karma);
        SetActiveDefinitions(snapshot.Factions, snapshot.Levels, snapshot.Ai, snapshot.Attacks, snapshot.Projectiles, snapshot.Creatures, "local YAML bundle");
    }

    internal static bool TryWriteReferenceConfigurationFile(out string path, out string error)
    {
        path = ReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildReferenceYaml());
        CreatureManagerPlugin.Log.LogInfo($"Wrote creature reference YAML to {path}.");
        return true;
    }

    internal static bool TryWriteFullScaffoldConfigurationFile(out string path, out string error)
    {
        path = FullScaffoldConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildFullScaffoldYaml());
        CreatureManagerPlugin.Log.LogInfo($"Wrote creature full scaffold YAML to {path}.");
        return true;
    }

    internal static bool TryWriteAttackReferenceConfigurationFile(out string path, out string error)
    {
        path = AttackReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildAttackReferenceYaml());
        CreatureManagerPlugin.Log.LogInfo($"Wrote attack reference YAML to {path}.");
        return true;
    }

    internal static bool TryWriteAiReferenceConfigurationFile(out string path, out string error)
    {
        path = AiReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildAiReferenceYaml());
        CreatureManagerPlugin.Log.LogInfo($"Wrote AI reference YAML to {path}.");
        return true;
    }

    internal static bool TryWriteTextureReferenceConfigurationFile(out string path, out string error)
    {
        path = TextureReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureTextureRegistry.BuildTextureReferenceText());
        CreatureManagerPlugin.Log.LogInfo($"Wrote texture reference to {path}.");
        return true;
    }

    internal static bool TryWriteLevelVisualReferenceConfigurationFile(out string path, out string error)
    {
        path = LevelVisualReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildLevelVisualReferenceYaml());
        CreatureManagerPlugin.Log.LogInfo($"Wrote level visual reference YAML to {path}.");
        return true;
    }

    internal static bool TryWriteCreatureLoadoutReferenceConfigurationFile(out string path, out string error)
    {
        path = CreatureLoadoutReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildCreatureLoadoutReferenceText());
        CreatureManagerPlugin.Log.LogInfo($"Wrote creature loadout reference to {path}.");
        return true;
    }

    internal static bool TryWriteProjectileReferenceConfigurationFile(out string path, out string error)
    {
        path = ProjectileReferenceConfigurationPath;
        error = "";

        if (!IsGameDataReady())
        {
            error = "Creature game data is not ready yet.";
            return false;
        }

        File.WriteAllText(path, CreatureReferenceWriter.BuildProjectileReferenceYaml());
        CreatureManagerPlugin.Log.LogInfo($"Wrote projectile reference YAML to {path}.");
        return true;
    }

    private static void RefreshReferenceConfigurationFilesIfNeeded()
    {
        if (!IsGameDataReady() || ConfigSync?.IsSourceOfTruth == false)
        {
            return;
        }

        CreatureAssetOwnerCatalog.RefreshMappings();
        string creatureYaml = CreatureReferenceWriter.BuildReferenceYaml();
        string aiYaml = CreatureReferenceWriter.BuildAiReferenceYaml();
        string attackYaml = CreatureReferenceWriter.BuildAttackReferenceYaml();
        string creatureLoadoutReference = CreatureReferenceWriter.BuildCreatureLoadoutReferenceText();
        string projectileReference = CreatureReferenceWriter.BuildProjectileReferenceYaml();
        string textureReference = CreatureTextureRegistry.BuildTextureReferenceText();
        if (creatureYaml == "[]\n" && aiYaml == "[]\n" && attackYaml == "[]\n" && creatureLoadoutReference == "[]\n" && projectileReference == "[]\n" && textureReference == "[]\n")
        {
            return;
        }

        string signature = ComputeStableSignature(ReferenceLogicVersion + "\n" + creatureYaml + "\n---ai---\n" + aiYaml + "\n---attacks---\n" + attackYaml + "\n---loadout---\n" + creatureLoadoutReference + "\n---projectile---\n" + projectileReference + "\n---textures---\n" + textureReference);
        if (ReferenceFilesAreCurrent(signature))
        {
            return;
        }

        File.WriteAllText(ReferenceConfigurationPath, creatureYaml);
        File.WriteAllText(AiReferenceConfigurationPath, aiYaml);
        File.WriteAllText(AttackReferenceConfigurationPath, attackYaml);
        File.WriteAllText(CreatureLoadoutReferenceConfigurationPath, creatureLoadoutReference);
        File.WriteAllText(ProjectileReferenceConfigurationPath, projectileReference);
        File.WriteAllText(TextureReferenceConfigurationPath, textureReference);
        RecordReferenceSignature(signature);
        CreatureManagerPlugin.Log.LogInfo($"Updated reference files at {ReferenceConfigurationPath}, {AiReferenceConfigurationPath}, {AttackReferenceConfigurationPath}, {CreatureLoadoutReferenceConfigurationPath}, {ProjectileReferenceConfigurationPath}, and {TextureReferenceConfigurationPath}.");
    }

    internal static bool IsGameDataReady()
    {
        return GameDataReady || HasGameDataInstances();
    }

    private static bool HasGameDataInstances()
    {
        return ZNetScene.instance != null && ObjectDB.instance != null;
    }

    private static bool ReferenceFilesAreCurrent(string signature)
    {
        if (!File.Exists(ReferenceConfigurationPath) ||
            !File.Exists(AiReferenceConfigurationPath) ||
            !File.Exists(AttackReferenceConfigurationPath) ||
            !File.Exists(CreatureLoadoutReferenceConfigurationPath) ||
            !File.Exists(ProjectileReferenceConfigurationPath) ||
            !File.Exists(TextureReferenceConfigurationPath) ||
            !File.Exists(ReferenceStatePath))
        {
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(ReferenceStatePath);
            return lines.Length >= 2 &&
                   string.Equals(lines[0], ReferenceLogicVersion, StringComparison.Ordinal) &&
                   string.Equals(lines[1], signature, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to read reference state '{ReferenceStatePath}': {ex.Message}");
            return false;
        }
    }

    private static void RecordReferenceSignature(string signature)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectoryPath);
            File.WriteAllLines(ReferenceStatePath, new[] { ReferenceLogicVersion, signature });
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to write reference state '{ReferenceStatePath}': {ex.Message}");
        }
    }

    private static string ComputeStableSignature(string value)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }

            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }
    }

    private static void OnSourceOfTruthChanged(bool isSourceOfTruth)
    {
        if (isSourceOfTruth)
        {
            RemoteBundleReady = false;
            SyncedApplyPending = false;
            PendingSyncedApplyTime = DateTime.MaxValue;
            RefreshReferenceConfigurationFilesIfNeeded();
            LoadDefinitionsFromDisk(assignSyncedValue: true);
            return;
        }

        DiskReloadPending = false;
        PendingDiskReloadTime = DateTime.MaxValue;
        RemoteBundleReady = false;
        RequestSyncedYamlApply();
    }

    private static void ApplySyncedYaml()
    {
        if (SyncedYamlBundle == null)
        {
            return;
        }

        string source = ConfigSync?.IsSourceOfTruth == true ? "local synchronized YAML bundle" : "server synchronized YAML bundle";
        if (!TryDeserializeBundle(SyncedYamlBundle.Value, source, out DefinitionSnapshot snapshot) ||
            !CreatureKarmaManager.TryParseYaml(snapshot.KarmaYaml, source, out CreatureKarmaManager.ParsedConfiguration karma))
        {
            CreatureManagerPlugin.Log.LogWarning($"Keeping the complete last-known-good CreatureManager configuration because {source} is invalid.");
            return;
        }

        CreatureKarmaManager.CommitParsedConfiguration(karma);
        RemoteBundleReady = ConfigSync?.IsSourceOfTruth == false;
        SetActiveDefinitions(snapshot.Factions, snapshot.Levels, snapshot.Ai, snapshot.Attacks, snapshot.Projectiles, snapshot.Creatures, source);
    }

    private static bool TryBuildDiskSnapshot(out DefinitionSnapshot snapshot)
    {
        snapshot = new DefinitionSnapshot();
        bool factionsLoaded = TryLoadOverrideFiles("factions", CreatureYaml.TryReadDefinitions<FactionDefinition>, out snapshot.Factions);
        bool levelsLoaded = TryLoadLevelDefinitions(out snapshot.Levels);
        bool aiLoaded = TryLoadOverrideFiles("ai", CreatureYaml.TryReadDefinitions<AiDefinition>, out snapshot.Ai);
        bool attacksLoaded = TryLoadOverrideFiles("attacks", CreatureYaml.TryReadDefinitions<AttackDefinition>, out snapshot.Attacks);
        bool projectilesLoaded = TryLoadOverrideFiles("projectile", CreatureYaml.TryReadDefinitions<ProjectileDefinition>, out snapshot.Projectiles);
        RemoveProjectileReferenceMetadata(snapshot.Projectiles);
        bool creaturesLoaded = TryLoadOverrideFiles("creatures", CreatureYaml.TryReadDefinitions<CreatureDefinition>, out snapshot.Creatures);
        bool karmaLoaded = TryReadTextFile(KarmaConfigurationPath, "Karma", out snapshot.KarmaYaml);
        return factionsLoaded && levelsLoaded && aiLoaded && attacksLoaded && projectilesLoaded && creaturesLoaded && karmaLoaded;
    }

    private static bool TryReadTextFile(string path, string domain, out string text)
    {
        text = "";
        try
        {
            if (!File.Exists(path))
            {
                CreatureManagerPlugin.Log.LogError($"{domain} configuration was not found at {path}.");
                return false;
            }

            text = File.ReadAllText(path);
            return true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read {domain} configuration from {path}: {ex.Message}");
            return false;
        }
    }

    private static bool TrySerializeAndVerifyBundle(DefinitionSnapshot snapshot, out string serialized, out DefinitionSnapshot verified)
    {
        serialized = "";
        verified = new DefinitionSnapshot();
        try
        {
            SyncedYamlBundleData bundle = new()
            {
                Version = SyncedYamlBundleVersion,
                Factions = snapshot.Factions,
                Levels = snapshot.Levels,
                Ai = snapshot.Ai,
                Attacks = snapshot.Attacks,
                Projectiles = snapshot.Projectiles,
                Creatures = snapshot.Creatures,
                Karma = snapshot.KarmaYaml
            };
            serialized = Serializer.Serialize(bundle);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to serialize synchronized YAML bundle: {ex.Message}");
            return false;
        }

        return TryDeserializeBundle(serialized, "local synchronized YAML bundle round-trip", out verified);
    }

    private static bool TryDeserializeBundle(string yaml, string source, out DefinitionSnapshot snapshot)
    {
        snapshot = new DefinitionSnapshot();
        if (string.IsNullOrWhiteSpace(yaml))
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read {source}: the bundle is empty.");
            return false;
        }

        try
        {
            SyncedYamlBundleData? bundle = Deserializer.Deserialize<SyncedYamlBundleData>(yaml);
            if (bundle == null || bundle.Version != SyncedYamlBundleVersion)
            {
                string actualVersion = bundle == null ? "missing" : bundle.Version.ToString(CultureInfo.InvariantCulture);
                CreatureManagerPlugin.Log.LogError($"Failed to read {source}: expected bundle version {SyncedYamlBundleVersion} but got {actualVersion}.");
                return false;
            }

            if (bundle.Factions == null || bundle.Levels == null || bundle.Ai == null || bundle.Attacks == null || bundle.Projectiles == null ||
                bundle.Creatures == null || bundle.Karma == null)
            {
                CreatureManagerPlugin.Log.LogError($"Failed to read {source}: all seven configuration domains must be present.");
                return false;
            }

            if (!CreatureYaml.ValidateDefinitions(bundle.Factions, $"{source}.factions") ||
                !CreatureYaml.ValidateDefinitions(bundle.Levels, $"{source}.levels") ||
                !CreatureYaml.ValidateDefinitions(bundle.Ai, $"{source}.ai") ||
                !CreatureYaml.ValidateDefinitions(bundle.Attacks, $"{source}.attacks") ||
                !CreatureYaml.ValidateDefinitions(bundle.Projectiles, $"{source}.projectile") ||
                !CreatureYaml.ValidateDefinitions(bundle.Creatures, $"{source}.creatures"))
            {
                return false;
            }

            snapshot.Factions = bundle.Factions;
            snapshot.Levels = bundle.Levels;
            snapshot.Ai = bundle.Ai;
            snapshot.Attacks = bundle.Attacks;
            RemoveProjectileReferenceMetadata(bundle.Projectiles);
            snapshot.Projectiles = bundle.Projectiles;
            snapshot.Creatures = bundle.Creatures;
            snapshot.KarmaYaml = bundle.Karma;
            return true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read {source}: {ex.Message}");
            return false;
        }
    }

    private static void RequestSyncedYamlApply()
    {
        if (SuppressSyncedApply)
        {
            return;
        }

        SyncedApplyPending = true;
        PendingSyncedApplyTime = DateTime.UtcNow.AddTicks(SyncedApplyDebounceTicks);
    }

    private static void RemoveProjectileReferenceMetadata(IEnumerable<ProjectileDefinition> definitions)
    {
        foreach (ProjectileDefinition definition in definitions)
        {
            definition.UsedByAttacks = null;
        }
    }

    private static void SetActiveDefinitions(List<FactionDefinition> factionDefinitions, List<LevelDefinition> levelDefinitions, List<AiDefinition> aiDefinitions, List<AttackDefinition> attackDefinitions, List<ProjectileDefinition> projectileDefinitions, List<CreatureDefinition> creatureDefinitions, string source)
    {
        lock (Sync)
        {
            ActiveFactionDefinitions = factionDefinitions;
            ActiveLevelDefinitions = levelDefinitions;
            ActiveAiDefinitions = aiDefinitions;
            ActiveAttackDefinitions = attackDefinitions;
            ActiveProjectileDefinitions = projectileDefinitions;
            ActiveDefinitions = creatureDefinitions;
        }

        CreatureFactionManager.Load(factionDefinitions);
        CreatureLevelManager.Load(levelDefinitions);
        CreatureManagerPlugin.Log.LogInfo($"Loaded {factionDefinitions.Count} faction definition(s), {levelDefinitions.Count} level rule definition(s), {aiDefinitions.Count} AI definition(s), {attackDefinitions.Count} attack definition(s), {projectileDefinitions.Count} projectile definition(s), and {creatureDefinitions.Count} creature definition(s) from {source}.");
        ApplyDefinitionsToGameData();
    }

    private static void ApplyDefinitionsToGameData()
    {
        if (!CanApplyDefinitionsToGameData())
        {
            return;
        }

        CreaturePrefabBaseline.BeginApplyPass();
        ManagedRagdollPrefabs.Clear();
        CreaturePrefabRegistry.BeginCloneApplyPass();
        EquipmentVisualScales.Clear();
        RandomHairPrefabsByCreature.Clear();
        AppearanceByCreature.Clear();
        AppearanceByRagdollPrefab.Clear();
        RagdollScalesByPrefab.Clear();
        RagdollTexturesByPrefab.Clear();
        AppliedRagdollTextureVisuals.Clear();
        BeginTextureOverrideApply();
        bool applyCompleted = false;
        try
        {
            List<AiDefinition> aiDefinitions;
            List<AttackDefinition> attackDefinitions;
            List<ProjectileDefinition> projectileDefinitions;
            List<CreatureDefinition> creatureDefinitions;
            lock (Sync)
            {
                aiDefinitions = ActiveAiDefinitions.ToList();
                attackDefinitions = ActiveAttackDefinitions.ToList();
                projectileDefinitions = ActiveProjectileDefinitions.ToList();
                creatureDefinitions = ActiveDefinitions.ToList();
            }

            Dictionary<string, AiDefinition> aiDefinitionsByName = BuildAiDefinitionLookup(aiDefinitions);
            foreach (ProjectileDefinition definition in projectileDefinitions)
            {
                if (!definition.IsEnabled)
                {
                    continue;
                }

                try
                {
                    ApplyProjectileDefinition(definition);
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogError($"Failed to apply projectile definition for '{definition.Prefab}': {ex}");
                }
            }

            foreach (AttackDefinition definition in attackDefinitions)
            {
                if (!definition.IsEnabled)
                {
                    continue;
                }

                try
                {
                    ApplyAttackDefinition(definition);
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogError($"Failed to apply attack definition for '{definition.Prefab}': {ex}");
                }
            }

            foreach (CreatureDefinition definition in creatureDefinitions)
            {
                if (!definition.IsEnabled)
                {
                    continue;
                }

                try
                {
                    ApplyDefinition(definition, aiDefinitionsByName);
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogError($"Failed to apply creature definition for '{definition.Prefab}': {ex}");
                }
            }

            applyCompleted = true;
        }
        finally
        {
            CompleteTextureOverrideApply();
            if (applyCompleted)
            {
                CreaturePrefabRegistry.CompleteCloneApplyPass();
                try
                {
                    CreatureManagerRandomHairRuntime.RefreshLoadedHumanoids();
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogWarning($"Failed to refresh random hair on loaded humanoids: {ex.Message}");
                }
            }
            else
            {
                CreaturePrefabRegistry.CancelCloneApplyPass();
            }
        }
    }

    private static bool CanApplyDefinitionsToGameData()
    {
        if (!IsGameDataReady() || ZNet.instance == null)
        {
            return false;
        }

        if (ZNet.instance.IsServer())
        {
            return true;
        }

        return ConfigSync?.IsSourceOfTruth == false && RemoteBundleReady;
    }

    private static Dictionary<string, AiDefinition> BuildAiDefinitionLookup(IEnumerable<AiDefinition> definitions)
    {
        Dictionary<string, AiDefinition> lookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (AiDefinition definition in definitions)
        {
            string key = (definition.Ai ?? "").Trim();
            if (!definition.IsEnabled || key.Length == 0)
            {
                continue;
            }

            if (!HasAnyAiPresetContent(definition))
            {
                CreatureManagerPlugin.Log.LogWarning($"AI preset '{key}' has no baseAI, monsterAI, clonedFrom, or copyFrom fields. Check block names and indentation.");
            }

            lookup[key] = definition;
        }

        return lookup;
    }

    private static bool HasAnyAiPresetContent(AiDefinition definition)
    {
        return definition.BaseAI != null ||
               definition.MonsterAI != null ||
               !string.IsNullOrWhiteSpace(definition.ClonedFrom) ||
               !string.IsNullOrWhiteSpace(definition.CopyFrom);
    }

    private static void ApplyDefinition(CreatureDefinition definition, Dictionary<string, AiDefinition> aiDefinitionsByName)
    {
        if (string.IsNullOrWhiteSpace(definition.Prefab))
        {
            CreatureManagerPlugin.Log.LogWarning("Skipping creature definition without prefab.");
            return;
        }

        GameObject? prefab = ResolveTargetPrefab(definition);
        if (prefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Skipping creature '{definition.Prefab}': prefab not found.");
            return;
        }

        if (CreaturePrefabRegistry.IsPlayerPrefab(prefab))
        {
            CreatureManagerPlugin.Log.LogWarning($"Skipping creature '{definition.Prefab}': Player prefabs are not managed by CreatureManager.");
            return;
        }

        ApplyCharacter(prefab, definition.Character);
        ApplyAi(prefab, definition.Ai, aiDefinitionsByName);
        ApplyHumanoid(prefab, definition.Humanoid);
        ApplyVisEquipment(prefab, definition.Appearance, definition.ClonedFrom);
        ApplyVisual(prefab, definition.Scale, definition.Textures);
    }

    private static void ApplyAi(GameObject prefab, string? aiName, Dictionary<string, AiDefinition> aiDefinitionsByName)
    {
        string key = (aiName ?? "").Trim();
        if (key.Length == 0)
        {
            return;
        }

        if (!aiDefinitionsByName.TryGetValue(key, out AiDefinition definition))
        {
            ApplyAiFromPrefab(prefab, key);
            return;
        }

        if (definition.MonsterAI != null)
        {
            MonsterAI monsterAI = prefab.GetComponent<MonsterAI>();
            if (monsterAI == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' references MonsterAI preset '{key}' but has no MonsterAI component.");
                return;
            }

            ApplyMonsterAiDefinition(prefab, monsterAI, definition, aiDefinitionsByName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return;
        }

        MonsterAI? fallbackMonsterAI = prefab.GetComponent<MonsterAI>();
        if (fallbackMonsterAI != null)
        {
            ApplyMonsterAiDefinition(prefab, fallbackMonsterAI, definition, aiDefinitionsByName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return;
        }

        AnimalAI? fallbackAnimalAI = prefab.GetComponent<AnimalAI>();
        if (fallbackAnimalAI != null)
        {
            ApplyAnimalAiDefinition(prefab, fallbackAnimalAI, definition, aiDefinitionsByName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return;
        }

        CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' references AI preset '{key}' but has no MonsterAI or AnimalAI component.");
    }

    private static void ApplyAiFromPrefab(GameObject targetPrefab, string sourceName)
    {
        GameObject? sourcePrefab = CreaturePrefabRegistry.GetPrefab(sourceName);
        if (sourcePrefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' references unknown AI preset or prefab '{sourceName}'.");
            return;
        }

        MonsterAI? sourceMonsterAI = sourcePrefab.GetComponent<MonsterAI>();
        AnimalAI? sourceAnimalAI = sourcePrefab.GetComponent<AnimalAI>();
        MonsterAI? targetMonsterAI = targetPrefab.GetComponent<MonsterAI>();
        AnimalAI? targetAnimalAI = targetPrefab.GetComponent<AnimalAI>();

        if (sourceMonsterAI != null)
        {
            if (targetMonsterAI == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' references AI prefab '{sourceName}' with MonsterAI but target has no MonsterAI component.");
                return;
            }

            CopySupportedMonsterAiFields(sourceMonsterAI, targetMonsterAI);
            return;
        }

        if (sourceAnimalAI != null)
        {
            if (targetAnimalAI == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' references AI prefab '{sourceName}' with AnimalAI but target has no AnimalAI component.");
                return;
            }

            CopySupportedAnimalAiFields(sourceAnimalAI, targetAnimalAI);
            return;
        }

        CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' references AI prefab '{sourceName}' but source has no MonsterAI or AnimalAI component.");
    }

    private static void ApplyMonsterAiDefinition(
        GameObject targetPrefab,
        MonsterAI target,
        AiDefinition definition,
        Dictionary<string, AiDefinition> aiDefinitionsByName,
        HashSet<string> visited)
    {
        string aiName = (definition.Ai ?? "").Trim();
        if (aiName.Length == 0)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' has an AI preset without an ai name.");
            return;
        }

        if (!visited.Add(aiName))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' has a copyFrom cycle.");
            return;
        }

        string copyFrom = (definition.CopyFrom ?? "").Trim();
        string clonedFrom = (definition.ClonedFrom ?? "").Trim();
        if (copyFrom.Length > 0)
        {
            if (aiDefinitionsByName.TryGetValue(copyFrom, out AiDefinition parent))
            {
                ApplyMonsterAiDefinition(targetPrefab, target, parent, aiDefinitionsByName, visited);
            }
            else if (!TryCopyMonsterAiFromPrefab(target, copyFrom))
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' copyFrom preset or prefab '{copyFrom}' was not found.");
            }
        }
        else if (clonedFrom.Length == 0)
        {
            TryCopyMonsterAiFromPrefab(target, aiName);
        }

        if (clonedFrom.Length > 0)
        {
            GameObject? sourcePrefab = CreaturePrefabRegistry.GetPrefab(clonedFrom);
            MonsterAI? source = sourcePrefab != null ? sourcePrefab.GetComponent<MonsterAI>() : null;
            if (source == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' clonedFrom '{clonedFrom}' was not found or has no MonsterAI component.");
            }
            else
            {
                CopySupportedMonsterAiFields(source, target);
            }
        }

        ApplyBaseAiDefinition(targetPrefab, (BaseAI)target, definition.BaseAI);
        ApplyMonsterAiDefinition(targetPrefab, target, definition.MonsterAI);
    }

    private static void ApplyAnimalAiDefinition(
        GameObject targetPrefab,
        AnimalAI target,
        AiDefinition definition,
        Dictionary<string, AiDefinition> aiDefinitionsByName,
        HashSet<string> visited)
    {
        string aiName = (definition.Ai ?? "").Trim();
        if (aiName.Length == 0)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' has an AI preset without an ai name.");
            return;
        }

        if (!visited.Add(aiName))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' has a copyFrom cycle.");
            return;
        }

        if (definition.MonsterAI != null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' has a monsterAI block but is being applied as AnimalAI.");
            return;
        }

        string copyFrom = (definition.CopyFrom ?? "").Trim();
        string clonedFrom = (definition.ClonedFrom ?? "").Trim();
        if (copyFrom.Length > 0)
        {
            if (aiDefinitionsByName.TryGetValue(copyFrom, out AiDefinition parent))
            {
                ApplyAnimalAiDefinition(targetPrefab, target, parent, aiDefinitionsByName, visited);
            }
            else if (!TryCopyAnimalAiFromPrefab(target, copyFrom))
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' copyFrom preset or prefab '{copyFrom}' was not found.");
            }
        }
        else if (clonedFrom.Length == 0)
        {
            TryCopyAnimalAiFromPrefab(target, aiName);
        }

        if (clonedFrom.Length > 0)
        {
            GameObject? sourcePrefab = CreaturePrefabRegistry.GetPrefab(clonedFrom);
            AnimalAI? source = sourcePrefab != null ? sourcePrefab.GetComponent<AnimalAI>() : null;
            if (source == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{targetPrefab.name}' AI preset '{aiName}' clonedFrom '{clonedFrom}' was not found or has no AnimalAI component.");
            }
            else
            {
                CopySupportedAnimalAiFields(source, target);
            }
        }

        ApplyBaseAiDefinition(targetPrefab, (BaseAI)target, definition.BaseAI);
    }

    private static void CopySupportedMonsterAiFields(MonsterAI source, MonsterAI target)
    {
        CreaturePrefabBaseline.Capture(
            target.gameObject,
            CreaturePrefabBaselineGroup.BaseAiAll | CreaturePrefabBaselineGroup.MonsterAiAll);
        CopySupportedBaseAiFields(source, target);

        target.m_alertRange = source.m_alertRange;
        target.m_fleeIfHurtWhenTargetCantBeReached = source.m_fleeIfHurtWhenTargetCantBeReached;
        target.m_fleeUnreachableSinceAttacking = source.m_fleeUnreachableSinceAttacking;
        target.m_fleeUnreachableSinceHurt = source.m_fleeUnreachableSinceHurt;
        target.m_fleeIfNotAlerted = source.m_fleeIfNotAlerted;
        target.m_fleeIfLowHealth = source.m_fleeIfLowHealth;
        target.m_fleeTimeSinceHurt = source.m_fleeTimeSinceHurt;
        target.m_fleeInLava = source.m_fleeInLava;
        target.m_circulateWhileCharging = source.m_circulateWhileCharging;
        target.m_circulateWhileChargingFlying = source.m_circulateWhileChargingFlying;
        target.m_enableHuntPlayer = source.m_enableHuntPlayer;
        target.m_attackPlayerObjects = source.m_attackPlayerObjects;
        target.m_privateAreaTriggerTreshold = source.m_privateAreaTriggerTreshold;
        target.m_interceptTimeMax = source.m_interceptTimeMax;
        target.m_interceptTimeMin = source.m_interceptTimeMin;
        target.m_maxChaseDistance = source.m_maxChaseDistance;
        target.m_minAttackInterval = source.m_minAttackInterval;
        target.m_circleTargetInterval = source.m_circleTargetInterval;
        target.m_circleTargetDuration = source.m_circleTargetDuration;
        target.m_circleTargetDistance = source.m_circleTargetDistance;
        target.m_sleeping = source.m_sleeping;
        target.m_wakeupRange = source.m_wakeupRange;
        target.m_noiseWakeup = source.m_noiseWakeup;
        target.m_maxNoiseWakeupRange = source.m_maxNoiseWakeupRange;
        target.m_wakeUpDelayMin = source.m_wakeUpDelayMin;
        target.m_wakeUpDelayMax = source.m_wakeUpDelayMax;
        target.m_fallAsleepDistance = source.m_fallAsleepDistance;
        target.m_avoidLand = source.m_avoidLand;
    }

    private static bool TryCopyMonsterAiFromPrefab(MonsterAI target, string sourceName)
    {
        GameObject? sourcePrefab = CreaturePrefabRegistry.GetPrefab(sourceName);
        MonsterAI? source = sourcePrefab != null ? sourcePrefab.GetComponent<MonsterAI>() : null;
        if (source == null)
        {
            return false;
        }

        CopySupportedMonsterAiFields(source, target);
        return true;
    }

    private static bool TryCopyAnimalAiFromPrefab(AnimalAI target, string sourceName)
    {
        GameObject? sourcePrefab = CreaturePrefabRegistry.GetPrefab(sourceName);
        AnimalAI? source = sourcePrefab != null ? sourcePrefab.GetComponent<AnimalAI>() : null;
        if (source == null)
        {
            return false;
        }

        CopySupportedAnimalAiFields(source, target);
        return true;
    }

    private static void CopySupportedAnimalAiFields(AnimalAI source, AnimalAI target)
    {
        CreaturePrefabBaseline.Capture(target.gameObject, CreaturePrefabBaselineGroup.BaseAiAll);
        CopySupportedBaseAiFields(source, target);
    }

    private static void CopySupportedBaseAiFields(BaseAI sourceBase, BaseAI targetBase)
    {
        targetBase.m_viewRange = sourceBase.m_viewRange;
        targetBase.m_viewAngle = sourceBase.m_viewAngle;
        targetBase.m_hearRange = sourceBase.m_hearRange;
        targetBase.m_mistVision = sourceBase.m_mistVision;
        targetBase.m_idleSoundInterval = sourceBase.m_idleSoundInterval;
        targetBase.m_idleSoundChance = sourceBase.m_idleSoundChance;
        targetBase.m_pathAgentType = sourceBase.m_pathAgentType;
        targetBase.m_moveMinAngle = sourceBase.m_moveMinAngle;
        targetBase.m_smoothMovement = sourceBase.m_smoothMovement;
        targetBase.m_serpentMovement = sourceBase.m_serpentMovement;
        targetBase.m_serpentTurnRadius = sourceBase.m_serpentTurnRadius;
        targetBase.m_jumpInterval = sourceBase.m_jumpInterval;
        targetBase.m_randomCircleInterval = sourceBase.m_randomCircleInterval;
        targetBase.m_randomMoveInterval = sourceBase.m_randomMoveInterval;
        targetBase.m_randomMoveRange = sourceBase.m_randomMoveRange;
        targetBase.m_randomFly = sourceBase.m_randomFly;
        targetBase.m_chanceToTakeoff = sourceBase.m_chanceToTakeoff;
        targetBase.m_chanceToLand = sourceBase.m_chanceToLand;
        targetBase.m_groundDuration = sourceBase.m_groundDuration;
        targetBase.m_airDuration = sourceBase.m_airDuration;
        targetBase.m_maxLandAltitude = sourceBase.m_maxLandAltitude;
        targetBase.m_takeoffTime = sourceBase.m_takeoffTime;
        targetBase.m_flyAltitudeMin = sourceBase.m_flyAltitudeMin;
        targetBase.m_flyAltitudeMax = sourceBase.m_flyAltitudeMax;
        targetBase.m_flyAbsMinAltitude = sourceBase.m_flyAbsMinAltitude;
        targetBase.m_avoidFire = sourceBase.m_avoidFire;
        targetBase.m_afraidOfFire = sourceBase.m_afraidOfFire;
        targetBase.m_avoidWater = sourceBase.m_avoidWater;
        targetBase.m_avoidLava = sourceBase.m_avoidLava;
        targetBase.m_skipLavaTargets = sourceBase.m_skipLavaTargets;
        targetBase.m_avoidLavaFlee = sourceBase.m_avoidLavaFlee;
        targetBase.m_aggravatable = sourceBase.m_aggravatable;
        targetBase.m_passiveAggresive = sourceBase.m_passiveAggresive;
        targetBase.m_spawnMessage = sourceBase.m_spawnMessage;
        targetBase.m_deathMessage = sourceBase.m_deathMessage;
        targetBase.m_alertedMessage = sourceBase.m_alertedMessage;
        targetBase.m_fleeRange = sourceBase.m_fleeRange;
        targetBase.m_fleeAngle = sourceBase.m_fleeAngle;
        targetBase.m_fleeInterval = sourceBase.m_fleeInterval;
        targetBase.m_patrol = sourceBase.m_patrol;
    }

    private static void ApplyBaseAiDefinition(GameObject prefab, BaseAI ai, BaseAiDefinition? definition)
    {
        if (definition == null)
        {
            return;
        }

        CreaturePrefabBaselineGroup baselineGroups = CreaturePrefabBaselineGroup.None;
        if (definition.Senses != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiSenses;
        if (definition.IdleSound != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiIdleSound;
        if (definition.Movement != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiMovement;
        if (definition.Serpent != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiSerpent;
        if (definition.RandomMove != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiRandomMove;
        if (definition.Flight != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiFlight;
        if (definition.Avoid != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiAvoid;
        if (definition.Flee != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiFlee;
        if (definition.Aggressive != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiAggressive;
        if (definition.Messages != null) baselineGroups |= CreaturePrefabBaselineGroup.BaseAiMessages;
        CreaturePrefabBaseline.Capture(prefab, baselineGroups);

        if (TryGetStringTuple(definition.Senses, 4, prefab.name, "base.senses", out string[] senses))
        {
            if (TryParseFloat(senses[0], prefab.name, "base.senses viewRange", out float viewRange)) ai.m_viewRange = viewRange;
            if (TryParseFloat(senses[1], prefab.name, "base.senses viewAngle", out float viewAngle)) ai.m_viewAngle = viewAngle;
            if (TryParseFloat(senses[2], prefab.name, "base.senses hearRange", out float hearRange)) ai.m_hearRange = hearRange;
            if (TryParseBool(senses[3], prefab.name, "base.senses mistVision", out bool mistVision)) ai.m_mistVision = mistVision;
        }

        if (TryGetFloatTuple(definition.IdleSound, 2, prefab.name, "base.idleSound", out float[] idleSound))
        {
            ai.m_idleSoundInterval = idleSound[0];
            ai.m_idleSoundChance = idleSound[1];
        }

        if (TryGetStringTuple(definition.Movement, 4, prefab.name, "base.movement", out string[] movement))
        {
            if (TryParseBool(movement[0], prefab.name, "base.movement patrol", out bool patrol)) ai.m_patrol = patrol;
            if (!TrySetEnumField(ai, "m_pathAgentType", movement[1]))
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' base.movement pathAgentType '{movement[1]}' is invalid.");
            }

            if (TryParseFloat(movement[2], prefab.name, "base.movement moveMinAngle", out float moveMinAngle)) ai.m_moveMinAngle = moveMinAngle;
            if (TryParseBool(movement[3], prefab.name, "base.movement smoothMovement", out bool smoothMovement)) ai.m_smoothMovement = smoothMovement;
        }

        if (TryGetStringTuple(definition.Serpent, 2, prefab.name, "base.serpent", out string[] serpent))
        {
            if (TryParseBool(serpent[0], prefab.name, "base.serpent serpentMovement", out bool serpentMovement)) ai.m_serpentMovement = serpentMovement;
            if (TryParseFloat(serpent[1], prefab.name, "base.serpent serpentTurnRadius", out float serpentTurnRadius)) ai.m_serpentTurnRadius = serpentTurnRadius;
        }

        if (TryGetFloatTuple(definition.RandomMove, 4, prefab.name, "base.randomMove", out float[] randomMove))
        {
            ai.m_jumpInterval = randomMove[0];
            ai.m_randomCircleInterval = randomMove[1];
            ai.m_randomMoveInterval = randomMove[2];
            ai.m_randomMoveRange = randomMove[3];
        }

        if (TryGetStringTuple(definition.Flight, 10, prefab.name, "base.flight", out string[] flight))
        {
            if (TryParseBool(flight[0], prefab.name, "base.flight randomFly", out bool randomFly)) ai.m_randomFly = randomFly;
            if (TryParseFloat(flight[1], prefab.name, "base.flight chanceToTakeoff", out float chanceToTakeoff)) ai.m_chanceToTakeoff = chanceToTakeoff;
            if (TryParseFloat(flight[2], prefab.name, "base.flight chanceToLand", out float chanceToLand)) ai.m_chanceToLand = chanceToLand;
            if (TryParseFloat(flight[3], prefab.name, "base.flight groundDuration", out float groundDuration)) ai.m_groundDuration = groundDuration;
            if (TryParseFloat(flight[4], prefab.name, "base.flight airDuration", out float airDuration)) ai.m_airDuration = airDuration;
            if (TryParseFloat(flight[5], prefab.name, "base.flight maxLandAltitude", out float maxLandAltitude)) ai.m_maxLandAltitude = maxLandAltitude;
            if (TryParseFloat(flight[6], prefab.name, "base.flight takeoffTime", out float takeoffTime)) ai.m_takeoffTime = takeoffTime;
            if (TryParseFloat(flight[7], prefab.name, "base.flight flyAltitudeMin", out float flyAltitudeMin)) ai.m_flyAltitudeMin = flyAltitudeMin;
            if (TryParseFloat(flight[8], prefab.name, "base.flight flyAltitudeMax", out float flyAltitudeMax)) ai.m_flyAltitudeMax = flyAltitudeMax;
            if (TryParseFloat(flight[9], prefab.name, "base.flight flyAbsMinAltitude", out float flyAbsMinAltitude)) ai.m_flyAbsMinAltitude = flyAbsMinAltitude;
        }

        if (TryGetStringTuple(definition.Avoid, 6, prefab.name, "base.avoid", out string[] avoid))
        {
            if (TryParseBool(avoid[0], prefab.name, "base.avoid avoidFire", out bool avoidFire)) ai.m_avoidFire = avoidFire;
            if (TryParseBool(avoid[1], prefab.name, "base.avoid afraidOfFire", out bool afraidOfFire)) ai.m_afraidOfFire = afraidOfFire;
            if (TryParseBool(avoid[2], prefab.name, "base.avoid avoidWater", out bool avoidWater)) ai.m_avoidWater = avoidWater;
            if (TryParseBool(avoid[3], prefab.name, "base.avoid avoidLava", out bool avoidLava)) ai.m_avoidLava = avoidLava;
            if (TryParseBool(avoid[4], prefab.name, "base.avoid skipLavaTargets", out bool skipLavaTargets)) ai.m_skipLavaTargets = skipLavaTargets;
            if (TryParseBool(avoid[5], prefab.name, "base.avoid avoidLavaFlee", out bool avoidLavaFlee)) ai.m_avoidLavaFlee = avoidLavaFlee;
        }

        if (TryGetFloatTuple(definition.Flee, 3, prefab.name, "base.flee", out float[] flee))
        {
            ai.m_fleeRange = flee[0];
            ai.m_fleeAngle = flee[1];
            ai.m_fleeInterval = flee[2];
        }

        if (TryGetStringTuple(definition.Aggressive, 2, prefab.name, "base.aggressive", out string[] aggressive))
        {
            if (TryParseBool(aggressive[0], prefab.name, "base.aggressive aggravatable", out bool aggravatable)) ai.m_aggravatable = aggravatable;
            if (TryParseBool(aggressive[1], prefab.name, "base.aggressive passiveAggressive", out bool passiveAggressive)) ai.m_passiveAggresive = passiveAggressive;
        }

        if (TryGetStringTuple(definition.Messages, 3, prefab.name, "base.messages", out string[] messages))
        {
            ai.m_spawnMessage = messages[0];
            ai.m_deathMessage = messages[1];
            ai.m_alertedMessage = messages[2];
        }

    }

    private static void ApplyMonsterAiDefinition(GameObject prefab, MonsterAI ai, MonsterAiDefinition? definition)
    {
        if (definition == null)
        {
            return;
        }

        CreaturePrefabBaselineGroup baselineGroups = CreaturePrefabBaselineGroup.None;
        if (definition.AlertRange.HasValue) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiAlertRange;
        if (definition.Hunt != null) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiHunt;
        if (definition.Chase != null) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiChase;
        if (definition.Circle != null) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiCircle;
        if (definition.HurtFlee != null) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiHurtFlee;
        if (definition.Charge != null) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiCharge;
        if (definition.Sleep != null) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiSleep;
        if (definition.AvoidLand.HasValue) baselineGroups |= CreaturePrefabBaselineGroup.MonsterAiAvoidLand;
        CreaturePrefabBaseline.Capture(prefab, baselineGroups);

        if (definition.AlertRange.HasValue)
        {
            ai.m_alertRange = definition.AlertRange.Value;
        }

        if (TryGetStringTuple(definition.Hunt, 3, prefab.name, "monster.hunt", out string[] hunt))
        {
            if (TryParseBool(hunt[0], prefab.name, "monster.hunt enableHuntPlayer", out bool enableHuntPlayer)) ai.m_enableHuntPlayer = enableHuntPlayer;
            if (TryParseBool(hunt[1], prefab.name, "monster.hunt attackPlayerObjects", out bool attackPlayerObjects)) ai.m_attackPlayerObjects = attackPlayerObjects;
            if (TryParseInt(hunt[2], prefab.name, "monster.hunt privateAreaTriggerThreshold", out int privateAreaTriggerThreshold)) ai.m_privateAreaTriggerTreshold = privateAreaTriggerThreshold;
        }

        if (TryGetFloatTuple(definition.Chase, 4, prefab.name, "monster.chase", out float[] chase))
        {
            ai.m_interceptTimeMin = chase[0];
            ai.m_interceptTimeMax = chase[1];
            ai.m_maxChaseDistance = chase[2];
            ai.m_minAttackInterval = chase[3];
        }

        if (TryGetFloatTuple(definition.Circle, 3, prefab.name, "monster.circle", out float[] circle))
        {
            ai.m_circleTargetInterval = circle[0];
            ai.m_circleTargetDuration = circle[1];
            ai.m_circleTargetDistance = circle[2];
        }

        if (TryGetStringTuple(definition.HurtFlee, 7, prefab.name, "monster.hurtFlee", out string[] hurtFlee))
        {
            if (TryParseBool(hurtFlee[0], prefab.name, "monster.hurtFlee fleeIfHurtWhenTargetCantBeReached", out bool fleeIfHurt)) ai.m_fleeIfHurtWhenTargetCantBeReached = fleeIfHurt;
            if (TryParseFloat(hurtFlee[1], prefab.name, "monster.hurtFlee fleeUnreachableSinceAttacking", out float sinceAttacking)) ai.m_fleeUnreachableSinceAttacking = sinceAttacking;
            if (TryParseFloat(hurtFlee[2], prefab.name, "monster.hurtFlee fleeUnreachableSinceHurt", out float sinceHurt)) ai.m_fleeUnreachableSinceHurt = sinceHurt;
            if (TryParseBool(hurtFlee[3], prefab.name, "monster.hurtFlee fleeIfNotAlerted", out bool fleeIfNotAlerted)) ai.m_fleeIfNotAlerted = fleeIfNotAlerted;
            if (TryParseFloat(hurtFlee[4], prefab.name, "monster.hurtFlee fleeIfLowHealth", out float lowHealth)) ai.m_fleeIfLowHealth = lowHealth;
            if (TryParseFloat(hurtFlee[5], prefab.name, "monster.hurtFlee fleeTimeSinceHurt", out float timeSinceHurt)) ai.m_fleeTimeSinceHurt = timeSinceHurt;
            if (TryParseBool(hurtFlee[6], prefab.name, "monster.hurtFlee fleeInLava", out bool fleeInLava)) ai.m_fleeInLava = fleeInLava;
        }

        if (TryGetStringTuple(definition.Charge, 2, prefab.name, "monster.charge", out string[] charge))
        {
            if (TryParseBool(charge[0], prefab.name, "monster.charge circulateWhileCharging", out bool circulate)) ai.m_circulateWhileCharging = circulate;
            if (TryParseBool(charge[1], prefab.name, "monster.charge circulateWhileChargingFlying", out bool circulateFlying)) ai.m_circulateWhileChargingFlying = circulateFlying;
        }

        if (TryGetStringTuple(definition.Sleep, 7, prefab.name, "monster.sleep", out string[] sleep))
        {
            if (TryParseBool(sleep[0], prefab.name, "monster.sleep sleeping", out bool sleeping)) ai.m_sleeping = sleeping;
            if (TryParseFloat(sleep[1], prefab.name, "monster.sleep wakeupRange", out float wakeupRange)) ai.m_wakeupRange = wakeupRange;
            if (TryParseBool(sleep[2], prefab.name, "monster.sleep noiseWakeup", out bool noiseWakeup)) ai.m_noiseWakeup = noiseWakeup;
            if (TryParseFloat(sleep[3], prefab.name, "monster.sleep maxNoiseWakeupRange", out float maxNoiseWakeupRange)) ai.m_maxNoiseWakeupRange = maxNoiseWakeupRange;
            if (TryParseFloat(sleep[4], prefab.name, "monster.sleep wakeUpDelayMin", out float wakeUpDelayMin)) ai.m_wakeUpDelayMin = wakeUpDelayMin;
            if (TryParseFloat(sleep[5], prefab.name, "monster.sleep wakeUpDelayMax", out float wakeUpDelayMax)) ai.m_wakeUpDelayMax = wakeUpDelayMax;
            if (TryParseFloat(sleep[6], prefab.name, "monster.sleep fallAsleepDistance", out float fallAsleepDistance)) ai.m_fallAsleepDistance = fallAsleepDistance;
        }

        if (definition.AvoidLand.HasValue)
        {
            ai.m_avoidLand = definition.AvoidLand.Value;
        }

    }

    private static bool TryGetStringTuple(List<string>? tuple, int expectedCount, string prefabName, string label, out string[] values)
    {
        values = Array.Empty<string>();
        if (tuple == null)
        {
            return false;
        }

        string[] cleaned = tuple
            .Select(value => (value ?? "").Trim())
            .ToArray();
        if (cleaned.Length != expectedCount)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefabName}' {label} tuple expected {expectedCount} values but got {cleaned.Length}.");
            return false;
        }

        values = cleaned;
        return true;
    }

    private static bool TryGetFloatTuple(List<float>? tuple, int expectedCount, string prefabName, string label, out float[] values)
    {
        values = Array.Empty<float>();
        if (tuple == null)
        {
            return false;
        }

        if (tuple.Count != expectedCount)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefabName}' {label} tuple expected {expectedCount} values but got {tuple.Count}.");
            return false;
        }

        values = tuple.ToArray();
        return true;
    }

    private static bool TryParseBool(string token, string prefabName, string label, out bool value)
    {
        if (bool.TryParse(token, out value))
        {
            return true;
        }

        switch ((token ?? "").Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "on":
                value = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "off":
                value = false;
                return true;
            default:
                value = false;
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefabName}' {label} has invalid boolean '{token}'.");
                return false;
        }
    }

    private static bool TrySetEnumField(object target, string fieldName, string token)
    {
        System.Reflection.FieldInfo? field = target.GetType().GetField(fieldName);
        Type? enumType = field?.FieldType;
        if (field == null || enumType == null || !enumType.IsEnum)
        {
            return false;
        }

        try
        {
            object value = Enum.Parse(enumType, token, ignoreCase: true);
            field.SetValue(target, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyProjectileDefinition(ProjectileDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Prefab))
        {
            CreatureManagerPlugin.Log.LogWarning("Skipping projectile definition without prefab.");
            return;
        }

        GameObject? prefab = ResolveProjectilePrefab(definition);
        if (prefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Skipping projectile '{definition.Prefab}': prefab not found.");
            return;
        }

        bool setSpawnOnHit = definition.Projectile?.SpawnOnHitSpecified == true;
        Projectile? projectile = null;
        GameObject? spawnOnHit = null;
        if (setSpawnOnHit)
        {
            projectile = prefab.GetComponent<Projectile>();
            if (projectile == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Projectile definition '{prefab.name}' sets projectile.spawnOnHit, but the prefab has no root Projectile component.");
                return;
            }

            string? spawnOnHitName = definition.Projectile!.SpawnOnHit;
            if (spawnOnHitName != null)
            {
                spawnOnHit = CreaturePrefabRegistry.GetPrefab(spawnOnHitName);
                if (spawnOnHit == null)
                {
                    CreatureManagerPlugin.Log.LogWarning($"Projectile definition '{prefab.name}' references missing projectile.spawnOnHit prefab '{spawnOnHitName}'.");
                    return;
                }
            }
        }

        SpawnAbility? spawnAbility = null;
        GameObject[]? spawnPrefabs = null;
        if (definition.SpawnAbility?.SpawnPrefabs != null)
        {
            spawnAbility = prefab.GetComponent<SpawnAbility>();
            if (spawnAbility == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Projectile definition '{prefab.name}' sets spawnAbility.spawnPrefabs, but the prefab has no root SpawnAbility component.");
                return;
            }

            if (!CreatureYaml.TryParseSpawnPrefabEntries(
                    definition.SpawnAbility.SpawnPrefabs,
                    out List<(string PrefabName, int Weight)> weightedSpawnPrefabs,
                    out string spawnPrefabError))
            {
                CreatureManagerPlugin.Log.LogWarning($"Projectile definition '{prefab.name}' has invalid spawnAbility.spawnPrefabs: {spawnPrefabError}");
                return;
            }

            int expandedCount = weightedSpawnPrefabs.Sum(entry => entry.Weight);
            spawnPrefabs = new GameObject[expandedCount];
            int expandedIndex = 0;
            foreach ((string spawnPrefabName, int weight) in weightedSpawnPrefabs)
            {
                GameObject? spawnPrefab = CreaturePrefabRegistry.GetPrefab(spawnPrefabName);
                if (spawnPrefab == null)
                {
                    CreatureManagerPlugin.Log.LogWarning($"Projectile definition '{prefab.name}' references missing spawnAbility.spawnPrefabs prefab '{spawnPrefabName}'.");
                    return;
                }

                for (int weightIndex = 0; weightIndex < weight; ++weightIndex)
                {
                    spawnPrefabs[expandedIndex++] = spawnPrefab;
                }
            }
        }

        CreaturePrefabBaselineGroup baselineGroups = CreaturePrefabBaselineGroup.None;
        if (setSpawnOnHit) baselineGroups |= CreaturePrefabBaselineGroup.ProjectileSpawnOnHit;
        if (spawnPrefabs != null) baselineGroups |= CreaturePrefabBaselineGroup.SpawnAbilitySpawnPrefabs;
        CreaturePrefabBaseline.Capture(prefab, baselineGroups);

        if (setSpawnOnHit)
        {
            projectile!.m_spawnOnHit = spawnOnHit;
        }

        if (spawnPrefabs != null)
        {
            spawnAbility!.m_spawnPrefab = spawnPrefabs;
        }
    }

    private static GameObject? ResolveProjectilePrefab(ProjectileDefinition definition)
    {
        string targetName = definition.Prefab!;
        string sourceName = definition.ClonedFrom ?? "";
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return CreaturePrefabRegistry.GetPrefab(targetName);
        }

        GameObject? source = CreaturePrefabRegistry.GetPrefab(sourceName);
        if (source == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Projectile clone source '{sourceName}' for '{targetName}' was not found.");
            return null;
        }

        return CreaturePrefabRegistry.ClonePrefab(source, targetName);
    }

    private static void ApplyAttackDefinition(AttackDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Prefab))
        {
            CreatureManagerPlugin.Log.LogWarning("Skipping attack definition without prefab.");
            return;
        }

        GameObject? prefab = ResolveAttackPrefab(definition);
        if (prefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Skipping attack '{definition.Prefab}': prefab not found.");
            return;
        }

        ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
        if (itemDrop == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' has no ItemDrop component.");
            return;
        }

        ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
        CreaturePrefabBaselineGroup baselineGroups = CreaturePrefabBaselineGroup.None;
        if (definition.Damage != null) baselineGroups |= CreaturePrefabBaselineGroup.AttackDamage;
        if (definition.Attack != null) baselineGroups |= CreaturePrefabBaselineGroup.AttackTuple;
        if (definition.StatusEffect != null) baselineGroups |= CreaturePrefabBaselineGroup.AttackStatusEffect;
        if (definition.Projectile != null) baselineGroups |= CreaturePrefabBaselineGroup.AttackProjectile;
        if (definition.Ai != null) baselineGroups |= CreaturePrefabBaselineGroup.AttackAi;
        if (shared.m_attack == null) baselineGroups |= CreaturePrefabBaselineGroup.AttackTuple;
        CreaturePrefabBaseline.Capture(prefab, baselineGroups);

        if (shared.m_attack == null)
        {
            shared.m_attack = new Attack();
        }

        ApplyAttackDamage(shared, definition.Damage);
        ApplyAttackTuple(prefab, shared.m_attack, definition.Attack);
        ApplyAttackStatusEffectTuple(prefab, shared, definition.StatusEffect);
        ApplyProjectileTuple(prefab, shared.m_attack, definition.Projectile);
        ApplyAttackAiTuple(prefab, shared, definition.Ai);
    }

    private static GameObject? ResolveAttackPrefab(AttackDefinition definition)
    {
        string targetName = definition.Prefab!;
        string sourceName = definition.ClonedFrom ?? "";
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return CreaturePrefabRegistry.GetPrefab(targetName);
        }

        GameObject? source = CreaturePrefabRegistry.GetPrefab(sourceName);
        if (source == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack clone source '{sourceName}' for '{targetName}' was not found.");
            return null;
        }

        return CreaturePrefabRegistry.ClonePrefab(source, targetName);
    }

    private static GameObject? ResolveTargetPrefab(CreatureDefinition definition)
    {
        string targetName = definition.Prefab!;
        string sourceName = definition.ClonedFrom ?? "";
        if (string.Equals(targetName, "Player", StringComparison.OrdinalIgnoreCase))
        {
            CreatureManagerPlugin.Log.LogWarning("Creature prefab 'Player' is not managed by CreatureManager.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return CreaturePrefabRegistry.GetPrefab(targetName);
        }

        GameObject? source = CreaturePrefabRegistry.GetPrefab(sourceName);
        if (source == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Clone source '{sourceName}' for '{targetName}' was not found.");
            return null;
        }

        if (CreaturePrefabRegistry.IsPlayerPrefab(source))
        {
            CreatureManagerPlugin.Log.LogWarning($"Clone source '{sourceName}' for '{targetName}' is a Player prefab and is not managed by CreatureManager.");
            return null;
        }

        return CreaturePrefabRegistry.ClonePrefab(source, targetName);
    }

    private static void ApplyAttackDamage(ItemDrop.ItemData.SharedData shared, AttackDamageDefinition? definition)
    {
        if (definition == null)
        {
            return;
        }

        HitData.DamageTypes damages = shared.m_damages;
        ApplyDamageValue(ref damages.m_damage, definition.Damage);
        ApplyDamageValue(ref damages.m_blunt, definition.Blunt);
        ApplyDamageValue(ref damages.m_slash, definition.Slash);
        ApplyDamageValue(ref damages.m_pierce, definition.Pierce);
        ApplyDamageValue(ref damages.m_chop, definition.Chop);
        ApplyDamageValue(ref damages.m_pickaxe, definition.Pickaxe);
        ApplyDamageValue(ref damages.m_fire, definition.Fire);
        ApplyDamageValue(ref damages.m_frost, definition.Frost);
        ApplyDamageValue(ref damages.m_lightning, definition.Lightning);
        ApplyDamageValue(ref damages.m_poison, definition.Poison);
        ApplyDamageValue(ref damages.m_spirit, definition.Spirit);
        shared.m_damages = damages;

        if (definition.AttackForce.HasValue)
        {
            shared.m_attackForce = definition.AttackForce.Value;
        }

        if (definition.ToolTier.HasValue)
        {
            shared.m_toolTier = definition.ToolTier.Value;
        }
    }

    private static void ApplyDamageValue(ref float target, float? value)
    {
        if (value.HasValue)
        {
            target = value.Value;
        }
    }

    private static void ApplyAttackTuple(GameObject prefab, Attack attack, List<string>? tuple)
    {
        if (tuple == null)
        {
            return;
        }

        string[] tokens = CleanTuple(tuple);
        if (tokens.Length != 2)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' attack tuple expected '[type, animation]' but got {tokens.Length} values.");
            return;
        }

        if (Enum.TryParse(tokens[0], true, out Attack.AttackType attackType))
        {
            attack.m_attackType = attackType;
        }
        else
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' has unknown attack type '{tokens[0]}'.");
        }

        attack.m_attackAnimation = tokens[1];
    }

    private static void ApplyProjectileTuple(GameObject prefab, Attack attack, List<string>? tuple)
    {
        if (tuple == null)
        {
            return;
        }

        string[] tokens = CleanTuple(tuple);
        if (tokens.Length == 0)
        {
            return;
        }

        if (tokens.Length is < 1 or > 4)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' projectile tuple expected '[prefab, velocity, accuracy, count]' with 1-4 values but got {tokens.Length}.");
            return;
        }

        GameObject? projectile = CreaturePrefabRegistry.GetPrefab(tokens[0]);
        if (projectile == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' projectile prefab '{tokens[0]}' was not found.");
            return;
        }

        attack.m_attackProjectile = projectile;
        if (tokens.Length >= 2 && TryParseFloat(tokens[1], prefab.name, "projectile velocity", out float velocity))
        {
            attack.m_projectileVel = velocity;
        }

        if (tokens.Length >= 3 && TryParseFloat(tokens[2], prefab.name, "projectile accuracy", out float accuracy))
        {
            attack.m_projectileAccuracy = accuracy;
        }

        if (tokens.Length >= 4 && TryParseInt(tokens[3], prefab.name, "projectile count", out int count))
        {
            attack.m_projectiles = count;
        }
    }

    private static void ApplyAttackStatusEffectTuple(GameObject prefab, ItemDrop.ItemData.SharedData shared, List<string>? tuple)
    {
        if (tuple == null)
        {
            return;
        }

        string[] tokens = CleanTuple(tuple);
        if (tokens.Length != 2)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' statusEffect tuple expected '[effect, chance]' but got {tokens.Length} values.");
            return;
        }

        if (!TryParseFloat(tokens[1], prefab.name, "status effect chance", out float chance))
        {
            return;
        }

        StatusEffect? statusEffect = ResolveAttackStatusEffect(tokens[0]);
        if (statusEffect == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' status effect '{tokens[0]}' was not found in ObjectDB.");
            return;
        }

        if (chance is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' status effect chance {chance.ToString(CultureInfo.InvariantCulture)} is outside 0-1 and will be clamped.");
        }

        shared.m_attackStatusEffect = statusEffect;
        shared.m_attackStatusEffectChance = Mathf.Clamp01(chance);
    }

    private static StatusEffect? ResolveAttackStatusEffect(string effectName)
    {
        ObjectDB? objectDb = ObjectDB.instance;
        if (objectDb == null || objectDb.m_StatusEffects == null)
        {
            return null;
        }

        StatusEffect? statusEffect = objectDb.GetStatusEffect(effectName.GetStableHashCode());
        return statusEffect ?? objectDb.m_StatusEffects.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.name, effectName, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyAttackAiTuple(GameObject prefab, ItemDrop.ItemData.SharedData shared, List<float>? tuple)
    {
        if (tuple == null)
        {
            return;
        }

        if (tuple.Count != 4)
        {
            CreatureManagerPlugin.Log.LogWarning($"Attack '{prefab.name}' ai tuple expected '[interval, minRange, maxRange, maxAngle]' but got {tuple.Count} values.");
            return;
        }

        shared.m_aiAttackInterval = tuple[0];
        shared.m_aiAttackRangeMin = tuple[1];
        shared.m_aiAttackRange = tuple[2];
        shared.m_aiAttackMaxAngle = tuple[3];
    }

    private static string[] CleanTuple(IEnumerable<string> tuple)
    {
        return tuple
            .Select(value => value?.Trim() ?? "")
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private static bool TryParseFloat(string token, string prefabName, string label, out float value)
    {
        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        CreatureManagerPlugin.Log.LogWarning($"Attack '{prefabName}' has invalid {label} '{token}'.");
        return false;
    }

    private static bool TryParseInt(string token, string prefabName, string label, out int value)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        CreatureManagerPlugin.Log.LogWarning($"Attack '{prefabName}' has invalid {label} '{token}'.");
        return false;
    }

    private static void ApplyCharacter(GameObject prefab, CharacterDefinition? definition)
    {
        if (definition == null)
        {
            return;
        }

        Character character = prefab.GetComponent<Character>();
        if (character == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' has no Character component.");
            return;
        }

        CreaturePrefabBaselineGroup baselineGroups = CreaturePrefabBaselineGroup.None;
        if (definition.Name != null || definition.Faction != null) baselineGroups |= CreaturePrefabBaselineGroup.CharacterIdentity;
        if (!string.IsNullOrWhiteSpace(definition.Boss)) baselineGroups |= CreaturePrefabBaselineGroup.CharacterBoss;
        if (definition.DefeatSetGlobalKey != null) baselineGroups |= CreaturePrefabBaselineGroup.CharacterGlobalKey;
        if (!string.IsNullOrWhiteSpace(definition.Health)) baselineGroups |= CreaturePrefabBaselineGroup.CharacterHealth;
        if (definition.DamageModifiers != null) baselineGroups |= CreaturePrefabBaselineGroup.CharacterDamageModifiers;
        if (!string.IsNullOrWhiteSpace(definition.Speed)) baselineGroups |= CreaturePrefabBaselineGroup.CharacterSpeed;
        if (!string.IsNullOrWhiteSpace(definition.Jump)) baselineGroups |= CreaturePrefabBaselineGroup.CharacterJump;
        if (!string.IsNullOrWhiteSpace(definition.Swim)) baselineGroups |= CreaturePrefabBaselineGroup.CharacterSwim;
        if (!string.IsNullOrWhiteSpace(definition.Flight)) baselineGroups |= CreaturePrefabBaselineGroup.CharacterFlight;
        CreaturePrefabBaseline.Capture(prefab, baselineGroups);

        if (definition.Name != null) character.m_name = definition.Name;
        if (definition.Faction != null)
        {
            if (CreatureFactionManager.TryGetFaction(definition.Faction, out Character.Faction faction))
            {
                character.m_faction = faction;
            }
            else
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' has unknown faction '{definition.Faction}'.");
            }
        }

        ApplyBossTuple(character, definition.Boss);
        if (definition.DefeatSetGlobalKey != null) character.m_defeatSetGlobalKey = definition.DefeatSetGlobalKey;
        ApplyHealthTuple(character, definition.Health);

        ApplyDamageModifiers(character, definition.DamageModifiers);
        ApplySpeedTuple(character, definition.Speed);
        ApplyJumpTuple(character, definition.Jump);
        ApplySwimTuple(character, definition.Swim);
        ApplyFlightTuple(character, definition.Flight);
    }

    private static void ApplyBossTuple(Character character, string? tuple)
    {
        string tupleValue = tuple ?? "";
        if (string.IsNullOrWhiteSpace(tupleValue))
        {
            return;
        }

        if (!CreatureYaml.TryParseBossTuple(tupleValue, out bool boss, out bool dontHideBossHud, out string? bossEvent, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{character.name}' boss skipped: {error}");
            return;
        }

        character.m_boss = boss;
        character.m_dontHideBossHud = dontHideBossHud;
        if (bossEvent != null)
        {
            character.m_bossEvent = bossEvent;
        }
    }

    private static void ApplyHealthTuple(Character character, string? tuple)
    {
        string tupleValue = tuple ?? "";
        if (string.IsNullOrWhiteSpace(tupleValue))
        {
            return;
        }

        if (!CreatureYaml.TryParseHealthTuple(tupleValue, out float health, out float? regenAllHPTime, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{character.name}' health skipped: {error}");
            return;
        }

        character.m_health = health;
        if (regenAllHPTime.HasValue)
        {
            character.m_regenAllHPTime = regenAllHPTime.Value;
        }
    }

    private static void ApplyDamageModifiers(Character character, DamageModifiersDefinition? definition)
    {
        if (definition == null)
        {
            return;
        }

        HitData.DamageModifiers modifiers = character.m_damageModifiers;
        ApplyDamageModifier(ref modifiers.m_blunt, definition.Blunt);
        ApplyDamageModifier(ref modifiers.m_slash, definition.Slash);
        ApplyDamageModifier(ref modifiers.m_pierce, definition.Pierce);
        ApplyDamageModifier(ref modifiers.m_chop, definition.Chop);
        ApplyDamageModifier(ref modifiers.m_pickaxe, definition.Pickaxe);
        ApplyDamageModifier(ref modifiers.m_fire, definition.Fire);
        ApplyDamageModifier(ref modifiers.m_frost, definition.Frost);
        ApplyDamageModifier(ref modifiers.m_lightning, definition.Lightning);
        ApplyDamageModifier(ref modifiers.m_poison, definition.Poison);
        ApplyDamageModifier(ref modifiers.m_spirit, definition.Spirit);
        character.m_damageModifiers = modifiers;
    }

    private static void ApplyDamageModifier(ref HitData.DamageModifier target, string? value)
    {
        if (value == null)
        {
            return;
        }

        if (Enum.TryParse(value, true, out HitData.DamageModifier modifier))
        {
            target = modifier;
        }
        else
        {
            CreatureManagerPlugin.Log.LogWarning($"Unknown damage modifier '{value}'.");
        }
    }

    private static void ApplySpeedTuple(Character character, string? tuple)
    {
        string tupleValue = tuple ?? "";
        if (string.IsNullOrWhiteSpace(tupleValue))
        {
            return;
        }

        if (!CreatureYaml.TryParseFloatTuple(tupleValue, 7, "speed", out float[] values, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{character.name}' speed skipped: {error}");
            return;
        }

        character.m_crouchSpeed = values[0];
        character.m_walkSpeed = values[1];
        character.m_speed = values[2];
        character.m_turnSpeed = values[3];
        character.m_runSpeed = values[4];
        character.m_runTurnSpeed = values[5];
        character.m_acceleration = values[6];
    }

    private static void ApplyJumpTuple(Character character, string? tuple)
    {
        string tupleValue = tuple ?? "";
        if (string.IsNullOrWhiteSpace(tupleValue))
        {
            return;
        }

        if (!CreatureYaml.TryParseFloatTuple(tupleValue, 5, "jump", out float[] values, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{character.name}' jump skipped: {error}");
            return;
        }

        character.m_jumpForce = values[0];
        character.m_jumpForceForward = values[1];
        character.m_jumpForceTiredFactor = values[2];
        character.m_airControl = values[3];
        character.m_jumpStaminaUsage = values[4];
    }

    private static void ApplySwimTuple(Character character, string? tuple)
    {
        string tupleValue = tuple ?? "";
        if (string.IsNullOrWhiteSpace(tupleValue))
        {
            return;
        }

        if (!CreatureYaml.TryParseBoolFloatTuple(tupleValue, 4, "swim", out bool canSwim, out float[] values, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{character.name}' swim skipped: {error}");
            return;
        }

        character.m_canSwim = canSwim;
        character.m_swimDepth = values[0];
        character.m_swimSpeed = values[1];
        character.m_swimTurnSpeed = values[2];
        character.m_swimAcceleration = values[3];
    }

    private static void ApplyFlightTuple(Character character, string? tuple)
    {
        string tupleValue = tuple ?? "";
        if (string.IsNullOrWhiteSpace(tupleValue))
        {
            return;
        }

        if (!CreatureYaml.TryParseBoolFloatTuple(tupleValue, 3, "flight", out bool flying, out float[] values, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{character.name}' flight skipped: {error}");
            return;
        }

        character.m_flying = flying;
        character.m_flySlowSpeed = values[0];
        character.m_flyFastSpeed = values[1];
        character.m_flyTurnSpeed = values[2];
    }

    internal static bool TryGetRandomHairPrefabs(Humanoid humanoid, out IReadOnlyList<GameObject> prefabs)
    {
        if (humanoid == null)
        {
            prefabs = Array.Empty<GameObject>();
            return false;
        }

        return TryGetRandomHairPrefabs(humanoid.gameObject, out prefabs);
    }

    internal static bool TryGetRandomHairPrefabs(GameObject creature, out IReadOnlyList<GameObject> prefabs)
    {
        prefabs = Array.Empty<GameObject>();
        if (creature == null ||
            !RandomHairPrefabsByCreature.TryGetValue(GetPrefabName(creature.name), out GameObject[] values))
        {
            return false;
        }

        prefabs = values;
        return true;
    }

    internal static bool TryGetConfiguredAppearanceHairColor(Humanoid humanoid, out Vector3 color)
    {
        color = Vector3.one;
        if (!TryGetConfiguredAppearance(humanoid, out CreatureAppearanceRuntimeState appearance) ||
            !appearance.HairColor.HasValue)
        {
            return false;
        }

        color = appearance.HairColor.Value;
        return true;
    }

    internal static bool TryGetConfiguredAppearance(
        Humanoid humanoid,
        out CreatureAppearanceRuntimeState appearance)
    {
        appearance = null!;
        return humanoid != null &&
               AppearanceByCreature.TryGetValue(GetPrefabName(humanoid.gameObject.name), out appearance);
    }

    internal static bool TryGetConfiguredRagdollAppearance(
        VisEquipment visEquipment,
        out CreatureAppearanceRuntimeState appearance)
    {
        appearance = null!;
        return visEquipment != null &&
               AppearanceByRagdollPrefab.TryGetValue(
                   GetPrefabName(visEquipment.gameObject.name),
                   out appearance);
    }

    internal static Vector3 GetInheritedAppearanceHairColor(Humanoid humanoid)
    {
        if (humanoid == null)
        {
            return Vector3.one;
        }

        GameObject? prefab = CreaturePrefabRegistry.GetPrefab(GetPrefabName(humanoid.gameObject.name));
        VisEquipment? visEquipment = prefab != null ? prefab.GetComponent<VisEquipment>() : null;
        return visEquipment?.m_hairColor ?? Vector3.one;
    }

    internal static bool IsConfirmedHairItemPrefab(GameObject itemPrefab)
    {
        if (itemPrefab == null)
        {
            return false;
        }

        ItemDrop? itemDrop = itemPrefab.GetComponent<ItemDrop>();
        ItemDrop.ItemData.SharedData? shared = itemDrop?.m_itemData?.m_shared;
        return shared != null &&
               shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet &&
               !string.IsNullOrWhiteSpace(shared.m_name) &&
               shared.m_name.StartsWith("$customization_hair", StringComparison.OrdinalIgnoreCase) &&
               HasAttachableHeadVisual(itemPrefab);
    }

    internal static bool IsConfirmedHairOnly(IEnumerable<GameObject>? itemPrefabs)
    {
        if (itemPrefabs == null)
        {
            return false;
        }

        bool foundAny = false;
        foreach (GameObject itemPrefab in itemPrefabs)
        {
            if (itemPrefab == null || !IsConfirmedHairItemPrefab(itemPrefab))
            {
                return false;
            }

            foundAny = true;
        }

        return foundAny;
    }

    private static void ApplyHumanoid(GameObject prefab, HumanoidDefinition? definition)
    {
        if (definition == null)
        {
            return;
        }

        Humanoid humanoid = prefab.GetComponent<Humanoid>();
        if (humanoid == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' has no Humanoid component.");
            return;
        }

        bool clearInheritedRandomHairArmor = definition.RandomHair != null &&
                                             definition.RandomArmor == null &&
                                             IsConfirmedHairOnly(humanoid.m_randomArmor);
        CreaturePrefabBaselineGroup baselineGroups = CreaturePrefabBaselineGroup.None;
        if (definition.DefaultItems != null) baselineGroups |= CreaturePrefabBaselineGroup.HumanoidDefaultItems;
        if (definition.RandomWeapon != null) baselineGroups |= CreaturePrefabBaselineGroup.HumanoidRandomWeapon;
        if (definition.RandomArmor != null || clearInheritedRandomHairArmor) baselineGroups |= CreaturePrefabBaselineGroup.HumanoidRandomArmor;
        if (definition.RandomShield != null) baselineGroups |= CreaturePrefabBaselineGroup.HumanoidRandomShield;
        if (definition.RandomItems != null) baselineGroups |= CreaturePrefabBaselineGroup.HumanoidRandomItems;
        if (definition.RandomSets != null) baselineGroups |= CreaturePrefabBaselineGroup.HumanoidRandomSets;
        CreaturePrefabBaseline.Capture(prefab, baselineGroups);

        if (definition.DefaultItems != null)
        {
            humanoid.m_defaultItems = BuildDefaultItems(prefab, definition.DefaultItems).ToArray();
        }

        if (definition.RandomWeapon != null)
        {
            humanoid.m_randomWeapon = ResolveItemPrefabs(prefab, "randomWeapon", definition.RandomWeapon).ToArray();
        }

        if (definition.RandomArmor != null)
        {
            humanoid.m_randomArmor = ResolveItemPrefabs(prefab, "randomArmor", definition.RandomArmor).ToArray();
        }
        else if (clearInheritedRandomHairArmor)
        {
            humanoid.m_randomArmor = Array.Empty<GameObject>();
        }

        if (definition.RandomHair != null)
        {
            RandomHairPrefabsByCreature[GetPrefabName(prefab.name)] =
                ResolveRandomHairPrefabs(prefab, definition.RandomHair).ToArray();
        }

        if (definition.RandomShield != null)
        {
            humanoid.m_randomShield = ResolveItemPrefabs(prefab, "randomShield", definition.RandomShield).ToArray();
        }

        if (definition.RandomItems != null)
        {
            humanoid.m_randomItems = BuildRandomItems(prefab, definition.RandomItems).ToArray();
        }

        if (definition.RandomSets != null)
        {
            humanoid.m_randomSets = BuildRandomSets(prefab, definition.RandomSets).ToArray();
        }
    }

    private static List<GameObject> ResolveRandomHairPrefabs(GameObject creaturePrefab, IEnumerable<string> itemNames)
    {
        List<GameObject> itemPrefabs = ResolveItemPrefabs(creaturePrefab, "randomHair", itemNames);
        for (int index = itemPrefabs.Count - 1; index >= 0; index--)
        {
            GameObject itemPrefab = itemPrefabs[index];
            GameObject? objectDbPrefab = ObjectDB.instance?.GetItemPrefab(itemPrefab.name);
            if (objectDbPrefab == null)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Creature '{creaturePrefab.name}' humanoid.randomHair item prefab '{itemPrefab.name}' is not registered in ObjectDB and was skipped.");
                itemPrefabs.RemoveAt(index);
                continue;
            }

            if (HasAttachableHeadVisual(objectDbPrefab))
            {
                itemPrefabs[index] = objectDbPrefab;
                continue;
            }

            CreatureManagerPlugin.Log.LogWarning(
                $"Creature '{creaturePrefab.name}' humanoid.randomHair item prefab '{itemPrefab.name}' has no direct attach or attach_skin visual and was skipped.");
            itemPrefabs.RemoveAt(index);
        }

        return itemPrefabs;
    }

    private static bool HasAttachableHeadVisual(GameObject itemPrefab)
    {
        if (itemPrefab == null)
        {
            return false;
        }

        Transform transform = itemPrefab.transform;
        for (int index = 0; index < transform.childCount; index++)
        {
            string childName = transform.GetChild(index).name;
            if (childName.Equals("attach", StringComparison.Ordinal) ||
                childName.Equals("attach_skin", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<GameObject> ResolveItemPrefabs(GameObject creaturePrefab, string fieldName, IEnumerable<string> itemNames)
    {
        List<GameObject> itemPrefabs = new();
        foreach (string itemName in itemNames)
        {
            string prefabName = itemName.Trim();
            if (prefabName.Length == 0)
            {
                continue;
            }

            GameObject? itemPrefab = ResolveItemPrefab(creaturePrefab, fieldName, prefabName);
            if (itemPrefab != null)
            {
                itemPrefabs.Add(itemPrefab);
            }
        }

        return itemPrefabs;
    }

    private static GameObject? ResolveItemPrefab(GameObject creaturePrefab, string fieldName, string prefabName)
    {
        GameObject? itemPrefab = CreaturePrefabRegistry.GetPrefab(prefabName);
        if (itemPrefab == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{creaturePrefab.name}' humanoid.{fieldName} item prefab '{prefabName}' was not found.");
            return null;
        }

        if (itemPrefab.GetComponent<ItemDrop>() == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{creaturePrefab.name}' humanoid.{fieldName} item prefab '{prefabName}' has no ItemDrop component.");
            return null;
        }

        return itemPrefab;
    }

    private static List<Humanoid.RandomItem> BuildRandomItems(GameObject creaturePrefab, IEnumerable<string> lines)
    {
        List<Humanoid.RandomItem> randomItems = new();
        foreach (string line in lines)
        {
            if (!CreatureYaml.TryParseRandomItemTuple(line, out string prefabName, out float chance, out string error))
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{creaturePrefab.name}' humanoid.randomItems entry skipped: {error}");
                continue;
            }

            GameObject? itemPrefab = ResolveItemPrefab(creaturePrefab, "randomItems", prefabName);
            if (itemPrefab == null)
            {
                continue;
            }

            randomItems.Add(new Humanoid.RandomItem
            {
                m_prefab = itemPrefab,
                m_chance = chance
            });
        }

        return randomItems;
    }

    private static List<Humanoid.ItemSet> BuildRandomSets(GameObject creaturePrefab, IEnumerable<string> lines)
    {
        List<Humanoid.ItemSet> randomSets = new();
        foreach (string line in lines)
        {
            if (!CreatureYaml.TryParseRandomSetTuple(line, out string setName, out string[] itemNames, out string error))
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{creaturePrefab.name}' humanoid.randomSets entry skipped: {error}");
                continue;
            }

            GameObject[] itemPrefabs = ResolveItemPrefabs(creaturePrefab, "randomSets", itemNames).ToArray();
            if (itemPrefabs.Length == 0)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{creaturePrefab.name}' humanoid.randomSets entry '{setName}' has no resolved item prefabs.");
                continue;
            }

            randomSets.Add(new Humanoid.ItemSet
            {
                m_name = setName,
                m_items = itemPrefabs
            });
        }

        return randomSets;
    }

    private static List<GameObject> BuildDefaultItems(GameObject creaturePrefab, List<string> lines)
    {
        List<GameObject> attackItems = new();
        List<GameObject> loadoutItems = new();

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            GameObject? itemPrefab = ResolveItemPrefab(creaturePrefab, "defaultItems", line);
            if (itemPrefab == null)
            {
                continue;
            }

            if (CreaturePrefabRegistry.IsAttackItem(itemPrefab))
            {
                attackItems.Add(itemPrefab);
            }
            else
            {
                loadoutItems.Add(itemPrefab);
            }
        }

        return AppendDistinctByName(attackItems, loadoutItems);
    }

    private static void ApplyVisual(GameObject prefab, float? scale, List<string>? textures)
    {
        bool hasTextureOverrides = textures is { Count: > 0 };
        AppearanceByCreature.TryGetValue(
            GetPrefabName(prefab.name),
            out CreatureAppearanceRuntimeState? appearance);

        if (scale.HasValue)
        {
            CreaturePrefabBaseline.Capture(prefab, CreaturePrefabBaselineGroup.VisualScale);
            prefab.transform.localScale = Vector3.one * scale.Value;
            RegisterEquipmentVisualScale(prefab);
        }

        if (scale.HasValue || hasTextureOverrides || appearance != null)
        {
            CreaturePrefabBaseline.Capture(prefab, CreaturePrefabBaselineGroup.RagdollReferences);
            ApplyRagdollVisuals(prefab, scale.HasValue, textures, appearance);
        }

        if (textures == null)
        {
            return;
        }

        foreach (string textureOverride in textures)
        {
            ApplyTextureOverride(prefab, textureOverride);
        }
    }

    private static RagdollTextureRuntimeState? ApplyTextureOverride(GameObject prefab, string definition)
    {
        if (!TryParseTextureOverride(definition, out string rendererName, out int materialIndex, out string textureName, out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' texture override '{definition}' is invalid: {error}");
            return null;
        }

        if (ShouldSkipRendererTextureWork())
        {
            return null;
        }

        Texture? texture = CreatureTextureRegistry.GetTexture(textureName);
        if (texture == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' texture '{textureName}' was not found.");
            return null;
        }

        List<Renderer> renderers = FindRenderers(prefab, rendererName).ToList();
        if (renderers.Count == 0)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' has no renderer matching '{rendererName}'.");
            return null;
        }

        bool applied = false;
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' renderer '{renderer.name}' has no material index {materialIndex}.");
                continue;
            }

            (int RendererId, int MaterialIndex) key = (renderer.GetInstanceID(), materialIndex);
            if (TextureMaterialOverrides.TryGetValue(key, out TextureMaterialOverride existingOverride))
            {
                if (existingOverride.Renderer == renderer && existingOverride.Generated != null)
                {
                    existingOverride.Active = true;
                    existingOverride.Generated.SetTexture(MainTextureProperty, texture);
                    if (materials[materialIndex] != existingOverride.Generated)
                    {
                        materials[materialIndex] = existingOverride.Generated;
                        renderer.sharedMaterials = materials;
                    }

                    applied = true;
                    continue;
                }

                // Unity can recycle instance IDs after a renderer is destroyed. Retire the stale slot before
                // replacing it so its generated material cannot leak or be restored onto the wrong renderer.
                RestoreTextureOverrideSlot(existingOverride);
                if (existingOverride.Generated != null)
                {
                    Object.Destroy(existingOverride.Generated);
                }

                TextureMaterialOverrides.Remove(key);
            }

            Material sourceMaterial = materials[materialIndex];
            if (!sourceMaterial.HasProperty(MainTextureProperty))
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' renderer '{renderer.name}' material '{sourceMaterial.name}' has no {MainTextureProperty} texture property.");
                continue;
            }

            Material material = new(sourceMaterial);
            material.name = $"{sourceMaterial.name}_CreatureManager";
            material.SetTexture(MainTextureProperty, texture);
            materials[materialIndex] = material;
            renderer.sharedMaterials = materials;
            TextureMaterialOverrides[key] = new TextureMaterialOverride
            {
                Renderer = renderer,
                MaterialIndex = materialIndex,
                Original = sourceMaterial,
                Generated = material,
                Active = true
            };
            applied = true;
        }

        return applied
            ? new RagdollTextureRuntimeState(rendererName, materialIndex, texture)
            : null;
    }

    private static bool ShouldSkipRendererTextureWork()
    {
        // Dedicated servers run in batch mode, while other headless launches expose a null graphics
        // device. Keep definition validation and managed ragdoll setup, but avoid decoding textures or
        // touching shader materials when there is no rendered output.
        return Application.isBatchMode ||
               SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }

    private static void BeginTextureOverrideApply()
    {
        foreach (TextureMaterialOverride textureOverride in TextureMaterialOverrides.Values)
        {
            textureOverride.Active = false;
        }
    }

    private static void CompleteTextureOverrideApply()
    {
        foreach (TextureMaterialOverride textureOverride in TextureMaterialOverrides.Values)
        {
            if (textureOverride.Active)
            {
                continue;
            }

            RestoreTextureOverrideSlot(textureOverride);
        }
    }

    private static void DisposeTextureOverrides()
    {
        foreach (TextureMaterialOverride textureOverride in TextureMaterialOverrides.Values)
        {
            RestoreTextureOverrideSlot(textureOverride);

            if (textureOverride.Generated != null)
            {
                Object.Destroy(textureOverride.Generated);
            }
        }

        TextureMaterialOverrides.Clear();
    }

    private static void RestoreTextureOverrideSlot(TextureMaterialOverride textureOverride)
    {
        Renderer renderer = textureOverride.Renderer;
        if (renderer == null)
        {
            return;
        }

        Material[] materials = renderer.sharedMaterials;
        int index = textureOverride.MaterialIndex;
        if (index >= 0 && index < materials.Length && materials[index] == textureOverride.Generated)
        {
            materials[index] = textureOverride.Original;
            renderer.sharedMaterials = materials;
        }
    }

    private static void ApplyRagdollVisuals(
        GameObject prefab,
        bool scaleSpecified,
        List<string>? textures,
        CreatureAppearanceRuntimeState? appearance)
    {
        Character character = prefab.GetComponent<Character>();
        EffectList.EffectData[]? deathEffects = character?.m_deathEffects?.m_effectPrefabs;
        if (deathEffects == null || deathEffects.Length == 0)
        {
            return;
        }

        Vector3 creatureScale = prefab.transform.localScale;
        bool hasTextureOverrides = textures is { Count: > 0 };
        foreach (EffectList.EffectData effect in deathEffects)
        {
            if (effect?.m_prefab == null || effect.m_prefab.GetComponent<Ragdoll>() == null)
            {
                continue;
            }

            GameObject inheritedRagdoll = effect.m_prefab;
            GameObject originalRagdoll = GetOriginalRagdollPrefab(inheritedRagdoll);
            bool inheritedManagedVisual = inheritedRagdoll != originalRagdoll;
            bool needsDedicatedRagdoll = hasTextureOverrides ||
                                          appearance != null ||
                                          inheritedManagedVisual ||
                                          scaleSpecified &&
                                          (!IsUnitScale(creatureScale) || EffectMayModifyRagdollScale(effect));
            if (!needsDedicatedRagdoll)
            {
                effect.m_prefab = originalRagdoll;
                continue;
            }

            string key = $"{prefab.name}|{originalRagdoll.name}";
            if (!ManagedRagdollPrefabs.TryGetValue(key, out GameObject? managedRagdoll) || managedRagdoll == null)
            {
                string cloneName = $"{originalRagdoll.name}_{prefab.name}{RagdollCloneSuffix}";
                managedRagdoll = CreaturePrefabRegistry.ClonePrefab(inheritedRagdoll, cloneName);
                if (managedRagdoll == null)
                {
                    CreatureManagerPlugin.Log.LogWarning(
                        $"Creature '{prefab.name}' failed to clone ragdoll '{inheritedRagdoll.name}' for its configured visuals; the shared source ragdoll was left unchanged.");
                    continue;
                }

                ManagedRagdollPrefabs[key] = managedRagdoll;
                RagdollCloneSources[managedRagdoll.name] = originalRagdoll;
            }

            RagdollScaleRuntimeState? ragdollScale = null;
            if (scaleSpecified)
            {
                CreaturePrefabBaseline.Capture(managedRagdoll, CreaturePrefabBaselineGroup.VisualScale);
                Vector3 originalScale = GetOriginalRagdollScale(originalRagdoll);
                Vector3 finalScale = Vector3.Scale(originalScale, creatureScale);
                managedRagdoll.transform.localScale = finalScale;
                ragdollScale = new RagdollScaleRuntimeState(finalScale, creatureScale);
            }
            else if (TryGetInheritedRagdollScale(inheritedRagdoll, out RagdollScaleRuntimeState inheritedScale))
            {
                CreaturePrefabBaseline.Capture(managedRagdoll, CreaturePrefabBaselineGroup.VisualScale);
                managedRagdoll.transform.localScale = inheritedScale.FinalScale;
                ragdollScale = inheritedScale;
            }

            string managedRagdollName = GetPrefabName(managedRagdoll.name);
            if (ragdollScale != null)
            {
                RagdollScalesByPrefab[managedRagdollName] = ragdollScale;
                InheritedRagdollScales[managedRagdollName] = ragdollScale;
                // AttachItem/AttachArmor preserve world scale while parenting, which cancels the scaled
                // creature root. Apply only the creature multiplier here, not the ragdoll's authored scale.
                RegisterEquipmentVisualScale(managedRagdollName, ragdollScale.EquipmentScale);
            }
            else
            {
                InheritedRagdollScales.Remove(managedRagdollName);
            }

            List<RagdollTextureRuntimeState> ragdollTextures = new();
            if (TryGetInheritedRagdollTextures(inheritedRagdoll, out IReadOnlyList<RagdollTextureRuntimeState> inheritedTextures))
            {
                ragdollTextures.AddRange(inheritedTextures);
            }

            if (hasTextureOverrides)
            {
                foreach (string textureOverride in textures!)
                {
                    RagdollTextureRuntimeState? appliedTexture = ApplyTextureOverride(managedRagdoll, textureOverride);
                    if (appliedTexture != null)
                    {
                        AddOrReplaceRagdollTexture(ragdollTextures, appliedTexture);
                    }
                }
            }

            if (ragdollTextures.Count > 0)
            {
                RagdollTextureRuntimeState[] runtimeTextures = ragdollTextures.ToArray();
                RagdollTexturesByPrefab[managedRagdollName] = runtimeTextures;
                InheritedRagdollTextures[managedRagdollName] = runtimeTextures;
            }
            else
            {
                InheritedRagdollTextures.Remove(managedRagdollName);
            }

            if (appearance != null)
            {
                ApplyAppearanceToRagdollPrefab(prefab, managedRagdoll, appearance);
            }

            effect.m_prefab = managedRagdoll;
        }
    }

    private static bool TryGetInheritedRagdollScale(
        GameObject ragdollPrefab,
        out RagdollScaleRuntimeState scale)
    {
        string prefabName = GetPrefabName(ragdollPrefab.name);
        return RagdollScalesByPrefab.TryGetValue(prefabName, out scale) ||
               InheritedRagdollScales.TryGetValue(prefabName, out scale);
    }

    private static bool TryGetInheritedRagdollTextures(
        GameObject ragdollPrefab,
        out IReadOnlyList<RagdollTextureRuntimeState> textures)
    {
        string prefabName = GetPrefabName(ragdollPrefab.name);
        if (RagdollTexturesByPrefab.TryGetValue(prefabName, out RagdollTextureRuntimeState[] activeTextures) ||
            InheritedRagdollTextures.TryGetValue(prefabName, out activeTextures))
        {
            textures = activeTextures;
            return true;
        }

        textures = Array.Empty<RagdollTextureRuntimeState>();
        return false;
    }

    private static void AddOrReplaceRagdollTexture(
        List<RagdollTextureRuntimeState> textures,
        RagdollTextureRuntimeState texture)
    {
        int existingIndex = textures.FindIndex(existing =>
            existing.MaterialIndex == texture.MaterialIndex &&
            string.Equals(existing.RendererName, texture.RendererName, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            textures[existingIndex] = texture;
            return;
        }

        textures.Add(texture);
    }

    private static bool EffectMayModifyRagdollScale(EffectList.EffectData effect)
    {
        return effect.m_scale || effect.m_inheritParentScale || effect.m_multiplyParentVisualScale;
    }

    private static GameObject GetOriginalRagdollPrefab(GameObject ragdollPrefab)
    {
        GameObject current = ragdollPrefab;
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        while (current != null)
        {
            if (!visited.Add(current.name))
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Detected a CreatureManager ragdoll provenance cycle while resolving '{ragdollPrefab.name}'.");
                break;
            }

            if (!RagdollCloneSources.TryGetValue(current.name, out GameObject original) ||
                original == null ||
                original == current)
            {
                break;
            }

            current = original;
        }

        return current != null ? current : ragdollPrefab;
    }

    private static Vector3 GetOriginalRagdollScale(GameObject ragdollPrefab)
    {
        if (OriginalRagdollScales.TryGetValue(ragdollPrefab.name, out Vector3 scale))
        {
            return scale;
        }

        scale = ragdollPrefab.transform.localScale;
        OriginalRagdollScales[ragdollPrefab.name] = scale;
        return scale;
    }

    private static bool TryParseTextureOverride(string line, out string rendererName, out int materialIndex, out string textureName, out string error)
    {
        rendererName = "";
        materialIndex = 0;
        textureName = "";
        error = "";

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "entry is empty.";
            return false;
        }

        string[] tokens = line.Split(':');
        if (tokens.Length != 3)
        {
            error = "expected 'rendererName:materialIndex:textureName'.";
            return false;
        }

        rendererName = tokens[0].Trim();
        string materialIndexToken = tokens[1].Trim();
        textureName = tokens[2].Trim();

        if (rendererName.Length == 0)
        {
            error = "rendererName is empty.";
            return false;
        }

        if (!int.TryParse(materialIndexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out materialIndex))
        {
            error = $"materialIndex '{materialIndexToken}' is not an integer.";
            return false;
        }

        if (textureName.Length == 0)
        {
            error = "textureName is empty.";
            return false;
        }

        return true;
    }

    private static IEnumerable<Renderer> FindRenderers(GameObject prefab, string? rendererName)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (string.IsNullOrWhiteSpace(rendererName))
        {
            return renderers;
        }

        return renderers.Where(renderer =>
            string.Equals(renderer.name, rendererName, StringComparison.OrdinalIgnoreCase) ||
            GetTransformPath(renderer.transform).EndsWith(rendererName!, StringComparison.OrdinalIgnoreCase));
    }

    internal static void ApplyConfiguredRagdollScale(Ragdoll ragdoll)
    {
        if (ragdoll == null ||
            !RagdollScalesByPrefab.TryGetValue(
                GetPrefabName(ragdoll.gameObject.name),
                out RagdollScaleRuntimeState scale))
        {
            return;
        }

        ZNetView? nview = ragdoll.GetComponent<ZNetView>();
        if (nview != null && nview.IsValid() && nview.IsOwner())
        {
            // EffectData is allowed to run unchanged. This exact assignment happens afterwards, and
            // SetLocalScale also updates the standard scale ZDO when this ragdoll opts into scale sync.
            nview.SetLocalScale(scale.FinalScale);
            return;
        }

        ragdoll.transform.localScale = scale.FinalScale;
    }

    internal static void ApplyConfiguredRagdollTextures(VisEquipment visEquipment)
    {
        if (visEquipment == null ||
            ShouldSkipRendererTextureWork() ||
            !RagdollTexturesByPrefab.TryGetValue(
                GetPrefabName(visEquipment.gameObject.name),
                out RagdollTextureRuntimeState[] textures))
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        if (!AppliedRagdollTextureVisuals.Add(visualId))
        {
            return;
        }

        foreach (RagdollTextureRuntimeState texture in textures)
        {
            foreach (Renderer renderer in FindRenderers(visEquipment.gameObject, texture.RendererName))
            {
                Material[] materials = renderer.sharedMaterials;
                int materialIndex = texture.MaterialIndex;
                if (materialIndex < 0 ||
                    materialIndex >= materials.Length ||
                    materials[materialIndex] == null ||
                    !materials[materialIndex].HasProperty(MainTextureProperty))
                {
                    continue;
                }

                // A per-material property block survives VisEquipment.UpdateBaseModel and does not create
                // per-corpse Material instances. Preserve any properties already supplied by Valheim/mods.
                RagdollTexturePropertyBlock.Clear();
                renderer.GetPropertyBlock(RagdollTexturePropertyBlock, materialIndex);
                RagdollTexturePropertyBlock.SetTexture(MainTextureProperty, texture.Texture);
                renderer.SetPropertyBlock(RagdollTexturePropertyBlock, materialIndex);
            }
        }
    }

    internal static void ForgetRagdollTextureVisual(VisEquipment visEquipment)
    {
        if (visEquipment != null)
        {
            AppliedRagdollTextureVisuals.Remove(visEquipment.GetInstanceID());
        }
    }

    internal static void ScaleAttachedEquipmentVisual(VisEquipment visEquipment, GameObject? item)
    {
        if (item == null || !TryGetEquipmentVisualScale(visEquipment, out Vector3 scale))
        {
            return;
        }

        item.transform.localScale = Vector3.Scale(item.transform.localScale, scale);
    }

    internal static void ScaleAttachedEquipmentVisuals(VisEquipment visEquipment, IEnumerable<GameObject>? items)
    {
        if (items == null)
        {
            return;
        }

        foreach (GameObject item in items)
        {
            ScaleAttachedEquipmentVisual(visEquipment, item);
        }
    }

    private static void RegisterEquipmentVisualScale(GameObject prefab)
    {
        RegisterEquipmentVisualScale(GetPrefabName(prefab.name), prefab.transform.localScale);
    }

    private static void RegisterEquipmentVisualScale(string prefabName, Vector3 scale)
    {
        if (IsUnitScale(scale))
        {
            EquipmentVisualScales.Remove(prefabName);
            return;
        }

        EquipmentVisualScales[prefabName] = scale;
    }

    private static bool TryGetEquipmentVisualScale(VisEquipment visEquipment, out Vector3 scale)
    {
        string prefabName = GetPrefabName(visEquipment.gameObject.name);
        if (EquipmentVisualScales.TryGetValue(prefabName, out scale))
        {
            return true;
        }

        scale = Vector3.one;
        return false;
    }

    private static bool IsUnitScale(Vector3 scale)
    {
        return Mathf.Approximately(scale.x, 1f) &&
               Mathf.Approximately(scale.y, 1f) &&
               Mathf.Approximately(scale.z, 1f);
    }

    private static string GetPrefabName(string objectName)
    {
        int cloneIndex = objectName.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIndex >= 0)
        {
            return objectName.Substring(0, cloneIndex);
        }

        return objectName;
    }

    private static void ApplyVisEquipment(
        GameObject prefab,
        AppearanceDefinition? appearance,
        string? clonedFrom)
    {
        Vector3? hairColor = null;
        if (appearance?.HairColor != null)
        {
            if (CreatureYaml.TryParseAppearanceColor(appearance.HairColor, out Vector3 parsedHairColor))
            {
                hairColor = parsedHairColor;
            }
            else
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' appearance.hairColor '{appearance.HairColor}' is invalid and was skipped.");
            }
        }

        Vector3? skinColor = null;
        if (appearance?.SkinColor != null)
        {
            if (CreatureYaml.TryParseAppearanceColor(appearance.SkinColor, out Vector3 parsedSkinColor))
            {
                skinColor = parsedSkinColor;
            }
            else
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' appearance.skinColor '{appearance.SkinColor}' is invalid and was skipped.");
            }
        }

        CreatureAppearanceRuntimeState? inheritedAppearance = null;
        string targetName = GetPrefabName(prefab.name);
        AppearanceByCreature.TryGetValue(targetName, out inheritedAppearance);
        string sourceName = (clonedFrom ?? "").Trim();
        bool isManagedClone = CreaturePrefabRegistry.IsCreatureManagerClone(prefab);
        if (inheritedAppearance == null && isManagedClone)
        {
            InheritedAppearanceByClone.TryGetValue(targetName, out inheritedAppearance);
        }

        if (inheritedAppearance == null && sourceName.Length > 0)
        {
            AppearanceByCreature.TryGetValue(GetPrefabName(sourceName), out inheritedAppearance);
            if (inheritedAppearance != null && isManagedClone)
            {
                // CreatureManager clones retain the source prefab's visual snapshot. Keep the matching
                // appearance provenance until teardown so definition reordering cannot lose it on reload.
                InheritedAppearanceByClone[targetName] = inheritedAppearance;
            }
        }

        CreatureAppearanceRuntimeState runtimeAppearance = new(
            appearance?.Hair ?? inheritedAppearance?.Hair,
            appearance?.Beard ?? inheritedAppearance?.Beard,
            appearance?.HairColor != null ? hairColor : inheritedAppearance?.HairColor,
            appearance?.SkinColor != null ? skinColor : inheritedAppearance?.SkinColor,
            appearance?.ModelIndex ?? inheritedAppearance?.ModelIndex);
        if (runtimeAppearance.HasSpecifiedFields)
        {
            AppearanceByCreature[targetName] = runtimeAppearance;
        }

        if (appearance == null || !appearance.HasSpecifiedFields)
        {
            return;
        }

        CreaturePrefabBaseline.Capture(prefab, CreaturePrefabBaselineGroup.Appearance);

        Humanoid humanoid = prefab.GetComponent<Humanoid>();
        VisEquipment visEquipment = prefab.GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' has no VisEquipment component.");
        }
        else
        {
            if (appearance.ModelIndex is int modelIndex)
            {
                if (visEquipment.m_models != null && visEquipment.m_models.Length > 0)
                {
                    if (modelIndex < 0 || modelIndex >= visEquipment.m_models.Length)
                    {
                        CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' appearance.modelIndex {modelIndex} is outside 0..{visEquipment.m_models.Length - 1}.");
                    }
                    else
                    {
                        visEquipment.m_modelIndex = modelIndex;
                    }
                }
                else if (modelIndex != 0)
                {
                    CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' appearance.modelIndex ignored because the prefab has no VisEquipment models.");
                }
            }

            if (skinColor.HasValue)
            {
                visEquipment.m_skinColor = skinColor.Value;
            }

            if (hairColor.HasValue)
            {
                visEquipment.m_hairColor = hairColor.Value;
            }

            if (appearance.Hair != null) visEquipment.m_hairItem = appearance.Hair;
            if (appearance.Beard != null) visEquipment.m_beardItem = appearance.Beard;
        }

        if (humanoid == null)
        {
            if (appearance.Hair != null || appearance.Beard != null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' has no Humanoid component for hair/beard appearance.");
            }

            return;
        }

        if (appearance.Hair != null)
        {
            WarnIfAppearancePrefabMissing(prefab, "hair", appearance.Hair);
            humanoid.m_hairItem = appearance.Hair;
        }

        if (appearance.Beard != null)
        {
            WarnIfAppearancePrefabMissing(prefab, "beard", appearance.Beard);
            humanoid.m_beardItem = appearance.Beard;
        }
    }

    private static void ApplyAppearanceToRagdollPrefab(
        GameObject creaturePrefab,
        GameObject ragdollPrefab,
        CreatureAppearanceRuntimeState appearance)
    {
        AppearanceByRagdollPrefab[GetPrefabName(ragdollPrefab.name)] = appearance;

        VisEquipment? visEquipment = ragdollPrefab.GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Creature '{creaturePrefab.name}' has configured appearance fields, but ragdoll '{ragdollPrefab.name}' has no VisEquipment component.");
            return;
        }

        CreaturePrefabBaseline.Capture(ragdollPrefab, CreaturePrefabBaselineGroup.Appearance);
        if (appearance.ModelIndex is int modelIndex)
        {
            if (visEquipment.m_models != null && visEquipment.m_models.Length > 0)
            {
                if (modelIndex >= 0 && modelIndex < visEquipment.m_models.Length)
                {
                    visEquipment.m_modelIndex = modelIndex;
                }
                else
                {
                    CreatureManagerPlugin.Log.LogWarning(
                        $"Creature '{creaturePrefab.name}' appearance.modelIndex {modelIndex} is outside ragdoll '{ragdollPrefab.name}' model range 0..{visEquipment.m_models.Length - 1}.");
                }
            }
            else if (modelIndex == 0)
            {
                visEquipment.m_modelIndex = 0;
            }
            else
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Creature '{creaturePrefab.name}' appearance.modelIndex {modelIndex} cannot be applied because ragdoll '{ragdollPrefab.name}' has no VisEquipment models.");
            }
        }

        if (appearance.SkinColor.HasValue)
        {
            visEquipment.m_skinColor = appearance.SkinColor.Value;
        }

        if (appearance.HairColor.HasValue)
        {
            visEquipment.m_hairColor = appearance.HairColor.Value;
        }

        if (appearance.Hair != null)
        {
            visEquipment.m_hairItem = appearance.Hair;
        }

        if (appearance.Beard != null)
        {
            visEquipment.m_beardItem = appearance.Beard;
        }
    }

    private static void WarnIfAppearancePrefabMissing(GameObject prefab, string label, string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName) || CreaturePrefabRegistry.GetPrefab(prefabName) != null)
        {
            return;
        }

        CreatureManagerPlugin.Log.LogWarning($"Creature '{prefab.name}' appearance {label} prefab '{prefabName}' was not found.");
    }

    private static List<GameObject> AppendDistinctByName(IEnumerable<GameObject> firstItems, IEnumerable<GameObject> secondItems)
    {
        List<GameObject> combined = new();
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameObject item in firstItems.Concat(secondItems))
        {
            if (item == null || !names.Add(item.name))
            {
                continue;
            }

            combined.Add(item);
        }

        return combined;
    }

    private static void SetupWatcher()
    {
        Watcher?.Dispose();
        Watcher = new FileSystemWatcher(ConfigDirectoryPath)
        {
            IncludeSubdirectories = true,
            Filter = "*.*",
            SynchronizingObject = ThreadingHelper.SynchronizingObject,
            EnableRaisingEvents = true
        };
        Watcher.Changed += OnOverrideFileChanged;
        Watcher.Created += OnOverrideFileChanged;
        Watcher.Renamed += OnOverrideFileChanged;
        Watcher.Deleted += OnOverrideFileChanged;
    }

    private static void OnOverrideFileChanged(object sender, FileSystemEventArgs e)
    {
        bool textureChanged = IsTextureFile(e.FullPath) ||
                              e is RenamedEventArgs renamed && IsTextureFile(renamed.OldFullPath);
        if (textureChanged)
        {
            TextureRefreshPending = true;
            PendingTextureRefreshTime = DateTime.UtcNow.AddTicks(ReloadDebounceTicks);
            return;
        }

        if (!IsOverrideFile(e.FullPath) || ConfigSync?.IsSourceOfTruth == false)
        {
            return;
        }

        RequestConfigurationReload();
    }

    private static bool IsTextureFile(string path)
    {
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return directory != null && directory.Equals(Path.GetFullPath(TextureDirectoryPath), StringComparison.OrdinalIgnoreCase);
    }

    private delegate bool TryReadYaml<T>(string yaml, string source, out List<T> definitions);

    private static bool TryLoadOverrideFiles<T>(string prefix, TryReadYaml<T> reader, out List<T> definitions)
    {
        definitions = new List<T>();
        try
        {
            foreach (string file in EnumerateOverrideFiles(prefix))
            {
                string yaml = File.ReadAllText(file);
                if (!reader(yaml, file, out List<T> fileDefinitions))
                {
                    definitions.Clear();
                    return false;
                }

                definitions.AddRange(fileDefinitions);
            }

            return true;
        }
        catch (Exception ex)
        {
            definitions.Clear();
            CreatureManagerPlugin.Log.LogError($"Failed to load {prefix} YAML files: {ex.Message}");
            return false;
        }
    }

    private static bool TryLoadLevelDefinitions(out List<LevelDefinition> definitions)
    {
        if (!TryLoadOverrideFiles("levels", CreatureYaml.TryReadLevelDefinitions, out List<LevelDefinition> overrideDefinitions))
        {
            definitions = new List<LevelDefinition>();
            return false;
        }

        definitions = LoadLevelPresetDefinitions();
        definitions.AddRange(overrideDefinitions);
        return true;
    }

    private static List<LevelDefinition> LoadLevelPresetDefinitions()
    {
        return BuildLevelPresetDefinitions(GetLevelPresetWeights(CreatureManagerPlugin.BiomeLevelPreset?.Value ?? CreatureManagerPlugin.LevelBiomePreset.Easy));
    }

    private static List<LevelDefinition> BuildLevelPresetDefinitions(IEnumerable<KeyValuePair<string, float[]>> presets)
    {
        return presets
            .Select(entry => new LevelDefinition
            {
                Target = entry.Key,
                Level = entry.Value.ToList(),
                IsPreset = true
            })
            .ToList();
    }

    private static IReadOnlyList<KeyValuePair<string, float[]>> GetLevelPresetWeights(CreatureManagerPlugin.LevelBiomePreset preset)
    {
        return preset switch
        {
            CreatureManagerPlugin.LevelBiomePreset.Easy => new[]
            {
                LevelPreset("Meadows", 100f),
                LevelPreset("BlackForest", 85f, 15f),
                LevelPreset("Swamp", 70f, 25f, 5f),
                LevelPreset("Mountain", 55f, 30f, 12f, 3f),
                LevelPreset("Plains", 45f, 30f, 18f, 6f, 1f),
                LevelPreset("Mistlands", 35f, 30f, 22f, 10f, 3f),
                LevelPreset("AshLands", 25f, 30f, 25f, 14f, 5f, 1f),
                LevelPreset("Ocean", 70f, 25f, 5f),
                LevelPreset("DeepNorth", 25f, 30f, 25f, 14f, 5f, 1f)
            },
            CreatureManagerPlugin.LevelBiomePreset.Normal => new[]
            {
                LevelPreset("Meadows", 95f, 5f),
                LevelPreset("BlackForest", 75f, 20f, 5f),
                LevelPreset("Swamp", 55f, 30f, 12f, 3f),
                LevelPreset("Mountain", 45f, 30f, 18f, 6f, 1f),
                LevelPreset("Plains", 35f, 30f, 22f, 10f, 3f),
                LevelPreset("Mistlands", 25f, 30f, 25f, 14f, 5f, 1f),
                LevelPreset("AshLands", 15f, 25f, 25f, 20f, 10f, 5f),
                LevelPreset("Ocean", 55f, 30f, 12f, 3f),
                LevelPreset("DeepNorth", 15f, 25f, 25f, 20f, 10f, 5f)
            },
            CreatureManagerPlugin.LevelBiomePreset.Hard => new[]
            {
                LevelPreset("Meadows", 90f, 10f),
                LevelPreset("BlackForest", 68f, 25f, 7f),
                LevelPreset("Swamp", 48f, 34f, 14f, 4f),
                LevelPreset("Mountain", 38f, 32f, 20f, 8f, 2f),
                LevelPreset("Plains", 30f, 30f, 23f, 12f, 4f, 1f),
                LevelPreset("Mistlands", 22f, 28f, 26f, 16f, 6f, 2f),
                LevelPreset("AshLands", 12f, 22f, 25f, 21f, 13f, 7f),
                LevelPreset("Ocean", 48f, 34f, 14f, 4f),
                LevelPreset("DeepNorth", 12f, 22f, 25f, 21f, 13f, 7f)
            },
            CreatureManagerPlugin.LevelBiomePreset.VeryHard => new[]
            {
                LevelPreset("Meadows", 85f, 15f),
                LevelPreset("BlackForest", 60f, 30f, 10f),
                LevelPreset("Swamp", 40f, 35f, 18f, 7f),
                LevelPreset("Mountain", 30f, 35f, 22f, 10f, 3f),
                LevelPreset("Plains", 25f, 30f, 25f, 14f, 5f, 1f),
                LevelPreset("Mistlands", 18f, 27f, 27f, 18f, 8f, 2f),
                LevelPreset("AshLands", 10f, 20f, 25f, 22f, 15f, 8f),
                LevelPreset("Ocean", 40f, 35f, 18f, 7f),
                LevelPreset("DeepNorth", 10f, 20f, 25f, 22f, 15f, 8f)
            },
            _ => GetLevelPresetWeights(CreatureManagerPlugin.LevelBiomePreset.Easy)
        };
    }

    private static KeyValuePair<string, float[]> LevelPreset(string biome, params float[] weights)
    {
        return new KeyValuePair<string, float[]>(biome, weights);
    }

    private static IEnumerable<string> EnumerateOverrideFiles(string prefix)
    {
        return Directory.EnumerateFiles(ConfigDirectoryPath, "*.yml")
            .Concat(Directory.EnumerateFiles(ConfigDirectoryPath, "*.yaml"))
            .Where(path => IsConfigurationFile(path, prefix))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsOverrideFile(string path)
    {
        return IsConfigurationFile(path, "creatures") ||
               IsConfigurationFile(path, "ai") ||
               IsConfigurationFile(path, "attacks") ||
               IsConfigurationFile(path, "projectile") ||
               IsConfigurationFile(path, "levels") ||
               IsConfigurationFile(path, "karma") ||
               IsConfigurationFile(path, "factions");
    }

    private static bool IsConfigurationFile(string path, string prefix)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        if (prefix.Equals("factions", StringComparison.OrdinalIgnoreCase))
        {
            return stem.Equals(prefix, StringComparison.OrdinalIgnoreCase) &&
                   Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase);
        }

        return stem.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
               stem.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureDirectoriesAndDefaultFiles()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
        Directory.CreateDirectory(CacheDirectoryPath);
        EnsureDefaultTextures();

        if (!File.Exists(FactionConfigurationPath))
        {
            File.WriteAllText(FactionConfigurationPath, CreatureFactionManager.BuildDefaultOverrideYaml());
        }

        if (!File.Exists(LevelConfigurationPath))
        {
            File.WriteAllText(LevelConfigurationPath, BuildDefaultLevelOverrideYaml());
        }

        if (!File.Exists(KarmaConfigurationPath))
        {
            File.WriteAllText(KarmaConfigurationPath, CreatureKarmaManager.BuildDefaultYaml());
        }

        if (!File.Exists(AttackConfigurationPath))
        {
            File.WriteAllText(AttackConfigurationPath, BuildDefaultAttackOverrideYaml());
        }

        if (!File.Exists(ProjectileConfigurationPath))
        {
            File.WriteAllText(ProjectileConfigurationPath, BuildDefaultProjectileOverrideYaml());
        }

        if (!File.Exists(AiConfigurationPath))
        {
            File.WriteAllText(AiConfigurationPath, BuildDefaultAiOverrideYaml());
        }

        if (!File.Exists(CreatureConfigurationPath))
        {
            File.WriteAllText(CreatureConfigurationPath, BuildDefaultOverrideYaml());
        }

        WriteEmbeddedDefaultIfMissing(
            CreatureSampleConfigurationPath,
            "CreatureManager.defaults.creatures.sample.yml");
        WriteEmbeddedDefaultIfMissing(
            AttackSampleConfigurationPath,
            "CreatureManager.defaults.attacks.sample.yml");
    }

    internal static void EnsureDefaultTextures()
    {
        Directory.CreateDirectory(TextureDirectoryPath);
        if (CreatureManagerPlugin.GenerateSampleTextures?.Value == CreatureManagerPlugin.Toggle.Off)
        {
            return;
        }

        foreach (string fileName in DefaultTextureFileNames)
        {
            WriteEmbeddedDefaultIfMissing(
                Path.Combine(TextureDirectoryPath, fileName),
                DefaultTextureResourcePrefix + fileName);
        }
    }

    private static void WriteEmbeddedDefaultIfMissing(string path, string resourceName)
    {
        if (File.Exists(path))
        {
            return;
        }

        using Stream? resource = typeof(CreatureDomainManager).Assembly.GetManifestResourceStream(resourceName);
        if (resource == null)
        {
            CreatureManagerPlugin.Log.LogError($"Embedded default resource '{resourceName}' was not found.");
            return;
        }

        try
        {
            using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            resource.CopyTo(output);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another initialization path created the same default first; never overwrite it.
        }
    }

    private static void AppendIndented(StringBuilder builder, int indent, string text)
    {
        builder.Append(' ', indent * 2);
        builder.AppendLine(text);
    }

    private static string FormatLevelFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string BuildDefaultLevelOverrideYaml()
    {
        StringBuilder builder = new();
        AppendTemplateComment(builder, "CreatureManager level configuration.");
        AppendTemplateComment(builder, "Loaded files: levels.yml, levels.yaml, levels_*.yml, levels_*.yaml.");
        AppendTemplateComment(builder, "BepInEx 'Enable Level System' is the master switch. BepInEx 'Biome Level Preset' adds built-in biome rules.");
        AppendTemplateComment(builder, "BepInEx 'Global Modifiers', 'Boss Modifiers', and 'Enforcer Modifiers' independently gate modifier rolls/effects for those creature classes.");
        AppendTemplateComment(builder, "Top-level targets: Global, Boss, biome names, group names, or prefab names.");
        AppendTemplateComment(builder, "Non-boss and Enforcer specificity: prefab > group > biome > Global. Omitted fields fall back independently.");
        AppendTemplateComment(builder, "Regular boss specificity: prefab > group > Boss. Missing fields never fall back to Global; level may use the opt-in biome preset.");
        AppendTemplateComment(builder, "For any modifiers field, omission or {} keeps lower-priority fallback; a mapping overrides only listed values.");
        AppendTemplateComment(builder, "Use modifiers: [] as a terminal clear that blocks every lower-priority modifier source for that target.");
        AppendTemplateComment(builder, "Enforcer summons ignore Boss and continue to use prefab/group, biome, and Global rules even when their source prefab is a boss.");
        AppendTemplateComment(builder, "Nested biome rules under a prefab/group beat the same target without a biome. User biome targets do not affect regular bosses; built-in preset biome levels can be opted in by config.");
        AppendTemplateComment(builder, "Rules apply to spawned Character instances on the server. Existing instances already marked by CreatureManager are not rerolled.");
        AppendTemplateComment(builder, "Modifiers are rolled at most once per group. Group totals below 100 leave the remainder as no modifier; totals above 100 are normalized as weights.");
        AppendTemplateComment(builder, "Reaping healing, max health, and damage work in dungeons, but new Reaping scale gains are disabled there.");
        AppendTemplateComment(builder, "CreatureManager does not manage loot/drop from this domain.");
        AppendTemplateBlankLine(builder);

        builder.AppendLine("Global:");
        AppendIndented(builder, 1, "level: [80, 20]                       # Fallback level weights. [80, 20] = level 1 weight 80, level 2 weight 20.");
        AppendIndented(builder, 1, "scalePerLevel: 0.1                    # Visual LevelEffects scale per level above 1. Always skipped in dungeons; saddle-able creatures are controlled by config.");
        AppendIndented(builder, 1, "damage: 1                            # Outgoing damage multiplier. Omit or keep 1 to keep baseline.");
        AppendIndented(builder, 1, "damagePerLevel: 0.25                 # Extra outgoing damage per level above 1: damage * (1 + (level - 1) * value).");
        AppendIndented(builder, 1, "health: 1                            # Base max-health multiplier. Omit or keep 1 to keep level 1 baseline.");
        AppendIndented(builder, 1, "healthPerLevel: 1                    # Max-health growth per level above 1, based on level 1 health. 1 keeps vanilla level growth.");
        AppendTemplateLine(builder, 1, "distanceScaling: [0.03, 0.08, 1000, 5] # damagePerStep, healthPerStep, interval, maxSteps. maxSteps 0 = no cap.");
        AppendTemplateLine(builder, 1, "modifierDistanceScaling: [0.03, 1000, 8] # Each chance * (1 + 0.03 * steps), 1 step per 1000 distance, max 8; e.g. 20% becomes 20.6% at 1000 and 24.8% at 8000+.");
        AppendDefaultLevelModifiers(builder, bossDefaults: false);
        builder.AppendLine();

        builder.AppendLine("Boss:");
        AppendIndented(builder, 1, "level: [100]                           # Boss fallback level weights. Keeps bosses at level 1 unless overridden or Karma adds bonus levels.");
        AppendIndented(builder, 1, "scalePerLevel: 0.1                   # Boss level scale per level above 1. Enforcer boss summons use Global.scalePerLevel instead.");
        AppendIndented(builder, 1, "damage: 1                            # Boss outgoing damage multiplier. Omit or keep 1 to keep baseline.");
        AppendIndented(builder, 1, "damagePerLevel: 0.1                  # Boss outgoing damage bonus per level above 1.");
        AppendIndented(builder, 1, "health: 1                            # Boss base max-health multiplier. Keep 1 to avoid changing level 1 bosses.");
        AppendIndented(builder, 1, "healthPerLevel: 0.5                  # Boss max-health growth per level above 1, based on level 1 health.");
        AppendTemplateLine(builder, 1, "distanceScaling: [0.03, 0.08, 1000, 5] # Boss damage/health distance scaling tuple.");
        AppendTemplateLine(builder, 1, "modifierDistanceScaling: [0.02, 1000, 6] # Optional Boss scaling. Omit to leave boss modifier chances unscaled by distance.");
        AppendDefaultLevelModifiers(builder, bossDefaults: true);
        builder.AppendLine();

        builder.AppendLine("TentaRoot:");
        AppendIndented(builder, 1, "modifiers: []                       # Terminal clear: disable modifier rolls and block lower-specificity fallback for this prefab.");
        builder.AppendLine();

        AppendTemplateComment(builder, "Optional examples. Uncomment or copy only the blocks you want to use.");
        AppendTemplateLine(builder, 0, "groups:");
        AppendTemplateLine(builder, 1, "ForestBrutes:");
        AppendTemplateLine(builder, 2, "- Troll");
        AppendTemplateLine(builder, 2, "- Greydwarf_Elite");
        AppendTemplateLine(builder, 2, "- Greydwarf_Shaman");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "Meadows:");
        AppendTemplateLine(builder, 1, "level: [100]                         # Biome rule for non-boss creatures in Meadows.");
        AppendTemplateLine(builder, 1, "health: 2                            # Level 1 max health becomes 2x baseline.");
        AppendTemplateLine(builder, 1, "healthPerLevel: 0.5                  # Health = level1Health * 2 * (1 + (level - 1) * 0.5): level 1=2x, level 2=3x, level 3=4x.");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "ForestBrutes:");
        AppendTemplateLine(builder, 1, "level: [50, 30, 20]                  # Group rule. Applies to prefabs listed in groups.ForestBrutes.");
        AppendTemplateLine(builder, 1, "damage: 1.05");
        AppendTemplateLine(builder, 1, "health: 1.1");
        AppendTemplateLine(builder, 1, "distanceScaling: [0.05, 0.1, 1000]  # maxSteps omitted or 0 = no cap.");
        AppendTemplateLine(builder, 1, "modifierDistanceScaling: [0.02, 1000, 6]");
        AppendTemplateLine(builder, 1, "modifiers:");
        AppendIndented(builder, 2, "# Offense: Enraged to Undodgeable");
        AppendTemplateLine(builder, 2, "enraged: 10");
        AppendTemplateLine(builder, 2, "spirit: 5, 0.25");
        AppendIndented(builder, 2, "# Defense: Armored to Chameleon");
        AppendTemplateLine(builder, 2, "armored: 20, 0.4");
        AppendTemplateLine(builder, 2, "deathward: 5, 0.25, 60, 3");
        AppendTemplateLine(builder, 2, "vortex: 5, 0.5");
        AppendTemplateLine(builder, 2, "unflinching: 5");
        AppendTemplateLine(builder, 2, "chameleon: 5, 10");
        AppendIndented(builder, 2, "# Affliction: Exposed to ToxicDeath");
        AppendTemplateLine(builder, 2, "withered: 10, 0.5, 0.5, 5");
        AppendIndented(builder, 2, "# Special: Swift to Blamer");
        AppendTemplateLine(builder, 2, "reaping: 5, 0.15, 20, 0.1, 2, 0.05, 1, 0.02, 0.4");
        AppendTemplateLine(builder, 2, "blink: 5, 1, 16, fx_Adrenaline1");
        AppendTemplateLine(builder, 2, "omen: 5, 0.25");
        AppendTemplateLine(builder, 2, "juggernaut: 5, 80, 5");
        AppendTemplateLine(builder, 2, "blamer: 5, 1, 60, 0.75");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "Troll:");
        AppendTemplateLine(builder, 1, "level: [0, 0, 10, 5]                # Prefab rule. 0 weights skip lower levels.");
        AppendTemplateLine(builder, 1, "damage: 1.15");
        AppendTemplateLine(builder, 1, "damagePerLevel: 0.5");
        AppendTemplateLine(builder, 1, "health: 1.25");
        AppendTemplateLine(builder, 1, "healthPerLevel: 0.75");
        AppendTemplateLine(builder, 1, "modifierDistanceScaling: [0, 1000, 0] # Disable inherited modifier chance distance scaling for this prefab.");
        AppendTemplateLine(builder, 1, "modifiers:");
        AppendIndented(builder, 2, "# Offense: Enraged to Undodgeable");
        AppendTemplateLine(builder, 2, "enraged: 25, 0.6");
        AppendIndented(builder, 2, "# Defense: Armored to Chameleon");
        AppendTemplateLine(builder, 2, "armored: 50, 0.5");
        AppendTemplateLine(builder, 2, "reflection: 10, 0.2, 0.5");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "VC_Vaettr:");
        AppendTemplateLine(builder, 1, "Meadows:");
        AppendTemplateLine(builder, 2, "level: [100]                       # Nested biome rule: only this prefab/group in Meadows.");
        AppendTemplateLine(builder, 2, "damage: 0.4");
        AppendTemplateBlankLine(builder);
        AppendLevelPresetTemplateComments(builder);
        return builder.ToString();
    }

    private static void AppendDefaultLevelModifiers(StringBuilder builder, bool bossDefaults)
    {
        string chance = bossDefaults ? "10" : "5";
        string regenerationPerSecond = bossDefaults ? "0.002" : "0.01";
        string blamerChance = bossDefaults ? "0" : chance;
        string blamerKarmaPerSecond = bossDefaults ? "1" : "0.5";
        string blamerMaxKarmaGain = bossDefaults ? "60" : "45";
        AppendIndented(builder, 1, "modifiers:                           # At most one modifier per group. Special tuples are documented inline.");
        AppendIndented(builder, 2, "# Offense: Enraged to Undodgeable");
        AppendIndented(builder, 2, $"enraged: {chance}, 0.15                 # chance%, outgoingDamageBonus.");
        AppendIndented(builder, 2, $"fire: {chance}, 0.2                     # chance%, addedFireDamage.");
        AppendIndented(builder, 2, $"frost: {chance}, 0.1                    # chance%, addedFrostDamage.");
        AppendIndented(builder, 2, $"lightning: {chance}, 0.1                # chance%, addedLightningDamage.");
        AppendIndented(builder, 2, $"spirit: {chance}, 0.05                  # chance%, addedSpiritDoT.");
        AppendIndented(builder, 2, $"armorPiercing: {chance}, 0.3            # chance%, ignoredPlayerArmor.");
        AppendIndented(builder, 2, $"staggering: {chance}, 0.6               # chance%, staggerBonus.");
        AppendIndented(builder, 2, $"undodgeable: {chance}, 0.25             # chance%, damageReduction; attacks against players ignore dodge invulnerability.");
        AppendIndented(builder, 2, "# Defense: Armored to Chameleon");
        AppendIndented(builder, 2, $"armored: {chance}, 0.3                 # chance%, damageReduction.");
        AppendIndented(builder, 2, $"deathward: {chance}, 0.2, 10, 3         # chance%, restoredHealth, cooldownSeconds, maxActivations.");
        AppendIndented(builder, 2, $"regenerating: {chance}, {regenerationPerSecond}          # chance%, maxHealthRegenPerSecond.");
        AppendIndented(builder, 2, $"reflection: {chance}, 0.1, 0.5         # chance%, actualMeleeDamageReflected, procChance.");
        AppendIndented(builder, 2, $"vortex: {chance}, 0.5                  # chance%, projectileIgnoreProc.");
        AppendIndented(builder, 2, $"adaptive: {chance}, 0.5                # chance%, rememberedTypeDamageReduction.");
        AppendIndented(builder, 2, $"unflinching: {chance}                    # chance%; prevents normal-hit and perfect-parry stagger.");
        AppendIndented(builder, 2, $"chameleon: {chance}, 10                  # chance%, immunitySwitchSeconds.");
        AppendIndented(builder, 2, "# Affliction: Exposed to ToxicDeath");
        AppendIndented(builder, 2, $"exposed: {chance}, 0.2, 0.5, 5          # chance%, damageTaken, proc, duration.");
        AppendIndented(builder, 2, $"weakened: {chance}, 0.2, 0.5, 5         # chance%, outgoingDamageReduction, proc, duration.");
        AppendIndented(builder, 2, $"withered: {chance}, 0.5, 0.5, 5         # chance%, healingReduction, proc, duration.");
        AppendIndented(builder, 2, $"crippling: {chance}, 0.5, 0.5, 0.5, 5  # chance%, moveReduction, jumpReduction, proc, duration.");
        AppendIndented(builder, 2, $"disruptive: {chance}, 0.5, 0.5, 0.5, 5  # chance%, staminaRegenReduction, eitrRegenReduction, proc, duration.");
        AppendIndented(builder, 2, $"adrenalineDrain: {chance}, 0.5, 0.5, 0.5, 5 # chance%, currentAdrenalineRemoved, adrenalineGainReduction, procChance, duration.");
        AppendIndented(builder, 2, $"corrosive: {chance}, 0.5, 0.5, 5          # chance%, durabilityLossBonus, procChance, duration. Equipped armor, weapons, and shield only.");
        AppendIndented(builder, 2, "toxicDeath: 10, 0.3, 4, blob_aoe   # chance%, maxHealthDamage, radius, triggerEffect.");
        AppendIndented(builder, 2, "# Special: Swift to Blamer");
        AppendIndented(builder, 2, $"swift: {chance}, 0.4                  # chance%, movementSpeedBonus.");
        AppendIndented(builder, 2, $"attackSpeed: {chance}, 0.3            # chance%, attackSpeedBonus.");
        AppendIndented(builder, 2, $"vampiric: {chance}, 0.3               # chance%, actualDirectDamageHealing.");
        AppendIndented(builder, 2, $"reaping: {chance}, 0.05, 20, 0.1, 2, 0.01, 0.2, 0.05, 0.5 # chance%, heal/base, healMaxActivations, maxHealth/base, maxHealthCap, damagePerKill, damageCap, scalePerKill, scaleCap. New scale gains are disabled in dungeons.");
        AppendIndented(builder, 2, $"blink: {chance}, 6, 16, fx_Adrenaline1  # chance%, cooldown, maxRange, startEffect.");
        AppendIndented(builder, 2, $"omen: {chance}, 0.5                  # chance%, forcedEnforcerChance.");
        AppendIndented(builder, 2, $"juggernaut: {chance}, 150, 5           # chance%, minimumPushForce, cooldownSeconds.");
        AppendIndented(builder, 2, $"blamer: {blamerChance}, {blamerKarmaPerSecond}, {blamerMaxKarmaGain}, 0.75           # chance%, karmaPerSecond, maxKarmaGain, fleeHealthRatio. 0 cap is unlimited.");
    }

    private static void AppendLevelPresetTemplateComments(StringBuilder builder)
    {
        AppendTemplateComment(builder, "Built-in biome level distributions used by the BepInEx 'Biome Level Preset' option.");
        AppendTemplateComment(builder, "The selected preset supplies these biome defaults. To customize, uncomment or copy only the biome blocks you want to override.");
        AppendTemplateComment(builder, "All other biomes keep following the selected preset.");
        foreach (CreatureManagerPlugin.LevelBiomePreset preset in new[]
                 {
                     CreatureManagerPlugin.LevelBiomePreset.Easy,
                     CreatureManagerPlugin.LevelBiomePreset.Normal,
                     CreatureManagerPlugin.LevelBiomePreset.Hard,
                     CreatureManagerPlugin.LevelBiomePreset.VeryHard
                 })
        {
            AppendTemplateBlankLine(builder);
            AppendTemplateComment(builder, $"{preset} preset:");
            foreach (KeyValuePair<string, float[]> entry in GetLevelPresetWeights(preset))
            {
                AppendTemplateLine(builder, 0, $"{entry.Key}:");
                AppendTemplateLine(builder, 1, $"level: [{string.Join(", ", entry.Value.Select(FormatLevelFloat))}]");
            }
        }
    }

    private static string BuildDefaultOverrideYaml()
    {
        StringBuilder builder = new();
        AppendTemplateComment(builder, "CreatureManager creature configuration.");
        AppendTemplateComment(builder, "Copy entries from creatures.reference.yml, or run cm:full creature to generate creatures.full.yml for exhaustive field examples.");
        AppendTemplateComment(builder, "Loaded files: creatures.yml, creatures.yaml, creatures_*.yml, creatures_*.yaml.");
        AppendTemplateComment(builder, "Omitted fields keep the current creature value. The schema below is commented out and safe to leave in the file.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Schema:");
        AppendTemplateLine(builder, 0, "- prefab: EikthyrClone                 # target creature prefab id; use an existing prefab or a new clone name.");
        AppendTemplateLine(builder, 1, "enabled: true                         # false skips this creature entry.");
        AppendTemplateLine(builder, 1, "clonedFrom: Eikthyr                   # optional source creature prefab used to create prefab.");
        AppendTemplateLine(builder, 1, "ai: eikthyr_like                      # optional AI preset from ai.yml, or an existing creature prefab name.");
        AppendTemplateLine(builder, 1, "character:");
        AppendTemplateLine(builder, 2, "name: $enemy_eikthyr                  # existing token, localization/<Language>.yml token, or literal display name.");
        AppendTemplateLine(builder, 2, "faction: Boss                         # Vanilla Character.Faction or a custom name from factions.yml.");
        AppendTemplateLine(builder, 2, "boss: true, false, event_eikthyr      # boss, dontHideBossHud, bossEvent.");
        AppendTemplateLine(builder, 2, "defeatSetGlobalKey: defeated_eikthyr  # global key set when defeated.");
        AppendTemplateLine(builder, 2, "health: 500, 3600                    # health, regenAllHPTime.");
        AppendTemplateLine(builder, 2, "damageModifiers:");
        AppendTemplateLine(builder, 3, "blunt: Normal                         # Normal, Resistant, VeryResistant, Weak, VeryWeak, Immune, Ignore, SlightlyResistant.");
        AppendTemplateLine(builder, 3, "slash: Normal");
        AppendTemplateLine(builder, 3, "pierce: Normal");
        AppendTemplateLine(builder, 3, "chop: Ignore");
        AppendTemplateLine(builder, 3, "pickaxe: Ignore");
        AppendTemplateLine(builder, 3, "fire: Weak");
        AppendTemplateLine(builder, 3, "frost: Normal");
        AppendTemplateLine(builder, 3, "lightning: Normal");
        AppendTemplateLine(builder, 3, "poison: Normal");
        AppendTemplateLine(builder, 3, "spirit: Normal");
        AppendTemplateLine(builder, 2, "speed: 2, 5, 10, 300, 20, 300, 1     # crouchSpeed, walkSpeed, speed, turnSpeed, runSpeed, runTurnSpeed, acceleration.");
        AppendTemplateLine(builder, 2, "jump: 10, 0, 0.7, 0.1, 10            # jumpForce, jumpForceForward, jumpForceTiredFactor, airControl, jumpStaminaUsage.");
        AppendTemplateLine(builder, 2, "swim: true, 2, 2, 100, 0.05          # canSwim, swimDepth, swimSpeed, swimTurnSpeed, swimAcceleration.");
        AppendTemplateLine(builder, 2, "flight: false, 5, 12, 12             # flying, flySlowSpeed, flyFastSpeed, flyTurnSpeed.");
        AppendTemplateLine(builder, 1, "humanoid:");
        AppendTemplateLine(builder, 2, "defaultItems:                        # attack prefabs first, then loadout/appearance item prefabs.");
        AppendTemplateLine(builder, 3, "- attack_eikthyr_stomp");
        AppendTemplateLine(builder, 3, "- attack_bow_alt1");
        AppendTemplateLine(builder, 3, "- NPC_SwordIron_Right");
        AppendTemplateLine(builder, 2, "randomWeapon: [NPC_SwordIron_Right]  # Humanoid random weapon prefab list.");
        AppendTemplateLine(builder, 2, "randomArmor: []                      # Humanoid random armor prefab list.");
        AppendTemplateLine(builder, 2, "randomHair: []                       # Visual-only attach/attach_skin hair; does not use the helmet slot or grant item armor/effects.");
        AppendTemplateLine(builder, 2, "randomShield: []                     # Humanoid random shield prefab list.");
        AppendTemplateLine(builder, 2, "randomItems:                         # prefab[, chance].");
        AppendTemplateLine(builder, 3, "- NPC_HelmetIron_Worn0, 0.5");
        AppendTemplateLine(builder, 2, "randomSets:                          # setName, itemPrefab...");
        AppendTemplateLine(builder, 3, "- warrior, NPC_SwordIron_Right, NPC_ShieldIron_Worn");
        AppendTemplateLine(builder, 1, "scale: 1.1                           # Uniform creature scale; equipment visuals and a target-specific death ragdoll follow it.");
        AppendTemplateLine(builder, 1, "textures:                            # rendererName:materialIndex:textureName; matching death-ragdoll renderers receive the same override.");
        AppendTemplateLine(builder, 2, "- 'Body:0:eikthyr_clone.png'");
        AppendTemplateLine(builder, 1, "appearance:                           # Optional mapping; omitted fields inherit the source prefab and specified fields follow its death ragdoll.");
        AppendTemplateLine(builder, 2, "hair: NPC_Black_Hair5                # Item prefab name; use '' to clear ordinary hair.");
        AppendTemplateLine(builder, 2, "beard: Beard1                        # Item prefab name; use '' to clear ordinary beard.");
        AppendTemplateLine(builder, 2, "hairColor: '#2A1B12'                # #RRGGBB; also tints humanoid.randomHair and its ragdoll.");
        AppendTemplateLine(builder, 2, "skinColor: '#AD7A55'                # #RRGGBB.");
        AppendTemplateLine(builder, 2, "modelIndex: 0                       # Zero-based VisEquipment model index.");
        AppendTemplateLine(builder, 1, "availableAttackAnimations: []        # reference-only attackAnimation names generated in full scaffold; ignored in config.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Examples:");
        AppendTemplateLine(builder, 0, "- prefab: EikthyrClone");
        AppendTemplateLine(builder, 1, "clonedFrom: Eikthyr                  # Creates a new creature prefab before applying the fields below.");
        AppendTemplateLine(builder, 1, "character:");
        AppendTemplateLine(builder, 2, "health: 1200, 3600");
        AppendTemplateLine(builder, 1, "scale: 1.2");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- prefab: Troll");
        AppendTemplateLine(builder, 1, "enabled: false                       # Keeps this configuration entry loaded in the file but prevents it from applying.");
        AppendTemplateBlankLine(builder);
        return builder.ToString();
    }

    private static string BuildDefaultAiOverrideYaml()
    {
        StringBuilder builder = new();
        AppendTemplateComment(builder, "CreatureManager AI configuration.");
        AppendTemplateComment(builder, "In creatures.yml, 'ai: existingPrefabName' can copy AI directly from that creature prefab.");
        AppendTemplateComment(builder, "In ai.yml, a preset named like an existing prefab uses that prefab as its baseline unless copyFrom or clonedFrom is set.");
        AppendTemplateComment(builder, "Copy entries from ai.reference.yml into ai.yml only when you want to create or edit a reusable preset.");
        AppendTemplateComment(builder, "Loaded files: ai.yml, ai.yaml, ai_*.yml, ai_*.yaml.");
        AppendTemplateComment(builder, "Omitted fields keep the baseline AI value. If no baseline is found, they keep the current target AI value.");
        AppendTemplateComment(builder, "Effect lists are intentionally not managed here.");
        AppendTemplateComment(builder, "Use baseAI for shared BaseAI fields. AnimalAI creatures use baseAI only; monsterAI adds MonsterAI-only fields.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Schema:");
        AppendTemplateLine(builder, 0, "- ai: draugr_melee                    # reusable AI preset name.");
        AppendTemplateLine(builder, 1, "enabled: true                         # false skips this AI preset.");
        AppendTemplateLine(builder, 1, "copyFrom: ''                          # optional parent AI preset or creature prefab applied before this entry.");
        AppendTemplateLine(builder, 1, "clonedFrom: Draugr                    # optional AI prefab baseline copied before overrides.");
        AppendTemplateLine(builder, 1, "baseAI:");
        AppendTemplateLine(builder, 2, "senses: [30, 90, 999, false]          # viewRange, viewAngle, hearRange, mistVision.");
        AppendTemplateLine(builder, 2, "idleSound: [5, 0.5]                   # idleSoundInterval, idleSoundChance.");
        AppendTemplateLine(builder, 2, "movement: [false, Humanoid, 5, true]  # patrol, pathAgentType, moveMinAngle, smoothMovement.");
        AppendTemplateLine(builder, 2, "serpent: [false, 20]                  # serpentMovement, serpentTurnRadius.");
        AppendTemplateLine(builder, 2, "randomMove: [0, 5, 10, 20]           # jumpInterval, randomCircleInterval, randomMoveInterval, randomMoveRange.");
        AppendTemplateLine(builder, 2, "flight: [false, 0, 0, 5, 5, 2, 1, 2, 5, 0] # randomFly, chanceToTakeoff, chanceToLand, groundDuration, airDuration, maxLandAltitude, takeoffTime, flyAltitudeMin, flyAltitudeMax, flyAbsMinAltitude.");
        AppendTemplateLine(builder, 2, "avoid: [false, false, false, false, false, false] # avoidFire, afraidOfFire, avoidWater, avoidLava, skipLavaTargets, avoidLavaFlee.");
        AppendTemplateLine(builder, 2, "flee: [0, 90, 2]                      # fleeRange, fleeAngle, fleeInterval.");
        AppendTemplateLine(builder, 2, "aggressive: [true, false]             # aggravatable, passiveAggressive.");
        AppendTemplateLine(builder, 2, "messages: ['', '', '']                # spawnMessage, deathMessage, alertedMessage.");
        AppendTemplateLine(builder, 1, "monsterAI:");
        AppendTemplateLine(builder, 2, "alertRange: 20");
        AppendTemplateLine(builder, 2, "hunt: [true, true, 3]                 # enableHuntPlayer, attackPlayerObjects, privateAreaTriggerThreshold.");
        AppendTemplateLine(builder, 2, "chase: [0, 999, 200, 2]              # interceptTimeMin, interceptTimeMax, maxChaseDistance, minAttackInterval.");
        AppendTemplateLine(builder, 2, "circle: [5, 2, 6]                    # circleTargetInterval, circleTargetDuration, circleTargetDistance.");
        AppendTemplateLine(builder, 2, "hurtFlee: [false, 0, 0, false, 0, 0, false] # fleeIfHurtWhenTargetCantBeReached, fleeUnreachableSinceAttacking, fleeUnreachableSinceHurt, fleeIfNotAlerted, fleeIfLowHealth, fleeTimeSinceHurt, fleeInLava.");
        AppendTemplateLine(builder, 2, "charge: [false, false]                # circulateWhileCharging, circulateWhileChargingFlying.");
        AppendTemplateLine(builder, 2, "sleep: [false, 20, true, 50, 0, 0, 999] # sleeping, wakeupRange, noiseWakeup, maxNoiseWakeupRange, wakeUpDelayMin, wakeUpDelayMax, fallAsleepDistance.");
        AppendTemplateLine(builder, 2, "avoidLand: false");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Examples:");
        AppendTemplateLine(builder, 0, "- ai: Draugr                          # same as an existing prefab, so Draugr is the implicit baseline.");
        AppendTemplateLine(builder, 1, "baseAI:");
        AppendTemplateLine(builder, 2, "senses: [40, 120, 999, false]");
        AppendTemplateLine(builder, 1, "monsterAI:");
        AppendTemplateLine(builder, 2, "chase: [0, 999, 400, 1]");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- ai: vincent_archer                  # custom preset name, so copyFrom chooses the baseline.");
        AppendTemplateLine(builder, 1, "copyFrom: GoblinArcher");
        AppendTemplateLine(builder, 1, "monsterAI:");
        AppendTemplateLine(builder, 2, "circle: [8, 6, 8]");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- ai: Boar                            # AnimalAI uses baseAI only.");
        AppendTemplateLine(builder, 1, "baseAI:");
        AppendTemplateLine(builder, 2, "senses: [20, 90, 999, false]");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- ai: disabled_example");
        AppendTemplateLine(builder, 1, "enabled: false");
        AppendTemplateBlankLine(builder);
        return builder.ToString();
    }

    private static string BuildDefaultAttackOverrideYaml()
    {
        StringBuilder builder = new();
        AppendTemplateComment(builder, "CreatureManager attack configuration.");
        AppendTemplateComment(builder, "Copy entries from attacks.reference.yml and use the schema below for optional fields.");
        AppendTemplateComment(builder, "Loaded files: attacks.yml, attacks.yaml, attacks_*.yml, attacks_*.yaml.");
        AppendTemplateComment(builder, "Omitted fields keep the current attack prefab value. The schema below is commented out and safe to leave in the file.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Schema:");
        AppendTemplateLine(builder, 0, "- prefab: attack_bow_alt1             # target attack item prefab id; use an existing prefab or a new clone name.");
        AppendTemplateLine(builder, 1, "enabled: true                         # false skips this attack entry.");
        AppendTemplateLine(builder, 1, "clonedFrom: attack_bow                # optional source attack item prefab used to create prefab.");
        AppendTemplateLine(builder, 1, "damage: { pierce: 50, poison: 50, attackForce: 15, toolTier: 0 } # damage keys plus attackForce and toolTier.");
        AppendTemplateLine(builder, 1, "attack: [Projectile, bow_fire]        # Attack.AttackType, animation name.");
        AppendTemplateLine(builder, 1, "statusEffect: [Puke, 0.4]            # ObjectDB StatusEffect name, application chance from 0 to 1.");
        AppendTemplateLine(builder, 1, "projectile: [BlobTar_projectile, 30, 2, 1] # projectile prefab, velocity, accuracy, projectile count.");
        AppendTemplateLine(builder, 1, "ai: [30, 2, 20, 25]                 # aiAttackInterval, aiAttackRangeMin, aiAttackRange, aiAttackMaxAngle.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Examples:");
        AppendTemplateLine(builder, 0, "- prefab: attack_bow_alt1");
        AppendTemplateLine(builder, 1, "clonedFrom: attack_bow               # Creates a new attack prefab before applying the fields below.");
        AppendTemplateLine(builder, 1, "damage: { pierce: 50, poison: 50, attackForce: 15, toolTier: 0 }");
        AppendTemplateLine(builder, 1, "attack: [Projectile, bow_fire]");
        AppendTemplateLine(builder, 1, "statusEffect: [Puke, 0.4]");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- prefab: StaffLightning");
        AppendTemplateLine(builder, 1, "enabled: false                       # Keeps this configuration entry loaded in the file but prevents it from applying.");
        AppendTemplateBlankLine(builder);
        return builder.ToString();
    }

    private static string BuildDefaultProjectileOverrideYaml()
    {
        StringBuilder builder = new();
        AppendTemplateComment(builder, "CreatureManager projectile configuration.");
        AppendTemplateComment(builder, "Copy entries from projectile.reference.yml and use only the fields that should change.");
        AppendTemplateComment(builder, "Loaded files: projectile.yml, projectile.yaml, projectile_*.yml, projectile_*.yaml.");
        AppendTemplateComment(builder, "Omitted fields inherit the source prefab. Clones retain every unexposed component and field unchanged.");
        AppendTemplateComment(builder, "usedByAttacks is generated reference metadata. It is accepted in this file but never changes runtime behavior.");
        AppendTemplateComment(builder, "There is no type field; projectile and spawnAbility blocks already identify the supported component.");
        AppendTemplateComment(builder, "spawnAbility.spawnPrefabs uses Prefab[:weight]. Weight defaults to 1, is limited to 1000, and expanded lists are limited to 4096 slots.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Schema:");
        AppendTemplateLine(builder, 0, "- prefab: CM_BombBlob_Tar_projectile # existing target prefab id or a new clone name.");
        AppendTemplateLine(builder, 1, "enabled: true");
        AppendTemplateLine(builder, 1, "clonedFrom: BombBlob_Tar_projectile # optional source used to clone the entire prefab.");
        AppendTemplateLine(builder, 1, "usedByAttacks: [BombBlob_Tar]       # informational only; ignored when applying this entry.");
        AppendTemplateLine(builder, 1, "projectile:");
        AppendTemplateLine(builder, 2, "spawnOnHit: BlobTar                 # replace with a prefab name, or use null to clear it explicitly.");
        AppendTemplateLine(builder, 1, "spawnAbility:");
        AppendTemplateLine(builder, 2, "spawnPrefabs: [Mistile]             # whole-list replacement using Prefab[:weight]; omitted weight is 1.");
        AppendTemplateBlankLine(builder);
        AppendTemplateComment(builder, "Examples:");
        AppendTemplateLine(builder, 0, "- prefab: CM_DvergerMistile_spawn");
        AppendTemplateLine(builder, 1, "clonedFrom: DvergerMistile_spawn");
        AppendTemplateLine(builder, 1, "spawnAbility:");
        AppendTemplateLine(builder, 2, "spawnPrefabs: [Mistile]");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- prefab: CM_bonemass_spawn");
        AppendTemplateLine(builder, 1, "clonedFrom: bonemass_spawn");
        AppendTemplateLine(builder, 1, "spawnAbility:");
        AppendTemplateLine(builder, 2, "spawnPrefabs: [Skeleton:10, Blob]   # 10:1 selection weight; each spawn is rolled independently.");
        AppendTemplateBlankLine(builder);
        AppendTemplateLine(builder, 0, "- prefab: CM_BombBlob_Tar_projectile");
        AppendTemplateLine(builder, 1, "clonedFrom: BombBlob_Tar_projectile");
        AppendTemplateLine(builder, 1, "projectile:");
        AppendTemplateLine(builder, 2, "spawnOnHit: null                    # explicit clear; omitting projectile keeps the inherited value.");
        AppendTemplateBlankLine(builder);
        return builder.ToString();
    }

    private static void AppendTemplateComment(StringBuilder builder, string text)
    {
        builder.Append("# ");
        builder.AppendLine(text);
    }

    private static void AppendTemplateLine(StringBuilder builder, int indent, string text)
    {
        builder.Append("# ");
        builder.Append(' ', indent * 2);
        builder.AppendLine(text);
    }

    private static void AppendTemplateBlankLine(StringBuilder builder)
    {
        builder.AppendLine("#");
    }

    private static string GetTransformPath(Transform transform)
    {
        List<string> names = new();
        for (Transform? current = transform; current != null; current = current.parent)
        {
            names.Add(current.name);
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
