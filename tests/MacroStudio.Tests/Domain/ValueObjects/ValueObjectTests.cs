using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Tests.Domain.ValueObjects;

public class ValueObjectTests
{
    [Fact]
    public void Point_Constructor_SetsCoordinatesCorrectly()
    {
        // Arrange & Act
        var point = new Point(100, 200);

        // Assert
        Assert.Equal(100, point.X);
        Assert.Equal(200, point.Y);
    }

    [Fact]
    public void Point_Zero_ReturnsOriginPoint()
    {
        // Act
        var zero = Point.Zero;

        // Assert
        Assert.Equal(0, zero.X);
        Assert.Equal(0, zero.Y);
    }

    [Fact]
    public void Point_ToString_ReturnsFormattedString()
    {
        // Arrange
        var point = new Point(150, 250);

        // Act
        var result = point.ToString();

        // Assert
        Assert.Equal("(150, 250)", result);
    }

    [Fact]
    public void Point_Equality_WithSameCoordinates_ReturnsTrue()
    {
        // Arrange
        var point1 = new Point(100, 200);
        var point2 = new Point(100, 200);

        // Act & Assert
        Assert.Equal(point1, point2);
        Assert.True(point1 == point2);
        Assert.False(point1 != point2);
        Assert.Equal(point1.GetHashCode(), point2.GetHashCode());
    }

    [Fact]
    public void Point_Equality_WithDifferentCoordinates_ReturnsFalse()
    {
        // Arrange
        var point1 = new Point(100, 200);
        var point2 = new Point(150, 250);

        // Act & Assert
        Assert.NotEqual(point1, point2);
        Assert.False(point1 == point2);
        Assert.True(point1 != point2);
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    [InlineData(MouseButton.XButton1)]
    [InlineData(MouseButton.XButton2)]
    public void MouseButton_AllValues_AreValid(MouseButton button)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(MouseButton), button));
    }

    [Theory]
    [InlineData(ClickType.Down)]
    [InlineData(ClickType.Up)]
    [InlineData(ClickType.Click)]
    public void ClickType_AllValues_AreValid(ClickType clickType)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(ClickType), clickType));
    }

    [Theory]
    [InlineData(ExecutionState.Idle)]
    [InlineData(ExecutionState.Running)]
    [InlineData(ExecutionState.Paused)]
    [InlineData(ExecutionState.Stopped)]
    [InlineData(ExecutionState.Stepping)]
    [InlineData(ExecutionState.Completed)]
    [InlineData(ExecutionState.Failed)]
    [InlineData(ExecutionState.Terminated)]
    public void ExecutionState_AllValues_AreValid(ExecutionState state)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(ExecutionState), state));
    }

    [Theory]
    [InlineData(VirtualKey.VK_A, 0x41)]
    [InlineData(VirtualKey.VK_CONTROL, 0x11)]
    [InlineData(VirtualKey.VK_RETURN, 0x0D)]
    [InlineData(VirtualKey.VK_SPACE, 0x20)]
    [InlineData(VirtualKey.VK_F1, 0x70)]
    public void VirtualKey_CommonKeys_HaveCorrectValues(VirtualKey key, int expectedValue)
    {
        // Act & Assert
        Assert.Equal(expectedValue, (int)key);
    }

    [Fact]
    public void VirtualKey_AllLetterKeys_AreSequential()
    {
        // Act & Assert
        for (int i = 0; i < 26; i++)
        {
            var expectedValue = 0x41 + i; // A = 0x41, B = 0x42, etc.
            var actualKey = (VirtualKey)expectedValue;
            Assert.True(Enum.IsDefined(typeof(VirtualKey), actualKey));
        }
    }

    [Fact]
    public void VirtualKey_AllNumberKeys_AreSequential()
    {
        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var expectedValue = 0x30 + i; // 0 = 0x30, 1 = 0x31, etc.
            var actualKey = (VirtualKey)expectedValue;
            Assert.True(Enum.IsDefined(typeof(VirtualKey), actualKey));
        }
    }

    [Fact]
    public void VirtualKey_AllFunctionKeys_AreSequential()
    {
        // Act & Assert
        for (int i = 0; i < 12; i++)
        {
            var expectedValue = 0x70 + i; // F1 = 0x70, F2 = 0x71, etc.
            var actualKey = (VirtualKey)expectedValue;
            Assert.True(Enum.IsDefined(typeof(VirtualKey), actualKey));
        }
    }
}