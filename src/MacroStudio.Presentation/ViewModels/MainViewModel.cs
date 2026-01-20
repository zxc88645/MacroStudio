using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;

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

    public MainViewModel(
        IScriptManager scriptManager,
        IRecordingService recordingService,
        IExecutionService executionService,
        ILoggingService loggingService,
        ISafetyService safetyService,
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

        // Auto-initialize scripts on first load so existing scripts are scanned.
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await ScriptList.RefreshAsync();
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

