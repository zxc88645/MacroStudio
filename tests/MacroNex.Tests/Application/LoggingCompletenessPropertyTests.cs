using FsCheck;
using FsCheck.Xunit;
using MacroNex.Application.Services;
using MacroNex.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroNex.Tests.Application;

/// <summary>
/// Property-based tests for Logging Completeness.
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**
/// </summary>
public class LoggingCompletenessPropertyTests
{
    [Property]
    // Feature: macro-studio, Property 7: Logging Completeness
    public bool LoggingCompleteness_AllLevels_AreCapturedWithTimestamps(NonEmptyString message)
    {
        var msg = message.Get.Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return true;

        var logger = NullLogger<LoggingService>.Instance;
        var fileWriter = new FakeFileLogWriter();
        var service = new LoggingService(logger, fileWriter);

        var entries = new List<LogEntry>();
        service.LogEntryCreated += (sender, e) => entries.Add(e.LogEntry);

        // Log at all levels
        service.LogInfoAsync($"Info: {msg}").GetAwaiter().GetResult();
        service.LogWarningAsync($"Warning: {msg}").GetAwaiter().GetResult();
        service.LogErrorAsync($"Error: {msg}").GetAwaiter().GetResult();

        // Verify all entries were created
        if (entries.Count != 3)
            return false;

        // Verify timestamps are present and reasonable
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            if (entry.Timestamp == default)
                return false;
            if (entry.Timestamp > now.AddSeconds(5) || entry.Timestamp < now.AddSeconds(-5))
                return false;
        }

        // Verify levels are correct
        var infoEntry = entries.FirstOrDefault(e => e.Level == LogLevel.Info);
        var warningEntry = entries.FirstOrDefault(e => e.Level == LogLevel.Warning);
        var errorEntry = entries.FirstOrDefault(e => e.Level == LogLevel.Error);

        if (infoEntry == null || warningEntry == null || errorEntry == null)
            return false;

        // Verify messages contain the original message
        if (!infoEntry.Message.Contains(msg) || !warningEntry.Message.Contains(msg) || !errorEntry.Message.Contains(msg))
            return false;

        return true;
    }

    [Property]
    // Feature: macro-studio, Property 7: Logging Completeness
    public bool LoggingCompleteness_WithContext_IsPreserved(NonEmptyString message, NonEmptyString contextKey, NonEmptyString contextValue)
    {
        var msg = message.Get.Trim();
        var key = contextKey.Get.Trim();
        var value = contextValue.Get.Trim();

        if (string.IsNullOrWhiteSpace(msg) || string.IsNullOrWhiteSpace(key))
            return true;

        var logger = NullLogger<LoggingService>.Instance;
        var fileWriter = new FakeFileLogWriter();
        var service = new LoggingService(logger, fileWriter);

        var entries = new List<LogEntry>();
        service.LogEntryCreated += (sender, e) => entries.Add(e.LogEntry);

        var context = new Dictionary<string, object> { { key, value } };
        service.LogInfoAsync(msg, context).GetAwaiter().GetResult();

        if (entries.Count != 1)
            return false;

        var entry = entries[0];
        if (!entry.Context.ContainsKey(key))
            return false;

        var contextValueStr = entry.Context[key]?.ToString();
        if (contextValueStr != value)
            return false;

        return true;
    }

    [Property]
    // Feature: macro-studio, Property 7: Logging Completeness
    public bool LoggingCompleteness_ErrorWithException_ContainsExceptionDetails(NonEmptyString message)
    {
        var msg = message.Get.Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return true;

        var logger = NullLogger<LoggingService>.Instance;
        var fileWriter = new FakeFileLogWriter();
        var service = new LoggingService(logger, fileWriter);

        var entries = new List<LogEntry>();
        service.LogEntryCreated += (sender, e) => entries.Add(e.LogEntry);

        var exception = new InvalidOperationException("Test exception");
        service.LogErrorAsync(msg, exception).GetAwaiter().GetResult();

        if (entries.Count != 1)
            return false;

        var entry = entries[0];
        if (entry.Level != LogLevel.Error)
            return false;

        if (string.IsNullOrEmpty(entry.ExceptionDetails))
            return false;

        if (!entry.ExceptionDetails.Contains("InvalidOperationException"))
            return false;

        if (!entry.ExceptionDetails.Contains("Test exception"))
            return false;

        return true;
    }

    [Property]
    // Feature: macro-studio, Property 7: Logging Completeness
    public bool LoggingCompleteness_Filtering_ReturnsCorrectEntries(NonEmptyString[] messages)
    {
        if (messages == null || messages.Length == 0)
            return true;

        var logger = NullLogger<LoggingService>.Instance;
        var fileWriter = new FakeFileLogWriter();
        var service = new LoggingService(logger, fileWriter);

        // Log messages at different levels
        foreach (var msg in messages.Take(10))
        {
            var trimmed = msg.Get.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            service.LogInfoAsync($"Info: {trimmed}").GetAwaiter().GetResult();
            service.LogWarningAsync($"Warning: {trimmed}").GetAwaiter().GetResult();
            service.LogErrorAsync($"Error: {trimmed}").GetAwaiter().GetResult();
        }

        // Test filtering by level
        var infoFilter = LogFilter.ByLevel(LogLevel.Info);
        var infoEntries = service.GetLogEntriesAsync(infoFilter).GetAwaiter().GetResult();
        if (infoEntries.Any(e => e.Level != LogLevel.Info))
            return false;

        var errorFilter = LogFilter.ByLevel(LogLevel.Error);
        var errorEntries = service.GetLogEntriesAsync(errorFilter).GetAwaiter().GetResult();
        if (errorEntries.Any(e => e.Level != LogLevel.Error))
            return false;

        // Test search functionality
        if (messages.Length > 0)
        {
            var firstMsg = messages[0].Get.Trim();
            if (!string.IsNullOrWhiteSpace(firstMsg))
            {
                var searchResults = service.SearchLogEntriesAsync(firstMsg, 100).GetAwaiter().GetResult();
                if (!searchResults.Any(e => e.Message.Contains(firstMsg)))
                    return false;
            }
        }

        return true;
    }

    [Property]
    // Feature: macro-studio, Property 7: Logging Completeness
    public bool LoggingCompleteness_Statistics_AreAccurate(NonEmptyString[] messages)
    {
        if (messages == null || messages.Length == 0)
            return true;

        var logger = NullLogger<LoggingService>.Instance;
        var fileWriter = new FakeFileLogWriter();
        var service = new LoggingService(logger, fileWriter);

        var infoCount = 0;
        var warningCount = 0;
        var errorCount = 0;

        foreach (var msg in messages.Take(10))
        {
            var trimmed = msg.Get.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            service.LogInfoAsync($"Info: {trimmed}").GetAwaiter().GetResult();
            infoCount++;

            service.LogWarningAsync($"Warning: {trimmed}").GetAwaiter().GetResult();
            warningCount++;

            service.LogErrorAsync($"Error: {trimmed}").GetAwaiter().GetResult();
            errorCount++;
        }

        var stats = service.GetLogStatisticsAsync().GetAwaiter().GetResult();

        if (stats.TotalEntries != infoCount + warningCount + errorCount)
            return false;

        if (stats.InfoCount != infoCount)
            return false;

        if (stats.WarningCount != warningCount)
            return false;

        if (stats.ErrorCount != errorCount)
            return false;

        return true;
    }
}

/// <summary>
/// Fake implementation of IFileLogWriter for testing.
/// </summary>
internal class FakeFileLogWriter : IFileLogWriter
{
    private readonly List<LogEntry> _entries = new();

    public Task WriteLogEntryAsync(LogEntry entry)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task ClearLogsAsync()
    {
        _entries.Clear();
        return Task.CompletedTask;
    }

    public Task ExportLogsAsync(string filePath, IEnumerable<LogEntry> entries)
    {
        // In tests, we don't actually write to files
        return Task.CompletedTask;
    }
}
