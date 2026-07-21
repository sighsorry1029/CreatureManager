using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using YamlDotNet.Serialization;

namespace LocalizationManager;

internal static class Localizer
{
    private static BaseUnityPlugin? _plugin;

    private static BaseUnityPlugin plugin
    {
        get
        {
            if (_plugin is null)
            {
                IEnumerable<TypeInfo> types;
                try
                {
                    types = Assembly.GetExecutingAssembly().DefinedTypes.ToList();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).Select(t => t.GetTypeInfo());
                }

                _plugin = (BaseUnityPlugin)Chainloader.ManagerObject.GetComponent(types.First(t => t.IsClass && typeof(BaseUnityPlugin).IsAssignableFrom(t)));
            }

            return _plugin;
        }
    }

    private static readonly List<string> fileExtensions = [".json", ".yml"];

    internal static void Load(Harmony harmony)
    {
        _ = plugin;
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(Localization), nameof(Localization.SetupLanguage), new[] { typeof(string) }),
            prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Localizer), nameof(BeforeLanguageSetup)), Priority.First),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Localizer), nameof(SafeLoadLocalization)), Priority.Last));
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(FejdStartup), nameof(FejdStartup.SetupGui), Type.EmptyTypes),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Localizer), nameof(LoadLocalizationLater)), Priority.Last));
    }

    internal static void Unload()
    {
        _plugin = null;
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
                $"Failed to load {plugin.Info.Metadata.Name} localization for '{language}'. " +
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
        foreach (string file in Directory.GetFiles(Path.GetDirectoryName(Paths.PluginPath)!, $"{plugin.Info.Metadata.Name}.*", SearchOption.AllDirectories).Where(f => fileExtensions.IndexOf(Path.GetExtension(f)) >= 0))
        {
            string[] parts = Path.GetFileNameWithoutExtension(file).Split('.');
            if (parts.Length < 2)
            {
                continue;
            }

            string key = parts[1];
            if (localizationFiles.ContainsKey(key))
            {
                // Handle duplicate key
                Debug.LogWarning($"Duplicate key {key} found for {plugin.Info.Metadata.Name}. The duplicate file found at {file} will be skipped.");
            }
            else
            {
                localizationFiles[key] = file;
            }
        }

        if (LoadTranslationFromAssembly("English") is not { } englishAssemblyData)
        {
            throw new Exception($"Found no English localizations in mod {plugin.Info.Metadata.Name}. Expected an embedded resource translations/English.json or translations/English.yml.");
        }

        Dictionary<string, string>? localizationTexts = new DeserializerBuilder().IgnoreFields().Build().Deserialize<Dictionary<string, string>?>(Encoding.UTF8.GetString(englishAssemblyData));
        if (localizationTexts is null)
        {
            throw new Exception($"Localization for mod {plugin.Info.Metadata.Name} failed: Localization file was empty.");
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
            foreach (KeyValuePair<string, string> kv in new DeserializerBuilder().IgnoreFields().Build().Deserialize<Dictionary<string, string>?>(localizationData) ?? new Dictionary<string, string>())
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
        foreach (string extension in fileExtensions)
        {
            if (ReadEmbeddedFileBytes("translations." + language + extension) is { } data)
            {
                return data;
            }
        }

        return null;
    }

    public static byte[]? ReadEmbeddedFileBytes(string resourceFileName, Assembly? containingAssembly = null)
    {
        using MemoryStream stream = new();
        containingAssembly ??= Assembly.GetCallingAssembly();
        if (containingAssembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(resourceFileName, StringComparison.Ordinal)) is { } name)
        {
            containingAssembly.GetManifestResourceStream(name)?.CopyTo(stream);
        }

        return stream.Length == 0 ? null : stream.ToArray();
    }
}
