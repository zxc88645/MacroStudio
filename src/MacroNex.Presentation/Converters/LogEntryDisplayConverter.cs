using MacroNex.Domain.Interfaces;
using System.Globalization;
using System.Windows.Data;

namespace MacroNex.Presentation.Converters;

public sealed class LogEntryDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogEntry entry) return string.Empty;
        var ts = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        return $"[{ts}] [{entry.Level}] {entry.Message}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

