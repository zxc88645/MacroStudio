using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;

namespace MacroNex.Infrastructure.Logging;

/// <summary>
/// Simple file logger provider for diagnostic logging.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _filePath, _lock);
    }

    public void Dispose()
    {
    }

    private class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly object _lock;

        public FileLogger(string categoryName, string filePath, object lockObject)
        {
            _categoryName = categoryName;
            _filePath = filePath;
            _lock = lockObject;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning; // Only log Warning and above
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";

            if (exception != null)
            {
                logLine += $"\n{exception}";
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, logLine + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Ignore file write errors
                }
            }
        }
    }
}
