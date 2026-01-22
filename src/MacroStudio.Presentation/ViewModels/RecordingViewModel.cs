using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Application.Services;
using MacroStudio.Presentation.Views;
using MacroStudio.Presentation.Utilities;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

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
    private readonly CommandGridViewModel _commandGridViewModel;

    public ObservableCollection<Command> RecordedCommands { get; } = new();

    [ObservableProperty]
    private string recordingStatusText = "";

    [ObservableProperty]
    private bool recordMouseMovements = true;

    [ObservableProperty]
    private bool useLowLevelMouseMove = true;

    [ObservableProperty]
    private bool useRelativeMouseMove = false;

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
        ScriptListViewModel scriptListViewModel,
        CommandGridViewModel commandGridViewModel)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _scriptListViewModel = scriptListViewModel ?? throw new ArgumentNullException(nameof(scriptListViewModel));
        _commandGridViewModel = commandGridViewModel ?? throw new ArgumentNullException(nameof(commandGridViewModel));

        _recordingService.CommandRecorded += OnCommandRecorded;
        _recordingService.RecordingStateChanged += OnRecordingStateChanged;
        _recordingService.RecordingError += OnRecordingError;

        UpdateStatusFromService();
    }

    public bool IsRecording => _recordingService.IsRecording;

    public bool IsPaused => _recordingService.CurrentSession?.State == RecordingState.Paused;

    public string PauseButtonText => IsPaused
        ? UiText.Get("Ui.Recording.Resume", "Resume")
        : UiText.Get("Ui.Recording.Pause", "Pause");

    private void UpdateStatusFromService()
    {
        var session = _recordingService.CurrentSession;
        if (session == null)
        {
            RecordingStatusText = UiText.Get("Ui.Recording.Status.NotRecording", "Not recording");
            return;
        }

        RecordingStatusText = session.State switch
        {
            RecordingState.Active => UiText.Get("Ui.Recording.Status.Recording", "Recording…"),
            RecordingState.Paused => UiText.Get("Ui.Recording.Status.Paused", "Paused"),
            RecordingState.Stopped => UiText.Get("Ui.Recording.Status.Stopped", "Stopped"),
            RecordingState.Error => UiText.Get("Ui.Recording.Status.Error", "Error"),
            _ => UiText.Get("Ui.Recording.Status.NotRecording", "Not recording")
        };
    }

    [RelayCommand(CanExecute = nameof(CanInsertRecordedIntoEditor))]
    private void InsertRecordedIntoEditor()
    {
        try
        {
            if (RecordedCommands.Count == 0)
                return;

            // Convert recorded commands directly to Lua/text and insert at current editor caret.
            var text = ScriptTextConverter.CommandsToText(RecordedCommands);
            _commandGridViewModel.InsertTextAtCaret(text, ensureStandaloneLine: true);
        }
        catch (Exception ex)
        {
            _ = _loggingService.LogErrorAsync("Failed to insert recorded script into editor", ex);
        }
    }

    private bool CanInsertRecordedIntoEditor() => RecordedCommands.Count > 0;

    [RelayCommand]
    private void CopyCurrentScriptText()
    {
        try
        {
            var text = _commandGridViewModel.Document.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return;

            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            _ = _loggingService.LogErrorAsync("Failed to copy script text to clipboard", ex);
        }
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
            InsertRecordedIntoEditorCommand.NotifyCanExecuteChanged();

            var options = new RecordingOptions
            {
                RecordMouseMovements = RecordMouseMovements,
                RecordMouseClicks = RecordMouseClicks,
                RecordKeyboardInput = RecordKeyboardInput,
                FilterSystemEvents = FilterSystemEvents,
                UseLowLevelMouseMove = UseLowLevelMouseMove,
                UseRelativeMouseMove = UseRelativeMouseMove
            };

            await _recordingService.StartRecordingAsync(options);
        }
        catch (Exception ex)
        {
            RecordingStatusText = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Failed to start recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            InsertRecordedIntoEditorCommand.NotifyCanExecuteChanged();
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
            RecordingStatusText = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Failed to stop recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            InsertRecordedIntoEditorCommand.NotifyCanExecuteChanged();
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
            RecordingStatusText = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Failed to pause recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            InsertRecordedIntoEditorCommand.NotifyCanExecuteChanged();
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
            RecordingStatusText = UiText.Format("Ui.ErrorPrefix", ex.Message, "Error: {0}");
            await _loggingService.LogErrorAsync("Failed to resume recording", ex);
        }
        finally
        {
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
            InsertRecordedIntoEditorCommand.NotifyCanExecuteChanged();
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
            UiText.Get("Ui.Dialog.SaveRecording.Title", "Save Recording"),
            UiText.Get("Ui.Dialog.SaveRecording.Subtitle", "Save the recorded commands as a new script."),
            UiText.Get("Ui.Dialog.SaveRecording.Label", "Script name:"),
            UiText.Get("Ui.Dialog.SaveRecording.DefaultName", "Recorded Script"));

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
        
        script.SourceText = ScriptTextConverter.CommandsToText(RecordedCommands);

        await _scriptManager.UpdateScriptAsync(script);

        await _scriptListViewModel.RefreshAsync();
        _scriptListViewModel.SelectedScript = _scriptListViewModel.Scripts.FirstOrDefault(s => s.Id == script.Id) ?? _scriptListViewModel.Scripts.FirstOrDefault();

        await _loggingService.LogInfoAsync("Recording saved as script", new Dictionary<string, object>
        {
            { "ScriptId", script.Id },
            { "ScriptName", script.Name },
            { "CommandCount", RecordedCommands.Count },
            { "SourceTextLength", script.SourceTextLength }
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
            InsertRecordedIntoEditorCommand.NotifyCanExecuteChanged();
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
            RecordingStatusText = UiText.Format("Ui.ErrorPrefix", e.Error.Message, "Error: {0}");
            StartRecordingCommand.NotifyCanExecuteChanged();
            StopRecordingCommand.NotifyCanExecuteChanged();
            PauseRecordingCommand.NotifyCanExecuteChanged();
            ResumeRecordingCommand.NotifyCanExecuteChanged();
            SaveAsScriptCommand.NotifyCanExecuteChanged();
        });
    }
}

