using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.Views;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// ViewModel for execution controls and progress display.
/// </summary>
public partial class ExecutionControlViewModel : ObservableObject
{
    private readonly IExecutionService _executionService;
    private readonly ILoggingService _loggingService;
    private readonly ISettingsService _settingsService;

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
    private double speedMultiplier = 1.0;

    [ObservableProperty]
    private bool showCountdown = true;

    [ObservableProperty]
    private TimeSpan countdownDuration = TimeSpan.FromSeconds(3);

    [ObservableProperty]
    private double countdownSeconds = 3.0;

    [ObservableProperty]
    private string? lastError;

    public ExecutionControlViewModel(IExecutionService executionService, ILoggingService loggingService, ISettingsService settingsService)
    {
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Mirror service state
        State = _executionService.State;
        Script = _executionService.CurrentScript;
        CurrentCommandIndex = _executionService.CurrentCommandIndex;
        TotalCommandCount = _executionService.CurrentScript?.CommandCount ?? 0;

        _executionService.ProgressChanged += OnProgressChanged;
        _executionService.StateChanged += OnStateChanged;
        _executionService.ExecutionError += OnExecutionError;
        _executionService.ExecutionCompleted += OnExecutionCompleted;

        // Load defaults (fire and forget; UI will reflect when done)
        _ = LoadDefaultsAsync();
    }

    private async Task LoadDefaultsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        SpeedMultiplier = settings.DefaultSpeedMultiplier;
        ShowCountdown = settings.ShowCountdown;
        CountdownSeconds = settings.CountdownSeconds <= 0 ? 3 : settings.CountdownSeconds;
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
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (Script == null) return;

        var options = ExecutionOptions.Default();
        options.SpeedMultiplier = SpeedMultiplier <= 0 ? 1.0 : SpeedMultiplier;
        options.ShowCountdown = false; // UI handles countdown (focus warning)
        options.CountdownDuration = TimeSpan.Zero;

        if (ShowCountdown && CountdownDuration > TimeSpan.Zero)
        {
            var wnd = new CountdownWindow();
            await wnd.ShowCountdownAsync(CountdownDuration);
        }

        await _loggingService.LogInfoAsync("Start execution (UI)", new Dictionary<string, object>
        {
            { "ScriptId", Script.Id },
            { "ScriptName", Script.Name },
            { "SpeedMultiplier", options.SpeedMultiplier },
            { "ShowCountdown", ShowCountdown },
            { "CountdownSeconds", CountdownDuration.TotalSeconds }
        });

        await _executionService.StartExecutionAsync(Script, options);
        RefreshFromService();
    }

    private bool CanStart() => Script != null && (State == ExecutionState.Idle || State == ExecutionState.Stopped || State == ExecutionState.Completed || State == ExecutionState.Failed || State == ExecutionState.Terminated);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        await _executionService.PauseExecutionAsync();
        RefreshFromService();
    }

    private bool CanPause() => State == ExecutionState.Running;

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        await _executionService.ResumeExecutionAsync();
        RefreshFromService();
    }

    private bool CanResume() => State == ExecutionState.Paused;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        await _executionService.StopExecutionAsync();
        RefreshFromService();
    }

    private bool CanStop() => State == ExecutionState.Running || State == ExecutionState.Paused || State == ExecutionState.Stepping;

    [RelayCommand(CanExecute = nameof(CanStep))]
    private async Task StepAsync()
    {
        await _executionService.StepExecutionAsync();
        RefreshFromService();
    }

    private bool CanStep() => Script != null && (State == ExecutionState.Idle || State == ExecutionState.Paused || State == ExecutionState.Stopped);

    [RelayCommand]
    private async Task TerminateAsync()
    {
        await _executionService.TerminateExecutionAsync();
        RefreshFromService();
    }

    private void OnProgressChanged(object? sender, ExecutionProgressEventArgs e)
    {
        CurrentCommandIndex = e.CurrentCommandIndex;
        TotalCommandCount = e.TotalCommands;
        CompletionPercentage = e.TotalCommands > 0 ? (double)e.CurrentCommandIndex / e.TotalCommands * 100.0 : 0.0;
    }

    private void OnStateChanged(object? sender, ExecutionStateChangedEventArgs e)
    {
        State = e.NewState;
        UpdateCanExecute();
    }

    private void OnExecutionError(object? sender, ExecutionErrorEventArgs e)
    {
        LastError = $"{e.Context}: {e.Error.GetType().Name} - {e.Error.Message}";
        UpdateCanExecute();
    }

    private void OnExecutionCompleted(object? sender, ExecutionCompletedEventArgs e)
    {
        State = e.FinalState;
        UpdateCanExecute();
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
    }
}

