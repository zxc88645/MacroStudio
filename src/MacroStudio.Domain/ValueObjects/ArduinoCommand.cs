namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents a command to be sent to the Arduino.
/// </summary>
public abstract class ArduinoCommand
{
    /// <summary>
    /// Gets the command type.
    /// </summary>
    public abstract ArduinoCommandType CommandType { get; }

    /// <summary>
    /// Serializes the command data to a byte array.
    /// </summary>
    /// <returns>The serialized command data.</returns>
    public abstract byte[] Serialize();
}

/// <summary>
/// Command to move the mouse to an absolute position.
/// </summary>
public sealed class ArduinoMouseMoveAbsoluteCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.MouseMoveAbsolute;

    /// <summary>
    /// Gets the target X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the target Y coordinate.
    /// </summary>
    public int Y { get; }

    public ArduinoMouseMoveAbsoluteCommand(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override byte[] Serialize()
    {
        return new byte[]
        {
            (byte)(X & 0xFF),
            (byte)((X >> 8) & 0xFF),
            (byte)(Y & 0xFF),
            (byte)((Y >> 8) & 0xFF)
        };
    }
}

/// <summary>
/// Command to move the mouse relative to current position.
/// </summary>
public sealed class ArduinoMouseMoveRelativeCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.MouseMoveRelative;

    /// <summary>
    /// Gets the relative X displacement.
    /// </summary>
    public int DeltaX { get; }

    /// <summary>
    /// Gets the relative Y displacement.
    /// </summary>
    public int DeltaY { get; }

    public ArduinoMouseMoveRelativeCommand(int deltaX, int deltaY)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    public override byte[] Serialize()
    {
        return new byte[]
        {
            (byte)(DeltaX & 0xFF),
            (byte)((DeltaX >> 8) & 0xFF),
            (byte)(DeltaY & 0xFF),
            (byte)((DeltaY >> 8) & 0xFF)
        };
    }
}

/// <summary>
/// Command to perform a mouse click.
/// </summary>
public sealed class ArduinoMouseClickCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.MouseClick;

    /// <summary>
    /// Gets the mouse button to click.
    /// </summary>
    public MouseButton Button { get; }

    /// <summary>
    /// Gets the click type.
    /// </summary>
    public ClickType ClickType { get; }

    public ArduinoMouseClickCommand(MouseButton button, ClickType clickType)
    {
        Button = button;
        ClickType = clickType;
    }

    public override byte[] Serialize()
    {
        return new byte[]
        {
            (byte)Button,
            (byte)ClickType
        };
    }
}

/// <summary>
/// Command to type text using keyboard.
/// </summary>
public sealed class ArduinoKeyboardTextCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.KeyboardText;

    /// <summary>
    /// Gets the text to type.
    /// </summary>
    public string Text { get; }

    public ArduinoKeyboardTextCommand(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public override byte[] Serialize()
    {
        return System.Text.Encoding.UTF8.GetBytes(Text);
    }
}

/// <summary>
/// Command to press or release a key.
/// </summary>
public sealed class ArduinoKeyPressCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.KeyPress;

    /// <summary>
    /// Gets the virtual key code.
    /// </summary>
    public VirtualKey Key { get; }

    /// <summary>
    /// Gets whether the key is being pressed (true) or released (false).
    /// </summary>
    public bool IsDown { get; }

    public ArduinoKeyPressCommand(VirtualKey key, bool isDown)
    {
        Key = key;
        IsDown = isDown;
    }

    public override byte[] Serialize()
    {
        return new byte[]
        {
            (byte)((ushort)Key & 0xFF),
            (byte)(((ushort)Key >> 8) & 0xFF),
            (byte)(IsDown ? 1 : 0)
        };
    }
}

/// <summary>
/// Command to introduce a delay.
/// </summary>
public sealed class ArduinoDelayCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.Delay;

    /// <summary>
    /// Gets the delay duration in milliseconds.
    /// </summary>
    public uint DurationMs { get; }

    public ArduinoDelayCommand(uint durationMs)
    {
        DurationMs = durationMs;
    }

    public override byte[] Serialize()
    {
        return new byte[]
        {
            (byte)(DurationMs & 0xFF),
            (byte)((DurationMs >> 8) & 0xFF),
            (byte)((DurationMs >> 16) & 0xFF),
            (byte)((DurationMs >> 24) & 0xFF)
        };
    }
}

/// <summary>
/// Command to start recording.
/// </summary>
public sealed class ArduinoStartRecordingCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.StartRecording;

    public override byte[] Serialize()
    {
        return Array.Empty<byte>();
    }
}

/// <summary>
/// Command to stop recording.
/// </summary>
public sealed class ArduinoStopRecordingCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.StopRecording;

    public override byte[] Serialize()
    {
        return Array.Empty<byte>();
    }
}

/// <summary>
/// Command to query Arduino status (heartbeat).
/// </summary>
public sealed class ArduinoStatusQueryCommand : ArduinoCommand
{
    public override ArduinoCommandType CommandType => ArduinoCommandType.StatusQuery;

    public override byte[] Serialize()
    {
        return Array.Empty<byte>();
    }
}
