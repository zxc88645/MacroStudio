using FsCheck;
using FsCheck.Xunit;
using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroStudio.Tests.Application;

public class UiResponsivenessPropertyTests
{
    [Property]
    // Feature: macro-studio, Property 8: UI Responsiveness and Functionality
    public bool ExecutionService_EventHandlersThrowing_AreSwallowed(NonEmptyString scriptName)
    {
        var name = scriptName.Get.Trim();
        if (string.IsNullOrWhiteSpace(name)) return true;

        var input = new FakeInputSimulator();
        var hotkeys = new FakeGlobalHotkeyService();
        var safety = new SafetyService(NullLogger<SafetyService>.Instance);
        var exec = new ExecutionService(input, hotkeys, safety, NullLogger<ExecutionService>.Instance);

        // attach handlers that throw - service should swallow in its raise methods
        exec.StateChanged += (_, _) => throw new InvalidOperationException("boom");
        exec.ProgressChanged += (_, _) => throw new InvalidOperationException("boom");
        exec.ExecutionError += (_, _) => throw new InvalidOperationException("boom");
        exec.ExecutionCompleted += (_, _) => throw new InvalidOperationException("boom");
        exec.CommandExecuting += (_, _) => throw new InvalidOperationException("boom");
        exec.CommandExecuted += (_, _) => throw new InvalidOperationException("boom");

        var script = new Script(name);
        script.AddCommand(new SleepCommand(TimeSpan.FromMilliseconds(1)));

        // If any handler exception leaks out, the test fails.
        exec.StartExecutionAsync(script, ExecutionOptions.Debug()).GetAwaiter().GetResult();
        WaitUntil(() => exec.State is ExecutionState.Completed or ExecutionState.Failed, timeoutMs: 2000);

        return true;
    }

    [Property]
    // Feature: macro-studio, Property 8: UI Responsiveness and Functionality
    public bool LoggingService_LogEntryCreatedHandlerThrowing_DoesNotBreakLogging(NonEmptyString message)
    {
        var msg = message.Get.Trim();
        if (string.IsNullOrWhiteSpace(msg)) return true;

        var service = new LoggingService(NullLogger<LoggingService>.Instance, new FakeFileLogWriter());

        service.LogEntryCreated += (_, _) => throw new InvalidOperationException("boom");

        // Should not throw even if event subscriber throws.
        service.LogInfoAsync(msg).GetAwaiter().GetResult();
        return true;
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var start = Environment.TickCount;
        while (Environment.TickCount - start < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return false;
    }

    private sealed class FakeFileLogWriter : IFileLogWriter
    {
        public Task WriteLogEntryAsync(LogEntry entry) => Task.CompletedTask;
        public Task ClearLogsAsync() => Task.CompletedTask;
        public Task ExportLogsAsync(string filePath, IEnumerable<LogEntry> entries) => Task.CompletedTask;
    }

    private sealed class FakeGlobalHotkeyService : IGlobalHotkeyService
    {
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        public Task RegisterHotkeyAsync(HotkeyDefinition hotkey) => Task.CompletedTask;
        public Task UnregisterHotkeyAsync(HotkeyDefinition hotkey) => Task.CompletedTask;
        public Task UnregisterAllHotkeysAsync() => Task.CompletedTask;
        public Task<IEnumerable<HotkeyDefinition>> GetRegisteredHotkeysAsync() =>
            Task.FromResult<IEnumerable<HotkeyDefinition>>(Array.Empty<HotkeyDefinition>());
        public Task<bool> IsHotkeyRegisteredAsync(HotkeyDefinition hotkey) => Task.FromResult(false);
        public Task<bool> IsReadyAsync() => Task.FromResult(true);
    }

    private sealed class FakeInputSimulator : IInputSimulator
    {
        public Task SimulateMouseMoveAsync(Point position) => Task.CompletedTask;
        public Task SimulateMouseClickAsync(Point position, MouseButton button, ClickType clickType) => Task.CompletedTask;
        public Task SimulateKeyboardInputAsync(string text) => Task.CompletedTask;
        public Task SimulateKeyPressAsync(VirtualKey key, bool isKeyDown) => Task.CompletedTask;
        public Task SimulateKeyComboAsync(IEnumerable<VirtualKey> keys) => Task.CompletedTask;
        public Task DelayAsync(TimeSpan delay) => Task.CompletedTask;
        public Task<Point> GetCursorPositionAsync() => Task.FromResult(Point.Zero);
        public Task<bool> IsReadyAsync() => Task.FromResult(true);
    }
}

