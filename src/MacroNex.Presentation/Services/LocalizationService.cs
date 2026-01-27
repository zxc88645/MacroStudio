using System.Windows;

namespace MacroNex.Presentation.Services;

/// <summary>
/// Runtime UI localization via swapping the Strings ResourceDictionary.
/// </summary>
public sealed class LocalizationService
{
    private const string StringsPrefix = "Resources/Strings.";

    /// <summary>
    /// Apply a language by culture name (e.g., "zh-TW", "en-US"). Defaults to zh-TW on invalid input.
    /// </summary>
    public void ApplyLanguage(string? cultureName)
    {
        var lang = NormalizeCulture(cultureName);
        var uri = new Uri($"{StringsPrefix}{lang}.xaml", UriKind.Relative);

        var app = System.Windows.Application.Current;
        if (app == null) return;

        var merged = app.Resources.MergedDictionaries;

        // Replace existing Strings dictionary if present; otherwise add it.
        var existingIdx = -1;
        for (int i = 0; i < merged.Count; i++)
        {
            var src = merged[i].Source?.ToString();
            if (src != null && src.Contains(StringsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                existingIdx = i;
                break;
            }
        }

        var newDict = new ResourceDictionary { Source = uri };
        if (existingIdx >= 0)
            merged[existingIdx] = newDict;
        else
            merged.Insert(0, newDict);
    }

    private static string NormalizeCulture(string? cultureName)
    {
        var n = (cultureName ?? string.Empty).Trim();
        if (string.Equals(n, "en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        if (string.Equals(n, "zh", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        if (string.Equals(n, "zh-tw", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        if (string.Equals(n, "en-us", StringComparison.OrdinalIgnoreCase)) return "en-US";

        // Only supported languages in this rollout.
        if (string.Equals(n, "zh-TW", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        if (string.Equals(n, "en-US", StringComparison.OrdinalIgnoreCase)) return "en-US";

        return "zh-TW";
    }
}

