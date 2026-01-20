namespace MacroStudio.Domain.ValueObjects;

/// <summary>
/// Represents a 2D coordinate point with X and Y values.
/// </summary>
public readonly record struct Point(int X, int Y)
{
    /// <summary>
    /// Creates a Point with both X and Y set to zero.
    /// </summary>
    public static Point Zero => new(0, 0);

    /// <summary>
    /// Returns a string representation of the point in format "(X, Y)".
    /// </summary>
    public override string ToString() => $"({X}, {Y})";
}