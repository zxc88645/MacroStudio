namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// Represents modifier keys for hotkey combinations.
/// This enum supports bitwise operations to combine multiple modifiers.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>
    /// No modifier keys.
    /// </summary>
    None = 0,

    /// <summary>
    /// Alt key modifier.
    /// </summary>
    Alt = 1,

    /// <summary>
    /// Control key modifier.
    /// </summary>
    Control = 2,

    /// <summary>
    /// Shift key modifier.
    /// </summary>
    Shift = 4,

    /// <summary>
    /// Windows key modifier.
    /// </summary>
    Windows = 8,

    /// <summary>
    /// Common combination: Control + Alt.
    /// </summary>
    ControlAlt = Control | Alt,

    /// <summary>
    /// Common combination: Control + Shift.
    /// </summary>
    ControlShift = Control | Shift,

    /// <summary>
    /// Common combination: Alt + Shift.
    /// </summary>
    AltShift = Alt | Shift,

    /// <summary>
    /// Common combination: Control + Alt + Shift.
    /// </summary>
    ControlAltShift = Control | Alt | Shift,

    /// <summary>
    /// Common combination: Control + Windows.
    /// </summary>
    ControlWindows = Control | Windows,

    /// <summary>
    /// Common combination: Alt + Windows.
    /// </summary>
    AltWindows = Alt | Windows,

    /// <summary>
    /// Common combination: Shift + Windows.
    /// </summary>
    ShiftWindows = Shift | Windows
}

/// <summary>
/// Extension methods for HotkeyModifiers enum.
/// </summary>
public static class HotkeyModifiersExtensions
{
    /// <summary>
    /// Converts HotkeyModifiers to Win32 modifier flags.
    /// </summary>
    /// <param name="modifiers">The hotkey modifiers to convert.</param>
    /// <returns>Win32 modifier flags.</returns>
    public static uint ToWin32Modifiers(this HotkeyModifiers modifiers)
    {
        uint win32Modifiers = 0;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
            win32Modifiers |= 0x0001; // MOD_ALT

        if (modifiers.HasFlag(HotkeyModifiers.Control))
            win32Modifiers |= 0x0002; // MOD_CONTROL

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
            win32Modifiers |= 0x0004; // MOD_SHIFT

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
            win32Modifiers |= 0x0008; // MOD_WIN

        return win32Modifiers;
    }

    /// <summary>
    /// Converts Win32 modifier flags to HotkeyModifiers.
    /// </summary>
    /// <param name="win32Modifiers">The Win32 modifier flags to convert.</param>
    /// <returns>HotkeyModifiers enum value.</returns>
    public static HotkeyModifiers FromWin32Modifiers(uint win32Modifiers)
    {
        var modifiers = HotkeyModifiers.None;

        if ((win32Modifiers & 0x0001) != 0) // MOD_ALT
            modifiers |= HotkeyModifiers.Alt;

        if ((win32Modifiers & 0x0002) != 0) // MOD_CONTROL
            modifiers |= HotkeyModifiers.Control;

        if ((win32Modifiers & 0x0004) != 0) // MOD_SHIFT
            modifiers |= HotkeyModifiers.Shift;

        if ((win32Modifiers & 0x0008) != 0) // MOD_WIN
            modifiers |= HotkeyModifiers.Windows;

        return modifiers;
    }

    /// <summary>
    /// Gets a human-readable string representation of the modifiers.
    /// </summary>
    /// <param name="modifiers">The hotkey modifiers.</param>
    /// <returns>A human-readable string representation.</returns>
    public static string GetDisplayString(this HotkeyModifiers modifiers)
    {
        if (modifiers == HotkeyModifiers.None)
            return "None";

        var parts = new List<string>();

        if (modifiers.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
            parts.Add("Win");

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Validates that the modifier combination is valid for hotkey registration.
    /// </summary>
    /// <param name="modifiers">The hotkey modifiers to validate.</param>
    /// <returns>True if the modifier combination is valid, false otherwise.</returns>
    public static bool IsValidCombination(this HotkeyModifiers modifiers)
    {
        // All combinations are valid, but some may be reserved by the system
        // This method can be extended to check for system-reserved combinations
        return true;
    }
}