using MacroNex.Domain.ValueObjects;

namespace MacroNex.Domain.Interfaces;

/// <summary>
/// Domain interface (port) for capturing global mouse/keyboard input events.
/// Implementations are platform-specific (e.g., Win32 low-level hooks).
/// </summary>
public interface IInputHookService
{
    /// <summary>
    /// Whether hooks are currently installed.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Raised when the mouse moves.
    /// </summary>
    event EventHandler<InputHookMouseMoveEventArgs> MouseMoved;

    /// <summary>
    /// Raised when the mouse button is pressed/released.
    /// </summary>
    event EventHandler<InputHookMouseClickEventArgs> MouseClicked;

    /// <summary>
    /// Raised when a low-level key event is detected (down/up).
    /// </summary>
    event EventHandler<InputHookKeyEventArgs> KeyboardInput;

    /// <summary>
    /// Installs global hooks based on recording options.
    /// </summary>
    Task InstallHooksAsync(RecordingOptions options);

    /// <summary>
    /// Uninstalls any installed global hooks.
    /// </summary>
    Task UninstallHooksAsync();
}

public sealed class InputHookMouseMoveEventArgs : EventArgs
{
    /// <summary>
    /// The mouse position (absolute coordinates, or accumulated position for relative events).
    /// </summary>
    public Point Position { get; }
    
    /// <summary>
    /// Whether this is a relative movement event (from hardware input).
    /// When true, DeltaX and DeltaY contain the original relative movement values.
    /// </summary>
    public bool IsRelative { get; }
    
    /// <summary>
    /// The relative X displacement (only valid when IsRelative is true).
    /// </summary>
    public int DeltaX { get; }
    
    /// <summary>
    /// The relative Y displacement (only valid when IsRelative is true).
    /// </summary>
    public int DeltaY { get; }

    /// <summary>
    /// Creates an absolute position mouse move event.
    /// </summary>
    public InputHookMouseMoveEventArgs(Point position)
    {
        Position = position;
        IsRelative = false;
        DeltaX = 0;
        DeltaY = 0;
    }
    
    /// <summary>
    /// Creates a relative movement mouse move event (from hardware input).
    /// </summary>
    /// <param name="deltaX">The relative X displacement.</param>
    /// <param name="deltaY">The relative Y displacement.</param>
    /// <param name="accumulatedPosition">The accumulated position for reference.</param>
    public InputHookMouseMoveEventArgs(int deltaX, int deltaY, Point accumulatedPosition)
    {
        Position = accumulatedPosition;
        IsRelative = true;
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}

public sealed class InputHookMouseClickEventArgs : EventArgs
{
    public Point Position { get; }
    public MouseButton Button { get; }
    public ClickType ClickType { get; }

    /// <summary>
    /// Whether Windows reports this event as injected (synthesized).
    /// </summary>
    public bool IsInjected { get; }

    public InputHookMouseClickEventArgs(Point position, MouseButton button, ClickType clickType, bool isInjected)
    {
        Position = position;
        Button = button;
        ClickType = clickType;
        IsInjected = isInjected;
    }
}

public sealed class InputHookKeyEventArgs : EventArgs
{
    public VirtualKey Key { get; }

    /// <summary>
    /// True = key down, False = key up.
    /// </summary>
    public bool IsDown { get; }

    /// <summary>
    /// Whether Windows reports this event as injected (synthesized).
    /// </summary>
    public bool IsInjected { get; }

    public InputHookKeyEventArgs(VirtualKey key, bool isDown, bool isInjected)
    {
        Key = key;
        IsDown = isDown;
        IsInjected = isInjected;
    }
}

