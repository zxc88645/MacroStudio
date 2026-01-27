using MacroNex.Domain.ValueObjects;
using Xunit;

namespace MacroNex.Tests.Domain.ValueObjects;

/// <summary>
/// Unit tests for HotkeyDefinition value object.
/// </summary>
public class HotkeyDefinitionTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Hotkey";
        var modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt;
        var key = VirtualKey.VK_F1;

        // Act
        var hotkey = new HotkeyDefinition(id, name, modifiers, key);

        // Assert
        Assert.Equal(id, hotkey.Id);
        Assert.Equal(name, hotkey.Name);
        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal(key, hotkey.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        // Arrange
        var id = Guid.NewGuid();
        var modifiers = HotkeyModifiers.Control;
        var key = VirtualKey.VK_F1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HotkeyDefinition(id, invalidName, modifiers, key));
    }

    [Fact]
    public void Constructor_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var modifiers = HotkeyModifiers.Control;
        var key = VirtualKey.VK_F1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HotkeyDefinition(id, null!, modifiers, key));
    }

    [Fact]
    public void Constructor_WithInvalidVirtualKey_ShouldThrowArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Hotkey";
        var modifiers = HotkeyModifiers.Control;
        var invalidKey = (VirtualKey)9999;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new HotkeyDefinition(id, name, modifiers, invalidKey));
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ShouldTrimName()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "  Test Hotkey  ";
        var modifiers = HotkeyModifiers.Control;
        var key = VirtualKey.VK_F1;

        // Act
        var hotkey = new HotkeyDefinition(id, name, modifiers, key);

        // Assert
        Assert.Equal("Test Hotkey", hotkey.Name);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateInstanceWithGeneratedId()
    {
        // Arrange
        var name = "Test Hotkey";
        var modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt;
        var key = VirtualKey.VK_F1;

        // Act
        var hotkey = HotkeyDefinition.Create(name, modifiers, key);

        // Assert
        Assert.NotEqual(Guid.Empty, hotkey.Id);
        Assert.Equal(name, hotkey.Name);
        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal(key, hotkey.Key);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control, VirtualKey.VK_A, "Ctrl + A")]
    [InlineData(HotkeyModifiers.Alt, VirtualKey.VK_F1, "Alt + F1")]
    [InlineData(HotkeyModifiers.Shift, VirtualKey.VK_ESCAPE, "Shift + Esc")]
    [InlineData(HotkeyModifiers.Windows, VirtualKey.VK_R, "Win + R")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_DELETE, "Ctrl + Alt + Delete")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, VirtualKey.VK_ESCAPE, "Ctrl + Shift + Esc")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, VirtualKey.VK_F12, "Ctrl + Alt + Shift + F12")]
    public void GetDisplayString_WithVariousModifiersAndKeys_ShouldReturnCorrectString(HotkeyModifiers modifiers, VirtualKey key, string expected)
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test", modifiers, key);

        // Act
        var displayString = hotkey.GetDisplayString();

        // Assert
        Assert.Equal(expected, displayString);
    }

    [Theory]
    [InlineData(VirtualKey.VK_ESCAPE, "Esc")]
    [InlineData(VirtualKey.VK_SPACE, "Space")]
    [InlineData(VirtualKey.VK_RETURN, "Enter")]
    [InlineData(VirtualKey.VK_BACK, "Backspace")]
    [InlineData(VirtualKey.VK_TAB, "Tab")]
    [InlineData(VirtualKey.VK_F1, "F1")]
    [InlineData(VirtualKey.VK_F12, "F12")]
    [InlineData(VirtualKey.VK_A, "A")]
    [InlineData(VirtualKey.VK_Z, "Z")]
    [InlineData(VirtualKey.VK_0, "0")]
    [InlineData(VirtualKey.VK_9, "9")]
    [InlineData(VirtualKey.VK_NUMPAD0, "Num 0")]
    [InlineData(VirtualKey.VK_NUMPAD9, "Num 9")]
    public void GetDisplayString_WithSpecialKeys_ShouldReturnCorrectKeyName(VirtualKey key, string expectedKeyName)
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test", HotkeyModifiers.Control, key);

        // Act
        var displayString = hotkey.GetDisplayString();

        // Assert
        Assert.EndsWith(expectedKeyName, displayString);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control, VirtualKey.VK_A, true)]
    [InlineData(HotkeyModifiers.Alt, VirtualKey.VK_F1, true)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_DELETE, true)]
    [InlineData(HotkeyModifiers.None, VirtualKey.VK_F1, true)] // Function keys allowed without modifiers
    [InlineData(HotkeyModifiers.None, VirtualKey.VK_ESCAPE, true)] // Special keys allowed without modifiers
    [InlineData(HotkeyModifiers.None, VirtualKey.VK_PAUSE, true)]
    [InlineData(HotkeyModifiers.None, VirtualKey.VK_SNAPSHOT, true)]
    public void IsValid_WithValidHotkeys_ShouldReturnTrue(HotkeyModifiers modifiers, VirtualKey key, bool expected)
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test", modifiers, key);

        // Act
        var isValid = hotkey.IsValid();

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control, VirtualKey.VK_CONTROL)] // Cannot use modifier as main key
    [InlineData(HotkeyModifiers.Alt, VirtualKey.VK_MENU)]
    [InlineData(HotkeyModifiers.Shift, VirtualKey.VK_SHIFT)]
    public void IsValid_WithInvalidHotkeys_ShouldReturnFalse(HotkeyModifiers modifiers, VirtualKey key)
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test", modifiers, key);

        // Act
        var isValid = hotkey.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ToString_ShouldReturnNameAndDisplayString()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Kill Switch", HotkeyModifiers.Control | HotkeyModifiers.Shift, VirtualKey.VK_ESCAPE);

        // Act
        var toString = hotkey.ToString();

        // Assert
        Assert.Equal("Kill Switch: Ctrl + Shift + Esc", toString);
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var hotkey1 = new HotkeyDefinition(id, "Test", HotkeyModifiers.Control, VirtualKey.VK_A);
        var hotkey2 = new HotkeyDefinition(id, "Test", HotkeyModifiers.Control, VirtualKey.VK_A);

        // Act & Assert
        Assert.Equal(hotkey1, hotkey2);
        Assert.True(hotkey1 == hotkey2);
        Assert.False(hotkey1 != hotkey2);
        Assert.Equal(hotkey1.GetHashCode(), hotkey2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var hotkey1 = HotkeyDefinition.Create("Test1", HotkeyModifiers.Control, VirtualKey.VK_A);
        var hotkey2 = HotkeyDefinition.Create("Test2", HotkeyModifiers.Alt, VirtualKey.VK_B);

        // Act & Assert
        Assert.NotEqual(hotkey1, hotkey2);
        Assert.False(hotkey1 == hotkey2);
        Assert.True(hotkey1 != hotkey2);
    }

    [Fact]
    public void Equality_WithDifferentTriggerModes_ShouldNotBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var hotkey1 = new HotkeyDefinition(id, "Test", HotkeyModifiers.Control, VirtualKey.VK_A, HotkeyTriggerMode.Once);
        var hotkey2 = new HotkeyDefinition(id, "Test", HotkeyModifiers.Control, VirtualKey.VK_A, HotkeyTriggerMode.RepeatWhileHeld);

        // Act & Assert
        Assert.NotEqual(hotkey1, hotkey2);
        Assert.False(hotkey1 == hotkey2);
        Assert.True(hotkey1 != hotkey2);
    }

    [Fact]
    public void TriggerMode_DefaultValue_ShouldBeOnce()
    {
        // Arrange & Act
        var hotkey = HotkeyDefinition.Create("Test", HotkeyModifiers.Control, VirtualKey.VK_A);

        // Assert
        Assert.Equal(HotkeyTriggerMode.Once, hotkey.TriggerMode);
    }

    [Theory]
    [InlineData(HotkeyTriggerMode.Once)]
    [InlineData(HotkeyTriggerMode.RepeatWhileHeld)]
    public void TriggerMode_WithExplicitValue_ShouldBeSet(HotkeyTriggerMode triggerMode)
    {
        // Arrange & Act
        var hotkey = HotkeyDefinition.Create("Test", HotkeyModifiers.Control, VirtualKey.VK_A, triggerMode);

        // Assert
        Assert.Equal(triggerMode, hotkey.TriggerMode);
    }
}