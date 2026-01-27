using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Interfaces;

/// <summary>
/// Low-level keyboard hook based hotkey service for script triggers (no RegisterHotKey).
/// </summary>
public interface IScriptHotkeyHookService
{
    /// <summary>
    /// Raised when a script hotkey is detected (key down).
    /// Hotkey.Name is expected to carry the ScriptId (string) for lookup.
    /// </summary>
    event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;

    /// <summary>
    /// Replace all registered script hotkeys.
    /// Key: ScriptId, Value: HotkeyDefinition
    /// </summary>
    void SetScriptHotkeys(IReadOnlyDictionary<Guid, HotkeyDefinition> hotkeys);
}

