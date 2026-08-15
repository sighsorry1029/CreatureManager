using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using YamlDotNet.Serialization;

namespace LocalizationManager;

internal static class Localizer
{
    private const string ModName = CreatureManager.CreatureManagerPlugin.ModName;
    private const string LocalizationExtension = ".yml";
    private static readonly Assembly ModAssembly = typeof(CreatureManager.CreatureManagerPlugin).Assembly;
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreFields()
        .Build();

    internal static void Load(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(Localization), nameof(Localization.SetupLanguage), new[] { typeof(string) }),
            prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Localizer), nameof(BeforeLanguageSetup)), Priority.First),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Localizer), nameof(SafeLoadLocalization)), Priority.Last));
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(FejdStartup), nameof(FejdStartup.SetupGui), Type.EmptyTypes),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Localizer), nameof(LoadLocalizationLater)), Priority.Last));
    }

    private static void LoadLocalizationLater()
    {
        Localization localization = Localization.instance;
        if (localization != null)
        {
            SafeLoadLocalization(localization, localization.GetSelectedLanguage());
        }
    }

    [HarmonyPriority(Priority.First)]
    private static void BeforeLanguageSetup(Localization __instance)
    {
        try
        {
            CreatureManager.CreatureServerLocalization.BeforeLanguageSetup(__instance);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to prepare CreatureManager server localization before a language change. {ex.Message}");
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void SafeLoadLocalization(Localization __instance, string language)
    {
        try
        {
            LoadLocalization(__instance, language);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Failed to load {ModName} localization for '{language}'. " +
                $"Vanilla localization will remain active. {ex.Message}");
        }

        try
        {
            CreatureManager.CreatureServerLocalization.ApplyCurrentLocalization(__instance, language);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to apply CreatureManager server localization for '{language}'. {ex.Message}");
        }
    }

    private static void LoadLocalization(Localization __instance, string language)
    {
        Dictionary<string, string> localizationFiles = new();
        string? pluginDirectory = Path.GetDirectoryName(ModAssembly.Location);
        IEnumerable<string> files = pluginDirectory != null && Directory.Exists(pluginDirectory)
            ? Directory.EnumerateFiles(pluginDirectory, $"{ModName}.*", SearchOption.TopDirectoryOnly)
            : Enumerable.Empty<string>();
        foreach (string file in files.Where(file =>
                     Path.GetExtension(file).Equals(LocalizationExtension, StringComparison.OrdinalIgnoreCase)))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Length <= ModName.Length + 1 || fileName[ModName.Length] != '.')
            {
                continue;
            }

            string key = fileName.Substring(ModName.Length + 1);
            if (localizationFiles.ContainsKey(key))
            {
                // Handle duplicate key
                Debug.LogWarning($"Duplicate key {key} found for {ModName}. The duplicate file found at {file} will be skipped.");
            }
            else
            {
                localizationFiles[key] = file;
            }
        }

        if (LoadTranslationFromAssembly("English") is not { } englishAssemblyData)
        {
            throw new Exception($"Found no English localizations in mod {ModName}. Expected an embedded resource translations/English.yml.");
        }

        Dictionary<string, string>? localizationTexts = Deserializer.Deserialize<Dictionary<string, string>?>(Encoding.UTF8.GetString(englishAssemblyData));
        if (localizationTexts is null)
        {
            throw new Exception($"Localization for mod {ModName} failed: Localization file was empty.");
        }

        string? localizationData = null;
        if (language != "English")
        {
            if (localizationFiles.TryGetValue(language, out string? localizationFile))
            {
                localizationData = File.ReadAllText(localizationFile);
            }
            else if (LoadTranslationFromAssembly(language) is { } languageAssemblyData)
            {
                localizationData = Encoding.UTF8.GetString(languageAssemblyData);
            }
        }

        if (localizationData is null && localizationFiles.TryGetValue("English", out string? localizationFile1))
        {
            localizationData = File.ReadAllText(localizationFile1);
        }

        if (localizationData is not null)
        {
            foreach (KeyValuePair<string, string> kv in Deserializer.Deserialize<Dictionary<string, string>?>(localizationData) ?? new Dictionary<string, string>())
            {
                localizationTexts[kv.Key] = kv.Value;
            }
        }

        foreach (KeyValuePair<string, string> s in localizationTexts)
        {
            __instance.AddWord(s.Key, s.Value);
        }
    }

    private static byte[]? LoadTranslationFromAssembly(string language)
    {
        return ReadEmbeddedFileBytes("translations." + language + LocalizationExtension);
    }

    private static byte[]? ReadEmbeddedFileBytes(string resourceFileName)
    {
        using MemoryStream stream = new();
        if (ModAssembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(resourceFileName, StringComparison.Ordinal)) is { } name)
        {
            ModAssembly.GetManifestResourceStream(name)?.CopyTo(stream);
        }

        return stream.Length == 0 ? null : stream.ToArray();
    }
}
