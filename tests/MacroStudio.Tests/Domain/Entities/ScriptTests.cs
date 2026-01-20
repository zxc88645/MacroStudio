using MacroStudio.Domain.Entities;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Tests.Domain.Entities;

public class ScriptTests
{
    [Fact]
    public void Constructor_WithValidName_CreatesScriptWithCorrectProperties()
    {
        // Arrange
        var name = "Test Script";

        // Act
        var script = new Script(name);

        // Assert
        Assert.Equal(name, script.Name);
        Assert.NotEqual(Guid.Empty, script.Id);
        Assert.Empty(script.Commands);
        Assert.Equal(0, script.CommandCount);
        Assert.True(script.CreatedAt <= DateTime.UtcNow);
        Assert.True(script.ModifiedAt <= DateTime.UtcNow);
        Assert.Equal(TimeSpan.Zero, script.EstimatedDuration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Script(invalidName));
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Script(null!));
    }

    [Fact]
    public void Name_SetWithValidValue_UpdatesNameAndModifiedAt()
    {
        // Arrange
        var script = new Script("Original Name");
        var originalModifiedAt = script.ModifiedAt;
        Thread.Sleep(1); // Ensure time difference
        var newName = "New Name";

        // Act
        script.Name = newName;

        // Assert
        Assert.Equal(newName, script.Name);
        Assert.True(script.ModifiedAt > originalModifiedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_SetWithInvalidValue_ThrowsArgumentException(string invalidName)
    {
        // Arrange
        var script = new Script("Valid Name");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => script.Name = invalidName);
    }

    [Fact]
    public void Name_SetWithNull_ThrowsArgumentException()
    {
        // Arrange
        var script = new Script("Valid Name");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => script.Name = null!);
    }

    [Fact]
    public void AddCommand_WithValidCommand_AddsCommandAndUpdatesModifiedAt()
    {
        // Arrange
        var script = new Script("Test Script");
        var command = new MouseMoveCommand(new Point(100, 200));
        var originalModifiedAt = script.ModifiedAt;
        Thread.Sleep(1);

        // Act
        script.AddCommand(command);

        // Assert
        Assert.Single(script.Commands);
        Assert.Equal(command, script.Commands[0]);
        Assert.Equal(1, script.CommandCount);
        Assert.True(script.ModifiedAt > originalModifiedAt);
    }

    [Fact]
    public void AddCommand_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var script = new Script("Test Script");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => script.AddCommand(null!));
    }

    [Fact]
    public void InsertCommand_AtValidIndex_InsertsCommandCorrectly()
    {
        // Arrange
        var script = new Script("Test Script");
        var command1 = new MouseMoveCommand(new Point(100, 200));
        var command2 = new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click);
        var command3 = new SleepCommand(TimeSpan.FromMilliseconds(500));
        
        script.AddCommand(command1);
        script.AddCommand(command3);

        // Act
        script.InsertCommand(1, command2);

        // Assert
        Assert.Equal(3, script.CommandCount);
        Assert.Equal(command1, script.Commands[0]);
        Assert.Equal(command2, script.Commands[1]);
        Assert.Equal(command3, script.Commands[2]);
    }

    [Fact]
    public void RemoveCommandAt_WithValidIndex_RemovesCommandAndReturnsTrue()
    {
        // Arrange
        var script = new Script("Test Script");
        var command1 = new MouseMoveCommand(new Point(100, 200));
        var command2 = new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click);
        script.AddCommand(command1);
        script.AddCommand(command2);

        // Act
        var result = script.RemoveCommandAt(0);

        // Assert
        Assert.True(result);
        Assert.Single(script.Commands);
        Assert.Equal(command2, script.Commands[0]);
    }

    [Fact]
    public void RemoveCommandAt_WithInvalidIndex_ReturnsFalse()
    {
        // Arrange
        var script = new Script("Test Script");

        // Act
        var result = script.RemoveCommandAt(0);

        // Assert
        Assert.False(result);
        Assert.Empty(script.Commands);
    }

    [Fact]
    public void MoveCommand_WithValidIndices_MovesCommandCorrectly()
    {
        // Arrange
        var script = new Script("Test Script");
        var command1 = new MouseMoveCommand(new Point(100, 200));
        var command2 = new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click);
        var command3 = new SleepCommand(TimeSpan.FromMilliseconds(500));
        
        script.AddCommand(command1);
        script.AddCommand(command2);
        script.AddCommand(command3);

        // Act
        var result = script.MoveCommand(0, 2);

        // Assert
        Assert.True(result);
        Assert.Equal(command2, script.Commands[0]);
        Assert.Equal(command3, script.Commands[1]);
        Assert.Equal(command1, script.Commands[2]);
    }

    [Fact]
    public void EstimatedDuration_WithCommandsAndDelays_CalculatesCorrectly()
    {
        // Arrange
        var script = new Script("Test Script");
        var command1 = new MouseMoveCommand(new Point(100, 200)) { Delay = TimeSpan.FromMilliseconds(100) };
        var command2 = new SleepCommand(TimeSpan.FromMilliseconds(500)) { Delay = TimeSpan.FromMilliseconds(50) };
        var command3 = new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click) { Delay = TimeSpan.FromMilliseconds(25) };
        
        script.AddCommand(command1);
        script.AddCommand(command2);
        script.AddCommand(command3);

        // Act
        var duration = script.EstimatedDuration;

        // Assert
        // Total: 100ms (delay1) + 50ms (delay2) + 500ms (sleep duration) + 25ms (delay3) = 675ms
        Assert.Equal(TimeSpan.FromMilliseconds(675), duration);
    }

    [Fact]
    public void Duplicate_WithValidName_CreatesNewScriptWithSameCommands()
    {
        // Arrange
        var originalScript = new Script("Original Script");
        var command1 = new MouseMoveCommand(new Point(100, 200));
        var command2 = new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click);
        originalScript.AddCommand(command1);
        originalScript.AddCommand(command2);

        // Act
        var duplicatedScript = originalScript.Duplicate("Duplicated Script");

        // Assert
        Assert.NotEqual(originalScript.Id, duplicatedScript.Id);
        Assert.Equal("Duplicated Script", duplicatedScript.Name);
        Assert.Equal(originalScript.CommandCount, duplicatedScript.CommandCount);
        
        // Commands should be clones, not the same instances
        Assert.NotEqual(originalScript.Commands[0].Id, duplicatedScript.Commands[0].Id);
        Assert.NotEqual(originalScript.Commands[1].Id, duplicatedScript.Commands[1].Id);
        
        // But they should have the same type and parameters
        Assert.IsType<MouseMoveCommand>(duplicatedScript.Commands[0]);
        Assert.IsType<MouseClickCommand>(duplicatedScript.Commands[1]);
    }

    [Fact]
    public void IsValid_WithAllValidCommands_ReturnsTrue()
    {
        // Arrange
        var script = new Script("Test Script");
        script.AddCommand(new MouseMoveCommand(new Point(100, 200)));
        script.AddCommand(new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click));
        script.AddCommand(new KeyboardCommand("Hello World"));
        script.AddCommand(new SleepCommand(TimeSpan.FromMilliseconds(500)));

        // Act
        var isValid = script.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WithInvalidCommand_ReturnsFalse()
    {
        // Arrange
        var script = new Script("Test Script");
        script.AddCommand(new MouseMoveCommand(new Point(100, 200)));
        script.AddCommand(new MouseMoveCommand(new Point(-10, 200))); // Invalid negative coordinate

        // Act
        var isValid = script.IsValid();

        // Assert
        Assert.False(isValid);
    }
}