using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for simulating user input (mouse and keyboard).
/// This interface abstracts the platform-specific input simulation implementation.
/// </summary>
public interface IInputSimulator
{
    /// <summary>
    /// Simulates moving the mouse cursor to the specified position.
    /// </summary>
    /// <param name="position">The target position to move the mouse cursor to.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when position coordinates are invalid.</exception>
    /// <exception cref="InputSimulationException">Thrown when input simulation fails.</exception>
    Task SimulateMouseMoveAsync(Point position);

    /// <summary>
    /// Simulates a mouse click action using the current cursor position.
    /// </summary>
    /// <param name="button">The mouse button to click.</param>
    /// <param name="type">The type of click action to perform.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when button/type are invalid.</exception>
    /// <exception cref="InputSimulationException">Thrown when input simulation fails.</exception>
    Task SimulateMouseClickAsync(MouseButton button, ClickType type);

    /// <summary>
    /// Simulates typing the specified text.
    /// </summary>
    /// <param name="text">The text to be typed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
    /// <exception cref="InputSimulationException">Thrown when input simulation fails.</exception>
    Task SimulateKeyboardInputAsync(string text);

    /// <summary>
    /// Simulates pressing or releasing a specific virtual key.
    /// </summary>
    /// <param name="key">The virtual key to press or release.</param>
    /// <param name="isDown">True to press the key down, false to release it.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the virtual key is invalid.</exception>
    /// <exception cref="InputSimulationException">Thrown when input simulation fails.</exception>
    Task SimulateKeyPressAsync(VirtualKey key, bool isDown);

    /// <summary>
    /// Simulates pressing a combination of keys simultaneously.
    /// </summary>
    /// <param name="keys">The virtual keys to press together.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when keys collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when keys collection is empty or contains invalid keys.</exception>
    /// <exception cref="InputSimulationException">Thrown when input simulation fails.</exception>
    Task SimulateKeyComboAsync(IEnumerable<VirtualKey> keys);

    /// <summary>
    /// Introduces a delay in the input simulation sequence.
    /// </summary>
    /// <param name="duration">The duration to wait.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when duration is negative.</exception>
    Task DelayAsync(TimeSpan duration);

    /// <summary>
    /// Gets the current mouse cursor position.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the current cursor position.</returns>
    /// <exception cref="InputSimulationException">Thrown when unable to retrieve cursor position.</exception>
    Task<Point> GetCursorPositionAsync();

    /// <summary>
    /// Validates that the input simulator is ready for use.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the simulator is ready.</returns>
    Task<bool> IsReadyAsync();
}

/// <summary>
/// Exception thrown when input simulation operations fail.
/// </summary>
public class InputSimulationException : Exception
{
    /// <summary>
    /// The Win32 error code associated with the failure, if applicable.
    /// </summary>
    public int? Win32ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the InputSimulationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InputSimulationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InputSimulationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InputSimulationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InputSimulationException class with a Win32 error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="win32ErrorCode">The Win32 error code.</param>
    public InputSimulationException(string message, int win32ErrorCode) : base(message)
    {
        Win32ErrorCode = win32ErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the InputSimulationException class with a Win32 error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="win32ErrorCode">The Win32 error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public InputSimulationException(string message, int win32ErrorCode, Exception innerException) : base(message, innerException)
    {
        Win32ErrorCode = win32ErrorCode;
    }
}