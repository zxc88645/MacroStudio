namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents the input mode for recording and execution.
/// </summary>
public enum InputMode
{
    /// <summary>
    /// Software mode: uses Win32 API for input simulation and hooks.
    /// </summary>
    Software,

    /// <summary>
    /// Hardware mode: uses Arduino Leonardo + USB Host Shield for hardware-level input.
    /// </summary>
    Hardware
}
