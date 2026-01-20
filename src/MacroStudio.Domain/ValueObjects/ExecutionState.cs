namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents the current state of script execution.
/// </summary>
public enum ExecutionState
{
    /// <summary>
    /// Execution is not active - script is idle.
    /// </summary>
    Idle,

    /// <summary>
    /// Execution is currently running and processing commands.
    /// </summary>
    Running,

    /// <summary>
    /// Execution is temporarily paused and can be resumed.
    /// </summary>
    Paused,

    /// <summary>
    /// Execution has been stopped and reset to the beginning.
    /// </summary>
    Stopped,

    /// <summary>
    /// Execution is in single-step mode for debugging.
    /// </summary>
    Stepping,

    /// <summary>
    /// Execution has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Execution has failed due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Execution was terminated by the kill switch or safety mechanism.
    /// </summary>
    Terminated
}