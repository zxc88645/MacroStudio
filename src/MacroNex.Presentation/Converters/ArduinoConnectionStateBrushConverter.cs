using MacroNex.Domain.ValueObjects;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MacroNex.Presentation.Converters;

/// <summary>
/// Converts ArduinoConnectionState to a Brush color for status indicator.
/// </summary>
public sealed class ArduinoConnectionStateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ArduinoConnectionState state)
            return Brushes.Gray;

        return state switch
        {
            ArduinoConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(128, 128, 128)), // Gray - 未連接
            ArduinoConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // Amber - 連接中
            ArduinoConnectionState.Connected => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green - 已連接
            ArduinoConnectionState.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),         // Red - 錯誤
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
