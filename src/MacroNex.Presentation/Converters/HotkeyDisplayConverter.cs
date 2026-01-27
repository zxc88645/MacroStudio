using System.Globalization;
using System.Windows.Data;
using MacroNex.Domain.ValueObjects;

namespace MacroNex.Presentation.Converters;

public class HotkeyDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HotkeyDefinition hotkey)
        {
            return $"({hotkey.GetDisplayString()})";
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
