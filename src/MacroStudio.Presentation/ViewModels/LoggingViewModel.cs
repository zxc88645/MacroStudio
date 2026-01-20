using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Interfaces;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

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

        // 確保在 UI 線程上修改集合
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() =>
            {
                Entries.Clear();
                foreach (var e in entries.OrderBy(e => e.Timestamp))
                    Entries.Add(e);
            });
        }
        else
        {
            Entries.Clear();
            foreach (var e in entries.OrderBy(e => e.Timestamp))
                Entries.Add(e);
        }
    }

    private bool _isClearing = false;

    [RelayCommand]
    public async Task ClearAsync()
    {
        // 設置清除標誌，防止 OnLogEntryCreated 在清除過程中添加項目
        _isClearing = true;
        try
        {
            // 確保在 UI 線程上清空集合
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() => Entries.Clear());
            }
            else
            {
                Entries.Clear();
            }
            
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

    [RelayCommand]
    public void Copy()
    {
        if (Entries.Count == 0)
            return;

        try
        {
            // 使用與 LogEntryDisplayConverter 相同的格式
            var lines = Entries.Select(entry =>
            {
                var ts = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
                return $"[{ts}] [{entry.Level}] {entry.Message}";
            });

            var text = string.Join(Environment.NewLine, lines);
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            // 如果複製失敗，記錄錯誤但不拋出異常
            System.Diagnostics.Debug.WriteLine($"Failed to copy logs to clipboard: {ex.Message}");
        }
    }

    private void OnLogEntryCreated(object? sender, LogEntryCreatedEventArgs e)
    {
        try
        {
            // 如果正在清除日誌，忽略新添加的日誌條目（避免在清除過程中添加項目）
            if (_isClearing)
                return;

            // 確保在 UI 線程上修改集合，避免 ItemsControl 同步問題
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => AddLogEntry(e.LogEntry), System.Windows.Threading.DispatcherPriority.Normal);
            }
            else
            {
                AddLogEntry(e.LogEntry);
            }
        }
        catch
        {
            // Never throw from event handler (protect UI thread).
        }
    }

    private void AddLogEntry(LogEntry entry)
    {
        try
        {
            // Keep a bounded list for UI responsiveness.
            // 注意：此方法必須在 UI 線程上調用
            const int max = 5000;
            if (Entries.Count >= max)
            {
                // 使用 RemoveAt 而不是 Remove，避免在快速添加時出現同步問題
                Entries.RemoveAt(0);
            }

            Entries.Add(entry);
        }
        catch
        {
            // 忽略集合修改錯誤，避免崩潰
        }
    }
}

