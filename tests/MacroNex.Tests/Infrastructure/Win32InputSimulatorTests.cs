using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MacroNex.Tests.Infrastructure;

/// <summary>
/// Unit tests for Win32InputSimulator.
/// Tests basic functionality and validation without actually sending input to the system.
/// </summary>
public class Win32InputSimulatorTests : IDisposable
{
    private readonly Mock<ILogger<Win32InputSimulator>> _mockLogger;
    private readonly Win32InputSimulator _inputSimulator;

    public Win32InputSimulatorTests()
    {
        _mockLogger = new Mock<ILogger<Win32InputSimulator>>();
        _inputSimulator = new Win32InputSimulator(_mockLogger.Object);
    }

    public void Dispose()
    {
        _inputSimulator?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Win32InputSimulator(null!));
    }

    [Fact]
    public async Task IsReadyAsync_WhenNotDisposed_ReturnsTrue()
    {
        // Act
        var result = await _inputSimulator.IsReadyAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsReadyAsync_WhenDisposed_ReturnsFalse()
    {
        // Arrange
        _inputSimulator.Dispose();

        // Act
        var result = await _inputSimulator.IsReadyAsync();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, -1)]
    public async Task SimulateMouseMoveAsync_WithNegativeCoordinates_ThrowsArgumentException(int x, int y)
    {
        // Arrange
        var position = new Point(x, y);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _inputSimulator.SimulateMouseMoveAsync(position));
    }

    [Fact]
    public async Task SimulateMouseClickAsync_WithInvalidMouseButton_ThrowsArgumentException()
    {
        // Arrange
        var invalidButton = (MouseButton)999;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _inputSimulator.SimulateMouseClickAsync(invalidButton, ClickType.Click));
    }

    [Fact]
    public async Task SimulateMouseClickAsync_WithInvalidClickType_ThrowsArgumentException()
    {
        // Arrange
        var invalidClickType = (ClickType)999;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _inputSimulator.SimulateMouseClickAsync(MouseButton.Left, invalidClickType));
    }

    [Fact]
    public async Task SimulateKeyboardInputAsync_WithNullText_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _inputSimulator.SimulateKeyboardInputAsync(null!));
    }

    [Fact]
    public async Task SimulateKeyboardInputAsync_WithEmptyText_DoesNotThrow()
    {
        // Act & Assert
        await _inputSimulator.SimulateKeyboardInputAsync(string.Empty);
        // Should complete without throwing
    }

    [Fact]
    public async Task SimulateKeyPressAsync_WithInvalidVirtualKey_ThrowsArgumentException()
    {
        // Arrange
        var invalidKey = (VirtualKey)999999;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _inputSimulator.SimulateKeyPressAsync(invalidKey, true));
    }

    [Fact]
    public async Task SimulateKeyComboAsync_WithNullKeys_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _inputSimulator.SimulateKeyComboAsync(null!));
    }

    [Fact]
    public async Task SimulateKeyComboAsync_WithEmptyKeys_ThrowsArgumentException()
    {
        // Arrange
        var emptyKeys = new List<VirtualKey>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _inputSimulator.SimulateKeyComboAsync(emptyKeys));
    }

    [Fact]
    public async Task SimulateKeyComboAsync_WithInvalidKey_ThrowsArgumentException()
    {
        // Arrange
        var keysWithInvalid = new List<VirtualKey> { VirtualKey.VK_A, (VirtualKey)999999 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _inputSimulator.SimulateKeyComboAsync(keysWithInvalid));
    }

    [Fact]
    public async Task DelayAsync_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var negativeDuration = TimeSpan.FromMilliseconds(-100);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _inputSimulator.DelayAsync(negativeDuration));
    }

    [Fact]
    public async Task DelayAsync_WithZeroDuration_DoesNotThrow()
    {
        // Act & Assert
        await _inputSimulator.DelayAsync(TimeSpan.Zero);
        // Should complete immediately without throwing
    }

    [Fact]
    public async Task DelayAsync_WithPositiveDuration_CompletesAfterDelay()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(50);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _inputSimulator.DelayAsync(delay);

        // Assert
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds >= 40); // Allow some tolerance
    }

    [Fact]
    public async Task GetCursorPositionAsync_WhenNotDisposed_ReturnsValidPoint()
    {
        // Act
        var position = await _inputSimulator.GetCursorPositionAsync();

        // Assert
        Assert.True(position.X >= 0);
        Assert.True(position.Y >= 0);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
    {
        // Act & Assert
        _inputSimulator.Dispose();
        _inputSimulator.Dispose(); // Should not throw
    }

    [Fact]
    public async Task SimulateMouseMoveAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();
        var position = new Point(100, 100);

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _inputSimulator.SimulateMouseMoveAsync(position));
    }

    [Fact]
    public async Task SimulateMouseClickAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _inputSimulator.SimulateMouseClickAsync(MouseButton.Left, ClickType.Click));
    }

    [Fact]
    public async Task SimulateKeyboardInputAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _inputSimulator.SimulateKeyboardInputAsync("test"));
    }

    [Fact]
    public async Task SimulateKeyPressAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _inputSimulator.SimulateKeyPressAsync(VirtualKey.VK_A, true));
    }

    [Fact]
    public async Task SimulateKeyComboAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();
        var keys = new List<VirtualKey> { VirtualKey.VK_CONTROL, VirtualKey.VK_C };

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _inputSimulator.SimulateKeyComboAsync(keys));
    }

    [Fact]
    public async Task DelayAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _inputSimulator.DelayAsync(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task GetCursorPositionAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _inputSimulator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _inputSimulator.GetCursorPositionAsync());
    }
}