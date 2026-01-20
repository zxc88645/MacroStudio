using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Entities;

/// <summary>
/// Represents a low-level keyboard event (key down / key up).
/// Useful for accurately replaying shortcuts and non-text interactions.
/// </summary>
public sealed class KeyPressCommand : Command
{
    public VirtualKey Key { get; set; }

    /// <summary>
    /// True = key down, False = key up.
    /// </summary>
    public bool IsDown { get; set; }

    public override string DisplayName => "Key Press";

    public override string Description => $"{(IsDown ? "Down" : "Up")} {Key}";

    public KeyPressCommand(VirtualKey key, bool isDown) : base()
    {
        Key = key;
        IsDown = isDown;
    }

    public KeyPressCommand(Guid id, TimeSpan delay, DateTime createdAt, VirtualKey key, bool isDown)
        : base(id, delay, createdAt)
    {
        Key = key;
        IsDown = isDown;
    }

    public override bool IsValid()
    {
        return Enum.IsDefined(typeof(VirtualKey), Key);
    }

    public override Command Clone()
    {
        return new KeyPressCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, Key, IsDown);
    }
}

