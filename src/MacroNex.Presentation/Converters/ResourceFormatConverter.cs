using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MacroNex.Presentation.Converters;

/// <summary>
/// Formats a value using a format string stored in Application.Current.Resources.
/// ConverterParameter should be the resource key of the format string.
/// </summary>
public sealed class ResourceFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = parameter as string;
        if (string.IsNullOrWhiteSpace(key))
            return value?.ToString() ?? string.Empty;

        var app = System.Windows.Application.Current;
        var format = app?.TryFindResource(key) as string;
        if (string.IsNullOrEmpty(format))
            return value?.ToString() ?? string.Empty;

        try
        {
            return string.Format(culture, format, value);
        }
        catch
        {
            // If format is invalid or value doesn't match, fall back.
            return value?.ToString() ?? string.Empty;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

