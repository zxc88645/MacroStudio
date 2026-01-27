using MacroNex.Domain.Entities;
using MacroNex.Domain.ValueObjects;

namespace MacroNex.Tests.Domain.Entities;

public class CommandTests
{
    [Fact]
    public void MouseMoveCommand_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var position = new Point(100, 200);

        // Act
        var command = new MouseMoveCommand(position);

        // Assert
        Assert.Equal(position, command.Position);
        Assert.NotEqual(Guid.Empty, command.Id);
        Assert.Equal(TimeSpan.Zero, command.Delay);
        Assert.True(command.CreatedAt <= DateTime.UtcNow);
        Assert.Equal("Mouse Move", command.DisplayName);
        Assert.Equal("Move to (100, 200)", command.Description);
    }

    [Fact]
    public void MouseMoveCommand_IsValid_WithValidPosition_ReturnsTrue()
    {
        // Arrange
        var command = new MouseMoveCommand(new Point(100, 200));

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void MouseMoveCommand_IsValid_WithNegativeCoordinates_ReturnsFalse()
    {
        // Arrange
        var command = new MouseMoveCommand(new Point(-10, 200));

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void MouseClickCommand_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var button = MouseButton.Right;
        var clickType = ClickType.Click;

        // Act
        var command = new MouseClickCommand(button, clickType);

        // Assert
        Assert.Equal(button, command.Button);
        Assert.Equal(clickType, command.Type);
        Assert.Equal("Mouse Click", command.DisplayName);
        Assert.Equal("Click Right", command.Description);
    }

    [Fact]
    public void MouseClickCommand_IsValid_WithValidParameters_ReturnsTrue()
    {
        // Arrange
        var command = new MouseClickCommand(MouseButton.Left, ClickType.Click);

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void MouseClickCommand_IsValid_WithInvalidButton_ReturnsFalse()
    {
        // Arrange
        var command = new MouseClickCommand((MouseButton)999, ClickType.Click);

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void KeyboardCommand_Constructor_WithText_SetsPropertiesCorrectly()
    {
        // Arrange
        var text = "Hello World";

        // Act
        var command = new KeyboardCommand(text);

        // Assert
        Assert.Equal(text, command.Text);
        Assert.Empty(command.Keys);
        Assert.Equal("Keyboard Input", command.DisplayName);
        Assert.Equal("Type: \"Hello World\"", command.Description);
    }

    [Fact]
    public void KeyboardCommand_Constructor_WithKeys_SetsPropertiesCorrectly()
    {
        // Arrange
        var keys = new[] { VirtualKey.VK_CONTROL, VirtualKey.VK_C };

        // Act
        var command = new KeyboardCommand(keys);

        // Assert
        Assert.Null(command.Text);
        Assert.Equal(keys, command.Keys);
        Assert.Equal("Keyboard Input", command.DisplayName);
        Assert.Contains("CONTROL + C", command.Description);
    }

    [Fact]
    public void KeyboardCommand_IsValid_WithText_ReturnsTrue()
    {
        // Arrange
        var command = new KeyboardCommand("Hello");

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void KeyboardCommand_IsValid_WithKeys_ReturnsTrue()
    {
        // Arrange
        var command = new KeyboardCommand(VirtualKey.VK_RETURN);

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void KeyboardCommand_IsValid_WithEmptyTextAndNoKeys_ReturnsFalse()
    {
        // Arrange
        var command = new KeyboardCommand("");

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void SleepCommand_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(500);

        // Act
        var command = new SleepCommand(duration);

        // Assert
        Assert.Equal(duration, command.Duration);
        Assert.Equal("Sleep", command.DisplayName);
        Assert.Equal("Wait 500ms", command.Description);
    }

    [Fact]
    public void SleepCommand_IsValid_WithPositiveDuration_ReturnsTrue()
    {
        // Arrange
        var command = new SleepCommand(TimeSpan.FromMilliseconds(100));

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void SleepCommand_IsValid_WithZeroDuration_ReturnsTrue()
    {
        // Arrange
        var command = new SleepCommand(TimeSpan.Zero);

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void SleepCommand_IsValid_WithNegativeDuration_ReturnsFalse()
    {
        // Arrange
        var command = new SleepCommand(TimeSpan.FromMilliseconds(-100));

        // Act
        var isValid = command.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void KeyPressCommand_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var key = VirtualKey.VK_A;

        // Act
        var down = new KeyPressCommand(key, isDown: true);
        var up = new KeyPressCommand(key, isDown: false);

        // Assert
        Assert.Equal(key, down.Key);
        Assert.True(down.IsDown);
        Assert.Equal("Key Press", down.DisplayName);
        Assert.Contains("Down", down.Description);

        Assert.Equal(key, up.Key);
        Assert.False(up.IsDown);
        Assert.Contains("Up", up.Description);
    }

    [Fact]
    public void KeyPressCommand_IsValid_WithValidKey_ReturnsTrue()
    {
        var command = new KeyPressCommand(VirtualKey.VK_RETURN, isDown: true);
        Assert.True(command.IsValid());
    }

    [Fact]
    public void KeyPressCommand_IsValid_WithInvalidKey_ReturnsFalse()
    {
        var command = new KeyPressCommand((VirtualKey)99999, isDown: true);
        Assert.False(command.IsValid());
    }

    [Fact]
    public void Command_Clone_CreatesNewInstanceWithDifferentId()
    {
        // Arrange
        var originalCommand = new MouseMoveCommand(new Point(100, 200));

        // Act
        var clonedCommand = originalCommand.Clone();

        // Assert
        Assert.NotEqual(originalCommand.Id, clonedCommand.Id);
        Assert.IsType<MouseMoveCommand>(clonedCommand);

        var clonedMouseMove = (MouseMoveCommand)clonedCommand;
        Assert.Equal(originalCommand.Position, clonedMouseMove.Position);
        Assert.Equal(originalCommand.Delay, clonedMouseMove.Delay);
    }

    [Fact]
    public void Command_Equals_WithSameId_ReturnsTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command1 = new MouseMoveCommand(id, TimeSpan.Zero, DateTime.UtcNow, new Point(100, 200));
        var command2 = new MouseMoveCommand(id, TimeSpan.Zero, DateTime.UtcNow, new Point(150, 250));

        // Act & Assert
        Assert.Equal(command1, command2);
        Assert.Equal(command1.GetHashCode(), command2.GetHashCode());
    }

    [Fact]
    public void Command_Equals_WithDifferentId_ReturnsFalse()
    {
        // Arrange
        var command1 = new MouseMoveCommand(new Point(100, 200));
        var command2 = new MouseMoveCommand(new Point(100, 200));

        // Act & Assert
        Assert.NotEqual(command1, command2);
    }
}