using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.Utilities;

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
    private readonly IRecordingHotkeyHookService _recordingHotkeyHookService;
    private readonly IScriptHotkeyHookService _scriptHotkeyHookService;

    public ScriptListViewModel ScriptList { get; }
    public CommandGridViewModel CommandGrid { get; }
    public ExecutionControlViewModel ExecutionControls { get; }
    public RecordingViewModel Recording { get; }
    public LoggingViewModel Logging { get; }
    public SettingsViewModel Settings { get; }
    public DebugViewModel Debug { get; }

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
        IRecordingHotkeyHookService recordingHotkeyHookService,
        IScriptHotkeyHookService scriptHotkeyHookService,
        ScriptListViewModel scriptListViewModel,
        CommandGridViewModel commandGridViewModel,
        ExecutionControlViewModel executionControlViewModel,
        RecordingViewModel recordingViewModel,
        LoggingViewModel loggingViewModel,
        SettingsViewModel settingsViewModel,
        DebugViewModel debugViewModel)
    {
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _recordingHotkeyHookService = recordingHotkeyHookService ?? throw new ArgumentNullException(nameof(recordingHotkeyHookService));
        _scriptHotkeyHookService = scriptHotkeyHookService ?? throw new ArgumentNullException(nameof(scriptHotkeyHookService));

        ScriptList = scriptListViewModel ?? throw new ArgumentNullException(nameof(scriptListViewModel));
        CommandGrid = commandGridViewModel ?? throw new ArgumentNullException(nameof(commandGridViewModel));
        ExecutionControls = executionControlViewModel ?? throw new ArgumentNullException(nameof(executionControlViewModel));
        Recording = recordingViewModel ?? throw new ArgumentNullException(nameof(recordingViewModel));
        Logging = loggingViewModel ?? throw new ArgumentNullException(nameof(loggingViewModel));
        Settings = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        Debug = debugViewModel ?? throw new ArgumentNullException(nameof(debugViewModel));

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
            StatusText = UiText.Format("Ui.Status.KillSwitchPrefix", args.Reason, "KILL SWITCH: {0}");
        };

        // Listen for script hotkey presses from low-level hook (no RegisterHotKey).
        _scriptHotkeyHookService.HotkeyPressed += OnScriptHotkeyPressed;
        // Listen for recording control hotkeys from low-level hook (no RegisterHotKey).
        _recordingHotkeyHookService.HotkeyPressed += OnRecordingHotkeyPressed;

        // Auto-initialize scripts on first load so existing scripts are scanned.
        _ = InitializeAsync();

        StatusText = UiText.Get("Ui.Status.Ready", "Ready");
    }

    private async void OnScriptHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
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
                // Hook service encodes ScriptId in Hotkey.Name
                if (!Guid.TryParse(e.Hotkey.Name, out var scriptId))
                    return;

                var script = scripts.FirstOrDefault(s => s.Id == scriptId);

                if (script != null && script.TriggerHotkey != null)
                {
                    // Prepare execution options
                    var options = ExecutionOptions.Default();
                    options.TriggerSource = ExecutionTriggerSource.Hotkey;
                    options.ControlMode = ExecutionControlMode.RunOnly;
                    options.ShowCountdown = false;
                    options.CountdownDuration = TimeSpan.Zero;

                    // For "RepeatWhileHeld" mode, check if script is already executing
                    // If it is, ignore the trigger to avoid concurrent executions
                    if (script.TriggerHotkey.TriggerMode == HotkeyTriggerMode.RepeatWhileHeld)
                    {
                        try
                        {
                            // Try to start execution - if it's already running, this will throw
                            // We catch and ignore it for repeat mode to allow continuous triggering
                            await _executionService.StartExecutionAsync(script, options);
                        }
                        catch (InvalidOperationException)
                        {
                            // Script is already executing, ignore this trigger
                            // This allows Windows keyboard repeat to trigger multiple times
                            // but prevents concurrent executions of the same script
                            return;
                        }
                    }
                    else
                    {
                        // For "Once" mode, always try to execute
                        // If already executing, it will throw and be logged below
                        await _executionService.StartExecutionAsync(script, options);
                    }

                    // Log asynchronously (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _loggingService.LogInfoAsync("Script triggered by hotkey", new Dictionary<string, object>
                            {
                                { "ScriptId", script.Id },
                                { "ScriptName", script.Name },
                                { "Hotkey", e.Hotkey.GetDisplayString() },
                                { "TriggerMode", script.TriggerHotkey.TriggerMode.ToString() }
                            });
                        }
                        catch { /* Ignore logging errors */ }
                    });

                    // Update UI on UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        StatusText = UiText.Format("Ui.Status.ExecutingPrefix", script.Name, "Executing: {0}");
                    });
                }
                else
                {
                    // Script was deleted or cache not yet refreshed; nothing to do.
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

    private void OnRecordingHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        // Don't trigger while capturing hotkeys in UI.
        if (_isHotkeyCaptureActive)
            return;

        try
        {
            var start = Settings.RecordingStartHotkey;
            var pause = Settings.RecordingPauseHotkey;
            var stop = Settings.RecordingStopHotkey;

            bool Match(HotkeyDefinition? hk) =>
                hk != null &&
                hk.Modifiers == e.Hotkey.Modifiers &&
                hk.Key == e.Hotkey.Key;

            if (Match(start))
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (Recording.StartRecordingCommand.CanExecute(null))
                        Recording.StartRecordingCommand.Execute(null);
                });
                return;
            }

            if (Match(pause))
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (Recording.PauseRecordingCommand.CanExecute(null))
                        Recording.PauseRecordingCommand.Execute(null);
                });
                return;
            }

            if (Match(stop))
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (Recording.StopRecordingCommand.CanExecute(null))
                        Recording.StopRecordingCommand.Execute(null);
                    else
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var state = _recordingService.CurrentSession?.State.ToString() ?? "null";
                                await _loggingService.LogInfoAsync("Recording stop hotkey pressed but Stop command not executable", new Dictionary<string, object>
                                {
                                    { "Hotkey", e.Hotkey.GetDisplayString() },
                                    { "RecordingState", state }
                                });
                            }
                            catch { }
                        });
                });
                return;
            }
        }
        catch
        {
        }
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

        // Hook-based script hotkeys: just ask ScriptManager to rebuild the mapping for the hook service.
        await _scriptManager.RegisterAllScriptHotkeysAsync();

        StatusText = UiText.Get("Ui.Status.LoadedScripts", "Loaded scripts");
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
        StatusText = UiText.Get("Ui.Status.Executing", "Executing");
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
        StatusText = UiText.Get("Ui.Status.KillSwitchReset", "Kill switch reset");
    }

    private bool CanResetKillSwitch() => IsKillSwitchActive;

    partial void OnIsKillSwitchActiveChanged(bool oldValue, bool newValue)
    {
        ResetKillSwitchCommand.NotifyCanExecuteChanged();
    }
}

