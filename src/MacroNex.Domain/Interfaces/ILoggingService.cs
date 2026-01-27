namespace MacroNex.Domain.Interfaces;

/// <summary>
/// Domain service interface for comprehensive logging of automation activities.
/// Provides real-time logging, persistent storage, filtering, and search capabilities.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Event raised when a new log entry is created.
    /// </summary>
    event EventHandler<LogEntryCreatedEventArgs>? LogEntryCreated;

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="context">Optional context information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogInfoAsync(string message, Dictionary<string, object>? context = null);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="context">Optional context information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogWarningAsync(string message, Dictionary<string, object>? context = null);

    /// <summary>
    /// Logs an error message with optional exception details.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="exception">Optional exception details.</param>
    /// <param name="context">Optional context information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogErrorAsync(string message, Exception? exception = null, Dictionary<string, object>? context = null);

    /// <summary>
    /// Retrieves log entries matching the specified filter criteria.
    /// </summary>
    /// <param name="filter">Filter criteria for log entries.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains filtered log entries.</returns>
    Task<IEnumerable<LogEntry>> GetLogEntriesAsync(LogFilter filter);

    /// <summary>
    /// Searches log entries by text content.
    /// </summary>
    /// <param name="searchTerm">The search term to match.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains matching log entries.</returns>
    Task<IEnumerable<LogEntry>> SearchLogEntriesAsync(string searchTerm, int maxResults = 100);

    /// <summary>
    /// Clears all log entries from memory and persistent storage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearLogsAsync();

    /// <summary>
    /// Exports log entries to a file.
    /// </summary>
    /// <param name="filePath">The path where logs should be exported.</param>
    /// <param name="filter">Optional filter criteria for exported entries.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportLogsAsync(string filePath, LogFilter? filter = null);

    /// <summary>
    /// Gets statistics about the current log entries.
    /// </summary>
    /// <returns>A task representing the asynchronous operation. The task result contains log statistics.</returns>
    Task<LogStatistics> GetLogStatisticsAsync();
}

/// <summary>
/// Interface for writing log entries to persistent storage.
/// </summary>
public interface IFileLogWriter
{
    /// <summary>
    /// Writes a log entry to persistent storage.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteLogEntryAsync(LogEntry entry);

    /// <summary>
    /// Clears all log files from persistent storage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearLogsAsync();

    /// <summary>
    /// Exports log entries to a specified file path.
    /// </summary>
    /// <param name="filePath">The path where logs should be exported.</param>
    /// <param name="entries">The log entries to export.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportLogsAsync(string filePath, IEnumerable<LogEntry> entries);
}

/// <summary>
/// Represents a log entry with timestamp, level, message, and context.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Timestamp when the log entry was created.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Log level (Info, Warning, Error).
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// The log message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional exception details for error logs.
    /// </summary>
    public string? ExceptionDetails { get; }

    /// <summary>
    /// Optional context information as key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; }

    /// <summary>
    /// Initializes a new log entry.
    /// </summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="timestamp">Timestamp when created.</param>
    /// <param name="level">Log level.</param>
    /// <param name="message">Log message.</param>
    /// <param name="exceptionDetails">Optional exception details.</param>
    /// <param name="context">Optional context information.</param>
    public LogEntry(Guid id, DateTime timestamp, LogLevel level, string message, string? exceptionDetails = null, Dictionary<string, object>? context = null)
    {
        Id = id;
        Timestamp = timestamp;
        Level = level;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        ExceptionDetails = exceptionDetails;
        Context = context != null ? new Dictionary<string, object>(context).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
    }
}

/// <summary>
/// Log levels supported by the logging service.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Informational messages.
    /// </summary>
    Info,

    /// <summary>
    /// Warning messages.
    /// </summary>
    Warning,

    /// <summary>
    /// Error messages.
    /// </summary>
    Error
}

/// <summary>
/// Filter criteria for querying log entries.
/// </summary>
public class LogFilter
{
    /// <summary>
    /// Minimum log level to include (inclusive).
    /// </summary>
    public LogLevel? MinLevel { get; set; }

    /// <summary>
    /// Maximum log level to include (inclusive).
    /// </summary>
    public LogLevel? MaxLevel { get; set; }

    /// <summary>
    /// Start timestamp for filtering (inclusive).
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End timestamp for filtering (inclusive).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Maximum number of entries to return.
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Optional search term to match in message content.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Creates a filter that matches all log entries.
    /// </summary>
    /// <returns>A filter matching all entries.</returns>
    public static LogFilter All() => new();

    /// <summary>
    /// Creates a filter for a specific log level.
    /// </summary>
    /// <param name="level">The log level to filter.</param>
    /// <returns>A filter for the specified level.</returns>
    public static LogFilter ByLevel(LogLevel level) => new() { MinLevel = level, MaxLevel = level };

    /// <summary>
    /// Creates a filter for entries within a time range.
    /// </summary>
    /// <param name="startTime">Start time (inclusive).</param>
    /// <param name="endTime">End time (inclusive).</param>
    /// <returns>A filter for the specified time range.</returns>
    public static LogFilter ByTimeRange(DateTime startTime, DateTime endTime) => new() { StartTime = startTime, EndTime = endTime };
}

/// <summary>
/// Statistics about log entries.
/// </summary>
public class LogStatistics
{
    /// <summary>
    /// Total number of log entries.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Number of info-level entries.
    /// </summary>
    public int InfoCount { get; set; }

    /// <summary>
    /// Number of warning-level entries.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Number of error-level entries.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Timestamp of the oldest log entry.
    /// </summary>
    public DateTime? OldestEntry { get; set; }

    /// <summary>
    /// Timestamp of the newest log entry.
    /// </summary>
    public DateTime? NewestEntry { get; set; }
}

/// <summary>
/// Event arguments for log entry creation events.
/// </summary>
public class LogEntryCreatedEventArgs : EventArgs
{
    /// <summary>
    /// The log entry that was created.
    /// </summary>
    public LogEntry LogEntry { get; }

    /// <summary>
    /// Initializes a new log entry created event.
    /// </summary>
    /// <param name="logEntry">The log entry that was created.</param>
    public LogEntryCreatedEventArgs(LogEntry logEntry)
    {
        LogEntry = logEntry ?? throw new ArgumentNullException(nameof(logEntry));
    }
}
