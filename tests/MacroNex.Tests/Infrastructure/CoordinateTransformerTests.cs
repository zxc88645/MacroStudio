using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MacroNex.Tests.Infrastructure;

/// <summary>
/// Unit tests for CoordinateTransformer.
/// Tests coordinate transformation and validation logic.
/// </summary>
public class CoordinateTransformerTests
{
    private readonly Mock<ILogger<CoordinateTransformer>> _mockLogger;
    private readonly CoordinateTransformer _transformer;

    public CoordinateTransformerTests()
    {
        _mockLogger = new Mock<ILogger<CoordinateTransformer>>();
        _transformer = new CoordinateTransformer(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CoordinateTransformer(null!));
    }

    [Fact]
    public void GetScreenDimensions_ReturnsPositiveValues()
    {
        // Act
        var dimensions = _transformer.GetScreenDimensions();

        // Assert
        Assert.True(dimensions.X > 0);
        Assert.True(dimensions.Y > 0);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(100, 100, true)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(-1, -1, false)]
    public void IsPointInScreenBounds_WithVariousPoints_ReturnsExpectedResult(int x, int y, bool expectedInBounds)
    {
        // Arrange
        var point = new Point(x, y);

        // Act
        var result = _transformer.IsPointInScreenBounds(point);

        // Assert
        if (expectedInBounds && x >= 0 && y >= 0)
        {
            // For positive coordinates, we need to check against actual screen size
            var screenDimensions = _transformer.GetScreenDimensions();
            var actuallyInBounds = x < screenDimensions.X && y < screenDimensions.Y;
            Assert.Equal(actuallyInBounds, result);
        }
        else
        {
            Assert.Equal(expectedInBounds, result);
        }
    }

    [Fact]
    public void ClampToScreenBounds_WithNegativeCoordinates_ClampsToZero()
    {
        // Arrange
        var point = new Point(-10, -20);

        // Act
        var clampedPoint = _transformer.ClampToScreenBounds(point);

        // Assert
        Assert.Equal(0, clampedPoint.X);
        Assert.Equal(0, clampedPoint.Y);
    }

    [Fact]
    public void ClampToScreenBounds_WithValidCoordinates_ReturnsUnchanged()
    {
        // Arrange
        var point = new Point(100, 100);

        // Act
        var clampedPoint = _transformer.ClampToScreenBounds(point);

        // Assert
        Assert.Equal(point, clampedPoint);
    }

    [Fact]
    public void ClampToScreenBounds_WithOversizedCoordinates_ClampsToScreenBounds()
    {
        // Arrange
        var screenDimensions = _transformer.GetScreenDimensions();
        var point = new Point(screenDimensions.X + 100, screenDimensions.Y + 100);

        // Act
        var clampedPoint = _transformer.ClampToScreenBounds(point);

        // Assert
        Assert.Equal(screenDimensions.X - 1, clampedPoint.X);
        Assert.Equal(screenDimensions.Y - 1, clampedPoint.Y);
    }

    [Fact]
    public void ToRelativeCoordinates_WithZeroPoint_ReturnsZero()
    {
        // Arrange
        var point = new Point(0, 0);

        // Act
        var (relativeX, relativeY) = _transformer.ToRelativeCoordinates(point);

        // Assert
        Assert.Equal(0.0, relativeX);
        Assert.Equal(0.0, relativeY);
    }

    [Fact]
    public void ToRelativeCoordinates_WithScreenDimensions_ReturnsOne()
    {
        // Arrange
        var screenDimensions = _transformer.GetScreenDimensions();

        // Act
        var (relativeX, relativeY) = _transformer.ToRelativeCoordinates(screenDimensions);

        // Assert
        Assert.Equal(1.0, relativeX);
        Assert.Equal(1.0, relativeY);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    public void FromRelativeCoordinates_WithValidValues_ReturnsExpectedPoint(double relativeX, double relativeY)
    {
        // Act
        var point = _transformer.FromRelativeCoordinates(relativeX, relativeY);

        // Assert
        Assert.True(point.X >= 0);
        Assert.True(point.Y >= 0);

        var screenDimensions = _transformer.GetScreenDimensions();
        Assert.True(point.X <= screenDimensions.X);
        Assert.True(point.Y <= screenDimensions.Y);
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(1.1, 0.5)]
    [InlineData(0.5, -0.1)]
    [InlineData(0.5, 1.1)]
    public void FromRelativeCoordinates_WithInvalidValues_ThrowsArgumentOutOfRangeException(double relativeX, double relativeY)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _transformer.FromRelativeCoordinates(relativeX, relativeY));
    }

    [Fact]
    public void CalculateDistance_WithSamePoints_ReturnsZero()
    {
        // Arrange
        var point = new Point(100, 100);

        // Act
        var distance = _transformer.CalculateDistance(point, point);

        // Assert
        Assert.Equal(0.0, distance);
    }

    [Fact]
    public void CalculateDistance_WithKnownPoints_ReturnsCorrectDistance()
    {
        // Arrange
        var point1 = new Point(0, 0);
        var point2 = new Point(3, 4);

        // Act
        var distance = _transformer.CalculateDistance(point1, point2);

        // Assert
        Assert.Equal(5.0, distance, 1); // 3-4-5 triangle
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Interpolate_WithValidT_ReturnsExpectedPoint(double t)
    {
        // Arrange
        var startPoint = new Point(0, 0);
        var endPoint = new Point(100, 200);

        // Act
        var interpolatedPoint = _transformer.Interpolate(startPoint, endPoint, t);

        // Assert
        Assert.Equal((int)(t * 100), interpolatedPoint.X);
        Assert.Equal((int)(t * 200), interpolatedPoint.Y);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Interpolate_WithInvalidT_ThrowsArgumentOutOfRangeException(double t)
    {
        // Arrange
        var startPoint = new Point(0, 0);
        var endPoint = new Point(100, 100);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _transformer.Interpolate(startPoint, endPoint, t));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateSmoothPath_WithInvalidSteps_ThrowsArgumentException(int steps)
    {
        // Arrange
        var startPoint = new Point(0, 0);
        var endPoint = new Point(100, 100);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _transformer.GenerateSmoothPath(startPoint, endPoint, steps));
    }

    [Fact]
    public void GenerateSmoothPath_WithValidSteps_ReturnsCorrectNumberOfPoints()
    {
        // Arrange
        var startPoint = new Point(0, 0);
        var endPoint = new Point(100, 100);
        var steps = 5;

        // Act
        var path = _transformer.GenerateSmoothPath(startPoint, endPoint, steps).ToList();

        // Assert
        Assert.Equal(steps + 1, path.Count); // steps + 1 because we include both start and end
        Assert.Equal(startPoint, path.First());
        Assert.Equal(endPoint, path.Last());
    }

    [Fact]
    public void AddRandomVariation_WithZeroVariation_ReturnsOriginalPoint()
    {
        // Arrange
        var point = new Point(100, 100);
        var maxVariation = 0;

        // Act
        var variedPoint = _transformer.AddRandomVariation(point, maxVariation);

        // Assert
        Assert.Equal(point, variedPoint);
    }

    [Fact]
    public void AddRandomVariation_WithNegativeVariation_ThrowsArgumentException()
    {
        // Arrange
        var point = new Point(100, 100);
        var maxVariation = -1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _transformer.AddRandomVariation(point, maxVariation));
    }

    [Fact]
    public void AddRandomVariation_WithPositiveVariation_ReturnsPointWithinBounds()
    {
        // Arrange
        var point = new Point(500, 500); // Use a point well within screen bounds
        var maxVariation = 10;

        // Act
        var variedPoint = _transformer.AddRandomVariation(point, maxVariation);

        // Assert
        var distance = _transformer.CalculateDistance(point, variedPoint);
        Assert.True(distance <= maxVariation);
        Assert.True(_transformer.IsPointInScreenBounds(variedPoint));
    }

    [Fact]
    public void AddRandomVariation_WithSameRandomSeed_ProducesSameResult()
    {
        // Arrange
        var point = new Point(100, 100);
        var maxVariation = 10;
        var random1 = new Random(42);
        var random2 = new Random(42);

        // Act
        var variedPoint1 = _transformer.AddRandomVariation(point, maxVariation, random1);
        var variedPoint2 = _transformer.AddRandomVariation(point, maxVariation, random2);

        // Assert
        Assert.Equal(variedPoint1, variedPoint2);
    }
}