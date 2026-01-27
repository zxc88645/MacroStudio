namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// Represents the trigger mode for hotkey execution.
/// </summary>
public enum HotkeyTriggerMode
{
    /// <summary>
    /// Trigger only once when the hotkey is pressed (default behavior).
    /// Even if the key is held down, it will only trigger once.
    /// </summary>
    Once = 0,

    /// <summary>
    /// Trigger repeatedly while the hotkey is held down.
    /// The script will execute continuously as long as the hotkey combination is pressed.
    /// </summary>
    RepeatWhileHeld = 1
}
