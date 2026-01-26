namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents the type of event received from Arduino.
/// </summary>
public enum ArduinoEventType : byte
{
    /// <summary>
    /// Mouse move event.
    /// </summary>
    MouseMove = 0x01,

    /// <summary>
    /// Mouse click event.
    /// </summary>
    MouseClick = 0x02,

    /// <summary>
    /// Keyboard input event.
    /// </summary>
    KeyboardInput = 0x03,

    /// <summary>
    /// Status response.
    /// </summary>
    StatusResponse = 0x20,

    /// <summary>
    /// Error event.
    /// </summary>
    Error = 0xFF
}
