using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Interfaces;

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
    public Point Position { get; }

    public InputHookMouseMoveEventArgs(Point position)
    {
        Position = position;
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

