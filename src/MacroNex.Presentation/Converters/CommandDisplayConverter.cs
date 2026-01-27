using MacroNex.Domain.Entities;
using MacroNex.Domain.ValueObjects;
using System.Globalization;
using System.Windows.Data;

namespace MacroNex.Presentation.Converters;

public sealed class CommandDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Command cmd) return string.Empty;

        return cmd switch
        {
            MouseMoveCommand mm => $"Move to ({mm.Position.X}, {mm.Position.Y})",
            MouseMoveRelativeCommand mmr => $"Move relative ({mmr.DeltaX}, {mmr.DeltaY})",
            MouseClickCommand mc => $"{mc.Type} {mc.Button}",
            KeyPressCommand kp => $"{(kp.IsDown ? "KeyDown" : "KeyUp")} {kp.Key}",
            KeyboardCommand kc when !string.IsNullOrEmpty(kc.Text) => $"Type \"{kc.Text}\"",
            KeyboardCommand kc => $"Keys: {string.Join("+", kc.Keys)}",
            SleepCommand sc => $"Sleep {sc.Duration.TotalMilliseconds:0} ms",
            _ => cmd.Description
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

