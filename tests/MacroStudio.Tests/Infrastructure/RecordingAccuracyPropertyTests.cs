using FsCheck;
using FsCheck.Xunit;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroStudio.Tests.Infrastructure;

public class RecordingAccuracyPropertyTests
{
    [Property]
    // Feature: macro-studio, Property 3: Recording Accuracy
    public bool RecordingAccuracy_SmoothPath_StartEndPreserved(PositiveInt steps, int x1, int y1, int x2, int y2)
    {
        var transformer = new CoordinateTransformer(NullLogger<CoordinateTransformer>.Instance);

        // Clamp coordinates into a safe range (screen bounds vary in CI).
        var startX = Math.Abs(x1 % 500);
        var startY = Math.Abs(y1 % 500);
        var endX = Math.Abs(x2 % 500);
        var endY = Math.Abs(y2 % 500);

        var start = new Point(startX, startY);
        var end = new Point(endX, endY);
        var s = Math.Max(1, Math.Min(steps.Get, 50));

        var path = transformer.GenerateSmoothPath(start, end, s).ToList();
        if (path.Count != s + 1) return false;
        if (path[0] != start) return false;
        if (path[^1] != end) return false;

        return true;
    }

    [Property]
    // Feature: macro-studio, Property 3: Recording Accuracy
    public bool RecordingAccuracy_TimingUtilities_SpeedMultiplier_Monotonic(PositiveInt baseMs, PositiveInt multInt)
    {
        var timing = new TimingUtilities(NullLogger<TimingUtilities>.Instance);
        var baseDelay = TimeSpan.FromMilliseconds(Math.Min(baseMs.Get, 5000));

        // speed up should reduce delay; slow down should increase delay
        var fast = Math.Min(multInt.Get, 10);
        var slow = 1.0 / fast;

        var faster = timing.ApplySpeedMultiplier(baseDelay, fast);
        var slower = timing.ApplySpeedMultiplier(baseDelay, slow);

        return faster <= baseDelay && slower >= baseDelay;
    }
}

