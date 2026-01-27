using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Entities;

/// <summary>
/// Abstract base class for all automation commands.
/// Each command represents a single action that can be executed during script playback.
/// </summary>
public abstract class Command
{
    /// <summary>
    /// Unique identifier for this command.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Delay to wait before executing this command.
    /// This represents the time gap between the previous command and this one.
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// Timestamp when this command was created (typically during recording).
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Initializes a new command with a unique ID and current timestamp.
    /// </summary>
    protected Command()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Delay = TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new command with a specific ID (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this command.</param>
    /// <param name="delay">The delay before executing this command.</param>
    /// <param name="createdAt">The timestamp when this command was created.</param>
    protected Command(Guid id, TimeSpan delay, DateTime createdAt)
    {
        Id = id;
        Delay = delay;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the display name for this command type.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets a human-readable description of this command's parameters.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Validates that this command's parameters are valid for execution.
    /// </summary>
    /// <returns>True if the command is valid, false otherwise.</returns>
    public abstract bool IsValid();

    /// <summary>
    /// Creates a deep copy of this command.
    /// </summary>
    /// <returns>A new command instance with the same parameters.</returns>
    public abstract Command Clone();

    public override bool Equals(object? obj)
    {
        return obj is Command other && Id.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return $"{DisplayName}: {Description}";
    }
}