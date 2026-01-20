using FsCheck;
using FsCheck.Xunit;
using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroStudio.Tests.Application;

/// <summary>
/// Property-based tests for Execution State Management.
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6**
/// </summary>
public class ExecutionServicePropertyTests
{
    [Property]
    // Feature: macro-studio, Property 4: Execution State Management
    public bool ExecutionStateManagement(NonEmptyString scriptName, PositiveInt commandCount)
    {
        var name = scriptName.Get.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var count = Math.Min(commandCount.Get, 10);

        var logger = NullLogger<ExecutionService>.Instance;
        var inputSimulator = new FakeInputSimulator();
        var hotkeys = new FakeGlobalHotkeyService();
        var safety = new SafetyService(NullLogger<SafetyService>.Instance);
        var service = new ExecutionService(inputSimulator, hotkeys, safety, logger);

        var script = CreateScript(name, count);
        var options = ExecutionOptions.Debug();

        try
        {
            // Start
            service.StartExecutionAsync(script, options).GetAwaiter().GetResult();

            // While running, state should be Running or Completed eventually
            bool finished = WaitUntilCompleted(service, timeoutMs: 2000);
            if (!finished)
                return false;

            // Final state must be Completed
            if (service.State != ExecutionState.Completed)
                return false;

            var stats = service.GetExecutionStatistics();
            if (stats == null)
                return false;

            if (stats.TotalCommands != script.CommandCount)
                return false;
            if (stats.ExecutedCommands != script.CommandCount)
                return false;
            if (stats.RemainingCommands != 0)
                return false;

            // Estimated remaining time after completion should be zero or near-zero
            var remaining = service.GetEstimatedRemainingTime();
            if (remaining.HasValue && remaining.Value > TimeSpan.FromMilliseconds(10))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            service.Dispose();
        }
    }

    private static Script CreateScript(string name, int commandCount)
    {
        var script = new Script(name);
        for (var i = 0; i < commandCount; i++)
        {
            var cmd = new MouseMoveCommand(new Point(i * 10, i * 10))
            {
                Delay = TimeSpan.FromMilliseconds(1)
            };
            script.AddCommand(cmd);
        }
        return script;
    }

    private static bool WaitUntilCompleted(ExecutionService service, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (service.State == ExecutionState.Completed ||
                service.State == ExecutionState.Failed ||
                service.State == ExecutionState.Terminated)
            {
                return true;
            }
            Thread.Sleep(10);
        }
        return false;
    }

    private sealed class FakeInputSimulator : IInputSimulator
    {
        public Task SimulateMouseMoveAsync(Point position) => Task.CompletedTask;

        public Task SimulateMouseClickAsync(Point position, MouseButton button, ClickType type) => Task.CompletedTask;

        public Task SimulateKeyboardInputAsync(string text) => Task.CompletedTask;

        public Task SimulateKeyPressAsync(VirtualKey key, bool isDown) => Task.CompletedTask;

        public Task SimulateKeyComboAsync(IEnumerable<VirtualKey> keys) => Task.CompletedTask;

        public Task DelayAsync(TimeSpan duration) => Task.CompletedTask;

        public Task<Point> GetCursorPositionAsync() => Task.FromResult(new Point(0, 0));

        public Task<bool> IsReadyAsync() => Task.FromResult(true);
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

        public void Raise(HotkeyDefinition hotkey) =>
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkey, DateTime.Now));
    }
}

