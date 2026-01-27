using Microsoft.Extensions.Logging;

namespace MacroNex.Infrastructure.Utilities;

/// <summary>
/// Provides timing utilities for input simulation.
/// Handles delay calculations, timing adjustments, and human-like timing patterns.
/// </summary>
public class TimingUtilities
{
    private readonly ILogger<TimingUtilities> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the TimingUtilities class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public TimingUtilities(ILogger<TimingUtilities> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Calculates an appropriate delay based on the distance between two points.
    /// Simulates natural mouse movement timing.
    /// </summary>
    /// <param name="distance">The distance in pixels.</param>
    /// <param name="baseSpeed">Base movement speed in pixels per millisecond (default: 2.0).</param>
    /// <returns>The calculated delay duration.</returns>
    public TimeSpan CalculateMovementDelay(double distance, double baseSpeed = 2.0)
    {
        if (distance < 0)
            throw new ArgumentException("Distance must be non-negative", nameof(distance));

        if (baseSpeed <= 0)
            throw new ArgumentException("Base speed must be positive", nameof(baseSpeed));

        // Calculate base delay
        var baseDelayMs = distance / baseSpeed;

        // Add minimum delay for very short movements
        var minDelayMs = 10.0;
        var delayMs = Math.Max(minDelayMs, baseDelayMs);

        // Cap maximum delay for very long movements
        var maxDelayMs = 2000.0;
        delayMs = Math.Min(maxDelayMs, delayMs);

        var delay = TimeSpan.FromMilliseconds(delayMs);

        _logger.LogTrace("Calculated movement delay for distance {Distance:F2}px: {Delay}ms",
            distance, delay.TotalMilliseconds);

        return delay;
    }

    /// <summary>
    /// Adds random variation to a delay to make timing appear more human-like.
    /// </summary>
    /// <param name="baseDelay">The base delay duration.</param>
    /// <param name="variationPercent">The maximum variation as a percentage (0.0 to 1.0).</param>
    /// <returns>The delay with random variation applied.</returns>
    public TimeSpan AddRandomVariation(TimeSpan baseDelay, double variationPercent = 0.2)
    {
        if (variationPercent < 0.0 || variationPercent > 1.0)
            throw new ArgumentOutOfRangeException(nameof(variationPercent), "Variation percent must be between 0.0 and 1.0");

        if (baseDelay == TimeSpan.Zero || variationPercent == 0.0)
            return baseDelay;

        // Generate random variation factor (-variationPercent to +variationPercent)
        var variationFactor = (_random.NextDouble() - 0.5) * 2 * variationPercent;
        var variationMs = baseDelay.TotalMilliseconds * variationFactor;

        var variedDelayMs = baseDelay.TotalMilliseconds + variationMs;

        // Ensure the delay doesn't become negative
        variedDelayMs = Math.Max(0, variedDelayMs);

        var variedDelay = TimeSpan.FromMilliseconds(variedDelayMs);

        _logger.LogTrace("Added {Variation:F1}% variation to delay {Original}ms: {Varied}ms",
            variationFactor * 100, baseDelay.TotalMilliseconds, variedDelay.TotalMilliseconds);

        return variedDelay;
    }

    /// <summary>
    /// Applies a speed multiplier to a delay.
    /// </summary>
    /// <param name="originalDelay">The original delay duration.</param>
    /// <param name="speedMultiplier">The speed multiplier (1.0 = normal, 0.5 = half speed, 2.0 = double speed).</param>
    /// <returns>The adjusted delay duration.</returns>
    public TimeSpan ApplySpeedMultiplier(TimeSpan originalDelay, double speedMultiplier)
    {
        if (speedMultiplier <= 0)
            throw new ArgumentException("Speed multiplier must be positive", nameof(speedMultiplier));

        if (speedMultiplier == 1.0)
            return originalDelay;

        var adjustedDelayMs = originalDelay.TotalMilliseconds / speedMultiplier;
        var adjustedDelay = TimeSpan.FromMilliseconds(adjustedDelayMs);

        _logger.LogTrace("Applied speed multiplier {Multiplier}x to delay {Original}ms: {Adjusted}ms",
            speedMultiplier, originalDelay.TotalMilliseconds, adjustedDelay.TotalMilliseconds);

        return adjustedDelay;
    }

    /// <summary>
    /// Calculates typing delay based on text length and typing speed.
    /// </summary>
    /// <param name="textLength">The length of text to be typed.</param>
    /// <param name="wordsPerMinute">Typing speed in words per minute (default: 60 WPM).</param>
    /// <returns>The calculated typing delay.</returns>
    public TimeSpan CalculateTypingDelay(int textLength, double wordsPerMinute = 60.0)
    {
        if (textLength < 0)
            throw new ArgumentException("Text length must be non-negative", nameof(textLength));

        if (wordsPerMinute <= 0)
            throw new ArgumentException("Words per minute must be positive", nameof(wordsPerMinute));

        if (textLength == 0)
            return TimeSpan.Zero;

        // Average word length is approximately 5 characters
        var averageWordLength = 5.0;
        var charactersPerMinute = wordsPerMinute * averageWordLength;
        var charactersPerSecond = charactersPerMinute / 60.0;

        var typingTimeSeconds = textLength / charactersPerSecond;
        var typingDelay = TimeSpan.FromSeconds(typingTimeSeconds);

        _logger.LogTrace("Calculated typing delay for {Length} characters at {WPM} WPM: {Delay}ms",
            textLength, wordsPerMinute, typingDelay.TotalMilliseconds);

        return typingDelay;
    }

    /// <summary>
    /// Generates a human-like delay pattern for key combinations.
    /// </summary>
    /// <param name="keyCount">The number of keys in the combination.</param>
    /// <returns>Delays for press and release phases.</returns>
    public (TimeSpan pressDelay, TimeSpan holdDelay, TimeSpan releaseDelay) CalculateKeyComboTiming(int keyCount)
    {
        if (keyCount <= 0)
            throw new ArgumentException("Key count must be positive", nameof(keyCount));

        // Base delays for key combinations
        var basePressDelayMs = 20.0 + (keyCount - 1) * 10.0; // Slightly longer for more keys
        var baseHoldDelayMs = 50.0; // Hold all keys briefly
        var baseReleaseDelayMs = 15.0 + (keyCount - 1) * 5.0; // Slightly longer for more keys

        // Add small random variations
        var pressDelay = TimeSpan.FromMilliseconds(basePressDelayMs + _random.NextDouble() * 10);
        var holdDelay = TimeSpan.FromMilliseconds(baseHoldDelayMs + _random.NextDouble() * 20);
        var releaseDelay = TimeSpan.FromMilliseconds(baseReleaseDelayMs + _random.NextDouble() * 10);

        _logger.LogTrace("Calculated key combo timing for {KeyCount} keys: press={Press}ms, hold={Hold}ms, release={Release}ms",
            keyCount, pressDelay.TotalMilliseconds, holdDelay.TotalMilliseconds, releaseDelay.TotalMilliseconds);

        return (pressDelay, holdDelay, releaseDelay);
    }

    /// <summary>
    /// Calculates delay between individual keystrokes for natural typing.
    /// </summary>
    /// <param name="previousChar">The previous character typed (optional).</param>
    /// <param name="currentChar">The current character being typed.</param>
    /// <param name="baseDelayMs">Base delay between keystrokes in milliseconds (default: 100ms).</param>
    /// <returns>The calculated keystroke delay.</returns>
    public TimeSpan CalculateKeystrokeDelay(char? previousChar, char currentChar, double baseDelayMs = 100.0)
    {
        if (baseDelayMs < 0)
            throw new ArgumentException("Base delay must be non-negative", nameof(baseDelayMs));

        var delayMs = baseDelayMs;

        // Adjust delay based on character patterns
        if (previousChar.HasValue)
        {
            // Slightly longer delay after punctuation
            if (char.IsPunctuation(previousChar.Value))
            {
                delayMs *= 1.2;
            }

            // Shorter delay for common letter combinations
            var combo = $"{previousChar.Value}{currentChar}".ToLowerInvariant();
            if (IsCommonCombination(combo))
            {
                delayMs *= 0.8;
            }
        }

        // Add random variation (±20%)
        var variation = (_random.NextDouble() - 0.5) * 0.4;
        delayMs *= (1.0 + variation);

        // Ensure minimum delay
        delayMs = Math.Max(20.0, delayMs);

        var delay = TimeSpan.FromMilliseconds(delayMs);

        _logger.LogTrace("Calculated keystroke delay for '{Previous}' -> '{Current}': {Delay}ms",
            previousChar, currentChar, delay.TotalMilliseconds);

        return delay;
    }

    /// <summary>
    /// Determines if a character combination is commonly typed together.
    /// </summary>
    /// <param name="combination">The two-character combination.</param>
    /// <returns>True if the combination is common, false otherwise.</returns>
    private bool IsCommonCombination(string combination)
    {
        // Common English letter combinations
        var commonCombinations = new HashSet<string>
        {
            "th", "he", "in", "er", "an", "re", "ed", "nd", "on", "en",
            "at", "ou", "ea", "ha", "ng", "as", "or", "ti", "is", "et",
            "it", "ar", "te", "se", "hi", "of", "be", "to", "st", "nt"
        };

        return commonCombinations.Contains(combination);
    }

    /// <summary>
    /// Creates a delay with exponential backoff for retry scenarios.
    /// </summary>
    /// <param name="attempt">The current attempt number (starting from 1).</param>
    /// <param name="baseDelay">The base delay for the first attempt.</param>
    /// <param name="maxDelay">The maximum delay to cap exponential growth.</param>
    /// <returns>The calculated backoff delay.</returns>
    public TimeSpan CalculateExponentialBackoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (attempt < 1)
            throw new ArgumentException("Attempt must be at least 1", nameof(attempt));

        if (baseDelay < TimeSpan.Zero)
            throw new ArgumentException("Base delay must be non-negative", nameof(baseDelay));

        if (maxDelay < baseDelay)
            throw new ArgumentException("Max delay must be greater than or equal to base delay", nameof(maxDelay));

        var multiplier = Math.Pow(2, attempt - 1);
        var calculatedDelayMs = baseDelay.TotalMilliseconds * multiplier;

        // Cap at maximum delay
        calculatedDelayMs = Math.Min(calculatedDelayMs, maxDelay.TotalMilliseconds);

        var backoffDelay = TimeSpan.FromMilliseconds(calculatedDelayMs);

        _logger.LogTrace("Calculated exponential backoff for attempt {Attempt}: {Delay}ms",
            attempt, backoffDelay.TotalMilliseconds);

        return backoffDelay;
    }
}