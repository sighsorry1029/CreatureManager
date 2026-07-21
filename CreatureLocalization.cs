using System;

namespace CreatureManager;

internal static class CreatureLocalization
{
    internal static string Localize(string key, string fallback)
    {
        string normalizedKey = key.StartsWith("$", StringComparison.Ordinal) ? key.Substring(1) : key;
        string token = "$" + normalizedKey;
        Localization? localization = Localization.instance;
        if (localization == null)
        {
            return fallback;
        }

        try
        {
            string localized = localization.Localize(token);
            return string.IsNullOrEmpty(localized) ||
                   string.Equals(localized, token, StringComparison.Ordinal) ||
                   string.Equals(localized, normalizedKey, StringComparison.Ordinal)
                ? fallback
                : localized;
        }
        catch
        {
            return fallback;
        }
    }

    internal static string LocalizeText(string text)
    {
        if (string.IsNullOrEmpty(text) || Localization.instance == null)
        {
            return text;
        }

        try
        {
            return Localization.instance.Localize(text);
        }
        catch
        {
            return text;
        }
    }

    internal static string Format(string key, string fallback, params (string Name, string Value)[] placeholders)
    {
        string text = Localize(key, fallback);
        foreach ((string name, string value) in placeholders)
        {
            text = text.Replace("{" + name + "}", value);
        }

        return text;
    }
}
