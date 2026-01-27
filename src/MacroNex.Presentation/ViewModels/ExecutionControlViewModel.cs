using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroNex.Domain.Entities;
using MacroNex.Domain.Events;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Application.Services;
using MacroNex.Presentation.Views;
using MacroNex.Presentation.Utilities;


namespace MacroNex.Presentation.ViewModels;

/// <summary>
/// ViewModel for execution controls and progress display.
/// </summary>
public partial class ExecutionControlViewModel : ObservableObject
{
    private readonly IExecutionService _executionService;
    private readonly ILoggingService _loggingService;
    private readonly ISettingsService _settingsService;
    private readonly ArduinoConnectionService _arduinoConnectionService;

    [ObservableProperty]
    private Script? script;

    [ObservableProperty]
    private ExecutionState state = ExecutionState.Idle;

    [ObservableProperty]
    private int currentCommandIndex;

    [ObservableProperty]
    private int totalCommandCount;

    [ObservableProperty]
    private double completionPercentage;

    [ObservableProperty]
    private bool showCountdown = true;

    [ObservableProperty]
    private TimeSpan countdownDuration = TimeSpan.FromSeconds(3);

    [ObservableProperty]
    private double countdownSeconds = 3.0;

    [ObservableProperty]
    private string? lastError;

    [ObservableProperty]
    private InputMode inputMode = InputMode.HighLevel;

    [ObservableProperty]
    private ArduinoConnectionState arduinoConnectionState = ArduinoConnectionState.Disconnected;

    public ExecutionControlViewModel(IExecutionService executionService, ILoggingService loggingService, ISettingsService settingsService, ArduinoConnectionService arduinoConnectionService)
    {
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _arduinoConnectionService = arduinoConnectionService ?? throw new ArgumentNullException(nameof(arduinoConnectionService));

        // Mirror service state
        State = _executionService.State;
        Script = _executionService.CurrentScript;
        CurrentCommandIndex = _executionService.CurrentCommandIndex;
        TotalCommandCount = _executionService.CurrentScript?.CommandCount ?? 0;

        _executionService.ProgressChanged += OnProgressChanged;
        _executionService.StateChanged += OnStateChanged;
        _executionService.ExecutionError += OnExecutionError;
        _executionService.ExecutionCompleted += OnExecutionCompleted;

        _arduinoConnectionService.ConnectionStateChanged += OnArduinoConnectionStateChanged;

        // Load defaults (fire and forget; UI will reflect when done)
        _ = LoadDefaultsAsync();
    }

    private void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.Invoke(action);
        else
            action();
    }

    /// <summary>
    /// Best-effort: if the user is currently editing a script in the editor, sync the editor text
    /// into the same Script instance we're about to execute. This avoids executing a stale version.
    /// </summary>
    private void TrySyncFromEditorIfEditingSameScript(Script script)
    {
        try
        {
            var mainVm = System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
            var grid = mainVm?.CommandGrid;
            var editing = grid?.CurrentScript;
            if (editing == null || editing.Id != script.Id)
                return;

            // Don't sync invalid Lua text; keep last known good SourceText.
            if (grid!.HasDiagnostic)
                return;

            // Sync the in-memory script used by execution with what the editor currently shows.
            // (Persistence is handled by autosave; execution correctness matters more here.)
            script.SourceText = grid.Document.Text ?? string.Empty;
        }
        catch
        {
            // Best-effort only.
        }
    }

    private async Task LoadDefaultsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        ShowCountdown = settings.ShowCountdown;
        CountdownSeconds = settings.CountdownSeconds <= 0 ? 3 : settings.CountdownSeconds;
        InputMode = settings.GlobalInputMode;
    }

    partial void OnCountdownSecondsChanged(double oldValue, double newValue)
    {
        if (double.IsNaN(newValue) || double.IsInfinity(newValue))
            return;

        var seconds = Math.Clamp(newValue, 0, 60 * 10); // cap at 10 minutes for safety
        CountdownDuration = TimeSpan.FromSeconds(seconds);
    }

    partial void OnCountdownDurationChanged(TimeSpan oldValue, TimeSpan newValue)
    {
        var seconds = Math.Max(0, newValue.TotalSeconds);
        if (Math.Abs(seconds - CountdownSeconds) > 0.0001)
            CountdownSeconds = seconds;
    }

    public void SetScript(Script? script)
    {
        Script = script;
        TotalCommandCount = script?.CommandCount ?? 0;
        CurrentCommandIndex = 0;
        CompletionPercentage = 0;
        StartCommand.NotifyCanExecuteChanged();
        StepCommand.NotifyCanExecuteChanged();
        TerminateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (Script == null) return;

        // Reset UI progress immediately so it doesn't show stale "completed" progress while starting.
        RunOnUiThread(() =>
        {
            CurrentCommandIndex = 0;
            TotalCommandCount = Script.CommandCount;
            CompletionPercentage = 0;
            LastError = null;
        });

        // Get global input mode from settings
        var settings = await _settingsService.LoadAsync();
        var globalInputMode = settings.GlobalInputMode;
        
        var options = ExecutionOptions.Default();
        options.TriggerSource = ExecutionTriggerSource.DebugPanel;
        options.ControlMode = ExecutionControlMode.DebugInteractive;
        options.ShowCountdown = false; // UI handles countdown (focus warning)
        options.CountdownDuration = TimeSpan.Zero;
        options.InputMode = globalInputMode;

        if (ShowCountdown && CountdownDuration > TimeSpan.Zero)
        {
            var wnd = new CountdownWindow();
            await wnd.ShowCountdownAsync(CountdownDuration);
        }

        await _loggingService.LogInfoAsync("Start execution (UI)", new Dictionary<string, object>
        {
            { "ScriptId", Script.Id },
            { "ScriptName", Script.Name },
            { "ShowCountdown", ShowCountdown },
            { "CountdownSeconds", CountdownDuration.TotalSeconds }
        });

        try
        {
            // Ensure we execute what the user is currently editing (not a stale cached instance).
            TrySyncFromEditorIfEditingSameScript(Script);

            await _executionService.StartExecutionAsync(Script, options);
            RefreshFromService();
        }
        catch (Exception ex)
        {
            LastError = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Start execution failed (UI)", ex, new Dictionary<string, object>
            {
                { "ScriptId", Script.Id },
                { "ScriptName", Script.Name }
            });
            UpdateCanExecute();
        }
    }

    private bool CanStart() => Script != null && (State == ExecutionState.Idle || State == ExecutionState.Stopped || State == ExecutionState.Completed || State == ExecutionState.Failed || State == ExecutionState.Terminated);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        try
        {
            await _executionService.PauseExecutionAsync();
            RefreshFromService();
        }
        catch (Exception ex)
        {
            LastError = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Pause execution failed (UI)", ex);
            UpdateCanExecute();
        }
    }

    private bool CanPause() =>
        State == ExecutionState.Running &&
        _executionService.CurrentSession?.Options.ControlMode == ExecutionControlMode.DebugInteractive;

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        try
        {
            await _executionService.ResumeExecutionAsync();
            RefreshFromService();
        }
        catch (Exception ex)
        {
            LastError = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Resume execution failed (UI)", ex);
            UpdateCanExecute();
        }
    }

    private bool CanResume() =>
        State == ExecutionState.Paused &&
        _executionService.CurrentSession?.Options.ControlMode == ExecutionControlMode.DebugInteractive;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        try
        {
            await _executionService.StopExecutionAsync();
            RefreshFromService();
        }
        catch (Exception ex)
        {
            LastError = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Stop execution failed (UI)", ex);
            UpdateCanExecute();
        }
    }

    private bool CanStop() =>
        (State == ExecutionState.Running || State == ExecutionState.Paused || State == ExecutionState.Stepping) &&
        _executionService.CurrentSession?.Options.ControlMode == ExecutionControlMode.DebugInteractive;

    [RelayCommand(CanExecute = nameof(CanStep))]
    private async Task StepAsync()
    {
        try
        {
            await _executionService.StepExecutionAsync();
            RefreshFromService();
        }
        catch (Exception ex)
        {
            LastError = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Step execution failed (UI)", ex);
            UpdateCanExecute();
        }
    }

    // 單步只在「已暫停」時才有意義（避免尚未開始就能亂按、也避免隱性建立上下文造成狀態混亂）
    private bool CanStep() =>
        Script != null &&
        State == ExecutionState.Paused &&
        _executionService.CurrentSession?.Options.ControlMode == ExecutionControlMode.DebugInteractive;

    [RelayCommand(CanExecute = nameof(CanTerminate))]
    private async Task TerminateAsync()
    {
        try
        {
            await _executionService.TerminateExecutionAsync();
            RefreshFromService();
        }
        catch (Exception ex)
        {
            LastError = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Terminate execution failed (UI)", ex);
            UpdateCanExecute();
        }
    }

    private bool CanTerminate() => State == ExecutionState.Running || State == ExecutionState.Paused || State == ExecutionState.Stepping;

    private void OnProgressChanged(object? sender, ExecutionProgressEventArgs e)
    {
        RunOnUiThread(() =>
        {
            CurrentCommandIndex = e.CurrentCommandIndex;
            TotalCommandCount = e.TotalCommands;
            CompletionPercentage = Math.Clamp(e.CompletionPercentage, 0.0, 100.0);
        });
    }

    private void OnStateChanged(object? sender, ExecutionStateChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            State = e.NewState;
            UpdateCanExecute();
        });
    }

    private void OnExecutionError(object? sender, ExecutionErrorEventArgs e)
    {
        RunOnUiThread(() =>
        {
            LastError = UiText.Format("Ui.ErrorPrefix", $"{e.Context}: {e.Error.GetType().Name} - {e.Error.Message}", "Error: {0}");
            UpdateCanExecute();
        });
    }

    private void OnArduinoConnectionStateChanged(object? sender, Domain.Interfaces.ArduinoConnectionStateChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            ArduinoConnectionState = e.NewState;
            OnPropertyChanged(nameof(ArduinoConnectionState));
        });
    }

    private void OnExecutionCompleted(object? sender, ExecutionCompletedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            // 執行完成後恢復到「待執行」（Idle），避免還要再按一次強制終止或停留在 Completed。
            // 仍保留 Script 選擇狀態，讓使用者可以直接再次按 Start。
            State = ExecutionState.Idle;
            CurrentCommandIndex = 0;
            TotalCommandCount = Script?.CommandCount ?? e.TotalCommandCount;
            CompletionPercentage = 0;
            UpdateCanExecute();
        });
    }

    private void RefreshFromService()
    {
        State = _executionService.State;
        CurrentCommandIndex = _executionService.CurrentCommandIndex;
        TotalCommandCount = _executionService.CurrentScript?.CommandCount ?? TotalCommandCount;
        CompletionPercentage = TotalCommandCount > 0 ? (double)CurrentCommandIndex / TotalCommandCount * 100.0 : 0.0;
        UpdateCanExecute();
    }

    private void UpdateCanExecute()
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        StepCommand.NotifyCanExecuteChanged();
        TerminateCommand.NotifyCanExecuteChanged();
    }
}

