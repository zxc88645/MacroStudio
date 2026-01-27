using System.Globalization;

namespace MacroNex.Presentation.Utilities;

/// <summary>
/// Lightweight helper for retrieving localized strings from Application resources.
/// Intended for ViewModels where DynamicResource does not apply.
/// </summary>
public static class UiText
{
    public static string Get(string key, string fallback = "")
    {
        var app = System.Windows.Application.Current;
        var s = app?.TryFindResource(key) as string;
        return string.IsNullOrEmpty(s) ? fallback : s;
    }

    public static string Format(string key, object? arg0, string fallbackFormat = "{0}")
    {
        var fmt = Get(key, fallbackFormat);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, fmt, arg0);
        }
        catch
        {
            return arg0?.ToString() ?? string.Empty;
        }
    }
}

