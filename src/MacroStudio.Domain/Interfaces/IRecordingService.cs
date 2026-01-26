using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for recording user input and converting it to automation commands.
/// Handles the capture of mouse and keyboard events during recording sessions.
/// </summary>
public interface IRecordingService
{
    /// <summary>
    /// Gets whether recording is currently active.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Gets the current recording session information.
    /// </summary>
    RecordingSession? CurrentSession { get; }

    /// <summary>
    /// Event raised when a new command is recorded during an active recording session.
    /// </summary>
    event EventHandler<CommandRecordedEventArgs> CommandRecorded;

    /// <summary>
    /// Event raised when recording state changes (started, stopped, paused).
    /// </summary>
    event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;

    /// <summary>
    /// Event raised when an error occurs during recording.
    /// </summary>
    event EventHandler<RecordingErrorEventArgs> RecordingError;

    /// <summary>
    /// Starts a new recording session.
    /// </summary>
    /// <param name="options">Optional recording configuration options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when recording is already active.</exception>
    Task StartRecordingAsync(RecordingOptions? options = null);

    /// <summary>
    /// Stops the current recording session and finalizes the recorded commands.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no recording session is active.</exception>
    Task StopRecordingAsync();

    /// <summary>
    /// Pauses the current recording session without stopping it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no recording session is active or recording is already paused.</exception>
    Task PauseRecordingAsync();

    /// <summary>
    /// Resumes a paused recording session.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no recording session is active or recording is not paused.</exception>
    Task ResumeRecordingAsync();

    /// <summary>
    /// Gets all commands recorded in the current session.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the recorded commands.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no recording session is active.</exception>
    Task<IReadOnlyList<Command>> GetRecordedCommandsAsync();

    /// <summary>
    /// Clears all commands from the current recording session.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no recording session is active.</exception>
    Task ClearRecordedCommandsAsync();

    /// <summary>
    /// Validates the recording configuration and system readiness.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<RecordingValidationResult> ValidateRecordingSetupAsync();

    /// <summary>
    /// Gets statistics about the current recording session.
    /// </summary>
    /// <returns>Recording session statistics, or null if no session is active.</returns>
    RecordingStatistics? GetRecordingStatistics();
}

/// <summary>
/// Represents a recording session with its configuration and state.
/// </summary>
public class RecordingSession
{
    /// <summary>
    /// Unique identifier for this recording session.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Timestamp when the recording session started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Current state of the recording session.
    /// </summary>
    public RecordingState State { get; private set; }

    /// <summary>
    /// Configuration options for this recording session.
    /// </summary>
    public RecordingOptions Options { get; }

    /// <summary>
    /// List of commands recorded in this session.
    /// </summary>
    public IReadOnlyList<Command> Commands => _commands.AsReadOnly();

    private readonly List<Command> _commands;

    /// <summary>
    /// Initializes a new recording session.
    /// </summary>
    /// <param name="options">Recording configuration options.</param>
    public RecordingSession(RecordingOptions options)
    {
        Id = Guid.NewGuid();
        StartedAt = DateTime.UtcNow;
        State = RecordingState.Active;
        Options = options;
        _commands = new List<Command>();
    }

    /// <summary>
    /// Adds a command to this recording session.
    /// </summary>
    /// <param name="command">The command to add.</param>
    public void AddCommand(Command command)
    {
        if (State != RecordingState.Active)
            throw new InvalidOperationException("Cannot add commands to a non-active recording session.");

        _commands.Add(command);
    }

    /// <summary>
    /// Changes the state of this recording session.
    /// </summary>
    /// <param name="newState">The new state.</param>
    public void ChangeState(RecordingState newState)
    {
        State = newState;
    }

    /// <summary>
    /// Clears all commands from this recording session.
    /// </summary>
    public void ClearCommands()
    {
        _commands.Clear();
    }
}

/// <summary>
/// Configuration options for recording sessions.
/// </summary>
public class RecordingOptions
{
    /// <summary>
    /// Whether to record mouse movements.
    /// </summary>
    public bool RecordMouseMovements { get; set; } = true;

    /// <summary>
    /// When recording mouse movements, choose whether to emit a lower-level move command (move_ll) instead of move.
    /// </summary>
    public bool UseLowLevelMouseMove { get; set; } = true;

    /// <summary>
    /// Whether to record mouse movements as relative displacements instead of absolute positions.
    /// </summary>
    public bool UseRelativeMouseMove { get; set; } = false;

    /// <summary>
    /// Whether to record mouse clicks.
    /// </summary>
    public bool RecordMouseClicks { get; set; } = true;

    /// <summary>
    /// Whether to record keyboard input.
    /// </summary>
    public bool RecordKeyboardInput { get; set; } = true;

    /// <summary>
    /// Minimum delay between recorded commands (to reduce noise).
    /// </summary>
    public TimeSpan MinimumDelay { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Maximum delay between commands (longer delays will be capped).
    /// </summary>
    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to automatically insert sleep commands for long delays.
    /// </summary>
    public bool AutoInsertSleepCommands { get; set; } = true;

    /// <summary>
    /// Threshold for inserting sleep commands instead of using command delays.
    /// </summary>
    public TimeSpan SleepCommandThreshold { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to filter out system-generated events.
    /// </summary>
    public bool FilterSystemEvents { get; set; } = true;

    /// <summary>
    /// The input mode to use for recording (Software or Hardware).
    /// </summary>
    public InputMode InputMode { get; set; } = InputMode.Software;

    /// <summary>
    /// Creates default recording options.
    /// </summary>
    /// <returns>A new RecordingOptions instance with default values.</returns>
    public static RecordingOptions Default() => new();
}



/// <summary>
/// Statistics about a recording session.
/// </summary>
public class RecordingStatistics
{
    /// <summary>
    /// Total number of commands recorded.
    /// </summary>
    public int TotalCommands { get; set; }

    /// <summary>
    /// Number of mouse movement commands recorded.
    /// </summary>
    public int MouseMoveCommands { get; set; }

    /// <summary>
    /// Number of mouse click commands recorded.
    /// </summary>
    public int MouseClickCommands { get; set; }

    /// <summary>
    /// Number of keyboard commands recorded.
    /// </summary>
    public int KeyboardCommands { get; set; }

    /// <summary>
    /// Number of sleep commands recorded.
    /// </summary>
    public int SleepCommands { get; set; }

    /// <summary>
    /// Total duration of the recording session.
    /// </summary>
    public TimeSpan SessionDuration { get; set; }

    /// <summary>
    /// Estimated total execution time of recorded commands.
    /// </summary>
    public TimeSpan EstimatedExecutionTime { get; set; }
}

/// <summary>
/// Result of recording setup validation.
/// </summary>
public class RecordingValidationResult
{
    /// <summary>
    /// Whether the recording setup is valid.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Initializes a new recording validation result.
    /// </summary>
    /// <param name="isValid">Whether the setup is valid.</param>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">List of validation warnings.</param>
    public RecordingValidationResult(bool isValid, IEnumerable<string> errors, IEnumerable<string> warnings)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
        Warnings = warnings.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A validation result indicating success.</returns>
    public static RecordingValidationResult Success(IEnumerable<string>? warnings = null)
    {
        return new RecordingValidationResult(true, Array.Empty<string>(), warnings ?? Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static RecordingValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
    {
        return new RecordingValidationResult(false, errors, warnings ?? Array.Empty<string>());
    }
}