namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents a hotkey definition with modifiers and a key.
/// </summary>
public record HotkeyDefinition
{
    /// <summary>
    /// The unique identifier for this hotkey.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The name or description of this hotkey.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The modifier keys that must be pressed with the main key.
    /// </summary>
    public HotkeyModifiers Modifiers { get; init; }

    /// <summary>
    /// The main key that triggers the hotkey.
    /// </summary>
    public VirtualKey Key { get; init; }

    /// <summary>
    /// Initializes a new instance of the HotkeyDefinition record.
    /// </summary>
    /// <param name="id">The unique identifier for this hotkey.</param>
    /// <param name="name">The name or description of this hotkey.</param>
    /// <param name="modifiers">The modifier keys.</param>
    /// <param name="key">The main key.</param>
    public HotkeyDefinition(Guid id, string name, HotkeyModifiers modifiers, VirtualKey key)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Hotkey name cannot be null or whitespace", nameof(name));

        if (!Enum.IsDefined(typeof(VirtualKey), key))
            throw new ArgumentException($"Invalid virtual key: {key}", nameof(key));

        Id = id;
        Name = name.Trim();
        Modifiers = modifiers;
        Key = key;
    }

    /// <summary>
    /// Creates a new hotkey definition with a generated ID.
    /// </summary>
    /// <param name="name">The name or description of this hotkey.</param>
    /// <param name="modifiers">The modifier keys.</param>
    /// <param name="key">The main key.</param>
    /// <returns>A new HotkeyDefinition instance.</returns>
    public static HotkeyDefinition Create(string name, HotkeyModifiers modifiers, VirtualKey key)
    {
        return new HotkeyDefinition(Guid.NewGuid(), name, modifiers, key);
    }

    /// <summary>
    /// Gets a string representation of the hotkey combination.
    /// </summary>
    /// <returns>A human-readable string representation of the hotkey.</returns>
    public string GetDisplayString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
            parts.Add("Win");

        // Convert VirtualKey to display string
        parts.Add(GetKeyDisplayString(Key));

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Converts a VirtualKey to a human-readable display string.
    /// </summary>
    /// <param name="key">The virtual key to convert.</param>
    /// <returns>A human-readable string representation of the key.</returns>
    private static string GetKeyDisplayString(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.VK_ESCAPE => "Esc",
            VirtualKey.VK_SPACE => "Space",
            VirtualKey.VK_RETURN => "Enter",
            VirtualKey.VK_BACK => "Backspace",
            VirtualKey.VK_TAB => "Tab",
            VirtualKey.VK_CAPITAL => "Caps Lock",
            VirtualKey.VK_SHIFT => "Shift",
            VirtualKey.VK_CONTROL => "Ctrl",
            VirtualKey.VK_MENU => "Alt",
            VirtualKey.VK_PAUSE => "Pause",
            VirtualKey.VK_PRIOR => "Page Up",
            VirtualKey.VK_NEXT => "Page Down",
            VirtualKey.VK_END => "End",
            VirtualKey.VK_HOME => "Home",
            VirtualKey.VK_LEFT => "Left",
            VirtualKey.VK_UP => "Up",
            VirtualKey.VK_RIGHT => "Right",
            VirtualKey.VK_DOWN => "Down",
            VirtualKey.VK_INSERT => "Insert",
            VirtualKey.VK_DELETE => "Delete",
            VirtualKey.VK_SNAPSHOT => "Print Screen",
            VirtualKey.VK_LWIN => "Left Win",
            VirtualKey.VK_RWIN => "Right Win",
            VirtualKey.VK_APPS => "Menu",
            VirtualKey.VK_NUMLOCK => "Num Lock",
            VirtualKey.VK_SCROLL => "Scroll Lock",
            
            // Number keys
            VirtualKey.VK_0 => "0",
            VirtualKey.VK_1 => "1",
            VirtualKey.VK_2 => "2",
            VirtualKey.VK_3 => "3",
            VirtualKey.VK_4 => "4",
            VirtualKey.VK_5 => "5",
            VirtualKey.VK_6 => "6",
            VirtualKey.VK_7 => "7",
            VirtualKey.VK_8 => "8",
            VirtualKey.VK_9 => "9",
            
            // Letter keys
            VirtualKey.VK_A => "A",
            VirtualKey.VK_B => "B",
            VirtualKey.VK_C => "C",
            VirtualKey.VK_D => "D",
            VirtualKey.VK_E => "E",
            VirtualKey.VK_F => "F",
            VirtualKey.VK_G => "G",
            VirtualKey.VK_H => "H",
            VirtualKey.VK_I => "I",
            VirtualKey.VK_J => "J",
            VirtualKey.VK_K => "K",
            VirtualKey.VK_L => "L",
            VirtualKey.VK_M => "M",
            VirtualKey.VK_N => "N",
            VirtualKey.VK_O => "O",
            VirtualKey.VK_P => "P",
            VirtualKey.VK_Q => "Q",
            VirtualKey.VK_R => "R",
            VirtualKey.VK_S => "S",
            VirtualKey.VK_T => "T",
            VirtualKey.VK_U => "U",
            VirtualKey.VK_V => "V",
            VirtualKey.VK_W => "W",
            VirtualKey.VK_X => "X",
            VirtualKey.VK_Y => "Y",
            VirtualKey.VK_Z => "Z",
            
            // Function keys
            VirtualKey.VK_F1 => "F1",
            VirtualKey.VK_F2 => "F2",
            VirtualKey.VK_F3 => "F3",
            VirtualKey.VK_F4 => "F4",
            VirtualKey.VK_F5 => "F5",
            VirtualKey.VK_F6 => "F6",
            VirtualKey.VK_F7 => "F7",
            VirtualKey.VK_F8 => "F8",
            VirtualKey.VK_F9 => "F9",
            VirtualKey.VK_F10 => "F10",
            VirtualKey.VK_F11 => "F11",
            VirtualKey.VK_F12 => "F12",
            
            // Numpad keys
            VirtualKey.VK_NUMPAD0 => "Num 0",
            VirtualKey.VK_NUMPAD1 => "Num 1",
            VirtualKey.VK_NUMPAD2 => "Num 2",
            VirtualKey.VK_NUMPAD3 => "Num 3",
            VirtualKey.VK_NUMPAD4 => "Num 4",
            VirtualKey.VK_NUMPAD5 => "Num 5",
            VirtualKey.VK_NUMPAD6 => "Num 6",
            VirtualKey.VK_NUMPAD7 => "Num 7",
            VirtualKey.VK_NUMPAD8 => "Num 8",
            VirtualKey.VK_NUMPAD9 => "Num 9",
            VirtualKey.VK_MULTIPLY => "Num *",
            VirtualKey.VK_ADD => "Num +",
            VirtualKey.VK_SUBTRACT => "Num -",
            VirtualKey.VK_DECIMAL => "Num .",
            VirtualKey.VK_DIVIDE => "Num /",
            
            _ => key.ToString().Replace("VK_", "")
        };
    }

    /// <summary>
    /// Validates that this hotkey definition is valid for registration.
    /// </summary>
    /// <returns>True if the hotkey is valid, false otherwise.</returns>
    public bool IsValid()
    {
        // Key must be valid
        if (!Enum.IsDefined(typeof(VirtualKey), Key))
            return false;

        // If no modifiers, allow single keys (but not modifier keys themselves)
        if (Modifiers == HotkeyModifiers.None)
        {
            // Don't allow modifier keys as single keys
            return Key != VirtualKey.VK_SHIFT &&
                   Key != VirtualKey.VK_CONTROL &&
                   Key != VirtualKey.VK_MENU &&
                   Key != VirtualKey.VK_LSHIFT &&
                   Key != VirtualKey.VK_RSHIFT &&
                   Key != VirtualKey.VK_LCONTROL &&
                   Key != VirtualKey.VK_RCONTROL &&
                   Key != VirtualKey.VK_LMENU &&
                   Key != VirtualKey.VK_RMENU &&
                   Key != VirtualKey.VK_LWIN &&
                   Key != VirtualKey.VK_RWIN;
        }

        // Cannot use modifier keys as the main key when modifiers are specified
        return Key != VirtualKey.VK_SHIFT &&
               Key != VirtualKey.VK_CONTROL &&
               Key != VirtualKey.VK_MENU &&
               Key != VirtualKey.VK_LSHIFT &&
               Key != VirtualKey.VK_RSHIFT &&
               Key != VirtualKey.VK_LCONTROL &&
               Key != VirtualKey.VK_RCONTROL &&
               Key != VirtualKey.VK_LMENU &&
               Key != VirtualKey.VK_RMENU &&
               Key != VirtualKey.VK_LWIN &&
               Key != VirtualKey.VK_RWIN;
    }

    /// <summary>
    /// Returns a string representation of this hotkey definition.
    /// </summary>
    /// <returns>A string representation of the hotkey.</returns>
    public override string ToString()
    {
        return $"{Name}: {GetDisplayString()}";
    }
}