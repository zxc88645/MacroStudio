using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.Services;
using MacroStudio.Presentation.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Threading;
using Xunit;

namespace MacroStudio.Tests.Presentation;

public class ExecutionUsesCurrentlyEditedScriptTests
{
    [Fact]
    public void DebugPanel_Start_ShouldExecuteSameScriptAsEditor_IsEditing_AndSyncEditorText()
    {
        RunInSta(() =>
        {
            // Ensure WPF Application exists for Dispatcher + MainWindow access.
            if (global::System.Windows.Application.Current == null)
                _ = new global::System.Windows.Application();

            // Arrange: a script loaded into editor
            var script = new Script("S1");
            script.SourceText = "print('old')";

            // Fake execution service captures what was passed to StartExecutionAsync
            var exec = new CapturingExecutionService();

            var logging = new Mock<ILoggingService>();
            logging.Setup(x => x.LogInfoAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(Task.CompletedTask);
            logging.Setup(x => x.LogErrorAsync(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(Task.CompletedTask);
            logging.Setup(x => x.LogErrorAsync(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(Task.CompletedTask);

            var settings = new Mock<ISettingsService>();
            settings.Setup(x => x.LoadAsync())
                .ReturnsAsync(new AppSettings
                {
                    ShowCountdown = false,
                    CountdownSeconds = 0
                });

            // Script manager and other services (only needed to construct MainViewModel)
            var scriptManager = new Mock<IScriptManager>();
            scriptManager.Setup(x => x.GetAllScriptsAsync()).ReturnsAsync(Array.Empty<Script>());
            scriptManager.Setup(x => x.RegisterAllScriptHotkeysAsync()).Returns(Task.CompletedTask);

            var recordingService = new Mock<IRecordingService>();
            recordingService.SetupGet(x => x.CurrentSession).Returns((RecordingSession?)null);
            recordingService.SetupGet(x => x.IsRecording).Returns(false);

            var safetyService = new Mock<ISafetyService>();
            safetyService.SetupGet(x => x.IsKillSwitchActive).Returns(false);

            var recordingHotkeyHook = new Mock<IRecordingHotkeyHookService>();
            var scriptHotkeyHook = new Mock<IScriptHotkeyHookService>();

            var inputSimulator = new Mock<IInputSimulator>();
            var inputSimulatorFactory = new Mock<IInputSimulatorFactory>();
            inputSimulatorFactory.Setup(x => x.GetInputSimulator(It.IsAny<InputMode>())).Returns(inputSimulator.Object);

            var arduinoConnectionService = new ArduinoConnectionService(new FakeArduinoService(), NullLogger<ArduinoConnectionService>.Instance);
            var scriptListVm = new ScriptListViewModel(scriptManager.Object, logging.Object);
            var commandGridVm = new CommandGridViewModel(scriptManager.Object, logging.Object, inputSimulator.Object);
            var execVm = new ExecutionControlViewModel(exec, logging.Object, settings.Object, arduinoConnectionService);
            var recordingVm = new RecordingViewModel(recordingService.Object, scriptManager.Object, logging.Object, scriptListVm, commandGridVm, arduinoConnectionService, settings.Object);
            var loggingVm = new LoggingViewModel(logging.Object);
            var settingsVm = new SettingsViewModel(settings.Object, recordingHotkeyHook.Object, logging.Object, new LocalizationService(), arduinoConnectionService);
            var debugVm = new DebugViewModel(arduinoConnectionService, logging.Object, inputSimulatorFactory.Object, settings.Object);

            var mainVm = new MainViewModel(
                scriptManager.Object,
                recordingService.Object,
                exec,
                logging.Object,
                safetyService.Object,
                recordingHotkeyHook.Object,
                scriptHotkeyHook.Object,
                scriptListVm,
                commandGridVm,
                execVm,
                recordingVm,
                loggingVm,
                settingsVm,
                debugVm);

            // Hook DataContext so ExecutionControlViewModel can see "currently edited script"
            var wnd = new global::System.Windows.Window { DataContext = mainVm };
            global::System.Windows.Application.Current!.MainWindow = wnd;

            // Make editor load that same script instance
            mainVm.CommandGrid.LoadScript(script);
            mainVm.ExecutionControls.SetScript(script);

            // Simulate user editing the script text (valid, no diagnostic)
            var edited = "print('edited')";
            mainVm.CommandGrid.Document.Text = edited;
            Assert.False(mainVm.CommandGrid.HasDiagnostic);

            // Act: click Debug-panel Start
            mainVm.ExecutionControls.StartCommand.Execute(null);

            // Assert: executed script is the same script instance (or at least same Id),
            // and the SourceText at execution time equals the editor text.
            Assert.NotNull(exec.LastStartedScript);
            Assert.Equal(script.Id, exec.LastStartedScript!.Id);
            Assert.Equal(edited, exec.LastStartedScript.SourceText);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var done = new ManualResetEventSlim(false);

        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
            finally { done.Set(); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();

        done.Wait(TimeSpan.FromSeconds(15));
        if (ex != null) throw new Exception("STA test failed", ex);
    }

    private sealed class CapturingExecutionService : IExecutionService
    {
        public ExecutionState State { get; private set; } = ExecutionState.Idle;
        public Script? CurrentScript { get; private set; }
        public int CurrentCommandIndex { get; private set; }
        public ExecutionSession? CurrentSession { get; private set; }

#pragma warning disable CS0067 // The interface requires these events; tests don't need to raise all of them.
        public event EventHandler<ExecutionProgressEventArgs>? ProgressChanged;
        public event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;
        public event EventHandler<ExecutionErrorEventArgs>? ExecutionError;
        public event EventHandler<ExecutionCompletedEventArgs>? ExecutionCompleted;
#pragma warning restore CS0067

        public Script? LastStartedScript { get; private set; }
        public ExecutionOptions? LastStartedOptions { get; private set; }

        public Task StartExecutionAsync(Script script, ExecutionOptions? options = null)
        {
            LastStartedScript = script;
            LastStartedOptions = options;
            CurrentScript = script;
            CurrentSession = new ExecutionSession(script, options ?? ExecutionOptions.Default());
            State = ExecutionState.Running;
            StateChanged?.Invoke(this, new ExecutionStateChangedEventArgs(ExecutionState.Idle, ExecutionState.Running, CurrentSession.Id, "Test start"));
            return Task.CompletedTask;
        }

        public Task PauseExecutionAsync() => Task.CompletedTask;
        public Task ResumeExecutionAsync() => Task.CompletedTask;
        public Task StopExecutionAsync() => Task.CompletedTask;
        public Task StepExecutionAsync() => Task.CompletedTask;
        public Task TerminateExecutionAsync() => Task.CompletedTask;
        public Task<ExecutionValidationResult> ValidateScriptForExecutionAsync(Script script) => Task.FromResult(ExecutionValidationResult.Success());
        public ExecutionStatistics? GetExecutionStatistics() => null;
        public TimeSpan? GetEstimatedRemainingTime() => null;
    }


    private sealed class FakeArduinoService : IArduinoService
    {
        public ArduinoConnectionState ConnectionState => ArduinoConnectionState.Disconnected;
        public bool IsConnected => false;
        public string? ConnectedPortName => null;

#pragma warning disable CS0067 // Events are required by IArduinoService but not used in this test double.
        public event EventHandler<ArduinoConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<ArduinoEventReceivedEventArgs>? EventReceived;
        public event EventHandler<ArduinoErrorEventArgs>? ErrorOccurred;
#pragma warning restore CS0067

        public Task<IReadOnlyList<string>> GetAvailablePortsAsync() => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task ConnectAsync(string portName) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendCommandAsync(ArduinoCommand command) => Task.CompletedTask;
    }
}

