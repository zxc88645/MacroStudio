using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for managing global hotkeys.
/// This interface abstracts the platform-specific global hotkey registration and handling.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>
    /// Event raised when a registered hotkey is pressed.
    /// </summary>
    event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey with the system.
    /// </summary>
    /// <param name="hotkey">The hotkey definition to register.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when hotkey is null.</exception>
    /// <exception cref="HotkeyRegistrationException">Thrown when hotkey registration fails.</exception>
    Task RegisterHotkeyAsync(HotkeyDefinition hotkey);

    /// <summary>
    /// Unregisters a previously registered global hotkey.
    /// </summary>
    /// <param name="hotkey">The hotkey definition to unregister.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when hotkey is null.</exception>
    /// <exception cref="HotkeyRegistrationException">Thrown when hotkey unregistration fails.</exception>
    Task UnregisterHotkeyAsync(HotkeyDefinition hotkey);

    /// <summary>
    /// Unregisters all previously registered global hotkeys.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UnregisterAllHotkeysAsync();

    /// <summary>
    /// Gets all currently registered hotkeys.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the collection of registered hotkeys.</returns>
    Task<IEnumerable<HotkeyDefinition>> GetRegisteredHotkeysAsync();

    /// <summary>
    /// Checks if a hotkey is currently registered.
    /// </summary>
    /// <param name="hotkey">The hotkey definition to check.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the hotkey is registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when hotkey is null.</exception>
    Task<bool> IsHotkeyRegisteredAsync(HotkeyDefinition hotkey);

    /// <summary>
    /// Validates that the hotkey service is ready for use.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the service is ready.</returns>
    Task<bool> IsReadyAsync();
}

/// <summary>
/// Event arguments for hotkey pressed events.
/// </summary>
public class HotkeyPressedEventArgs : EventArgs
{
    /// <summary>
    /// The hotkey that was pressed.
    /// </summary>
    public HotkeyDefinition Hotkey { get; }

    /// <summary>
    /// The timestamp when the hotkey was pressed.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the HotkeyPressedEventArgs class.
    /// </summary>
    /// <param name="hotkey">The hotkey that was pressed.</param>
    /// <param name="timestamp">The timestamp when the hotkey was pressed.</param>
    public HotkeyPressedEventArgs(HotkeyDefinition hotkey, DateTime timestamp)
    {
        Hotkey = hotkey ?? throw new ArgumentNullException(nameof(hotkey));
        Timestamp = timestamp;
    }
}

/// <summary>
/// Exception thrown when hotkey registration or unregistration operations fail.
/// </summary>
public class HotkeyRegistrationException : Exception
{
    /// <summary>
    /// The Win32 error code associated with the failure, if applicable.
    /// </summary>
    public int? Win32ErrorCode { get; }

    /// <summary>
    /// The hotkey that caused the registration failure, if applicable.
    /// </summary>
    public HotkeyDefinition? Hotkey { get; }

    /// <summary>
    /// Initializes a new instance of the HotkeyRegistrationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public HotkeyRegistrationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the HotkeyRegistrationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public HotkeyRegistrationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the HotkeyRegistrationException class with a hotkey.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="hotkey">The hotkey that caused the failure.</param>
    public HotkeyRegistrationException(string message, HotkeyDefinition hotkey) : base(message)
    {
        Hotkey = hotkey;
    }

    /// <summary>
    /// Initializes a new instance of the HotkeyRegistrationException class with a Win32 error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="win32ErrorCode">The Win32 error code.</param>
    public HotkeyRegistrationException(string message, int win32ErrorCode) : base(message)
    {
        Win32ErrorCode = win32ErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the HotkeyRegistrationException class with a hotkey and Win32 error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="hotkey">The hotkey that caused the failure.</param>
    /// <param name="win32ErrorCode">The Win32 error code.</param>
    public HotkeyRegistrationException(string message, HotkeyDefinition hotkey, int win32ErrorCode) : base(message)
    {
        Hotkey = hotkey;
        Win32ErrorCode = win32ErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the HotkeyRegistrationException class with a hotkey, Win32 error code, and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="hotkey">The hotkey that caused the failure.</param>
    /// <param name="win32ErrorCode">The Win32 error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public HotkeyRegistrationException(string message, HotkeyDefinition hotkey, int win32ErrorCode, Exception innerException) : base(message, innerException)
    {
        Hotkey = hotkey;
        Win32ErrorCode = win32ErrorCode;
    }
}