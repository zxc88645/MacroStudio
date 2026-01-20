using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.Views;

namespace MacroStudio.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IRecordingHotkeyHookService _recordingHotkeyHookService;
    private readonly ILoggingService _logging;

    private AppSettings? _settings;

    [ObservableProperty]
    private HotkeyDefinition? recordingStartHotkey;

    [ObservableProperty]
    private HotkeyDefinition? recordingPauseHotkey;

    [ObservableProperty]
    private HotkeyDefinition? recordingStopHotkey;

    [ObservableProperty]
    private string? lastMessage;

    public SettingsViewModel(ISettingsService settingsService, IRecordingHotkeyHookService recordingHotkeyHookService, ILoggingService logging)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _recordingHotkeyHookService = recordingHotkeyHookService ?? throw new ArgumentNullException(nameof(recordingHotkeyHookService));
        _logging = logging ?? throw new ArgumentNullException(nameof(logging));

        // Fire-and-forget initialization (load persisted settings and register hotkeys).
        _ = InitializeAsync();
    }

    public string RecordingStartHotkeyDisplay => RecordingStartHotkey?.GetDisplayString() ?? "(未設定)";
    public string RecordingPauseHotkeyDisplay => RecordingPauseHotkey?.GetDisplayString() ?? "(未設定)";
    public string RecordingStopHotkeyDisplay => RecordingStopHotkey?.GetDisplayString() ?? "(未設定)";

    private async Task InitializeAsync()
    {
        try
        {
            _settings = await _settingsService.LoadAsync();
            _settings.EnsureDefaults();

            RecordingStartHotkey = _settings.RecordingStartHotkey;
            RecordingPauseHotkey = _settings.RecordingPauseHotkey;
            RecordingStopHotkey = _settings.RecordingStopHotkey;

            ApplyToHookService();
            LastMessage = "設定已載入";
        }
        catch (Exception ex)
        {
            LastMessage = $"設定載入失敗：{ex.Message}";
            try { await _logging.LogErrorAsync("Failed to initialize settings", ex); } catch { }
        }
    }

    private void ApplyToHookService()
    {
        try
        {
            _recordingHotkeyHookService.SetHotkeys(RecordingStartHotkey, RecordingPauseHotkey, RecordingStopHotkey);
        }
        catch (Exception ex)
        {
            // Hook service is best-effort; log and keep UI responsive.
            _ = Task.Run(async () =>
            {
                try { await _logging.LogErrorAsync("Failed to apply recording hotkeys to hook service", ex); } catch { }
            });
        }
    }

    private static bool Matches(HotkeyDefinition a, HotkeyDefinition b)
        => a.Modifiers == b.Modifiers && a.Key == b.Key && a.TriggerMode == b.TriggerMode;

    private bool ConflictsWithOther(HotkeyDefinition candidate, HotkeyDefinition? other1, HotkeyDefinition? other2)
    {
        if (other1 != null && Matches(candidate, other1)) return true;
        if (other2 != null && Matches(candidate, other2)) return true;
        return false;
    }

    private void SetHotkeyCaptureActive(bool isActive)
    {
        // Avoid triggering scripts while capturing hotkeys.
        if (System.Windows.Application.Current?.MainWindow?.DataContext is MainViewModel main)
        {
            main.SetHotkeyCaptureActive(isActive);
        }
    }

    private async Task ApplyAndPersistAsync(
        string keyName,
        HotkeyDefinition? oldHotkey,
        HotkeyDefinition? newHotkey,
        Action<AppSettings, HotkeyDefinition?> assign)
    {
        _settings ??= AppSettings.Default();
        _settings.EnsureDefaults();

        assign(_settings, newHotkey);
        await _settingsService.SaveAsync(_settings);
        ApplyToHookService();
        LastMessage = $"{keyName} 已更新";
    }

    [RelayCommand]
    private async Task ChangeRecordingStartHotkeyAsync()
    {
        SetHotkeyCaptureActive(true);
        try
        {
            var wnd = new HotkeyCaptureWindow(RecordingStartHotkey) { Owner = System.Windows.Application.Current?.MainWindow };
            if (wnd.ShowDialog() == true && wnd.ResultHotkey != null)
            {
                var candidate = wnd.ResultHotkey with { Name = "Recording Start" };
                if (ConflictsWithOther(candidate, RecordingPauseHotkey, RecordingStopHotkey))
                {
                    System.Windows.MessageBox.Show("此熱鍵與其他錄製控制熱鍵衝突，請改用不同按鍵。", "熱鍵衝突", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var old = RecordingStartHotkey;
                RecordingStartHotkey = candidate;
                await ApplyAndPersistAsync("開始錄製", old, candidate, (s, v) => s.RecordingStartHotkey = v);
                OnPropertyChanged(nameof(RecordingStartHotkeyDisplay));
            }
        }
        finally
        {
            SetHotkeyCaptureActive(false);
        }
    }

    [RelayCommand]
    private async Task ChangeRecordingPauseHotkeyAsync()
    {
        SetHotkeyCaptureActive(true);
        try
        {
            var wnd = new HotkeyCaptureWindow(RecordingPauseHotkey) { Owner = System.Windows.Application.Current?.MainWindow };
            if (wnd.ShowDialog() == true && wnd.ResultHotkey != null)
            {
                var candidate = wnd.ResultHotkey with { Name = "Recording Pause" };
                if (ConflictsWithOther(candidate, RecordingStartHotkey, RecordingStopHotkey))
                {
                    System.Windows.MessageBox.Show("此熱鍵與其他錄製控制熱鍵衝突，請改用不同按鍵。", "熱鍵衝突", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var old = RecordingPauseHotkey;
                RecordingPauseHotkey = candidate;
                await ApplyAndPersistAsync("暫停錄製", old, candidate, (s, v) => s.RecordingPauseHotkey = v);
                OnPropertyChanged(nameof(RecordingPauseHotkeyDisplay));
            }
        }
        finally
        {
            SetHotkeyCaptureActive(false);
        }
    }

    [RelayCommand]
    private async Task ChangeRecordingStopHotkeyAsync()
    {
        SetHotkeyCaptureActive(true);
        try
        {
            var wnd = new HotkeyCaptureWindow(RecordingStopHotkey) { Owner = System.Windows.Application.Current?.MainWindow };
            if (wnd.ShowDialog() == true && wnd.ResultHotkey != null)
            {
                var candidate = wnd.ResultHotkey with { Name = "Recording Stop" };
                if (ConflictsWithOther(candidate, RecordingStartHotkey, RecordingPauseHotkey))
                {
                    System.Windows.MessageBox.Show("此熱鍵與其他錄製控制熱鍵衝突，請改用不同按鍵。", "熱鍵衝突", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var old = RecordingStopHotkey;
                RecordingStopHotkey = candidate;
                await ApplyAndPersistAsync("停止錄製", old, candidate, (s, v) => s.RecordingStopHotkey = v);
                OnPropertyChanged(nameof(RecordingStopHotkeyDisplay));
            }
        }
        finally
        {
            SetHotkeyCaptureActive(false);
        }
    }

    [RelayCommand]
    private async Task ResetRecordingHotkeysToDefaultAsync()
    {
        var defaults = AppSettings.Default();
        defaults.EnsureDefaults();

        var oldStart = RecordingStartHotkey;
        var oldPause = RecordingPauseHotkey;
        var oldStop = RecordingStopHotkey;

        RecordingStartHotkey = defaults.RecordingStartHotkey;
        RecordingPauseHotkey = defaults.RecordingPauseHotkey;
        RecordingStopHotkey = defaults.RecordingStopHotkey;

        _settings ??= AppSettings.Default();
        _settings.EnsureDefaults();

        _settings.RecordingStartHotkey = RecordingStartHotkey;
        _settings.RecordingPauseHotkey = RecordingPauseHotkey;
        _settings.RecordingStopHotkey = RecordingStopHotkey;

        await _settingsService.SaveAsync(_settings);
        ApplyToHookService();

        OnPropertyChanged(nameof(RecordingStartHotkeyDisplay));
        OnPropertyChanged(nameof(RecordingPauseHotkeyDisplay));
        OnPropertyChanged(nameof(RecordingStopHotkeyDisplay));

        LastMessage = "已重設為預設熱鍵";
    }

    partial void OnRecordingStartHotkeyChanged(HotkeyDefinition? oldValue, HotkeyDefinition? newValue)
    {
        OnPropertyChanged(nameof(RecordingStartHotkeyDisplay));
        ApplyToHookService();
    }

    partial void OnRecordingPauseHotkeyChanged(HotkeyDefinition? oldValue, HotkeyDefinition? newValue)
    {
        OnPropertyChanged(nameof(RecordingPauseHotkeyDisplay));
        ApplyToHookService();
    }

    partial void OnRecordingStopHotkeyChanged(HotkeyDefinition? oldValue, HotkeyDefinition? newValue)
    {
        OnPropertyChanged(nameof(RecordingStopHotkeyDisplay));
        ApplyToHookService();
    }
}

