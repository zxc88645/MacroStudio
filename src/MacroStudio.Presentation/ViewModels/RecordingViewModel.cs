using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.Views;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// ViewModel for controlling macro recording and turning recorded input into a Script.
/// </summary>
public partial class RecordingViewModel : ObservableObject
{
    private readonly IRecordingService _recordingService;
    private readonly IScriptManager _scriptManager;
    private readonly ILoggingService _loggingService;
    private readonly ScriptListViewModel _scriptListViewModel;

    public ObservableCollection<Command> RecordedCommands { get; } = new();

    [ObservableProperty]
    private string recordingStatusText = "Not recording";

    [ObservableProperty]
    private bool recordMouseMovements = true;

    [ObservableProperty]
    private bool recordMouseClicks = true;

    [ObservableProperty]
    private bool recordKeyboardInput = true;

    [ObservableProperty]
    private bool filterSystemEvents = true;

    [ObservableProperty]
    private int totalCommands;

    public RecordingViewModel(
        IRecordingService recordingService,
        IScriptManager scriptManager,
        ILoggingService loggingService,
        ScriptListViewModel scriptListViewModel)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _scriptListViewModel = scriptListViewModel ?? throw new ArgumentNullException(nameof(scriptListViewModel));

        _recordingService.CommandRecorded += OnCommandRecorded;
        _recordingService.RecordingStateChanged += OnRecordingStateChanged;
        _recordingService.RecordingError += OnRecordingError;

        UpdateStatusFromService();
    }

    public bool IsRecording => _recordingService.IsRecording;

    public bool IsPaused => _recordingService.CurrentSession?.State == RecordingState.Paused;

    public string PauseButtonText => IsPaused ? "Resume" : "Pause";

    private void UpdateStatusFromService()
    {
        var session = _recordingService.CurrentSession;
        if (session == null)
        {
            RecordingStatusText = "Not recording";
            return;
        }

        RecordingStatusText = session.State switch
        {
            RecordingState.Active => "Recording…",
            RecordingState.Paused => "Paused",
            RecordingState.Stopped => "Stopped",
            RecordingState.Error => "Error",
            _ => "Not recording"
        };
    }

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        try
        {
            // If currently paused, Start should continue (resume) rather than clear and restart.
            if (_recordingService.CurrentSession?.State == RecordingState.Paused)
            {
                await _recordingService.ResumeRecordingAsync();
                return;
            }

            RecordedCommands.Clear();
            TotalCommands = 0;

            var options = new RecordingOptions
            {
                RecordMouseMovements = RecordMouseMovements,
                RecordMouseClicks = RecordMouseClicks,
                RecordKeyboardInput = RecordKeyboardInput,
                FilterSystemEvents = FilterSystemEvents
            };

            await _recordingService.StartRecordingAsync(options);
        }
        catch (Exception ex)
        {
            RecordingStatusText = $"Error: {ex.Message}";
            await _loggingService.LogErrorAsync("Failed to start recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PauseButtonText));
        }
    }

    private bool CanStartRecording()
        => !_recordingService.IsRecording || _recordingService.CurrentSession?.State == RecordingState.Paused;

    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private async Task StopRecordingAsync()
    {
        try
        {
            await _recordingService.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            RecordingStatusText = $"Error: {ex.Message}";
            await _loggingService.LogErrorAsync("Failed to stop recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanStopRecording()
        => _recordingService.CurrentSession != null &&
           (_recordingService.CurrentSession.State == RecordingState.Active ||
            _recordingService.CurrentSession.State == RecordingState.Paused);

    [RelayCommand(CanExecute = nameof(CanPauseRecording))]
    private async Task PauseRecordingAsync()
    {
        try
        {
            // Toggle: Active -> Pause, Paused -> Resume
            if (_recordingService.CurrentSession?.State == RecordingState.Paused)
            {
                await _recordingService.ResumeRecordingAsync();
            }
            else
            {
                await _recordingService.PauseRecordingAsync();
            }
        }
        catch (Exception ex)
        {
            RecordingStatusText = $"Error: {ex.Message}";
            await _loggingService.LogErrorAsync("Failed to pause recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PauseButtonText));
        }
    }

    private bool CanPauseRecording()
        => _recordingService.CurrentSession != null &&
           (_recordingService.CurrentSession.State == RecordingState.Active ||
            _recordingService.CurrentSession.State == RecordingState.Paused);

    [RelayCommand(CanExecute = nameof(CanResumeRecording))]
    private async Task ResumeRecordingAsync()
    {
        try
        {
            await _recordingService.ResumeRecordingAsync();
        }
        catch (Exception ex)
        {
            RecordingStatusText = $"Error: {ex.Message}";
            await _loggingService.LogErrorAsync("Failed to resume recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PauseButtonText));
        }
    }

    private bool CanResumeRecording()
        => _recordingService.CurrentSession != null && _recordingService.CurrentSession.State == RecordingState.Paused;

    [RelayCommand(CanExecute = nameof(CanSaveAsScript))]
    private async Task SaveAsScriptAsync()
    {
        if (RecordedCommands.Count == 0)
            return;

        var dlg = new InputDialog(
            "Save Recording",
            "將目前錄製的命令另存為新的腳本。",
            "Script name:",
            "Recorded Script");

        dlg.Owner = System.Windows.Application.Current?.MainWindow;
        if (dlg.ShowDialog() != true)
            return;

        var name = (dlg.ValueText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        // Ensure unique script name
        var baseName = name;
        var counter = 1;
        while (!await _scriptManager.IsValidScriptNameAsync(name))
        {
            counter++;
            name = $"{baseName} ({counter})";
        }

        var script = await _scriptManager.CreateScriptAsync(name);
        foreach (var cmd in RecordedCommands)
            script.AddCommand(cmd.Clone());

        await _scriptManager.UpdateScriptAsync(script);

        await _scriptListViewModel.RefreshAsync();
        _scriptListViewModel.SelectedScript = _scriptListViewModel.Scripts.FirstOrDefault(s => s.Id == script.Id) ?? _scriptListViewModel.Scripts.FirstOrDefault();

        await _loggingService.LogInfoAsync("Recording saved as script", new Dictionary<string, object>
        {
            { "ScriptId", script.Id },
            { "ScriptName", script.Name },
            { "CommandCount", script.CommandCount }
        });

        SaveAsScriptCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveAsScript()
        => !_recordingService.IsRecording && RecordedCommands.Count > 0;

    private void OnCommandRecorded(object? sender, CommandRecordedEventArgs e)
    {
        // Always store clones locally so we don't depend on RecordingService session lifetime.
        var cloned = e.Command.Clone();

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            RecordedCommands.Add(cloned);
            TotalCommands = RecordedCommands.Count;
        });
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateStatusFromService();
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PauseButtonText));
        });
    }

    private void OnRecordingError(object? sender, RecordingErrorEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            RecordingStatusText = $"Error: {e.Error.Message}";
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
        });
    }
}

