using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Entities;

/// <summary>
/// Represents a keyboard input command that can send text or key combinations.
/// </summary>
public class KeyboardCommand : Command
{
    /// <summary>
    /// Text to be typed. If specified, this takes precedence over individual keys.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// List of virtual keys to be pressed. Used for key combinations or special keys.
    /// </summary>
    public List<VirtualKey> Keys { get; set; }

    /// <summary>
    /// Gets the display name for this command type.
    /// </summary>
    public override string DisplayName => "Keyboard Input";

    /// <summary>
    /// Gets a human-readable description of this command's parameters.
    /// </summary>
    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(Text))
            {
                return $"Type: \"{Text}\"";
            }
            else if (Keys.Count > 0)
            {
                var keyNames = Keys.Select(k => k.ToString().Replace("VK_", ""));
                return $"Keys: {string.Join(" + ", keyNames)}";
            }
            else
            {
                return "No input specified";
            }
        }
    }

    /// <summary>
    /// Initializes a new keyboard command for typing text.
    /// </summary>
    /// <param name="text">The text to be typed.</param>
    public KeyboardCommand(string text) : base()
    {
        Text = text;
        Keys = new List<VirtualKey>();
    }

    /// <summary>
    /// Initializes a new keyboard command for pressing key combinations.
    /// </summary>
    /// <param name="keys">The virtual keys to be pressed.</param>
    public KeyboardCommand(IEnumerable<VirtualKey> keys) : base()
    {
        Text = null;
        Keys = new List<VirtualKey>(keys);
    }

    /// <summary>
    /// Initializes a new keyboard command for pressing a single key.
    /// </summary>
    /// <param name="key">The virtual key to be pressed.</param>
    public KeyboardCommand(VirtualKey key) : base()
    {
        Text = null;
        Keys = new List<VirtualKey> { key };
    }

    /// <summary>
    /// Initializes a new keyboard command with specific parameters (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this command.</param>
    /// <param name="delay">The delay before executing this command.</param>
    /// <param name="createdAt">The timestamp when this command was created.</param>
    /// <param name="text">The text to be typed (can be null).</param>
    /// <param name="keys">The virtual keys to be pressed.</param>
    public KeyboardCommand(Guid id, TimeSpan delay, DateTime createdAt, string? text, IEnumerable<VirtualKey> keys)
        : base(id, delay, createdAt)
    {
        Text = text;
        Keys = new List<VirtualKey>(keys);
    }

    /// <summary>
    /// Validates that this command's parameters are valid for execution.
    /// </summary>
    /// <returns>True if either text is provided or at least one key is specified, false otherwise.</returns>
    public override bool IsValid()
    {
        return !string.IsNullOrEmpty(Text) || Keys.Count > 0;
    }

    /// <summary>
    /// Creates a deep copy of this command.
    /// </summary>
    /// <returns>A new KeyboardCommand instance with the same parameters.</returns>
    public override Command Clone()
    {
        return new KeyboardCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, Text, Keys);
    }
}