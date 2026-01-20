using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Low-level keyboard hook based hotkey service for recording controls.
/// Does NOT use RegisterHotKey; instead listens to WH_KEYBOARD_LL and matches configured hotkeys.
/// </summary>
public interface IRecordingHotkeyHookService
{
    /// <summary>
    /// Raised when a configured recording hotkey is detected (key down).
    /// </summary>
    event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;

    /// <summary>
    /// Updates the hotkeys to match against.
    /// </summary>
    void SetHotkeys(HotkeyDefinition? start, HotkeyDefinition? pause, HotkeyDefinition? stop);
}

