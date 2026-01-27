namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// Represents the state of a recording session.
/// </summary>
public enum RecordingState
{
    /// <summary>
    /// No recording session is active.
    /// </summary>
    Inactive,

    /// <summary>
    /// Recording is active and capturing input.
    /// </summary>
    Active,

    /// <summary>
    /// Recording is paused and not capturing input.
    /// </summary>
    Paused,

    /// <summary>
    /// Recording has been stopped and finalized.
    /// </summary>
    Stopped,

    /// <summary>
    /// Recording encountered an error and was terminated.
    /// </summary>
    Error
}