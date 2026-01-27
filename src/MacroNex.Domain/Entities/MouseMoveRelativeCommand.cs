namespace MacroNex.Domain.Entities;

/// <summary>
/// Represents a mouse movement command that moves the cursor relative to its current position.
/// </summary>
public class MouseMoveRelativeCommand : Command
{
    /// <summary>
    /// The horizontal displacement (delta X) in pixels. Can be negative.
    /// </summary>
    public int DeltaX { get; set; }

    /// <summary>
    /// The vertical displacement (delta Y) in pixels. Can be negative.
    /// </summary>
    public int DeltaY { get; set; }

    /// <summary>
    /// Gets the display name for this command type.
    /// </summary>
    public override string DisplayName => "Mouse Move (Relative)";

    /// <summary>
    /// Gets a human-readable description of this command's parameters.
    /// </summary>
    public override string Description => $"Move relative ({DeltaX}, {DeltaY})";

    /// <summary>
    /// Initializes a new relative mouse move command with the specified deltas.
    /// </summary>
    /// <param name="deltaX">The horizontal displacement in pixels.</param>
    /// <param name="deltaY">The vertical displacement in pixels.</param>
    public MouseMoveRelativeCommand(int deltaX, int deltaY) : base()
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    /// <summary>
    /// Initializes a new relative mouse move command with specific parameters (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this command.</param>
    /// <param name="delay">The delay before executing this command.</param>
    /// <param name="createdAt">The timestamp when this command was created.</param>
    /// <param name="deltaX">The horizontal displacement in pixels.</param>
    /// <param name="deltaY">The vertical displacement in pixels.</param>
    public MouseMoveRelativeCommand(Guid id, TimeSpan delay, DateTime createdAt, int deltaX, int deltaY)
        : base(id, delay, createdAt)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    /// <summary>
    /// Validates that this command's parameters are valid for execution.
    /// </summary>
    /// <returns>True if the deltas are within reasonable bounds, false otherwise.</returns>
    public override bool IsValid()
    {
        // Allow reasonable range for relative movement (e.g., -32768 to 32767 pixels)
        const int maxDelta = 32767;
        const int minDelta = -32768;
        return DeltaX >= minDelta && DeltaX <= maxDelta && DeltaY >= minDelta && DeltaY <= maxDelta;
    }

    /// <summary>
    /// Creates a deep copy of this command.
    /// </summary>
    /// <returns>A new MouseMoveRelativeCommand instance with the same parameters.</returns>
    public override Command Clone()
    {
        return new MouseMoveRelativeCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, DeltaX, DeltaY);
    }
}
