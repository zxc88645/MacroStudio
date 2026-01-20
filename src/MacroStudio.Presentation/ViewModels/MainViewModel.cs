using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// Main ViewModel coordinating application state, script selection, and high-level commands.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IScriptManager _scriptManager;
    private readonly IRecordingService _recordingService;
    private readonly IExecutionService _executionService;
    private readonly ILoggingService _loggingService;
    private readonly ISafetyService _safetyService;
    private readonly IGlobalHotkeyService _hotkeyService;

    public ScriptListViewModel ScriptList { get; }
    public CommandGridViewModel CommandGrid { get; }
    public ExecutionControlViewModel ExecutionControls { get; }
    public LoggingViewModel Logging { get; }

    [ObservableProperty]
    private Script? selectedScript;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isKillSwitchActive;

    private bool _isHotkeyCaptureActive = false;

    public MainViewModel(
        IScriptManager scriptManager,
        IRecordingService recordingService,
        IExecutionService executionService,
        ILoggingService loggingService,
        ISafetyService safetyService,
        IGlobalHotkeyService hotkeyService,
        ScriptListViewModel scriptListViewModel,
        CommandGridViewModel commandGridViewModel,
        ExecutionControlViewModel executionControlViewModel,
        LoggingViewModel loggingViewModel)
    {
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));

        ScriptList = scriptListViewModel ?? throw new ArgumentNullException(nameof(scriptListViewModel));
        CommandGrid = commandGridViewModel ?? throw new ArgumentNullException(nameof(commandGridViewModel));
        ExecutionControls = executionControlViewModel ?? throw new ArgumentNullException(nameof(executionControlViewModel));
        Logging = loggingViewModel ?? throw new ArgumentNullException(nameof(loggingViewModel));

        ScriptList.SelectedScriptChanged += (_, script) =>
        {
            SelectedScript = script;
            CommandGrid.LoadScript(script);
            ExecutionControls.SetScript(script);
        };

        IsKillSwitchActive = _safetyService.IsKillSwitchActive;
        _safetyService.KillSwitchActivated += (_, args) =>
        {
            IsKillSwitchActive = true;
            StatusText = $"KILL SWITCH: {args.Reason}";
        };

        // Listen for hotkey presses to trigger scripts
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Auto-initialize scripts on first load so existing scripts are scanned.
        _ = InitializeAsync();
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        // Don't trigger scripts when capturing hotkeys
        if (_isHotkeyCaptureActive)
            return;

        // Fire and forget - don't await anything to avoid delays
        _ = Task.Run(async () =>
        {
            try
            {
                // Find script with matching hotkey
                var scripts = await _scriptManager.GetAllScriptsAsync();
                var script = scripts.FirstOrDefault(s => s.TriggerHotkey != null && 
                    s.TriggerHotkey.Modifiers == e.Hotkey.Modifiers && 
                    s.TriggerHotkey.Key == e.Hotkey.Key);

                if (script != null)
                {
                    // Log asynchronously (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _loggingService.LogInfoAsync("Script triggered by hotkey", new Dictionary<string, object>
                            {
                                { "ScriptId", script.Id },
                                { "ScriptName", script.Name },
                                { "Hotkey", e.Hotkey.GetDisplayString() }
                            });
                        }
                        catch { /* Ignore logging errors */ }
                    });

                    // Update UI on UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Executing: {script.Name}";
                    });

                    // Start execution immediately without countdown
                    var options = ExecutionOptions.Default();
                    options.ShowCountdown = false;
                    options.CountdownDuration = TimeSpan.Zero;

                    // Start execution immediately
                    await _executionService.StartExecutionAsync(script, options);
                }
                else
                {
                    // Log warning asynchronously
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _loggingService.LogWarningAsync("No script found for hotkey", new Dictionary<string, object>
                            {
                                { "Hotkey", e.Hotkey.GetDisplayString() },
                                { "Modifiers", e.Hotkey.Modifiers.ToString() },
                                { "Key", e.Hotkey.Key.ToString() }
                            });
                        }
                        catch { /* Ignore logging errors */ }
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _loggingService.LogErrorAsync("Error handling hotkey trigger", ex, new Dictionary<string, object>
                        {
                            { "Hotkey", e.Hotkey.GetDisplayString() }
                        });
                    }
                    catch { /* Ignore logging errors */ }
                });
            }
        });
    }

    /// <summary>
    /// Sets whether hotkey capture is active (to prevent triggering scripts while setting hotkeys).
    /// </summary>
    public void SetHotkeyCaptureActive(bool isActive)
    {
        _isHotkeyCaptureActive = isActive;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await ScriptList.RefreshAsync();
        
        // Register all script hotkeys
        var scripts = await _scriptManager.GetAllScriptsAsync();
        foreach (var script in scripts)
        {
            if (script.TriggerHotkey != null)
            {
                try
                {
                    await _hotkeyService.RegisterHotkeyAsync(script.TriggerHotkey);
                }
                catch
                {
                    await _loggingService.LogWarningAsync("Failed to register script hotkey", new Dictionary<string, object>
                    {
                        { "ScriptId", script.Id },
                        { "ScriptName", script.Name },
                        { "Hotkey", script.TriggerHotkey.GetDisplayString() }
                    });
                }
            }
        }
        
        StatusText = "Loaded scripts";
    }

    [RelayCommand(CanExecute = nameof(CanStartExecution))]
    private async Task StartExecutionAsync()
    {
        if (SelectedScript == null) return;

        await _loggingService.LogInfoAsync("Execution started", new Dictionary<string, object>
        {
            { "ScriptId", SelectedScript.Id },
            { "ScriptName", SelectedScript.Name }
        });

        await _executionService.StartExecutionAsync(SelectedScript);
        StatusText = "Executing";
    }

    private bool CanStartExecution() => SelectedScript != null;

    partial void OnSelectedScriptChanged(Script? oldValue, Script? newValue)
    {
        StartExecutionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanResetKillSwitch))]
    private async Task ResetKillSwitchAsync()
    {
        await _safetyService.DeactivateKillSwitchAsync();
        IsKillSwitchActive = _safetyService.IsKillSwitchActive;
        StatusText = "Kill switch reset";
    }

    private bool CanResetKillSwitch() => IsKillSwitchActive;

    partial void OnIsKillSwitchActiveChanged(bool oldValue, bool newValue)
    {
        ResetKillSwitchCommand.NotifyCanExecuteChanged();
    }
}

