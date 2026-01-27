using MacroNex.Domain.Interfaces;
using DomainLogLevel = MacroNex.Domain.Interfaces.LogLevel;
using Microsoft.Extensions.Logging;

namespace MacroNex.Application.Services;

/// <summary>
/// Application service for comprehensive logging of automation activities.
/// Provides real-time logging, persistent storage, filtering, and search capabilities.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly IFileLogWriter _fileLogWriter;
    private readonly object _lock = new();
    private readonly List<LogEntry> _inMemoryLogs = new();
    private const int MaxInMemoryEntries = 10000;

    /// <summary>
    /// Initializes a new instance of the LoggingService class.
    /// </summary>
    /// <param name="logger">Microsoft.Extensions.Logging logger for diagnostic information.</param>
    /// <param name="fileLogWriter">File log writer for persistent storage.</param>
    public LoggingService(ILogger<LoggingService> logger, IFileLogWriter fileLogWriter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileLogWriter = fileLogWriter ?? throw new ArgumentNullException(nameof(fileLogWriter));
        _logger.LogDebug("LoggingService initialized");
    }

    /// <inheritdoc />
    public event EventHandler<LogEntryCreatedEventArgs>? LogEntryCreated;

    /// <inheritdoc />
    public async Task LogInfoAsync(string message, Dictionary<string, object>? context = null)
    {
        await LogAsync(DomainLogLevel.Info, message, null, context);
    }

    /// <inheritdoc />
    public async Task LogWarningAsync(string message, Dictionary<string, object>? context = null)
    {
        await LogAsync(DomainLogLevel.Warning, message, null, context);
    }

    /// <inheritdoc />
    public async Task LogErrorAsync(string message, Exception? exception = null, Dictionary<string, object>? context = null)
    {
        string? exceptionDetails = null;
        if (exception != null)
        {
            exceptionDetails = $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
        }

        await LogAsync(DomainLogLevel.Error, message, exceptionDetails, context);
    }

    private async Task LogAsync(DomainLogLevel level, string message, string? exceptionDetails, Dictionary<string, object>? context)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Log message cannot be null or whitespace", nameof(message));

        var timestamp = DateTime.UtcNow;
        var logEntry = new LogEntry(Guid.NewGuid(), timestamp, level, message, exceptionDetails, context);

        lock (_lock)
        {
            _inMemoryLogs.Add(logEntry);

            // Trim in-memory logs if exceeding limit
            if (_inMemoryLogs.Count > MaxInMemoryEntries)
            {
                var removeCount = _inMemoryLogs.Count - MaxInMemoryEntries;
                _inMemoryLogs.RemoveRange(0, removeCount);
            }
        }

        // Write to persistent storage asynchronously
        try
        {
            await _fileLogWriter.WriteLogEntryAsync(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write log entry to persistent storage");
        }

        // Raise event for real-time UI updates (never let UI subscriber exceptions break logging)
        try
        {
            LogEntryCreated?.Invoke(this, new LogEntryCreatedEventArgs(logEntry));
        }
        catch
        {
        }

        // Also log to Microsoft.Extensions.Logging for diagnostic purposes
        var msLogLevel = level switch
        {
            DomainLogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            DomainLogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            DomainLogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
        _logger.Log(msLogLevel, "MacroNex: {Message}", message);
    }

    /// <inheritdoc />
    public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(LogFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        lock (_lock)
        {
            var query = _inMemoryLogs.AsEnumerable();

            // Apply level filter
            if (filter.MinLevel.HasValue)
            {
                query = query.Where(e => e.Level >= filter.MinLevel.Value);
            }
            if (filter.MaxLevel.HasValue)
            {
                query = query.Where(e => e.Level <= filter.MaxLevel.Value);
            }

            // Apply time range filter
            if (filter.StartTime.HasValue)
            {
                query = query.Where(e => e.Timestamp >= filter.StartTime.Value);
            }
            if (filter.EndTime.HasValue)
            {
                query = query.Where(e => e.Timestamp <= filter.EndTime.Value);
            }

            // Apply search term filter
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.ToLowerInvariant();
                query = query.Where(e =>
                    e.Message.ToLowerInvariant().Contains(searchTerm) ||
                    (e.ExceptionDetails != null && e.ExceptionDetails.ToLowerInvariant().Contains(searchTerm)));
            }

            // Order by timestamp descending (newest first)
            query = query.OrderByDescending(e => e.Timestamp);

            // Apply max results limit
            if (filter.MaxResults.HasValue)
            {
                query = query.Take(filter.MaxResults.Value);
            }

            return Task.FromResult(query.ToList().AsEnumerable());
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<LogEntry>> SearchLogEntriesAsync(string searchTerm, int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            throw new ArgumentException("Search term cannot be null or whitespace", nameof(searchTerm));

        var filter = new LogFilter
        {
            SearchTerm = searchTerm,
            MaxResults = maxResults
        };

        return GetLogEntriesAsync(filter);
    }

    /// <inheritdoc />
    public async Task ClearLogsAsync()
    {
        lock (_lock)
        {
            _inMemoryLogs.Clear();
        }

        try
        {
            await _fileLogWriter.ClearLogsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear persistent log files");
        }

        await LogInfoAsync("Logs cleared by user");
    }

    /// <inheritdoc />
    public async Task ExportLogsAsync(string filePath, LogFilter? filter = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));

        var entries = await GetLogEntriesAsync(filter ?? LogFilter.All());

        try
        {
            await _fileLogWriter.ExportLogsAsync(filePath, entries);
            await LogInfoAsync($"Logs exported to {filePath}", new Dictionary<string, object> { { "ExportPath", filePath }, { "EntryCount", entries.Count() } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export logs to {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<LogStatistics> GetLogStatisticsAsync()
    {
        lock (_lock)
        {
            if (_inMemoryLogs.Count == 0)
            {
                return Task.FromResult(new LogStatistics());
            }

            var stats = new LogStatistics
            {
                TotalEntries = _inMemoryLogs.Count,
                InfoCount = _inMemoryLogs.Count(e => e.Level == DomainLogLevel.Info),
                WarningCount = _inMemoryLogs.Count(e => e.Level == DomainLogLevel.Warning),
                ErrorCount = _inMemoryLogs.Count(e => e.Level == DomainLogLevel.Error),
                OldestEntry = _inMemoryLogs.Min(e => e.Timestamp),
                NewestEntry = _inMemoryLogs.Max(e => e.Timestamp)
            };

            return Task.FromResult(stats);
        }
    }
}
