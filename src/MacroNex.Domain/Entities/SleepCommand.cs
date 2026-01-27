namespace MacroNex.Domain.Entities;

/// <summary>
/// Represents a sleep/delay command that pauses execution for a specified duration.
/// </summary>
public class SleepCommand : Command
{
    /// <summary>
    /// The duration to sleep/pause execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets the display name for this command type.
    /// </summary>
    public override string DisplayName => "Sleep";

    /// <summary>
    /// Gets a human-readable description of this command's parameters.
    /// </summary>
    public override string Description => $"Wait {Duration.TotalMilliseconds:F0}ms";

    /// <summary>
    /// Initializes a new sleep command with the specified duration.
    /// </summary>
    /// <param name="duration">The duration to sleep.</param>
    public SleepCommand(TimeSpan duration) : base()
    {
        Duration = duration;
    }

    /// <summary>
    /// Initializes a new sleep command with specific parameters (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this command.</param>
    /// <param name="delay">The delay before executing this command.</param>
    /// <param name="createdAt">The timestamp when this command was created.</param>
    /// <param name="duration">The duration to sleep.</param>
    public SleepCommand(Guid id, TimeSpan delay, DateTime createdAt, TimeSpan duration)
        : base(id, delay, createdAt)
    {
        Duration = duration;
    }

    /// <summary>
    /// Validates that this command's parameters are valid for execution.
    /// </summary>
    /// <returns>True if the duration is non-negative, false otherwise.</returns>
    public override bool IsValid()
    {
        return Duration >= TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a deep copy of this command.
    /// </summary>
    /// <returns>A new SleepCommand instance with the same parameters.</returns>
    public override Command Clone()
    {
        return new SleepCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, Duration);
    }
}