using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CreatureManager;

internal static class CreatureTextureRegistry
{
    private const int MaxSynchronizedTextureObjects = 512;
    private static readonly Dictionary<string, FileTextureEntry> FileTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> ResourceTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, SynchronizedTextureEntry> SynchronizedTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly char[] PortableInvalidFileNameChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static bool ResourceTextureRefreshPending = true;
    private static bool SynchronizedTextureFilesAuthoritative;
    private static readonly MethodInfo? LoadImageMethod = AccessTools.Method(
        typeof(ImageConversion),
        nameof(ImageConversion.LoadImage),
        new[] { typeof(Texture2D), typeof(byte[]) });

    private sealed class FileTextureEntry
    {
        internal DateTime LastWriteTimeUtc;
        internal long Length;
        internal Texture2D Texture = null!;
        internal bool Dirty;
    }

    private sealed class SynchronizedTextureEntry
    {
        internal byte[] Bytes = Array.Empty<byte>();
        internal Texture2D? Texture;
        internal bool Active;
        internal bool Dirty = true;
    }

    internal static void Dispose()
    {
        foreach (FileTextureEntry entry in FileTextureCache.Values)
        {
            if (entry.Texture != null)
            {
                Object.Destroy(entry.Texture);
            }
        }

        foreach (SynchronizedTextureEntry entry in SynchronizedTextureCache.Values)
        {
            if (entry.Texture != null)
            {
                Object.Destroy(entry.Texture);
            }
        }

        FileTextureCache.Clear();
        SynchronizedTextureCache.Clear();
        SynchronizedTextureFilesAuthoritative = false;
        ResourceTextureCache.Clear();
        ResourceTextureRefreshPending = true;
    }

    internal static void InvalidateResourceTextures()
    {
        // Resource textures are owned by Unity. Drop only our lookup references so
        // the next game-data session can discover the currently loaded assets.
        ResourceTextureCache.Clear();
        ResourceTextureRefreshPending = true;
    }

    /// <summary>
    /// Releases synchronized textures that no longer belong to the active generation. Call only
    /// after live creature/ragdoll texture overrides have been detached at the world-unload
    /// boundary; an in-world renderer may otherwise still reference these Unity objects.
    /// </summary>
    internal static void PruneInactiveSynchronizedTextures()
    {
        foreach (string name in SynchronizedTextureCache
                     .Where(pair => !pair.Value.Active)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            SynchronizedTextureEntry entry = SynchronizedTextureCache[name];
            if (entry.Texture != null)
            {
                Object.Destroy(entry.Texture);
            }

            SynchronizedTextureCache.Remove(name);
        }
    }

    internal static void BeginResourceLookupPass()
    {
        // Permit one full Unity resource scan during each definition apply. Multiple
        // missing or misspelled names in the same YAML bundle must not each rescan it.
        ResourceTextureRefreshPending = true;
    }

    internal static bool TrySetSynchronizedTextureFiles(
        IReadOnlyDictionary<string, byte[]> files,
        bool authoritative,
        out string error)
    {
        error = "";
        if (files == null)
        {
            error = "the synchronized texture map is null.";
            return false;
        }

        Dictionary<string, byte[]> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, byte[]> pair in files)
        {
            if (!TryNormalizeTextureReference(
                    pair.Key,
                    out string lookupName,
                    out _,
                    out bool localPngAllowed,
                    out string nameError) ||
                !localPngAllowed)
            {
                error = $"synchronized texture name '{pair.Key}' is invalid: " +
                        (nameError.Length > 0 ? nameError : "only leaf .png file names are allowed.");
                return false;
            }

            byte[]? bytes = pair.Value;
            if (bytes == null || !HasPngSignature(bytes))
            {
                error = $"synchronized texture '{pair.Key}' is not a valid PNG payload.";
                return false;
            }

            if (normalized.ContainsKey(lookupName))
            {
                error = $"synchronized texture name '{pair.Key}' duplicates normalized name '{lookupName}'.";
                return false;
            }

            normalized[lookupName] = bytes;
        }

        if (!TryPreflightSynchronizedTextures(normalized, out error))
        {
            return false;
        }

        int retainedTextureObjects = SynchronizedTextureCache.Values.Count(entry => entry.Texture != null);
        int newlyDecodedNames = normalized.Keys.Count(name =>
            !SynchronizedTextureCache.TryGetValue(name, out SynchronizedTextureEntry? entry) || entry.Texture == null);
        if (retainedTextureObjects + newlyDecodedNames > MaxSynchronizedTextureObjects)
        {
            error = $"synchronized textures would retain more than {MaxSynchronizedTextureObjects} Unity texture objects before the next world unload.";
            return false;
        }

        foreach (SynchronizedTextureEntry entry in SynchronizedTextureCache.Values)
        {
            entry.Active = false;
        }

        if (authoritative)
        {
            foreach (KeyValuePair<string, byte[]> pair in normalized)
            {
                byte[] bytes = pair.Value;
                if (!SynchronizedTextureCache.TryGetValue(pair.Key, out SynchronizedTextureEntry? entry))
                {
                    entry = new SynchronizedTextureEntry();
                    SynchronizedTextureCache[pair.Key] = entry;
                }

                if (!BytesEqual(entry.Bytes, bytes))
                {
                    entry.Bytes = bytes.ToArray();
                    entry.Dirty = true;
                }

                entry.Active = true;
            }
        }

        foreach (SynchronizedTextureEntry entry in SynchronizedTextureCache.Values.Where(entry => !entry.Active))
        {
            // Retain the Unity texture object until the world-unload cleanup because live ragdoll
            // property blocks can still reference it, but release the no-longer-active wire bytes.
            entry.Bytes = Array.Empty<byte>();
            entry.Dirty = true;
        }

        foreach (string name in SynchronizedTextureCache
                     .Where(pair => !pair.Value.Active && pair.Value.Texture == null)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            SynchronizedTextureCache.Remove(name);
        }

        SynchronizedTextureFilesAuthoritative = authoritative;
        return true;
    }

    private static bool TryPreflightSynchronizedTextures(
        IReadOnlyDictionary<string, byte[]> files,
        out string error)
    {
        error = "";
        List<byte[]> decodedPayloads = new();
        Texture2D? probe = null;
        try
        {
            foreach (KeyValuePair<string, byte[]> pair in files)
            {
                if (decodedPayloads.Any(bytes => ReferenceEquals(bytes, pair.Value)))
                {
                    continue;
                }

                probe ??= new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!TryLoadImage(probe, pair.Value))
                {
                    error = $"synchronized texture '{pair.Key}' could not be decoded by Unity.";
                    return false;
                }

                decodedPayloads.Add(pair.Value);
            }

            return true;
        }
        finally
        {
            if (probe != null)
            {
                Object.Destroy(probe);
            }
        }
    }

    internal static bool TryResolveLocalPngFile(
        string textureName,
        out string lookupName,
        out string fullPath,
        out string error)
    {
        fullPath = "";
        if (!TryNormalizeTextureReference(
                textureName,
                out lookupName,
                out string pngFileName,
                out bool localPngAllowed,
                out error) ||
            !localPngAllowed)
        {
            if (error.Length == 0)
            {
                error = "only leaf .png file names can resolve to a local texture file.";
            }

            return false;
        }

        if (!TryGetSandboxedPngPath(pngFileName, out fullPath))
        {
            error = "the PNG path is outside the CreatureManager texture directory.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            // A missing local file is not an error: the name may refer to a loaded Unity texture.
            fullPath = "";
            return false;
        }

        if (!IsSafeRegularFile(fullPath))
        {
            error = "the PNG path is not a regular file inside the CreatureManager texture directory.";
            fullPath = "";
            return false;
        }

        return true;
    }

    internal static void MarkFileTextureDirty(string path)
    {
        if (!TryNormalizeWatchedPngPath(path, out string fullPath))
        {
            return;
        }

        if (FileTextureCache.TryGetValue(fullPath, out FileTextureEntry? cached))
        {
            cached.Dirty = true;
        }
    }

    internal static void MarkAllFileTexturesDirty()
    {
        foreach (FileTextureEntry entry in FileTextureCache.Values)
        {
            entry.Dirty = true;
        }
    }

    internal static string BuildTextureReferenceText()
    {
        CreatureAssetOwnerCatalog.PrepareMappings();
        List<TextureReferenceEntry> entries = GetAvailableTextureEntries();
        if (entries.Count == 0)
        {
            return "[]\n";
        }

        StringBuilder builder = new();
        builder.AppendLine("# Generated by CreatureManager (texture reference).");
        builder.AppendLine("# Use these names as the textureName value in visual.textures entries.");
        builder.AppendLine("# PNG files in BepInEx/config/CreatureManager/textures can also be used by file name.");
        builder.AppendLine("# Owner sections are best-effort guesses from the Valheim manifest and loaded asset bundles.");
        builder.AppendLine();

        bool wroteSection = false;
        foreach (IGrouping<string, TextureReferenceEntry> section in entries
                     .OrderBy(entry => CreatureReferenceSections.GetOwnerSortBucket(entry.OwnerName))
                     .ThenBy(entry => entry.OwnerName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(entry => entry.OwnerName, StringComparer.OrdinalIgnoreCase))
        {
            if (wroteSection)
            {
                builder.AppendLine();
            }

            builder.Append("# ===== ");
            builder.Append(section.Key);
            builder.AppendLine(" =====");
            foreach (TextureReferenceEntry entry in section)
            {
                builder.AppendLine(entry.Name);
            }

            wroteSection = true;
        }

        return builder.ToString();
    }

    internal static Texture? GetTexture(string textureName)
    {
        string rawName = (textureName ?? "").Trim();
        if (rawName.Length == 0)
        {
            return null;
        }

        bool portableFileName = TryNormalizeTextureReference(
                rawName,
                out string lookupName,
                out string pngFileName,
                out bool localPngAllowed,
                out _);

        if (portableFileName && SynchronizedTextureFilesAuthoritative)
        {
            if (SynchronizedTextureCache.TryGetValue(lookupName, out SynchronizedTextureEntry? synchronized) &&
                synchronized.Active)
            {
                Texture2D? synchronizedTexture = LoadSynchronizedTexture(lookupName, synchronized);
                if (synchronizedTexture != null)
                {
                    return synchronizedTexture;
                }
            }
        }
        else if (portableFileName && localPngAllowed &&
                 TryGetSandboxedPngPath(pngFileName, out string directPath) &&
                 IsSafeRegularFile(directPath))
        {
            return LoadTextureFile(directPath);
        }

        string[] resourceNames = portableFileName &&
                                 !rawName.Equals(lookupName, StringComparison.OrdinalIgnoreCase)
            ? new[] { rawName, lookupName }
            : new[] { rawName };
        foreach (string resourceName in resourceNames)
        {
            if (ResourceTextureCache.TryGetValue(resourceName, out Texture2D texture) && texture != null)
            {
                return texture;
            }
        }

        if (ResourceTextureRefreshPending)
        {
            RefreshResourceTextureCache();
        }

        foreach (string resourceName in resourceNames)
        {
            if (ResourceTextureCache.TryGetValue(resourceName, out Texture2D texture) && texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    private static Texture2D? LoadTextureFile(string path)
    {
        path = Path.GetFullPath(path);
        FileTextureCache.TryGetValue(path, out FileTextureEntry? cached);
        try
        {
            FileInfo file = new(path);
            if (cached?.Texture != null &&
                !cached.Dirty &&
                cached.LastWriteTimeUtc == file.LastWriteTimeUtc &&
                cached.Length == file.Length)
            {
                return cached.Texture;
            }

            byte[] bytes = File.ReadAllBytes(path);
            bool created = cached?.Texture == null;
            Texture2D texture = created ? new Texture2D(2, 2, TextureFormat.RGBA32, false) : cached!.Texture;
            if (!TryLoadImage(texture, bytes))
            {
                CreatureManagerPlugin.Log.LogWarning($"Failed to load texture file {path}.");
                if (created)
                {
                    Object.Destroy(texture);
                }

                return cached?.Texture;
            }

            texture.name = Path.GetFileNameWithoutExtension(path);
            if (cached != null)
            {
                cached.LastWriteTimeUtc = file.LastWriteTimeUtc;
                cached.Length = file.Length;
                cached.Texture = texture;
                cached.Dirty = false;
                return texture;
            }

            FileTextureCache[path] = new FileTextureEntry
            {
                LastWriteTimeUtc = file.LastWriteTimeUtc,
                Length = file.Length,
                Texture = texture,
                Dirty = false
            };
            return texture;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to load texture file {path}: {ex.Message}");
            return cached?.Texture;
        }
    }

    private static Texture2D? LoadSynchronizedTexture(
        string lookupName,
        SynchronizedTextureEntry entry)
    {
        if (!entry.Dirty && entry.Texture != null)
        {
            return entry.Texture;
        }

        bool created = entry.Texture == null;
        Texture2D texture = created
            ? new Texture2D(2, 2, TextureFormat.RGBA32, false)
            : entry.Texture!;
        if (!TryLoadImage(texture, entry.Bytes))
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Failed to load synchronized texture '{lookupName}'.");
            if (created)
            {
                Object.Destroy(texture);
            }

            return entry.Texture;
        }

        texture.name = lookupName;
        entry.Texture = texture;
        entry.Dirty = false;
        return texture;
    }

    private static bool TryLoadImage(Texture2D texture, byte[] bytes)
    {
        if (LoadImageMethod == null)
        {
            CreatureManagerPlugin.Log.LogWarning("UnityEngine.ImageConversion.LoadImage was not found.");
            return false;
        }

        try
        {
            return LoadImageMethod.Invoke(null, new object[] { texture, bytes }) is true;
        }
        catch
        {
            // A malformed image must fail only this visual override, not the surrounding
            // definition transaction. Callers retain the last successfully decoded texture.
            return false;
        }
    }

    private static bool TryNormalizeTextureReference(
        string? textureName,
        out string lookupName,
        out string pngFileName,
        out bool localPngAllowed,
        out string error)
    {
        lookupName = "";
        pngFileName = "";
        localPngAllowed = false;
        error = "";
        string value = (textureName ?? "").Trim();
        if (value.Length == 0)
        {
            error = "the name is empty.";
            return false;
        }

        try
        {
            if (Path.IsPathRooted(value))
            {
                error = "rooted paths are not allowed.";
                return false;
            }

            if (value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0)
            {
                error = "directory separators are not allowed.";
                return false;
            }

            if (value.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                error = "'..' is not allowed in a texture name.";
                return false;
            }

            if (value.Length > 255 ||
                value.EndsWith(".", StringComparison.Ordinal) ||
                value.EndsWith(" ", StringComparison.Ordinal) ||
                value.IndexOfAny(PortableInvalidFileNameChars) >= 0 ||
                value.Any(char.IsControl) ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "the name is not a valid portable file name.";
                return false;
            }

            string extension = Path.GetExtension(value);
            lookupName = extension.Length == 0
                ? value
                : Path.GetFileNameWithoutExtension(value);
            if (lookupName.Length == 0 || lookupName == ".")
            {
                error = "the PNG base name is empty.";
                return false;
            }

            localPngAllowed = extension.Length == 0 ||
                              extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            if (localPngAllowed)
            {
                pngFileName = extension.Length == 0 ? value + ".png" : value;
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryGetSandboxedPngPath(string pngFileName, out string fullPath)
    {
        fullPath = "";
        try
        {
            string textureDirectory = Path.GetFullPath(CreatureDomainManager.TextureDirectoryPath);
            string candidate = Path.GetFullPath(Path.Combine(textureDirectory, pngFileName));
            string? directory = Path.GetDirectoryName(candidate);
            if (directory == null ||
                !directory.Equals(textureDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryNormalizeWatchedPngPath(string path, out string fullPath)
    {
        fullPath = "";
        try
        {
            string candidate = Path.GetFullPath(path);
            string fileName = Path.GetFileName(candidate);
            if (!TryNormalizeTextureReference(
                    fileName,
                    out _,
                    out string pngFileName,
                    out bool localPngAllowed,
                    out _) ||
                !localPngAllowed ||
                !TryGetSandboxedPngPath(pngFileName, out string sandboxed) ||
                !candidate.Equals(sandboxed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSafeRegularFile(string path)
    {
        try
        {
            return File.Exists(path) &&
                   (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool HasPngSignature(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count < PngSignature.Length)
        {
            return false;
        }

        for (int index = 0; index < PngSignature.Length; index++)
        {
            if (bytes[index] != PngSignature[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool BytesEqual(IReadOnlyList<byte> first, IReadOnlyList<byte> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (int index = 0; index < first.Count; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void RefreshResourceTextureCache()
    {
        ResourceTextureRefreshPending = false;
        foreach (string staleName in ResourceTextureCache
                     .Where(entry => entry.Value == null)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            ResourceTextureCache.Remove(staleName);
        }

        foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>().Where(texture => texture != null && texture.GetInstanceID() >= 0))
        {
            if (!ResourceTextureCache.TryGetValue(texture.name, out Texture2D existing) || existing == null)
            {
                ResourceTextureCache[texture.name] = texture;
            }
        }
    }

    private static List<TextureReferenceEntry> GetAvailableTextureEntries()
    {
        RefreshResourceTextureCache();
        return ResourceTextureCache.Keys
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new TextureReferenceEntry
            {
                Name = name,
                OwnerName = CreatureTextureOwnerResolver.GetOwnerName(name)
            })
            .ToList();
    }

    private sealed class TextureReferenceEntry
    {
        public string Name { get; set; } = "";
        public string OwnerName { get; set; } = CreatureReferenceSections.UnknownOwnerName;
    }
}
