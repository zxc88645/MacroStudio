using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Interfaces;
using System.Collections.ObjectModel;
using System.IO;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// ViewModel for displaying and interacting with log entries.
/// </summary>
public partial class LoggingViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private LogLevel? selectedLevel;

    [ObservableProperty]
    private int maxResults = 200;

    public LoggingViewModel(ILoggingService loggingService)
    {
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _loggingService.LogEntryCreated += OnLogEntryCreated;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var filter = new LogFilter
        {
            MaxResults = MaxResults,
            SearchTerm = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            MinLevel = SelectedLevel,
            MaxLevel = SelectedLevel
        };

        var entries = await _loggingService.GetLogEntriesAsync(filter);

        Entries.Clear();
        foreach (var e in entries.OrderBy(e => e.Timestamp))
            Entries.Add(e);
    }

    private bool _isClearing = false;

    [RelayCommand]
    public async Task ClearAsync()
    {
        // 設置清除標誌，防止 OnLogEntryCreated 在清除過程中添加項目
        _isClearing = true;
        try
        {
            // 先清空 UI 集合，避免競態條件
            Entries.Clear();
            
            // 然後清空服務層的日誌（這會觸發 LogEntryCreated 事件，但由於 _isClearing 為 true，不會添加）
            await _loggingService.ClearLogsAsync();
        }
        finally
        {
            // 重置標誌，允許後續的日誌條目正常添加
            _isClearing = false;
        }
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        // Placeholder path; real UI will use SaveFileDialog later in Views layer.
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"macrostudio-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");

        LogFilter? filter = null;
        if (!string.IsNullOrWhiteSpace(SearchText) || SelectedLevel.HasValue)
        {
            filter = new LogFilter
            {
                MaxResults = null,
                SearchTerm = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                MinLevel = SelectedLevel,
                MaxLevel = SelectedLevel
            };
        }

        await _loggingService.ExportLogsAsync(path, filter);
        await _loggingService.LogInfoAsync("Logs exported (UI)", new Dictionary<string, object> { { "ExportPath", path } });
    }

    private void OnLogEntryCreated(object? sender, LogEntryCreatedEventArgs e)
    {
        try
        {
            // 如果正在清除日誌，忽略新添加的日誌條目（避免在清除過程中添加項目）
            if (_isClearing)
                return;

            // Keep a bounded list for UI responsiveness.
            const int max = 5000;
            if (Entries.Count >= max)
                Entries.RemoveAt(0);

            Entries.Add(e.LogEntry);
        }
        catch
        {
            // Never throw from event handler (protect UI thread).
        }
    }
}

