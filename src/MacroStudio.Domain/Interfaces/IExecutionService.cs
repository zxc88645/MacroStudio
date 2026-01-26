using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for executing automation scripts.
/// Manages script execution state, timing, and safety controls.
/// </summary>
public interface IExecutionService
{
    /// <summary>
    /// Gets the current execution state.
    /// </summary>
    ExecutionState State { get; }

    /// <summary>
    /// Gets the currently executing script, if any.
    /// </summary>
    Script? CurrentScript { get; }

    /// <summary>
    /// Gets the index of the currently executing command.
    /// </summary>
    int CurrentCommandIndex { get; }

    /// <summary>
    /// Gets the current execution session information.
    /// </summary>
    ExecutionSession? CurrentSession { get; }

    /// <summary>
    /// Event raised when execution progress changes.
    /// </summary>
    event EventHandler<ExecutionProgressEventArgs> ProgressChanged;

    /// <summary>
    /// Event raised when execution state changes.
    /// </summary>
    event EventHandler<ExecutionStateChangedEventArgs> StateChanged;

    /// <summary>
    /// Event raised when execution encounters an error.
    /// </summary>
    event EventHandler<ExecutionErrorEventArgs> ExecutionError;

    /// <summary>
    /// Event raised when execution completes (successfully or due to termination).
    /// </summary>
    event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

    /// <summary>
    /// Starts executing the specified script with the given options.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="options">Execution configuration options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when script is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when execution is already active or script is invalid.</exception>
    Task StartExecutionAsync(Script script, ExecutionOptions? options = null);

    /// <summary>
    /// Pauses the current execution, maintaining state for resumption.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when execution is not running.</exception>
    Task PauseExecutionAsync();

    /// <summary>
    /// Resumes paused execution from the current command.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when execution is not paused.</exception>
    Task ResumeExecutionAsync();

    /// <summary>
    /// Stops execution immediately and resets to the beginning.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StopExecutionAsync();

    /// <summary>
    /// Executes a single command and then pauses (for debugging).
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no script is loaded or execution is running.</exception>
    Task StepExecutionAsync();

    /// <summary>
    /// Terminates execution immediately (emergency stop).
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task TerminateExecutionAsync();

    /// <summary>
    /// Validates that a script is ready for execution.
    /// </summary>
    /// <param name="script">The script to validate.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<ExecutionValidationResult> ValidateScriptForExecutionAsync(Script script);

    /// <summary>
    /// Gets execution statistics for the current session.
    /// </summary>
    /// <returns>Execution statistics, or null if no session is active.</returns>
    ExecutionStatistics? GetExecutionStatistics();

    /// <summary>
    /// Estimates the remaining execution time based on current progress and timing.
    /// </summary>
    /// <returns>Estimated remaining time, or null if no execution is active.</returns>
    TimeSpan? GetEstimatedRemainingTime();
}

/// <summary>
/// Represents an execution session with its configuration and state.
/// </summary>
public class ExecutionSession
{
    /// <summary>
    /// Unique identifier for this execution session.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The script being executed.
    /// </summary>
    public Script Script { get; }

    /// <summary>
    /// Execution configuration options.
    /// </summary>
    public ExecutionOptions Options { get; }

    /// <summary>
    /// Timestamp when execution started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Current execution state.
    /// </summary>
    public ExecutionState State { get; private set; }

    /// <summary>
    /// Index of the currently executing command.
    /// </summary>
    public int CurrentCommandIndex { get; private set; }

    /// <summary>
    /// Number of commands executed so far.
    /// </summary>
    public int ExecutedCommandCount { get; private set; }

    /// <summary>
    /// Total execution time elapsed.
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartedAt;

    /// <summary>
    /// Timestamp when execution was paused (if applicable).
    /// </summary>
    public DateTime? PausedAt { get; private set; }

    /// <summary>
    /// Timestamp when execution completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Error that caused execution to fail (if applicable).
    /// </summary>
    public Exception? Error { get; private set; }

    /// <summary>
    /// Initializes a new execution session.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="options">Execution options.</param>
    public ExecutionSession(Script script, ExecutionOptions options)
    {
        Id = Guid.NewGuid();
        Script = script ?? throw new ArgumentNullException(nameof(script));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        StartedAt = DateTime.UtcNow;
        State = ExecutionState.Running;
        CurrentCommandIndex = 0;
        ExecutedCommandCount = 0;
    }

    /// <summary>
    /// Updates the current command index and executed count.
    /// </summary>
    /// <param name="commandIndex">The new command index.</param>
    public void UpdateProgress(int commandIndex)
    {
        CurrentCommandIndex = commandIndex;
        ExecutedCommandCount = commandIndex;
    }

    /// <summary>
    /// Changes the execution state.
    /// </summary>
    /// <param name="newState">The new state.</param>
    public void ChangeState(ExecutionState newState)
    {
        State = newState;

        switch (newState)
        {
            case ExecutionState.Paused:
                PausedAt = DateTime.UtcNow;
                break;
            case ExecutionState.Running when PausedAt.HasValue:
                PausedAt = null;
                break;
            case ExecutionState.Completed:
            case ExecutionState.Failed:
            case ExecutionState.Terminated:
                CompletedAt = DateTime.UtcNow;
                break;
        }
    }

    /// <summary>
    /// Sets the error that caused execution to fail.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    public void SetError(Exception error)
    {
        Error = error;
        ChangeState(ExecutionState.Failed);
    }
}

/// <summary>
/// Configuration options for script execution.
/// </summary>
public class ExecutionOptions
{
    /// <summary>
    /// Identifies where an execution was triggered from (for UI/UX behavior).
    /// </summary>
    public ExecutionTriggerSource TriggerSource { get; set; } = ExecutionTriggerSource.DebugPanel;

    /// <summary>
    /// Controls which execution features are allowed (debug interactive vs run-only).
    /// </summary>
    public ExecutionControlMode ControlMode { get; set; } = ExecutionControlMode.DebugInteractive;

    /// <summary>
    /// Whether to show a countdown before starting execution.
    /// </summary>
    public bool ShowCountdown { get; set; } = true;

    /// <summary>
    /// Duration of the countdown before execution starts.
    /// </summary>
    public TimeSpan CountdownDuration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum number of commands to execute (safety limit).
    /// </summary>
    public int MaxCommandCount { get; set; } = 10000;

    /// <summary>
    /// Maximum total execution time (safety limit).
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether to stop execution on the first error.
    /// </summary>
    public bool StopOnError { get; set; } = true;

    /// <summary>
    /// Whether to require authorization for potentially dangerous operations.
    /// </summary>
    public bool RequireAuthorization { get; set; } = true;

    /// <summary>
    /// Number of times to repeat the script execution (1 = execute once).
    /// </summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>
    /// Delay between script repetitions.
    /// </summary>
    public TimeSpan RepeatDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// The input mode to use for execution (Software or Hardware).
    /// </summary>
    public InputMode InputMode { get; set; } = InputMode.Software;

    /// <summary>
    /// Creates default execution options.
    /// </summary>
    /// <returns>A new ExecutionOptions instance with default values.</returns>
    public static ExecutionOptions Default() => new();

    /// <summary>
    /// Creates execution options for debugging (slower speed, step mode friendly).
    /// </summary>
    /// <returns>A new ExecutionOptions instance configured for debugging.</returns>
    public static ExecutionOptions Debug() => new()
    {
        TriggerSource = ExecutionTriggerSource.DebugPanel,
        ControlMode = ExecutionControlMode.DebugInteractive,
        ShowCountdown = false,
        StopOnError = true,
        RequireAuthorization = false
    };
}

/// <summary>
/// Describes where a script execution was initiated.
/// </summary>
public enum ExecutionTriggerSource
{
    /// <summary>
    /// Triggered from the UI "Execution" debug panel.
    /// </summary>
    DebugPanel,

    /// <summary>
    /// Triggered via a global/script hotkey.
    /// </summary>
    Hotkey
}

/// <summary>
/// Controls the available execution controls for a session.
/// </summary>
public enum ExecutionControlMode
{
    /// <summary>
    /// Debug mode: supports pause/resume/stop/step and per-command progress.
    /// </summary>
    DebugInteractive,

    /// <summary>
    /// Run-only mode: no pause/resume/stop/step; only termination is relevant.
    /// </summary>
    RunOnly
}

/// <summary>
/// Statistics about an execution session.
/// </summary>
public class ExecutionStatistics
{
    /// <summary>
    /// Total number of commands in the script.
    /// </summary>
    public int TotalCommands { get; set; }

    /// <summary>
    /// Number of commands executed so far.
    /// </summary>
    public int ExecutedCommands { get; set; }

    /// <summary>
    /// Number of commands remaining.
    /// </summary>
    public int RemainingCommands => TotalCommands - ExecutedCommands;

    /// <summary>
    /// Percentage of completion (0-100).
    /// </summary>
    public double CompletionPercentage => TotalCommands > 0 ? (double)ExecutedCommands / TotalCommands * 100 : 0;

    /// <summary>
    /// Total time elapsed since execution started.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Estimated total execution time.
    /// </summary>
    public TimeSpan EstimatedTotalTime { get; set; }

    /// <summary>
    /// Estimated remaining execution time.
    /// </summary>
    public TimeSpan EstimatedRemainingTime { get; set; }

    /// <summary>
    /// Average time per command executed.
    /// </summary>
    public TimeSpan AverageCommandTime { get; set; }

    /// <summary>
    /// Number of errors encountered during execution.
    /// </summary>
    public int ErrorCount { get; set; }
}

/// <summary>
/// Result of execution validation.
/// </summary>
public class ExecutionValidationResult
{
    /// <summary>
    /// Whether the script is valid for execution.
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
    /// List of dangerous operations that require authorization.
    /// </summary>
    public IReadOnlyList<string> DangerousOperations { get; }

    /// <summary>
    /// Initializes a new execution validation result.
    /// </summary>
    /// <param name="isValid">Whether the script is valid for execution.</param>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">List of validation warnings.</param>
    /// <param name="dangerousOperations">List of dangerous operations.</param>
    public ExecutionValidationResult(bool isValid, IEnumerable<string> errors, IEnumerable<string> warnings, IEnumerable<string> dangerousOperations)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
        Warnings = warnings.ToList().AsReadOnly();
        DangerousOperations = dangerousOperations.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <param name="dangerousOperations">Optional dangerous operations.</param>
    /// <returns>A validation result indicating success.</returns>
    public static ExecutionValidationResult Success(IEnumerable<string>? warnings = null, IEnumerable<string>? dangerousOperations = null)
    {
        return new ExecutionValidationResult(true, Array.Empty<string>(), warnings ?? Array.Empty<string>(), dangerousOperations ?? Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <param name="dangerousOperations">Optional dangerous operations.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ExecutionValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null, IEnumerable<string>? dangerousOperations = null)
    {
        return new ExecutionValidationResult(false, errors, warnings ?? Array.Empty<string>(), dangerousOperations ?? Array.Empty<string>());
    }
}