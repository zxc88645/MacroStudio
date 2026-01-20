using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Entities;

/// <summary>
/// Represents a mouse click command that performs a click action with a specific button.
/// The actual cursor position is determined by the current mouse location when executed.
/// </summary>
public class MouseClickCommand : Command
{
    /// <summary>
    /// The mouse button to click.
    /// </summary>
    public MouseButton Button { get; set; }

    /// <summary>
    /// The type of click action to perform.
    /// </summary>
    public ClickType Type { get; set; }

    /// <summary>
    /// Gets the display name for this command type.
    /// </summary>
    public override string DisplayName => "Mouse Click";

    /// <summary>
    /// Gets a human-readable description of this command's parameters.
    /// </summary>
    public override string Description => $"{Type} {Button}";

    /// <summary>
    /// Initializes a new mouse click command with the specified parameters.
    /// </summary>
    /// <param name="button">The mouse button to click.</param>
    /// <param name="type">The type of click action to perform.</param>
    public MouseClickCommand(MouseButton button, ClickType type) : base()
    {
        Button = button;
        Type = type;
    }

    /// <summary>
    /// Initializes a new mouse click command with specific parameters (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this command.</param>
    /// <param name="delay">The delay before executing this command.</param>
    /// <param name="createdAt">The timestamp when this command was created.</param>
    /// <param name="button">The mouse button to click.</param>
    /// <param name="type">The type of click action to perform.</param>
    public MouseClickCommand(Guid id, TimeSpan delay, DateTime createdAt, MouseButton button, ClickType type)
        : base(id, delay, createdAt)
    {
        Button = button;
        Type = type;
    }

    /// <summary>
    /// Validates that this command's parameters are valid for execution.
    /// </summary>
    /// <returns>True if button/type are valid enum values, false otherwise.</returns>
    public override bool IsValid()
    {
        return Enum.IsDefined(typeof(MouseButton), Button)
               && Enum.IsDefined(typeof(ClickType), Type);
    }

    /// <summary>
    /// Creates a deep copy of this command.
    /// </summary>
    /// <returns>A new MouseClickCommand instance with the same parameters.</returns>
    public override Command Clone()
    {
        return new MouseClickCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, Button, Type);
    }
}