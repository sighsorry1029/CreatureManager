using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Bootstrap;
using UnityEngine;

namespace CreatureManager;

internal static class CreatureReferenceSections
{
    internal const string VanillaOwnerName = "Valheim";
    internal const string UnknownOwnerName = "Unknown / Untracked";

    private sealed class GroupedPrefab
    {
        public GameObject Prefab { get; set; } = null!;
        public string PrefabName { get; set; } = "";
        public string OwnerName { get; set; } = UnknownOwnerName;
    }

    internal static void AppendPrefabSections(
        StringBuilder builder,
        IEnumerable<GameObject> prefabs,
        Action<StringBuilder, GameObject> appendEntry)
    {
        List<IGrouping<string, GroupedPrefab>> sections = prefabs
            .Where(prefab => prefab != null)
            .Select(prefab =>
            {
                string prefabName = (prefab.name ?? "").Trim();
                string ownerName = CreaturePrefabOwnerResolver.GetOwnerName(prefabName);
                return new GroupedPrefab
                {
                    Prefab = prefab,
                    PrefabName = prefabName,
                    OwnerName = string.IsNullOrWhiteSpace(ownerName) ? UnknownOwnerName : ownerName.Trim()
                };
            })
            .OrderBy(entry => GetOwnerSortBucket(entry.OwnerName))
            .ThenBy(entry => entry.OwnerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.PrefabName, StringComparer.OrdinalIgnoreCase)
            .GroupBy(entry => entry.OwnerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool wroteSection = false;
        foreach (IGrouping<string, GroupedPrefab> section in sections)
        {
            if (wroteSection)
            {
                builder.AppendLine();
            }

            AppendSectionHeaderComment(builder, section.Key);

            bool wroteEntry = false;
            foreach (GroupedPrefab entry in section)
            {
                if (wroteEntry)
                {
                    builder.AppendLine();
                }

                appendEntry(builder, entry.Prefab);
                wroteEntry = true;
            }

            wroteSection = true;
        }
    }

    private static void AppendSectionHeaderComment(StringBuilder builder, string ownerName)
    {
        builder.Append("# ===== ");
        builder.Append(string.IsNullOrWhiteSpace(ownerName) ? UnknownOwnerName : ownerName.Trim());
        builder.AppendLine(" =====");
    }

    internal static int GetOwnerSortBucket(string ownerName)
    {
        if (string.Equals(ownerName, VanillaOwnerName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return string.Equals(ownerName, UnknownOwnerName, StringComparison.OrdinalIgnoreCase) ? 2 : 1;
    }
}

internal static class CreaturePrefabOwnerResolver
{
    internal static string GetOwnerName(string? prefabName)
    {
        string normalizedName = NormalizeName(prefabName);
        if (normalizedName.Length == 0)
        {
            return CreatureReferenceSections.UnknownOwnerName;
        }

        foreach (string candidate in EnumerateLookupCandidates(normalizedName))
        {
            if (CreatureVanillaAssetCatalog.IsVanillaPrefab(candidate))
            {
                return CreatureReferenceSections.VanillaOwnerName;
            }
        }

        return CreatureAssetOwnerCatalog.GetOwnerName(normalizedName);
    }

    private static IEnumerable<string> EnumerateLookupCandidates(string normalizedName)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        AddIfNew(normalizedName);

        int aliasSeparatorIndex = normalizedName.IndexOf(':');
        if (aliasSeparatorIndex > 0)
        {
            AddIfNew(normalizedName.Substring(0, aliasSeparatorIndex));
        }

        foreach (string candidate in seen)
        {
            yield return candidate;
        }

        void AddIfNew(string candidate)
        {
            string normalizedCandidate = NormalizeName(candidate);
            if (normalizedCandidate.Length > 0)
            {
                seen.Add(normalizedCandidate);
            }
        }
    }

    private static string NormalizeName(string? name)
    {
        return (name ?? "").Replace("(Clone)", "").Trim();
    }

}

internal static class CreatureTextureOwnerResolver
{
    internal static string GetOwnerName(string? textureName)
    {
        string normalizedName = NormalizeName(textureName);
        if (normalizedName.Length == 0)
        {
            return CreatureReferenceSections.UnknownOwnerName;
        }

        foreach (string candidate in EnumerateLookupCandidates(normalizedName))
        {
            if (CreatureVanillaAssetCatalog.IsVanillaTexture(candidate))
            {
                return CreatureReferenceSections.VanillaOwnerName;
            }
        }

        return CreatureAssetOwnerCatalog.GetOwnerName(normalizedName);
    }

    private static IEnumerable<string> EnumerateLookupCandidates(string normalizedName)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        AddIfNew(normalizedName);

        int aliasSeparatorIndex = normalizedName.IndexOf(':');
        if (aliasSeparatorIndex > 0)
        {
            AddIfNew(normalizedName.Substring(0, aliasSeparatorIndex));
        }

        foreach (string candidate in seen)
        {
            yield return candidate;
        }

        void AddIfNew(string candidate)
        {
            string normalizedCandidate = NormalizeName(candidate);
            if (normalizedCandidate.Length > 0)
            {
                seen.Add(normalizedCandidate);
            }
        }
    }

    private static string NormalizeName(string? name)
    {
        return (name ?? "").Replace("(Clone)", "").Replace("(Instance)", "").Trim();
    }

}

internal static class CreatureVanillaAssetCatalog
{
    private enum CatalogState
    {
        Uninitialized,
        Loaded,
        Unavailable
    }

    private static readonly object Sync = new();
    private static readonly HashSet<string> PrefabNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TextureNames = new(StringComparer.OrdinalIgnoreCase);
    private static CatalogState State;

    internal static bool IsVanillaPrefab(string prefabName)
    {
        EnsureLoaded();
        return State == CatalogState.Loaded &&
               !string.IsNullOrWhiteSpace(prefabName) &&
               PrefabNames.Contains(prefabName);
    }

    internal static bool IsVanillaTexture(string textureName)
    {
        EnsureLoaded();
        return State == CatalogState.Loaded &&
               !string.IsNullOrWhiteSpace(textureName) &&
               TextureNames.Contains(textureName);
    }

    private static void EnsureLoaded()
    {
        if (State != CatalogState.Uninitialized)
        {
            return;
        }

        lock (Sync)
        {
            if (State != CatalogState.Uninitialized)
            {
                return;
            }

            string manifestPath = Path.Combine(Application.dataPath, "StreamingAssets", "SoftRef", "manifest_extended");
            if (!File.Exists(manifestPath))
            {
                State = CatalogState.Unavailable;
                CreatureManagerPlugin.Log.LogWarning(
                    $"Vanilla asset manifest was not found at '{manifestPath}'. Reference owner sections may place vanilla assets under '{CreatureReferenceSections.UnknownOwnerName}'.");
                return;
            }

            const string marker = "path in bundle:";
            foreach (string rawLine in File.ReadLines(manifestPath))
            {
                int markerIndex = rawLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    continue;
                }

                string assetPath = rawLine.Substring(markerIndex + marker.Length).Trim();
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                if (string.IsNullOrWhiteSpace(assetName))
                {
                    continue;
                }

                if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    PrefabNames.Add(assetName);
                }
                else if (IsTextureAssetPath(assetPath))
                {
                    TextureNames.Add(assetName);
                }
            }

            State = CatalogState.Loaded;
            CreatureManagerPlugin.Log.LogDebug(
                $"Loaded {PrefabNames.Count} vanilla prefab and {TextureNames.Count} texture names for reference owner sections.");
        }
    }

    private static bool IsTextureAssetPath(string assetPath)
    {
        return assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".exr", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class CreatureAssetOwnerCatalog
{
    private sealed class PluginResourceSnapshot
    {
        public string OwnerName { get; set; } = "";
        public string PluginName { get; set; } = "";
        public string PluginGuid { get; set; } = "";
        public string AssemblyName { get; set; } = "";
        public string[] ResourceNames { get; set; } = Array.Empty<string>();
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<string, string> AssetOwners = new(StringComparer.OrdinalIgnoreCase);
    private static string? LoadedSignature;
    private static bool MappingsPrepared;

    internal static string GetOwnerName(string assetName)
    {
        PrepareMappings();
        foreach (string candidate in EnumerateLookupCandidates(assetName))
        {
            if (AssetOwners.TryGetValue(candidate, out string ownerName) &&
                !string.IsNullOrWhiteSpace(ownerName))
            {
                return ownerName;
            }
        }

        return CreatureReferenceSections.UnknownOwnerName;
    }

    internal static void InvalidateMappings()
    {
        lock (Sync)
        {
            MappingsPrepared = false;
            LoadedSignature = null;
        }
    }

    internal static void PrepareMappings()
    {
        if (MappingsPrepared)
        {
            return;
        }

        RefreshMappings();
    }

    internal static void RefreshMappings()
    {
        string signature = BuildSignature();
        if (MappingsPrepared && string.Equals(signature, LoadedSignature, StringComparison.Ordinal))
        {
            return;
        }

        lock (Sync)
        {
            if (MappingsPrepared && string.Equals(signature, LoadedSignature, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(signature, LoadedSignature, StringComparison.Ordinal))
            {
                MappingsPrepared = true;
                return;
            }

            AssetOwners.Clear();
            List<PluginResourceSnapshot> plugins = GetPluginResources();
            foreach (AssetBundle assetBundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                string bundleName = assetBundle.name ?? "";
                if (bundleName.Length == 0)
                {
                    continue;
                }

                string ownerName = ResolveOwnerName(bundleName, plugins);
                if (string.IsNullOrWhiteSpace(ownerName))
                {
                    continue;
                }

                foreach (string assetPath in assetBundle.GetAllAssetNames())
                {
                    if (!IsTrackedAssetPath(assetPath))
                    {
                        continue;
                    }

                    string assetName = Path.GetFileNameWithoutExtension(assetPath);
                    if (!string.IsNullOrWhiteSpace(assetName))
                    {
                        AssetOwners[assetName] = ownerName;
                    }
                }
            }

            LoadedSignature = signature;
            MappingsPrepared = true;
            CreatureManagerPlugin.Log.LogDebug($"Tracked {AssetOwners.Count} asset owner mapping(s) for reference sections.");
        }
    }

    private static bool IsTrackedAssetPath(string assetPath)
    {
        return assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".exr", StringComparison.OrdinalIgnoreCase) ||
               assetPath.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateLookupCandidates(string assetName)
    {
        string normalizedName = (assetName ?? "").Replace("(Clone)", "").Trim();
        if (normalizedName.Length == 0)
        {
            yield break;
        }

        yield return normalizedName;
        int aliasSeparatorIndex = normalizedName.IndexOf(':');
        if (aliasSeparatorIndex > 0)
        {
            yield return normalizedName.Substring(0, aliasSeparatorIndex);
        }
    }

    private static List<PluginResourceSnapshot> GetPluginResources()
    {
        return Chainloader.PluginInfos.Values
            .Select(pluginInfo =>
            {
                string pluginName = (pluginInfo.Metadata.Name ?? "").Trim();
                string pluginGuid = (pluginInfo.Metadata.GUID ?? "").Trim();
                string assemblyName = "";
                string[] resourceNames = Array.Empty<string>();
                try
                {
                    assemblyName = pluginInfo.Instance?.GetType().Assembly.GetName().Name ?? "";
                    resourceNames = pluginInfo.Instance?.GetType().Assembly.GetManifestResourceNames() ?? Array.Empty<string>();
                }
                catch
                {
                    // Plugin assemblies can be partially initialized while ObjectDB is being copied.
                }

                return new PluginResourceSnapshot
                {
                    OwnerName = pluginName.Length > 0 ? pluginName : pluginGuid,
                    PluginName = pluginName,
                    PluginGuid = pluginGuid,
                    AssemblyName = assemblyName,
                    ResourceNames = resourceNames
                };
            })
            .Where(plugin => plugin.OwnerName.Length > 0)
            .ToList();
    }

    private static string ResolveOwnerName(string bundleName, List<PluginResourceSnapshot> plugins)
    {
        PluginResourceSnapshot? embeddedOwner = plugins.FirstOrDefault(plugin =>
            plugin.ResourceNames.Any(resourceName =>
                resourceName.EndsWith(bundleName, StringComparison.OrdinalIgnoreCase)));
        if (embeddedOwner != null)
        {
            return embeddedOwner.OwnerName;
        }

        string normalizedBundleName = NormalizeToken(Path.GetFileNameWithoutExtension(bundleName));
        if (normalizedBundleName.Length == 0)
        {
            return "";
        }

        PluginResourceSnapshot? tokenOwner = plugins.FirstOrDefault(plugin =>
        {
            string normalizedPluginName = NormalizeToken(plugin.PluginName);
            string normalizedPluginGuid = NormalizeToken(plugin.PluginGuid);
            string normalizedAssemblyName = NormalizeToken(plugin.AssemblyName);
            return IsTokenMatch(normalizedBundleName, normalizedPluginName) ||
                   IsTokenMatch(normalizedBundleName, normalizedPluginGuid) ||
                   IsTokenMatch(normalizedBundleName, normalizedAssemblyName);
        });

        return tokenOwner?.OwnerName ?? "";
    }

    private static bool IsTokenMatch(string bundleName, string pluginToken)
    {
        return pluginToken.Length > 0 &&
               (bundleName.IndexOf(pluginToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                pluginToken.IndexOf(bundleName, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        StringBuilder builder = new();
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string BuildSignature()
    {
        IEnumerable<string> bundleTokens = AssetBundle.GetAllLoadedAssetBundles()
            .Select(bundle => bundle.name ?? "")
            .Where(name => name.Length > 0)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> pluginTokens = Chainloader.PluginInfos.Values
            .Select(pluginInfo =>
            {
                string pluginName = pluginInfo.Metadata.Name ?? "";
                string pluginGuid = pluginInfo.Metadata.GUID ?? "";
                string assemblyName = "";
                try
                {
                    assemblyName = pluginInfo.Instance?.GetType().Assembly.GetName().Name ?? "";
                }
                catch
                {
                    // The signature only needs stable ownership inputs.
                }

                return $"{pluginGuid}:{pluginName}:{assemblyName}";
            })
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase);

        return string.Join("|", bundleTokens) + "||" + string.Join("|", pluginTokens);
    }
}
