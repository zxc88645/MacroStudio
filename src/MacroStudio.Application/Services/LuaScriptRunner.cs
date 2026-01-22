using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;

namespace MacroStudio.Application.Services;

public sealed class LuaScriptRunner
{
    private readonly IInputSimulator _inputSimulator;
    private readonly ISafetyService _safetyService;
    private readonly ILogger<LuaScriptRunner> _logger;

    public LuaScriptRunner(IInputSimulator inputSimulator, ISafetyService safetyService, ILogger<LuaScriptRunner> logger)
    {
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(string? sourceText, CancellationToken ct, LuaExecutionLimits? limits = null)
    {
        limits ??= LuaExecutionLimits.Default();

        if (_safetyService.IsKillSwitchActive)
            throw new InvalidOperationException("Kill switch is active.");

        var code = sourceText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Script source is empty.");

        // Sandbox: only keep safe core modules.
        var script = new Script(CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.Math);

        script.Options.DebugPrint = s => _logger.LogInformation("[lua] {Text}", s);

        // Enforce step limits & cancellation through a minimal debugger implementation.
        script.DebuggerEnabled = true;
        script.AttachDebugger(new LimiterDebugger(_safetyService, ct, limits));

        // Host API
        script.Globals["sleep"] = (Func<double, DynValue>)(seconds =>
        {
            DelayWithCancellation(TimeSpan.FromSeconds(seconds), ct).GetAwaiter().GetResult();
            return DynValue.Nil;
        });

        script.Globals["msleep"] = (Func<double, DynValue>)(ms =>
        {
            DelayWithCancellation(TimeSpan.FromMilliseconds(ms), ct).GetAwaiter().GetResult();
            return DynValue.Nil;
        });

        script.Globals["move"] = (Action<int, int>)((x, y) =>
        {
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseMoveAsync(new Point(x, y)).GetAwaiter().GetResult();
        });

        script.Globals["move_ll"] = (Action<int, int>)((x, y) =>
        {
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseMoveLowLevelAsync(new Point(x, y)).GetAwaiter().GetResult();
        });

        script.Globals["move_rel"] = (Action<int, int>)((dx, dy) =>
        {
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseMoveRelativeAsync(dx, dy).GetAwaiter().GetResult();
        });

        script.Globals["move_rel_ll"] = (Action<int, int>)((dx, dy) =>
        {
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseMoveRelativeLowLevelAsync(dx, dy).GetAwaiter().GetResult();
        });

        script.Globals["type_text"] = (Action<string>)(text =>
        {
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateKeyboardInputAsync(text ?? string.Empty).GetAwaiter().GetResult();
        });

        script.Globals["mouse_click"] = (Action<string>)(button =>
        {
            var b = ParseMouseButton(button);
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseClickAsync(b, ClickType.Click).GetAwaiter().GetResult();
        });

        script.Globals["mouse_down"] = (Action<string>)(button =>
        {
            var b = ParseMouseButton(button);
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseClickAsync(b, ClickType.Down).GetAwaiter().GetResult();
        });

        script.Globals["mouse_release"] = (Action<string>)(button =>
        {
            var b = ParseMouseButton(button);
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateMouseClickAsync(b, ClickType.Up).GetAwaiter().GetResult();
        });

        script.Globals["key_down"] = (Action<string>)(key =>
        {
            var vk = ParseVirtualKey(key);
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateKeyPressAsync(vk, true).GetAwaiter().GetResult();
        });

        script.Globals["key_release"] = (Action<string>)(key =>
        {
            var vk = ParseVirtualKey(key);
            ct.ThrowIfCancellationRequested();
            _inputSimulator.SimulateKeyPressAsync(vk, false).GetAwaiter().GetResult();
        });

        // Execute: run on threadpool so the caller can await without blocking UI.
        await Task.Run(() =>
        {
            var chunk = script.LoadString(code);
            script.Call(chunk);
        }, ct);
    }

    private static async Task DelayWithCancellation(TimeSpan duration, CancellationToken ct)
    {
        if (duration <= TimeSpan.Zero)
            return;

        // Use small slices so cancellation is responsive.
        var remaining = duration;
        var slice = TimeSpan.FromMilliseconds(50);

        while (remaining > TimeSpan.Zero)
        {
            ct.ThrowIfCancellationRequested();
            var current = remaining < slice ? remaining : slice;
            await Task.Delay(current, ct);
            remaining -= current;
        }
    }

    private static MouseButton ParseMouseButton(string? button)
    {
        var arg = (button ?? string.Empty).Trim().ToLowerInvariant();
        return arg switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => throw new ScriptRuntimeException($"unsupported mouse button '{arg}'")
        };
    }

    private static VirtualKey ParseVirtualKey(string? key)
    {
        var k = (key ?? string.Empty).Trim();
        if (k.Length == 0)
            throw new ScriptRuntimeException("key cannot be empty");

        // Accept single letters/digits as convenience (e.g., 'a', '1').
        if (k.Length == 1)
        {
            var c = k[0];

            if (char.IsLetter(c))
            {
                var upper = char.ToUpperInvariant(c);
                var name = $"VK_{upper}";
                if (Enum.TryParse<VirtualKey>(name, out var vk))
                    return vk;
            }

            if (char.IsDigit(c))
            {
                var name = $"VK_{c}";
                if (Enum.TryParse<VirtualKey>(name, out var vk))
                    return vk;
            }
        }

        // Fallback: try raw enum name (case-insensitive).
        if (Enum.TryParse<VirtualKey>(k, true, out var parsed))
            return parsed;

        throw new ScriptRuntimeException($"unsupported key '{k}'");
    }

    private sealed class LimiterDebugger : IDebugger
    {
        private readonly ISafetyService _safetyService;
        private readonly CancellationToken _ct;
        private readonly LuaExecutionLimits _limits;
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private long _steps;

        public LimiterDebugger(ISafetyService safetyService, CancellationToken ct, LuaExecutionLimits limits)
        {
            _safetyService = safetyService;
            _ct = ct;
            _limits = limits;
        }

        public DebuggerCaps GetDebuggerCaps() => (DebuggerCaps)0;
        public void SetDebugService(DebugService debugService) { }
        public void SetSourceCode(SourceCode sourceCode) { }
        public void SetByteCode(string[] byteCode) { }

        public bool IsPauseRequested()
        {
            // Called very frequently; keep it minimal.
            return false;
        }

        public bool SignalRuntimeException(ScriptRuntimeException ex) => false;

        public DebuggerAction GetAction(int ip, SourceRef sourceref)
        {
            _steps++;

            if (_ct.IsCancellationRequested)
                throw new ScriptRuntimeException("cancelled");

            if (_safetyService.IsKillSwitchActive)
                throw new ScriptRuntimeException("kill_switch");

            if (_limits.MaxSteps > 0 && _steps > _limits.MaxSteps)
                throw new ScriptRuntimeException("step_limit");

            if (_limits.MaxExecutionTime > TimeSpan.Zero && DateTime.UtcNow - _startedUtc > _limits.MaxExecutionTime)
                throw new ScriptRuntimeException("time_limit");

            return new DebuggerAction { Action = DebuggerAction.ActionType.Run };
        }

        public void SignalExecutionEnded() { }
        public void Update(WatchType watchType, IEnumerable<WatchItem> items) { }
        public List<DynamicExpression> GetWatchItems() => new();
        public void RefreshBreakpoints(IEnumerable<SourceRef> refs) { }
    }
}

public sealed class LuaExecutionLimits
{
    public long MaxSteps { get; init; } = 200_000;
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromMinutes(2);

    public static LuaExecutionLimits Default() => new();
}

