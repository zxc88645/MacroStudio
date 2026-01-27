using MacroNex.Domain.Entities;
using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Events;

/// <summary>
/// Base class for all domain event arguments.
/// </summary>
public abstract class DomainEventArgs : EventArgs
{
    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Initializes a new domain event.
    /// </summary>
    protected DomainEventArgs()
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
    }
}

/// <summary>
/// Event arguments for when a command is recorded during a recording session.
/// </summary>
public class CommandRecordedEventArgs : DomainEventArgs
{
    /// <summary>
    /// The command that was recorded.
    /// </summary>
    public Command Command { get; }

    /// <summary>
    /// The recording session that captured this command.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Initializes a new command recorded event.
    /// </summary>
    /// <param name="command">The recorded command.</param>
    /// <param name="sessionId">The recording session ID.</param>
    public CommandRecordedEventArgs(Command command, Guid sessionId)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        SessionId = sessionId;
    }
}

/// <summary>
/// Event arguments for when recording state changes.
/// </summary>
public class RecordingStateChangedEventArgs : DomainEventArgs
{
    /// <summary>
    /// The previous recording state.
    /// </summary>
    public RecordingState PreviousState { get; }

    /// <summary>
    /// The new recording state.
    /// </summary>
    public RecordingState NewState { get; }

    /// <summary>
    /// The recording session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Optional reason for the state change.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new recording state changed event.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="reason">Optional reason for the change.</param>
    public RecordingStateChangedEventArgs(RecordingState previousState, RecordingState newState, Guid sessionId, string? reason = null)
    {
        PreviousState = previousState;
        NewState = newState;
        SessionId = sessionId;
        Reason = reason;
    }
}

/// <summary>
/// Event arguments for recording errors.
/// </summary>
public class RecordingErrorEventArgs : DomainEventArgs
{
    /// <summary>
    /// The error that occurred.
    /// </summary>
    public Exception Error { get; }

    /// <summary>
    /// The recording session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Description of what was being done when the error occurred.
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// Initializes a new recording error event.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="context">Context description.</param>
    public RecordingErrorEventArgs(Exception error, Guid sessionId, string context)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
        SessionId = sessionId;
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }
}

/// <summary>
/// Event arguments for execution progress updates.
/// </summary>
public class ExecutionProgressEventArgs : DomainEventArgs
{
    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Current command index being executed.
    /// </summary>
    public int CurrentCommandIndex { get; }

    /// <summary>
    /// Total number of commands in the script.
    /// </summary>
    public int TotalCommands { get; }

    /// <summary>
    /// Percentage of completion (0-100).
    /// </summary>
    public double CompletionPercentage { get; }

    /// <summary>
    /// Time elapsed since execution started.
    /// </summary>
    public TimeSpan ElapsedTime { get; }

    /// <summary>
    /// Estimated remaining execution time.
    /// </summary>
    public TimeSpan? EstimatedRemainingTime { get; }

    /// <summary>
    /// Initializes a new execution progress event.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="currentCommandIndex">Current command index.</param>
    /// <param name="totalCommands">Total command count.</param>
    /// <param name="elapsedTime">Elapsed time.</param>
    /// <param name="estimatedRemainingTime">Estimated remaining time.</param>
    public ExecutionProgressEventArgs(Guid sessionId, int currentCommandIndex, int totalCommands, TimeSpan elapsedTime, TimeSpan? estimatedRemainingTime = null)
    {
        SessionId = sessionId;
        CurrentCommandIndex = currentCommandIndex;
        TotalCommands = totalCommands;
        CompletionPercentage = totalCommands > 0 ? (double)currentCommandIndex / totalCommands * 100 : 0;
        ElapsedTime = elapsedTime;
        EstimatedRemainingTime = estimatedRemainingTime;
    }
}

/// <summary>
/// Event arguments for execution state changes.
/// </summary>
public class ExecutionStateChangedEventArgs : DomainEventArgs
{
    /// <summary>
    /// The previous execution state.
    /// </summary>
    public ExecutionState PreviousState { get; }

    /// <summary>
    /// The new execution state.
    /// </summary>
    public ExecutionState NewState { get; }

    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Optional reason for the state change.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new execution state changed event.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="reason">Optional reason for the change.</param>
    public ExecutionStateChangedEventArgs(ExecutionState previousState, ExecutionState newState, Guid sessionId, string? reason = null)
    {
        PreviousState = previousState;
        NewState = newState;
        SessionId = sessionId;
        Reason = reason;
    }
}

/// <summary>
/// Event arguments for when a command is about to be executed.
/// </summary>
public class CommandExecutingEventArgs : DomainEventArgs
{
    /// <summary>
    /// The command about to be executed.
    /// </summary>
    public Command Command { get; }

    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Index of the command in the script.
    /// </summary>
    public int CommandIndex { get; }

    /// <summary>
    /// Whether execution can be cancelled at this point.
    /// </summary>
    public bool CanCancel { get; set; } = true;

    /// <summary>
    /// Initializes a new command executing event.
    /// </summary>
    /// <param name="command">The command about to be executed.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="commandIndex">The command index.</param>
    public CommandExecutingEventArgs(Command command, Guid sessionId, int commandIndex)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        SessionId = sessionId;
        CommandIndex = commandIndex;
    }
}

/// <summary>
/// Event arguments for when a command has been executed.
/// </summary>
public class CommandExecutedEventArgs : DomainEventArgs
{
    /// <summary>
    /// The command that was executed.
    /// </summary>
    public Command Command { get; }

    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Index of the command in the script.
    /// </summary>
    public int CommandIndex { get; }

    /// <summary>
    /// Whether the command executed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Error that occurred during execution, if any.
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Time taken to execute the command.
    /// </summary>
    public TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Initializes a new command executed event.
    /// </summary>
    /// <param name="command">The executed command.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="commandIndex">The command index.</param>
    /// <param name="success">Whether execution was successful.</param>
    /// <param name="executionTime">Time taken to execute.</param>
    /// <param name="error">Error that occurred, if any.</param>
    public CommandExecutedEventArgs(Command command, Guid sessionId, int commandIndex, bool success, TimeSpan executionTime, Exception? error = null)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        SessionId = sessionId;
        CommandIndex = commandIndex;
        Success = success;
        ExecutionTime = executionTime;
        Error = error;
    }
}

/// <summary>
/// Event arguments for execution errors.
/// </summary>
public class ExecutionErrorEventArgs : DomainEventArgs
{
    /// <summary>
    /// The error that occurred.
    /// </summary>
    public Exception Error { get; }

    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// The command that was being executed when the error occurred, if any.
    /// </summary>
    public Command? Command { get; }

    /// <summary>
    /// Index of the command that caused the error, if applicable.
    /// </summary>
    public int? CommandIndex { get; }

    /// <summary>
    /// Description of what was being done when the error occurred.
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// Whether execution can continue after this error.
    /// </summary>
    public bool CanContinue { get; }

    /// <summary>
    /// Initializes a new execution error event.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="context">Context description.</param>
    /// <param name="canContinue">Whether execution can continue.</param>
    /// <param name="command">The command being executed, if any.</param>
    /// <param name="commandIndex">The command index, if applicable.</param>
    public ExecutionErrorEventArgs(Exception error, Guid sessionId, string context, bool canContinue = false, Command? command = null, int? commandIndex = null)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
        SessionId = sessionId;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        CanContinue = canContinue;
        Command = command;
        CommandIndex = commandIndex;
    }
}

/// <summary>
/// Event arguments for when execution completes.
/// </summary>
public class ExecutionCompletedEventArgs : DomainEventArgs
{
    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// The final execution state.
    /// </summary>
    public ExecutionState FinalState { get; }

    /// <summary>
    /// Total number of commands that were executed.
    /// </summary>
    public int ExecutedCommandCount { get; }

    /// <summary>
    /// Total number of commands in the script.
    /// </summary>
    public int TotalCommandCount { get; }

    /// <summary>
    /// Total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Whether execution completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Error that caused execution to fail, if any.
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Reason for completion (e.g., "Completed successfully", "User terminated", "Error occurred").
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new execution completed event.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="finalState">The final execution state.</param>
    /// <param name="executedCommandCount">Number of commands executed.</param>
    /// <param name="totalCommandCount">Total number of commands.</param>
    /// <param name="totalExecutionTime">Total execution time.</param>
    /// <param name="success">Whether execution was successful.</param>
    /// <param name="reason">Reason for completion.</param>
    /// <param name="error">Error that occurred, if any.</param>
    public ExecutionCompletedEventArgs(Guid sessionId, ExecutionState finalState, int executedCommandCount, int totalCommandCount,
        TimeSpan totalExecutionTime, bool success, string reason, Exception? error = null)
    {
        SessionId = sessionId;
        FinalState = finalState;
        ExecutedCommandCount = executedCommandCount;
        TotalCommandCount = totalCommandCount;
        TotalExecutionTime = totalExecutionTime;
        Success = success;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Error = error;
    }
}