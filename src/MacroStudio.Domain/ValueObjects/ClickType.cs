namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents the type of mouse click action.
/// </summary>
public enum ClickType
{
    /// <summary>
    /// Mouse button press down action.
    /// </summary>
    Down,

    /// <summary>
    /// Mouse button release up action.
    /// </summary>
    Up,

    /// <summary>
    /// Complete click action (down followed by up).
    /// </summary>
    Click
}