using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Interfaces;

/// <summary>
/// Domain interface for communicating with Arduino Leonardo via serial port.
/// </summary>
public interface IArduinoService
{
    /// <summary>
    /// Gets the current connection state of the Arduino.
    /// </summary>
    ArduinoConnectionState ConnectionState { get; }

    /// <summary>
    /// Gets whether the Arduino is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the currently connected serial port name, or null if not connected.
    /// </summary>
    string? ConnectedPortName { get; }

    /// <summary>
    /// Gets a list of available serial ports.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of available port names.</returns>
    Task<IReadOnlyList<string>> GetAvailablePortsAsync();

    /// <summary>
    /// Connects to the Arduino on the specified serial port.
    /// </summary>
    /// <param name="portName">The name of the serial port (e.g., "COM3").</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when port name is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when already connected or connection fails.</exception>
    Task ConnectAsync(string portName);

    /// <summary>
    /// Disconnects from the Arduino.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DisconnectAsync();

    /// <summary>
    /// Sends a command to the Arduino.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
    /// <exception cref="ArduinoCommunicationException">Thrown when communication fails.</exception>
    Task SendCommandAsync(ArduinoCommand command);

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    event EventHandler<ArduinoConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised when an event is received from the Arduino.
    /// </summary>
    event EventHandler<ArduinoEventReceivedEventArgs>? EventReceived;

    /// <summary>
    /// Raised when a communication error occurs.
    /// </summary>
    event EventHandler<ArduinoErrorEventArgs>? ErrorOccurred;
}

/// <summary>
/// Event arguments for connection state changes.
/// </summary>
public sealed class ArduinoConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous connection state.
    /// </summary>
    public ArduinoConnectionState PreviousState { get; }

    /// <summary>
    /// Gets the new connection state.
    /// </summary>
    public ArduinoConnectionState NewState { get; }

    /// <summary>
    /// Gets the port name associated with this state change, if any.
    /// </summary>
    public string? PortName { get; }

    public ArduinoConnectionStateChangedEventArgs(ArduinoConnectionState previousState, ArduinoConnectionState newState, string? portName = null)
    {
        PreviousState = previousState;
        NewState = newState;
        PortName = portName;
    }
}

/// <summary>
/// Event arguments for events received from Arduino.
/// </summary>
public sealed class ArduinoEventReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public ArduinoEventType EventType { get; }

    /// <summary>
    /// Gets the event data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the timestamp of the event (milliseconds since Arduino boot).
    /// </summary>
    public uint Timestamp { get; }

    public ArduinoEventReceivedEventArgs(ArduinoEventType eventType, byte[] data, uint timestamp)
    {
        EventType = eventType;
        Data = data;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Event arguments for Arduino communication errors.
/// </summary>
public sealed class ArduinoErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception that caused the error, if any.
    /// </summary>
    public Exception? Exception { get; }

    public ArduinoErrorEventArgs(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }
}

/// <summary>
/// Exception thrown when Arduino communication fails.
/// </summary>
public class ArduinoCommunicationException : Exception
{
    public ArduinoCommunicationException(string message) : base(message)
    {
    }

    public ArduinoCommunicationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
