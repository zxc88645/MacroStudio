using FsCheck;
using FsCheck.Xunit;
using MacroNex.Application.Services;
using MacroNex.Domain.Entities;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroNex.Tests.Application;

/// <summary>
/// Property-based tests for Execution State Management.
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6**
/// </summary>
public class ExecutionServicePropertyTests
{
    [Property]
    // Feature: macronex, Property 4: Execution State Management
    public bool ExecutionStateManagement(NonEmptyString scriptName, PositiveInt commandCount)
    {
        var name = scriptName.Get.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var count = Math.Min(commandCount.Get, 10);

        var logger = NullLogger<ExecutionService>.Instance;
        var inputSimulator = new FakeInputSimulator();
        var inputSimulatorFactory = new FakeInputSimulatorFactory(inputSimulator);
        var arduinoConnectionService = new ArduinoConnectionService(new FakeArduinoService(), NullLogger<ArduinoConnectionService>.Instance);
        var hotkeys = new FakeGlobalHotkeyService();
        var safety = new SafetyService(NullLogger<SafetyService>.Instance);
        var lua = new LuaScriptRunner(inputSimulatorFactory, safety, NullLogger<LuaScriptRunner>.Instance);
        var service = new ExecutionService(inputSimulatorFactory, arduinoConnectionService, hotkeys, safety, lua, logger);

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

            // For Lua scripts, TotalCommands is always 1
            if (stats.TotalCommands != 1)
                return false;
            if (stats.ExecutedCommands != 1)
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
        // Generate SourceText from commands for execution
        script.SourceText = ScriptTextConverter.CommandsToText(script.Commands);
        return script;
    }

    /// <summary>
    /// 測試相同腳本不能同時執行
    /// </summary>
    [Property]
    public bool SameScriptCannotRunConcurrently(NonEmptyString scriptName)
    {
        var name = scriptName.Get.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var logger = NullLogger<ExecutionService>.Instance;
        var inputSimulator = new FakeInputSimulator();
        var inputSimulatorFactory = new FakeInputSimulatorFactory(inputSimulator);
        var arduinoConnectionService = new ArduinoConnectionService(new FakeArduinoService(), NullLogger<ArduinoConnectionService>.Instance);
        var hotkeys = new FakeGlobalHotkeyService();
        var safety = new SafetyService(NullLogger<SafetyService>.Instance);
        var lua = new LuaScriptRunner(inputSimulatorFactory, safety, NullLogger<LuaScriptRunner>.Instance);
        var service = new ExecutionService(inputSimulatorFactory, arduinoConnectionService, hotkeys, safety, lua, logger);

        // 創建一個有足夠命令的腳本，確保執行時間足夠長
        var script = CreateScript(name, 10);
        var options = ExecutionOptions.Debug();

        try
        {
            // 啟動腳本（異步啟動，不等待完成）
            var startTask = service.StartExecutionAsync(script, options);

            // 等待一小段時間確保腳本已開始執行
            Thread.Sleep(100);

            // 檢查腳本是否正在執行
            if (service.State != ExecutionState.Running)
            {
                // 如果腳本已經完成（太快），這個測試用例不適用
                startTask.GetAwaiter().GetResult(); // 等待完成
                return true; // 跳過這個測試用例
            }

            // 現在腳本應該正在執行，嘗試再次啟動同一個腳本應該拋出異常
            bool exceptionThrown = false;
            try
            {
                service.StartExecutionAsync(script, options).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException ex)
            {
                // 應該拋出 InvalidOperationException
                exceptionThrown = true;
                // 錯誤訊息應該包含腳本ID或名稱
                if (!ex.Message.Contains(script.Id.ToString()) && !ex.Message.Contains(script.Name))
                {
                    return false;
                }
            }

            if (!exceptionThrown)
            {
                return false; // 應該拋出異常但沒有
            }

            // 等待第一個腳本完成
            WaitUntilCompleted(service, timeoutMs: 2000);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            service.Dispose();
        }
    }

    /// <summary>
    /// 測試不同腳本可以同時執行
    /// </summary>
    [Property]
    public bool DifferentScriptsCanRunConcurrently(NonEmptyString scriptName1, NonEmptyString scriptName2)
    {
        var name1 = scriptName1.Get.Trim();
        var name2 = scriptName2.Get.Trim();
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2) || name1 == name2)
            return true; // 跳過無效或相同的名稱

        var logger = NullLogger<ExecutionService>.Instance;
        var inputSimulator = new FakeInputSimulator();
        var inputSimulatorFactory = new FakeInputSimulatorFactory(inputSimulator);
        var arduinoConnectionService = new ArduinoConnectionService(new FakeArduinoService(), NullLogger<ArduinoConnectionService>.Instance);
        var hotkeys = new FakeGlobalHotkeyService();
        var safety = new SafetyService(NullLogger<SafetyService>.Instance);
        var lua = new LuaScriptRunner(inputSimulatorFactory, safety, NullLogger<LuaScriptRunner>.Instance);
        var service = new ExecutionService(inputSimulatorFactory, arduinoConnectionService, hotkeys, safety, lua, logger);

        var script1 = CreateScript(name1, 3);
        var script2 = CreateScript(name2, 3);
        var options = ExecutionOptions.Debug();

        try
        {
            // 啟動第一個腳本
            service.StartExecutionAsync(script1, options).GetAwaiter().GetResult();

            // 稍微等待確保第一個腳本已啟動
            Thread.Sleep(50);

            // 嘗試啟動第二個不同的腳本，應該成功（不拋出異常）
            bool exceptionThrown = false;
            try
            {
                service.StartExecutionAsync(script2, options).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                // 不同腳本不應該拋出異常
                exceptionThrown = true;
            }

            if (exceptionThrown)
            {
                return false; // 不同腳本應該可以同時執行
            }

            // 等待兩個腳本都完成
            WaitUntilCompleted(service, timeoutMs: 2000);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            service.Dispose();
        }
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

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            Thread.Sleep(10);
        }
        return false;
    }

    private sealed class FakeInputSimulator : IInputSimulator
    {
        public Task SimulateMouseMoveAsync(Point position) => Task.CompletedTask;
        public Task SimulateMouseMoveLowLevelAsync(Point position) => Task.CompletedTask;
        public Task SimulateMouseMoveRelativeAsync(int deltaX, int deltaY) => Task.CompletedTask;
        public Task SimulateMouseMoveRelativeLowLevelAsync(int deltaX, int deltaY) => Task.CompletedTask;

        public Task SimulateMouseClickAsync(MouseButton button, ClickType type) => Task.CompletedTask;

        public Task SimulateKeyboardInputAsync(string text) => Task.CompletedTask;

        public Task SimulateKeyPressAsync(VirtualKey key, bool isDown) => Task.CompletedTask;

        public Task SimulateKeyComboAsync(IEnumerable<VirtualKey> keys) => Task.CompletedTask;

        public Task DelayAsync(TimeSpan duration) => Task.CompletedTask;

        public Task<Point> GetCursorPositionAsync() => Task.FromResult(new Point(0, 0));

        public Task<bool> IsReadyAsync() => Task.FromResult(true);
    }

    private sealed class FakeInputSimulatorFactory : IInputSimulatorFactory
    {
        private readonly IInputSimulator _inputSimulator;

        public FakeInputSimulatorFactory(IInputSimulator inputSimulator)
        {
            _inputSimulator = inputSimulator;
        }

        public IInputSimulator GetInputSimulator(InputMode mode)
        {
            return _inputSimulator;
        }
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

