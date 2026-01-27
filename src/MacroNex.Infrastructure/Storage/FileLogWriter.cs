using MacroNex.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MacroNex.Infrastructure.Storage;

/// <summary>
/// Infrastructure service for writing log entries to persistent file storage.
/// Handles log file rotation, formatting, and export operations.
/// </summary>
public class FileLogWriter : IFileLogWriter
{
    private readonly ILogger<FileLogWriter> _logger;
    private readonly string _logDirectory;
    private readonly string _logFileName;
    private const long MaxLogFileSize = 10 * 1024 * 1024; // 10 MB
    private const int MaxLogFiles = 10;
    private readonly object _fileLock = new();

    /// <summary>
    /// Initializes a new instance of the FileLogWriter class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="logDirectory">Directory where log files are stored. If null, uses default application data directory.</param>
    public FileLogWriter(ILogger<FileLogWriter> logger, string? logDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroNex",
            "Logs");
        _logFileName = "MacroNex.log";

        Directory.CreateDirectory(_logDirectory);
        _logger.LogDebug("FileLogWriter initialized with directory: {LogDirectory}", _logDirectory);
    }

    /// <inheritdoc />
    public async Task WriteLogEntryAsync(LogEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        var logFilePath = GetCurrentLogFilePath();

        lock (_fileLock)
        {
            // Check if rotation is needed
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length >= MaxLogFileSize)
                {
                    RotateLogFiles();
                }
            }

            // Write log entry
            var logLine = FormatLogEntry(entry);
            File.AppendAllText(logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ClearLogsAsync()
    {
        lock (_fileLock)
        {
            try
            {
                // Delete all log files
                var logFiles = Directory.GetFiles(_logDirectory, "MacroNex*.log");
                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete log file: {FilePath}", file);
                    }
                }

                _logger.LogInformation("Cleared {Count} log files", logFiles.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear log files");
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ExportLogsAsync(string filePath, IEnumerable<LogEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = new List<string>();
            lines.Add("MacroNex Log Export");
            lines.Add($"Export Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            lines.Add($"Total Entries: {entries.Count()}");
            lines.Add(new string('-', 80));
            lines.Add("");

            foreach (var entry in entries.OrderBy(e => e.Timestamp))
            {
                lines.Add(FormatLogEntry(entry));
            }

            await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);
            _logger.LogInformation("Exported {Count} log entries to {FilePath}", entries.Count(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export logs to {FilePath}", filePath);
            throw;
        }
    }

    private string GetCurrentLogFilePath()
    {
        return Path.Combine(_logDirectory, _logFileName);
    }

    private void RotateLogFiles()
    {
        try
        {
            // Rename existing files (MacroNex.log -> MacroNex.1.log, etc.)
            for (int i = MaxLogFiles - 1; i >= 1; i--)
            {
                var oldFile = Path.Combine(_logDirectory, $"MacroNex.{i}.log");
                var newFile = Path.Combine(_logDirectory, $"MacroNex.{i + 1}.log");

                if (File.Exists(oldFile))
                {
                    if (File.Exists(newFile))
                    {
                        File.Delete(newFile);
                    }
                    File.Move(oldFile, newFile);
                }
            }

            // Move current log to .1
            var currentLog = GetCurrentLogFilePath();
            if (File.Exists(currentLog))
            {
                var firstRotated = Path.Combine(_logDirectory, "MacroNex.1.log");
                if (File.Exists(firstRotated))
                {
                    File.Delete(firstRotated);
                }
                File.Move(currentLog, firstRotated);
            }

            // Delete oldest log if exceeding max files
            var oldestLog = Path.Combine(_logDirectory, $"MacroNex.{MaxLogFiles + 1}.log");
            if (File.Exists(oldestLog))
            {
                File.Delete(oldestLog);
            }

            _logger.LogInformation("Rotated log files");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate log files");
        }
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = entry.Level.ToString().PadRight(7);
        var message = entry.Message;

        var sb = new StringBuilder();
        sb.Append($"[{timestamp}] [{level}] {message}");

        if (!string.IsNullOrEmpty(entry.ExceptionDetails))
        {
            sb.AppendLine();
            sb.Append($"  Exception: {entry.ExceptionDetails}");
        }

        if (entry.Context != null && entry.Context.Count > 0)
        {
            sb.AppendLine();
            sb.Append("  Context: ");
            var contextPairs = entry.Context.Select(kvp => $"{kvp.Key}={kvp.Value}");
            sb.Append(string.Join(", ", contextPairs));
        }

        return sb.ToString();
    }
}
