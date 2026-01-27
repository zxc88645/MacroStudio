namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// Represents the input mode for recording and execution.
/// </summary>
public enum InputMode
{
    /// <summary>
    /// High-level mode: uses Win32 API high-level methods (SetCursorPos) for input simulation.
    /// </summary>
    HighLevel,

    /// <summary>
    /// Low-level mode: uses Win32 API low-level methods (SendInput) for input simulation.
    /// </summary>
    LowLevel,

    /// <summary>
    /// Hardware mode: uses Arduino Leonardo + USB Host Shield for hardware-level input.
    /// </summary>
    Hardware
}
