namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents the type of Arduino command.
/// </summary>
public enum ArduinoCommandType : byte
{
    /// <summary>
    /// Move mouse to absolute position.
    /// </summary>
    MouseMoveAbsolute = 0x01,

    /// <summary>
    /// Move mouse relative to current position.
    /// </summary>
    MouseMoveRelative = 0x02,

    /// <summary>
    /// Perform mouse click.
    /// </summary>
    MouseClick = 0x03,

    /// <summary>
    /// Type text using keyboard.
    /// </summary>
    KeyboardText = 0x04,

    /// <summary>
    /// Press or release a key.
    /// </summary>
    KeyPress = 0x05,

    /// <summary>
    /// Introduce a delay.
    /// </summary>
    Delay = 0x06,

    /// <summary>
    /// Start recording input.
    /// </summary>
    StartRecording = 0x10,

    /// <summary>
    /// Stop recording input.
    /// </summary>
    StopRecording = 0x11,

    /// <summary>
    /// Query Arduino status (heartbeat).
    /// </summary>
    StatusQuery = 0x20,

    /// <summary>
    /// Error response from Arduino.
    /// </summary>
    Error = 0xFF
}
