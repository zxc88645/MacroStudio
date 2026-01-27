using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Entities;

/// <summary>
/// Represents a mouse movement command that moves the cursor to a specific position.
/// </summary>
public class MouseMoveCommand : Command
{
    /// <summary>
    /// The target position to move the mouse cursor to.
    /// </summary>
    public Point Position { get; set; }

    /// <summary>
    /// Gets the display name for this command type.
    /// </summary>
    public override string DisplayName => "Mouse Move";

    /// <summary>
    /// Gets a human-readable description of this command's parameters.
    /// </summary>
    public override string Description => $"Move to {Position}";

    /// <summary>
    /// Initializes a new mouse move command with the specified position.
    /// </summary>
    /// <param name="position">The target position to move the mouse to.</param>
    public MouseMoveCommand(Point position) : base()
    {
        Position = position;
    }

    /// <summary>
    /// Initializes a new mouse move command with specific parameters (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this command.</param>
    /// <param name="delay">The delay before executing this command.</param>
    /// <param name="createdAt">The timestamp when this command was created.</param>
    /// <param name="position">The target position to move the mouse to.</param>
    public MouseMoveCommand(Guid id, TimeSpan delay, DateTime createdAt, Point position)
        : base(id, delay, createdAt)
    {
        Position = position;
    }

    /// <summary>
    /// Validates that this command's parameters are valid for execution.
    /// </summary>
    /// <returns>True if the position coordinates are non-negative, false otherwise.</returns>
    public override bool IsValid()
    {
        return Position.X >= 0 && Position.Y >= 0;
    }

    /// <summary>
    /// Creates a deep copy of this command.
    /// </summary>
    /// <returns>A new MouseMoveCommand instance with the same parameters.</returns>
    public override Command Clone()
    {
        return new MouseMoveCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, Position);
    }
}