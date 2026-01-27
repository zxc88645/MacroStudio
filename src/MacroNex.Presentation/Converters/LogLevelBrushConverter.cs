using MacroNex.Domain.Interfaces;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MacroNex.Presentation.Converters;

public sealed class LogLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LogLevel level)
            return Brushes.Gray;

        // Colors match App.xaml theme keys closely.
        return level switch
        {
            LogLevel.Info => new SolidColorBrush(Color.FromRgb(59, 130, 246)),    // blue-500
            LogLevel.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // amber-500
            LogLevel.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // red-500
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

