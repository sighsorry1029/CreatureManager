using System;

namespace CreatureManager;

internal static class CreatureLocalization
{
    internal static string Localize(string key, string fallback)
    {
        bool hasTokenPrefix = key.StartsWith("$", StringComparison.Ordinal);
        string token = hasTokenPrefix ? key : "$" + key;
        Localization? localization = Localization.instance;
        if (localization == null)
        {
            return fallback;
        }

        try
        {
            string localized = localization.Localize(token);
            if (string.IsNullOrEmpty(localized) || string.Equals(localized, token, StringComparison.Ordinal))
            {
                return fallback;
            }

            bool returnedKey = hasTokenPrefix
                ? localized.Length == key.Length - 1 &&
                  string.CompareOrdinal(localized, 0, key, 1, localized.Length) == 0
                : string.Equals(localized, key, StringComparison.Ordinal);
            return returnedKey ? fallback : localized;
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
