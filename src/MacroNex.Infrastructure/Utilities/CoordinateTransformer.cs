using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using static MacroNex.Infrastructure.Win32.Win32Api;

namespace MacroNex.Infrastructure.Utilities;

/// <summary>
/// Provides coordinate transformation utilities for input simulation.
/// Handles conversion between different coordinate systems and screen scaling.
/// </summary>
public class CoordinateTransformer
{
    private readonly ILogger<CoordinateTransformer> _logger;

    /// <summary>
    /// Initializes a new instance of the CoordinateTransformer class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public CoordinateTransformer(ILogger<CoordinateTransformer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current screen dimensions.
    /// </summary>
    /// <returns>A point representing the screen width and height.</returns>
    public Point GetScreenDimensions()
    {
        var width = GetSystemMetrics(SM_CXSCREEN);
        var height = GetSystemMetrics(SM_CYSCREEN);

        _logger.LogTrace("Screen dimensions: {Width}x{Height}", width, height);
        return new Point(width, height);
    }

    /// <summary>
    /// Validates that a point is within screen bounds.
    /// </summary>
    /// <param name="point">The point to validate.</param>
    /// <returns>True if the point is within screen bounds, false otherwise.</returns>
    public bool IsPointInScreenBounds(Point point)
    {
        if (point.X < 0 || point.Y < 0)
            return false;

        var screenDimensions = GetScreenDimensions();
        return point.X < screenDimensions.X && point.Y < screenDimensions.Y;
    }

    /// <summary>
    /// Clamps a point to screen bounds.
    /// </summary>
    /// <param name="point">The point to clamp.</param>
    /// <returns>A point clamped to screen bounds.</returns>
    public Point ClampToScreenBounds(Point point)
    {
        var screenDimensions = GetScreenDimensions();

        var clampedX = Math.Max(0, Math.Min(point.X, screenDimensions.X - 1));
        var clampedY = Math.Max(0, Math.Min(point.Y, screenDimensions.Y - 1));

        var clampedPoint = new Point(clampedX, clampedY);

        if (clampedPoint != point)
        {
            _logger.LogDebug("Clamped point {Original} to {Clamped} within screen bounds", point, clampedPoint);
        }

        return clampedPoint;
    }

    /// <summary>
    /// Converts absolute coordinates to relative coordinates (0.0 to 1.0).
    /// </summary>
    /// <param name="absolutePoint">The absolute point to convert.</param>
    /// <returns>A point with relative coordinates (0.0 to 1.0).</returns>
    public (double X, double Y) ToRelativeCoordinates(Point absolutePoint)
    {
        var screenDimensions = GetScreenDimensions();

        if (screenDimensions.X == 0 || screenDimensions.Y == 0)
        {
            _logger.LogWarning("Screen dimensions are zero, returning (0, 0) for relative coordinates");
            return (0.0, 0.0);
        }

        var relativeX = (double)absolutePoint.X / screenDimensions.X;
        var relativeY = (double)absolutePoint.Y / screenDimensions.Y;

        _logger.LogTrace("Converted absolute {Absolute} to relative ({RelativeX:F3}, {RelativeY:F3})",
            absolutePoint, relativeX, relativeY);

        return (relativeX, relativeY);
    }

    /// <summary>
    /// Converts relative coordinates (0.0 to 1.0) to absolute coordinates.
    /// </summary>
    /// <param name="relativeX">The relative X coordinate (0.0 to 1.0).</param>
    /// <param name="relativeY">The relative Y coordinate (0.0 to 1.0).</param>
    /// <returns>A point with absolute coordinates.</returns>
    public Point FromRelativeCoordinates(double relativeX, double relativeY)
    {
        if (relativeX < 0.0 || relativeX > 1.0)
            throw new ArgumentOutOfRangeException(nameof(relativeX), "Relative X coordinate must be between 0.0 and 1.0");

        if (relativeY < 0.0 || relativeY > 1.0)
            throw new ArgumentOutOfRangeException(nameof(relativeY), "Relative Y coordinate must be between 0.0 and 1.0");

        var screenDimensions = GetScreenDimensions();

        var absoluteX = (int)(relativeX * screenDimensions.X);
        var absoluteY = (int)(relativeY * screenDimensions.Y);

        var absolutePoint = new Point(absoluteX, absoluteY);

        _logger.LogTrace("Converted relative ({RelativeX:F3}, {RelativeY:F3}) to absolute {Absolute}",
            relativeX, relativeY, absolutePoint);

        return absolutePoint;
    }

    /// <summary>
    /// Calculates the distance between two points.
    /// </summary>
    /// <param name="point1">The first point.</param>
    /// <param name="point2">The second point.</param>
    /// <returns>The Euclidean distance between the points.</returns>
    public double CalculateDistance(Point point1, Point point2)
    {
        var deltaX = point2.X - point1.X;
        var deltaY = point2.Y - point1.Y;

        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        _logger.LogTrace("Distance between {Point1} and {Point2}: {Distance:F2}", point1, point2, distance);

        return distance;
    }

    /// <summary>
    /// Interpolates between two points.
    /// </summary>
    /// <param name="startPoint">The starting point.</param>
    /// <param name="endPoint">The ending point.</param>
    /// <param name="t">The interpolation factor (0.0 to 1.0).</param>
    /// <returns>The interpolated point.</returns>
    public Point Interpolate(Point startPoint, Point endPoint, double t)
    {
        if (t < 0.0 || t > 1.0)
            throw new ArgumentOutOfRangeException(nameof(t), "Interpolation factor must be between 0.0 and 1.0");

        var x = (int)(startPoint.X + t * (endPoint.X - startPoint.X));
        var y = (int)(startPoint.Y + t * (endPoint.Y - startPoint.Y));

        var interpolatedPoint = new Point(x, y);

        _logger.LogTrace("Interpolated between {Start} and {End} at t={T:F3}: {Result}",
            startPoint, endPoint, t, interpolatedPoint);

        return interpolatedPoint;
    }

    /// <summary>
    /// Generates a smooth path between two points.
    /// </summary>
    /// <param name="startPoint">The starting point.</param>
    /// <param name="endPoint">The ending point.</param>
    /// <param name="steps">The number of intermediate steps.</param>
    /// <returns>An enumerable of points representing the smooth path.</returns>
    public IEnumerable<Point> GenerateSmoothPath(Point startPoint, Point endPoint, int steps)
    {
        if (steps < 1)
            throw new ArgumentException("Steps must be at least 1", nameof(steps));

        _logger.LogDebug("Generating smooth path from {Start} to {End} with {Steps} steps",
            startPoint, endPoint, steps);

        var points = new List<Point>();

        for (int i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var point = Interpolate(startPoint, endPoint, t);
            points.Add(point);
        }

        _logger.LogTrace("Generated {Count} points for smooth path", points.Count);

        return points;
    }

    /// <summary>
    /// Adds random variation to a point within the specified radius.
    /// Useful for making automation appear more human-like.
    /// </summary>
    /// <param name="point">The base point.</param>
    /// <param name="maxVariation">The maximum variation radius in pixels.</param>
    /// <param name="random">Random number generator (optional).</param>
    /// <returns>A point with random variation applied.</returns>
    public Point AddRandomVariation(Point point, int maxVariation, Random? random = null)
    {
        if (maxVariation < 0)
            throw new ArgumentException("Max variation must be non-negative", nameof(maxVariation));

        if (maxVariation == 0)
            return point;

        random ??= new Random();

        // Generate random offset within a circle
        var angle = random.NextDouble() * 2 * Math.PI;
        var radius = random.NextDouble() * maxVariation;

        var offsetX = (int)(Math.Cos(angle) * radius);
        var offsetY = (int)(Math.Sin(angle) * radius);

        var variedPoint = new Point(point.X + offsetX, point.Y + offsetY);

        // Ensure the varied point is still within screen bounds
        variedPoint = ClampToScreenBounds(variedPoint);

        _logger.LogTrace("Added variation to {Original}: {Varied} (offset: {OffsetX}, {OffsetY})",
            point, variedPoint, offsetX, offsetY);

        return variedPoint;
    }
}