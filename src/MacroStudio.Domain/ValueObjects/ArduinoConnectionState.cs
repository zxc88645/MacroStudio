namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents the connection state of the Arduino device.
/// </summary>
public enum ArduinoConnectionState
{
    /// <summary>
    /// Arduino is not connected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Currently attempting to connect to Arduino.
    /// </summary>
    Connecting,

    /// <summary>
    /// Arduino is connected and ready.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection error occurred.
    /// </summary>
    Error
}
