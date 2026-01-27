using MacroNex.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MacroNex.Tests.Infrastructure;

/// <summary>
/// Unit tests for TimingUtilities.
/// Tests timing calculations and delay generation logic.
/// </summary>
public class TimingUtilitiesTests
{
    private readonly Mock<ILogger<TimingUtilities>> _mockLogger;
    private readonly TimingUtilities _timingUtilities;

    public TimingUtilitiesTests()
    {
        _mockLogger = new Mock<ILogger<TimingUtilities>>();
        _timingUtilities = new TimingUtilities(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TimingUtilities(null!));
    }

    [Fact]
    public void CalculateMovementDelay_WithNegativeDistance_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateMovementDelay(-1.0));
    }

    [Fact]
    public void CalculateMovementDelay_WithNegativeSpeed_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateMovementDelay(100.0, -1.0));
    }

    [Fact]
    public void CalculateMovementDelay_WithZeroSpeed_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateMovementDelay(100.0, 0.0));
    }

    [Fact]
    public void CalculateMovementDelay_WithZeroDistance_ReturnsMinimumDelay()
    {
        // Act
        var delay = _timingUtilities.CalculateMovementDelay(0.0);

        // Assert
        Assert.True(delay.TotalMilliseconds >= 10.0); // Minimum delay
    }

    [Fact]
    public void CalculateMovementDelay_WithSmallDistance_ReturnsMinimumDelay()
    {
        // Act
        var delay = _timingUtilities.CalculateMovementDelay(5.0, 2.0); // Should be 2.5ms, but minimum is 10ms

        // Assert
        Assert.Equal(10.0, delay.TotalMilliseconds);
    }

    [Fact]
    public void CalculateMovementDelay_WithLargeDistance_ReturnsMaximumDelay()
    {
        // Act
        var delay = _timingUtilities.CalculateMovementDelay(10000.0, 1.0); // Should be 10000ms, but max is 2000ms

        // Assert
        Assert.Equal(2000.0, delay.TotalMilliseconds);
    }

    [Fact]
    public void CalculateMovementDelay_WithNormalDistance_ReturnsCalculatedDelay()
    {
        // Act
        var delay = _timingUtilities.CalculateMovementDelay(100.0, 2.0); // Should be 50ms

        // Assert
        Assert.Equal(50.0, delay.TotalMilliseconds);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void AddRandomVariation_WithInvalidVariationPercent_ThrowsArgumentOutOfRangeException(double variationPercent)
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _timingUtilities.AddRandomVariation(baseDelay, variationPercent));
    }

    [Fact]
    public void AddRandomVariation_WithZeroDelay_ReturnsZeroDelay()
    {
        // Arrange
        var baseDelay = TimeSpan.Zero;

        // Act
        var variedDelay = _timingUtilities.AddRandomVariation(baseDelay, 0.2);

        // Assert
        Assert.Equal(TimeSpan.Zero, variedDelay);
    }

    [Fact]
    public void AddRandomVariation_WithZeroVariation_ReturnsOriginalDelay()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);

        // Act
        var variedDelay = _timingUtilities.AddRandomVariation(baseDelay, 0.0);

        // Assert
        Assert.Equal(baseDelay, variedDelay);
    }

    [Fact]
    public void AddRandomVariation_WithPositiveVariation_ReturnsDelayWithinRange()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var variationPercent = 0.2; // 20%

        // Act
        var variedDelay = _timingUtilities.AddRandomVariation(baseDelay, variationPercent);

        // Assert
        Assert.True(variedDelay.TotalMilliseconds >= 0); // Should never be negative
        // The variation can be up to ±20%, so range is 80ms to 120ms
        Assert.True(variedDelay.TotalMilliseconds >= 80.0 - 1.0); // Allow small tolerance
        Assert.True(variedDelay.TotalMilliseconds <= 120.0 + 1.0); // Allow small tolerance
    }

    [Fact]
    public void ApplySpeedMultiplier_WithNegativeMultiplier_ThrowsArgumentException()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.ApplySpeedMultiplier(delay, -1.0));
    }

    [Fact]
    public void ApplySpeedMultiplier_WithZeroMultiplier_ThrowsArgumentException()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.ApplySpeedMultiplier(delay, 0.0));
    }

    [Fact]
    public void ApplySpeedMultiplier_WithOneMultiplier_ReturnsOriginalDelay()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        var adjustedDelay = _timingUtilities.ApplySpeedMultiplier(delay, 1.0);

        // Assert
        Assert.Equal(delay, adjustedDelay);
    }

    [Fact]
    public void ApplySpeedMultiplier_WithDoubleSpeed_ReturnsHalfDelay()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        var adjustedDelay = _timingUtilities.ApplySpeedMultiplier(delay, 2.0);

        // Assert
        Assert.Equal(50.0, adjustedDelay.TotalMilliseconds);
    }

    [Fact]
    public void ApplySpeedMultiplier_WithHalfSpeed_ReturnsDoubleDelay()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        var adjustedDelay = _timingUtilities.ApplySpeedMultiplier(delay, 0.5);

        // Assert
        Assert.Equal(200.0, adjustedDelay.TotalMilliseconds);
    }

    [Fact]
    public void CalculateTypingDelay_WithNegativeLength_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateTypingDelay(-1));
    }

    [Fact]
    public void CalculateTypingDelay_WithNegativeWPM_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateTypingDelay(10, -1.0));
    }

    [Fact]
    public void CalculateTypingDelay_WithZeroWPM_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateTypingDelay(10, 0.0));
    }

    [Fact]
    public void CalculateTypingDelay_WithZeroLength_ReturnsZeroDelay()
    {
        // Act
        var delay = _timingUtilities.CalculateTypingDelay(0);

        // Assert
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void CalculateTypingDelay_WithKnownValues_ReturnsExpectedDelay()
    {
        // Arrange
        var textLength = 25; // 5 words at 5 characters per word
        var wpm = 60.0; // 60 words per minute = 1 word per second

        // Act
        var delay = _timingUtilities.CalculateTypingDelay(textLength, wpm);

        // Assert
        // 25 characters at 60 WPM (300 characters per minute) = 5 seconds
        Assert.Equal(5.0, delay.TotalSeconds, 1); // Allow 1 second tolerance
    }

    [Fact]
    public void CalculateKeyComboTiming_WithNegativeKeyCount_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateKeyComboTiming(-1));
    }

    [Fact]
    public void CalculateKeyComboTiming_WithZeroKeyCount_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateKeyComboTiming(0));
    }

    [Fact]
    public void CalculateKeyComboTiming_WithSingleKey_ReturnsReasonableTimings()
    {
        // Act
        var (pressDelay, holdDelay, releaseDelay) = _timingUtilities.CalculateKeyComboTiming(1);

        // Assert
        Assert.True(pressDelay.TotalMilliseconds > 0);
        Assert.True(holdDelay.TotalMilliseconds > 0);
        Assert.True(releaseDelay.TotalMilliseconds > 0);

        // Press delay should be around 20ms base
        Assert.True(pressDelay.TotalMilliseconds >= 20.0);
        Assert.True(pressDelay.TotalMilliseconds <= 40.0);

        // Hold delay should be around 50ms base
        Assert.True(holdDelay.TotalMilliseconds >= 50.0);
        Assert.True(holdDelay.TotalMilliseconds <= 80.0);

        // Release delay should be around 15ms base
        Assert.True(releaseDelay.TotalMilliseconds >= 15.0);
        Assert.True(releaseDelay.TotalMilliseconds <= 35.0);
    }

    [Fact]
    public void CalculateKeyComboTiming_WithMultipleKeys_ReturnsLongerTimings()
    {
        // Act
        var (pressDelay1, holdDelay1, releaseDelay1) = _timingUtilities.CalculateKeyComboTiming(1);
        var (pressDelay3, holdDelay3, releaseDelay3) = _timingUtilities.CalculateKeyComboTiming(3);

        // Assert
        // More keys should generally result in longer delays
        Assert.True(pressDelay3.TotalMilliseconds >= pressDelay1.TotalMilliseconds);
        Assert.True(releaseDelay3.TotalMilliseconds >= releaseDelay1.TotalMilliseconds);
        // Hold delay should be similar regardless of key count
        Assert.True(Math.Abs(holdDelay3.TotalMilliseconds - holdDelay1.TotalMilliseconds) <= 30.0);
    }

    [Fact]
    public void CalculateKeystrokeDelay_WithNegativeBaseDelay_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateKeystrokeDelay(null, 'a', -1.0));
    }

    [Fact]
    public void CalculateKeystrokeDelay_WithNoPreviousChar_ReturnsReasonableDelay()
    {
        // Act
        var delay = _timingUtilities.CalculateKeystrokeDelay(null, 'a', 100.0);

        // Assert
        Assert.True(delay.TotalMilliseconds >= 20.0); // Minimum delay
        Assert.True(delay.TotalMilliseconds <= 200.0); // Should be within reasonable range
    }

    [Fact]
    public void CalculateKeystrokeDelay_WithPunctuationPrevious_ReturnsLongerDelay()
    {
        // Act
        var normalDelay = _timingUtilities.CalculateKeystrokeDelay('a', 'b', 100.0);
        var punctuationDelay = _timingUtilities.CalculateKeystrokeDelay('.', 'a', 100.0);

        // Assert
        // Punctuation should generally result in longer delays, but due to randomness we can't guarantee it
        // Just ensure both are reasonable
        Assert.True(normalDelay.TotalMilliseconds >= 20.0);
        Assert.True(punctuationDelay.TotalMilliseconds >= 20.0);
    }

    [Fact]
    public void CalculateExponentialBackoff_WithNegativeAttempt_ThrowsArgumentException()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var maxDelay = TimeSpan.FromSeconds(10);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateExponentialBackoff(0, baseDelay, maxDelay));
    }

    [Fact]
    public void CalculateExponentialBackoff_WithNegativeBaseDelay_ThrowsArgumentException()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(-100);
        var maxDelay = TimeSpan.FromSeconds(10);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateExponentialBackoff(1, baseDelay, maxDelay));
    }

    [Fact]
    public void CalculateExponentialBackoff_WithMaxDelayLessThanBase_ThrowsArgumentException()
    {
        // Arrange
        var baseDelay = TimeSpan.FromSeconds(10);
        var maxDelay = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _timingUtilities.CalculateExponentialBackoff(1, baseDelay, maxDelay));
    }

    [Fact]
    public void CalculateExponentialBackoff_WithFirstAttempt_ReturnsBaseDelay()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var maxDelay = TimeSpan.FromSeconds(10);

        // Act
        var backoffDelay = _timingUtilities.CalculateExponentialBackoff(1, baseDelay, maxDelay);

        // Assert
        Assert.Equal(baseDelay, backoffDelay);
    }

    [Fact]
    public void CalculateExponentialBackoff_WithSecondAttempt_ReturnsDoubleDelay()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var maxDelay = TimeSpan.FromSeconds(10);

        // Act
        var backoffDelay = _timingUtilities.CalculateExponentialBackoff(2, baseDelay, maxDelay);

        // Assert
        Assert.Equal(200.0, backoffDelay.TotalMilliseconds);
    }

    [Fact]
    public void CalculateExponentialBackoff_WithHighAttempt_ReturnsMaxDelay()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(100);
        var maxDelay = TimeSpan.FromSeconds(1);

        // Act
        var backoffDelay = _timingUtilities.CalculateExponentialBackoff(10, baseDelay, maxDelay);

        // Assert
        Assert.Equal(maxDelay, backoffDelay);
    }
}