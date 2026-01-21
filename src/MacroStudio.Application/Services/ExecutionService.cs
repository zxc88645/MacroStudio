using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Events;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroStudio.Application.Services;

/// <summary>
/// Application service for executing automation scripts.
/// Provides start/pause/resume/stop/step/terminate controls and progress events.
/// </summary>
public sealed class ExecutionService : IExecutionService, IDisposable
{
    private readonly IInputSimulator _inputSimulator;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ISafetyService _safetyService;
    private readonly LuaScriptRunner _luaRunner;
    private readonly ILogger<ExecutionService> _logger;
    private readonly object _lockObject = new();

    // 追蹤每個腳本的執行狀態（以腳本ID為鍵）
    private readonly Dictionary<Guid, ScriptExecutionContext> _activeExecutions = new();

    // 向後兼容：保留最後啟動的腳本作為"當前"腳本
    private CancellationTokenSource? _cts;
    private Task? _executionTask;
    private readonly ManualResetEventSlim _pauseEvent = new(true);

    private ExecutionSession? _session;
    private Script? _currentScript;
    private int _currentCommandIndex;
    private ExecutionState _state = ExecutionState.Idle;

    /// <summary>
    /// 內部類別：追蹤單個腳本的執行上下文
    /// </summary>
    private sealed class ScriptExecutionContext
    {
        public Script Script { get; }
        public ExecutionSession Session { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public Task ExecutionTask { get; set; } = null!;
        public ManualResetEventSlim PauseEvent { get; }
        public int CurrentCommandIndex { get; set; }
        public ExecutionState State { get; set; }

        public ScriptExecutionContext(Script script, ExecutionSession session)
        {
            Script = script;
            Session = session;
            CancellationTokenSource = new CancellationTokenSource();
            PauseEvent = new ManualResetEventSlim(true);
            CurrentCommandIndex = 0;
            State = ExecutionState.Running;
        }

        public void Dispose()
        {
            try { CancellationTokenSource?.Cancel(); } catch { }
            try { PauseEvent?.Set(); } catch { }
            try { CancellationTokenSource?.Dispose(); } catch { }
            try { PauseEvent?.Dispose(); } catch { }
        }
    }

    public ExecutionService(IInputSimulator inputSimulator, IGlobalHotkeyService globalHotkeyService, ISafetyService safetyService, LuaScriptRunner luaRunner, ILogger<ExecutionService> logger)
    {
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _globalHotkeyService = globalHotkeyService ?? throw new ArgumentNullException(nameof(globalHotkeyService));
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _luaRunner = luaRunner ?? throw new ArgumentNullException(nameof(luaRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ExecutionState State
    {
        get { lock (_lockObject) return _state; }
        private set { lock (_lockObject) _state = value; }
    }

    public Script? CurrentScript
    {
        get { lock (_lockObject) return _currentScript; }
        private set { lock (_lockObject) _currentScript = value; }
    }

    public int CurrentCommandIndex
    {
        get { lock (_lockObject) return _currentCommandIndex; }
        private set { lock (_lockObject) _currentCommandIndex = value; }
    }

    public ExecutionSession? CurrentSession
    {
        get { lock (_lockObject) return _session; }
        private set { lock (_lockObject) _session = value; }
    }

    public event EventHandler<ExecutionProgressEventArgs>? ProgressChanged;
    public event EventHandler<ExecutionStateChangedEventArgs>? StateChanged;
    public event EventHandler<CommandExecutingEventArgs>? CommandExecuting;
    public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
    public event EventHandler<ExecutionErrorEventArgs>? ExecutionError;
    public event EventHandler<ExecutionCompletedEventArgs>? ExecutionCompleted;

    public async Task StartExecutionAsync(Script script, ExecutionOptions? options = null)
    {
        if (script == null) throw new ArgumentNullException(nameof(script));
        options ??= ExecutionOptions.Default();

        // 檢查該腳本是否已經在執行中
        lock (_lockObject)
        {
            if (_activeExecutions.TryGetValue(script.Id, out var existingContext))
            {
                if (existingContext.ExecutionTask is { IsCompleted: false })
                {
                    throw new InvalidOperationException($"Script '{script.Name}' (ID: {script.Id}) is already executing.");
                }
                else
                {
                    // 任務已完成但未清理，先清理
                    existingContext.Dispose();
                    _activeExecutions.Remove(script.Id);
                }
            }
        }

        // Validate script synchronously (fast, no I/O)
        var validation = ValidateScriptForExecutionAsync(script).GetAwaiter().GetResult();
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors);
            throw new InvalidOperationException($"Script is not valid for execution: {errors}");
        }

        // Log asynchronously (fire and forget)
        _ = Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Starting execution for script {ScriptId} ({ScriptName})", script.Id, script.Name);
            }
            catch { /* Ignore logging errors */ }
        });

        var session = new ExecutionSession(script, options);
        var context = new ScriptExecutionContext(script, session);

        lock (_lockObject)
        {
            _activeExecutions[script.Id] = context;
            
            // 向後兼容：更新"當前"腳本為最後啟動的腳本
            _cts = context.CancellationTokenSource;
            _pauseEvent.Set(); // 保持向後兼容
            CurrentScript = script;
            CurrentCommandIndex = 0;
            CurrentSession = session;
            State = ExecutionState.Running;
        }


        RaiseStateChanged(ExecutionState.Idle, ExecutionState.Running, session.Id, "Execution started");

        // 啟動執行任務
        context.ExecutionTask = Task.Run(async () => await ExecutionLoopAsync(context), context.CancellationTokenSource.Token);
        
        // 向後兼容：更新單一執行任務
        lock (_lockObject)
        {
            _executionTask = context.ExecutionTask;
        }
    }

    public Task PauseExecutionAsync()
    {
        ScriptExecutionContext? context;
        lock (_lockObject)
        {
            if (CurrentScript == null || !_activeExecutions.TryGetValue(CurrentScript.Id, out context))
            {
                throw new InvalidOperationException("No script is currently executing.");
            }

            if (context.State != ExecutionState.Running)
            {
                throw new InvalidOperationException("Execution is not running.");
            }
        }

        context.PauseEvent.Reset();
        var prev = context.State;
        context.State = ExecutionState.Paused;
        context.Session.ChangeState(ExecutionState.Paused);
        
        lock (_lockObject)
        {
            State = ExecutionState.Paused;
        }
        
        RaiseStateChanged(prev, ExecutionState.Paused, context.Session.Id, "Paused");
        return Task.CompletedTask;
    }

    public Task ResumeExecutionAsync()
    {
        ScriptExecutionContext? context;
        lock (_lockObject)
        {
            if (CurrentScript == null || !_activeExecutions.TryGetValue(CurrentScript.Id, out context))
            {
                throw new InvalidOperationException("No script is currently paused.");
            }

            if (context.State != ExecutionState.Paused)
            {
                throw new InvalidOperationException("Execution is not paused.");
            }
        }

        context.PauseEvent.Set();
        var prev = context.State;
        context.State = ExecutionState.Running;
        context.Session.ChangeState(ExecutionState.Running);
        
        lock (_lockObject)
        {
            State = ExecutionState.Running;
        }
        
        RaiseStateChanged(prev, ExecutionState.Running, context.Session.Id, "Resumed");
        return Task.CompletedTask;
    }

    public Task StopExecutionAsync()
    {
        ScriptExecutionContext? context;
        lock (_lockObject)
        {
            if (CurrentScript == null || !_activeExecutions.TryGetValue(CurrentScript.Id, out context))
            {
                State = ExecutionState.Stopped;
                return Task.CompletedTask;
            }
        }

        _logger.LogInformation("Stopping execution (session {SessionId}, script {ScriptId})", context.Session.Id, context.Script.Id);
        context.CancellationTokenSource.Cancel();
        context.PauseEvent.Set();

        var prev = context.State;
        context.State = ExecutionState.Stopped;
        context.Session.ChangeState(ExecutionState.Stopped);
        
        lock (_lockObject)
        {
            State = ExecutionState.Stopped;
            CurrentCommandIndex = 0;
        }
        
        RaiseStateChanged(prev, ExecutionState.Stopped, context.Session.Id, "Stopped");
        return Task.CompletedTask;
    }

    public async Task StepExecutionAsync()
    {
        ScriptExecutionContext? context;
        lock (_lockObject)
        {
            if (CurrentScript == null)
                throw new InvalidOperationException("No script is loaded.");

            // 如果腳本沒有在執行，創建一個臨時上下文用於單步執行
            if (!_activeExecutions.TryGetValue(CurrentScript.Id, out context))
            {
                // 創建臨時上下文用於單步執行
                var session = new ExecutionSession(CurrentScript, ExecutionOptions.Default());
                context = new ScriptExecutionContext(CurrentScript, session);
                _activeExecutions[CurrentScript.Id] = context;
                CurrentSession = session;
            }

            if (context.State == ExecutionState.Running)
                throw new InvalidOperationException("Cannot step while execution is running.");
        }

        var prev = context.State;
        context.State = ExecutionState.Stepping;
        context.Session.ChangeState(ExecutionState.Stepping);
        
        lock (_lockObject)
        {
            State = ExecutionState.Stepping;
        }
        
        RaiseStateChanged(prev, ExecutionState.Stepping, context.Session.Id, "Step");

        await ExecuteSingleCommandAsync(context, context.CurrentCommandIndex, CancellationToken.None);

        // After one step, pause
        prev = context.State;
        context.State = ExecutionState.Paused;
        context.Session.ChangeState(ExecutionState.Paused);
        
        lock (_lockObject)
        {
            State = ExecutionState.Paused;
        }
        
        RaiseStateChanged(prev, ExecutionState.Paused, context.Session.Id, "Paused after step");
    }

    public Task TerminateExecutionAsync()
    {
        _logger.LogWarning("Terminate requested for all executions");

        // 終止所有正在執行的腳本
        List<ScriptExecutionContext> contextsToTerminate;
        lock (_lockObject)
        {
            contextsToTerminate = _activeExecutions.Values.ToList();
        }

        foreach (var context in contextsToTerminate)
        {
            try
            {
                context.CancellationTokenSource.Cancel();
                context.PauseEvent.Set();
                
                var prev = context.State;
                context.State = ExecutionState.Terminated;
                context.Session.ChangeState(ExecutionState.Terminated);
                RaiseStateChanged(prev, ExecutionState.Terminated, context.Session.Id, "Terminated");
                RaiseCompleted(context.Session, ExecutionState.Terminated, false, "Terminated by user/kill switch", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating execution for script {ScriptId}", context.Script.Id);
            }
        }

        // 向後兼容：更新當前狀態
        lock (_lockObject)
        {
            if (CurrentSession != null)
            {
                State = ExecutionState.Terminated;
            }
        }

        return Task.CompletedTask;
    }

    public Task<ExecutionValidationResult> ValidateScriptForExecutionAsync(Script script)
    {
        if (script == null) throw new ArgumentNullException(nameof(script));

        var errors = new List<string>();
        var warnings = new List<string>();
        var dangerous = new List<string>();

        if (string.IsNullOrWhiteSpace(script.Name))
            errors.Add("Script name is empty.");

        if (script.CommandCount == 0)
            errors.Add("Script has no commands.");

        for (var i = 0; i < script.CommandCount; i++)
        {
            if (!script.Commands[i].IsValid())
                errors.Add($"Command at index {i} is invalid: {script.Commands[i]}");
        }

        // Basic dangerous heuristics (placeholder for ISafetyService later)
        if (script.CommandCount > 10000)
            warnings.Add("Large script; execution may be slow.");

        return Task.FromResult(errors.Count > 0
            ? ExecutionValidationResult.Failure(errors, warnings, dangerous)
            : ExecutionValidationResult.Success(warnings, dangerous));
    }

    public ExecutionStatistics? GetExecutionStatistics()
    {
        var session = CurrentSession;
        if (session == null || CurrentScript == null) return null;

        return new ExecutionStatistics
        {
            TotalCommands = CurrentScript.CommandCount,
            ExecutedCommands = session.ExecutedCommandCount,
            ElapsedTime = session.ElapsedTime,
            SpeedMultiplier = session.Options.SpeedMultiplier
        };
    }

    public TimeSpan? GetEstimatedRemainingTime()
    {
        ScriptExecutionContext? context;
        lock (_lockObject)
        {
            if (CurrentScript == null)
            {
                return null;
            }

            // 如果腳本仍在執行中，使用執行上下文
            if (_activeExecutions.TryGetValue(CurrentScript.Id, out context))
            {
                return CalculateRemainingTime(context);
            }

            // 如果執行已完成，使用 CurrentSession 計算剩餘時間
            if (CurrentSession != null && CurrentScript != null)
            {
                var script = CurrentScript;
                var idx = CurrentCommandIndex;
                if (idx < 0 || idx >= script.CommandCount) return null;

                var remaining = TimeSpan.Zero;
                for (var i = idx; i < script.CommandCount; i++)
                {
                    remaining = remaining.Add(script.Commands[i].Delay);
                    if (script.Commands[i] is SleepCommand s)
                        remaining = remaining.Add(s.Duration);
                }

                var speed = CurrentSession.Options.SpeedMultiplier <= 0 ? 1.0 : CurrentSession.Options.SpeedMultiplier;
                if (speed <= 0) speed = 1.0;

                return TimeSpan.FromMilliseconds(remaining.TotalMilliseconds / speed);
            }

            return null;
        }
    }

    private async Task ExecutionLoopAsync(ScriptExecutionContext context)
    {
        var session = context.Session;
        var script = session.Script;
        var ct = context.CancellationTokenSource.Token;

        try
        {
            if (_safetyService.IsKillSwitchActive)
                throw new InvalidOperationException("Kill switch is active.");

            // Safety limits
            if (script.CommandCount > session.Options.MaxCommandCount)
                throw new InvalidOperationException($"Script exceeds max command count limit ({session.Options.MaxCommandCount}).");

            var started = DateTime.UtcNow;

            // Optional countdown warning
            if (session.Options.ShowCountdown && session.Options.CountdownDuration > TimeSpan.Zero)
            {
                await _inputSimulator.DelayAsync(session.Options.CountdownDuration);
            }

            // Lua-only execution: run SourceText. For scripts created via recording,
            // SourceText might be empty; we fall back to the old command list as a
            // sequence of Lua function calls (which is valid Lua).
            var source = script.SourceText;
            if (string.IsNullOrWhiteSpace(source))
            {
                source = ScriptTextConverter.ToText(script);
            }

            ct.ThrowIfCancellationRequested();
            context.PauseEvent.Wait(ct);

            if (_safetyService.IsKillSwitchActive)
                throw new InvalidOperationException("Kill switch is active.");

            // Safety: max execution time (coarse guard; Lua runner also enforces limits)
            if (DateTime.UtcNow - started > session.Options.MaxExecutionTime)
                throw new InvalidOperationException("Execution time limit exceeded.");

            await _luaRunner.RunAsync(source, ct);

            lock (_lockObject)
            {
                context.CurrentCommandIndex = script.CommandCount;
                session.UpdateProgress(context.CurrentCommandIndex);
                context.State = ExecutionState.Completed;
                // 如果這是當前腳本，更新狀態
                if (CurrentScript?.Id == script.Id)
                {
                    State = ExecutionState.Completed;
                    CurrentCommandIndex = script.CommandCount; // best-effort for legacy UI
                }
            }

            session.ChangeState(ExecutionState.Completed);
            RaiseStateChanged(ExecutionState.Running, ExecutionState.Completed, session.Id, "Completed");
            RaiseCompleted(session, ExecutionState.Completed, true, "Completed successfully", null);
        }
        catch (OperationCanceledException)
        {
            // Stop/Terminate paths handle events/state separately.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution failed (session {SessionId}, script {ScriptId})", session.Id, script.Id);
            session.SetError(ex);

            lock (_lockObject)
            {
                context.State = ExecutionState.Failed;
                // 如果這是當前腳本，更新狀態
                if (CurrentScript?.Id == script.Id)
                {
                    State = ExecutionState.Failed;
                }
            }

            RaiseStateChanged(ExecutionState.Running, ExecutionState.Failed, session.Id, "Failed");
            RaiseExecutionError(ex, session.Id, "Execution loop", canContinue: false);
            RaiseCompleted(session, ExecutionState.Failed, false, "Execution failed", ex);
        }
        finally
        {
            // 清理該腳本的執行狀態
            lock (_lockObject)
            {
                _activeExecutions.Remove(script.Id);
                
                // 如果這是當前腳本，且狀態是終止或失敗，清理當前狀態
                // 但如果是 Completed，保留狀態以便查詢統計信息
                if (CurrentScript?.Id == script.Id)
                {
                    if (context.State == ExecutionState.Terminated || 
                        context.State == ExecutionState.Failed ||
                        context.State == ExecutionState.Stopped)
                    {
                        CurrentScript = null;
                        CurrentSession = null;
                        CurrentCommandIndex = 0;
                        if (State == ExecutionState.Running || State == ExecutionState.Paused)
                        {
                            State = ExecutionState.Idle;
                        }
                    }
                    // Completed 狀態保留 CurrentScript 和 CurrentSession，以便查詢統計信息
                    // 這些將在下一次執行時被覆蓋，或在 Dispose 時清理
                }
            }

            // 清理資源
            context.Dispose();
        }
    }

    private async Task ExecuteSingleCommandAsync(ScriptExecutionContext context, int index, CancellationToken ct)
    {
        var session = context.Session;
        var script = session.Script;
        if (index < 0 || index >= script.CommandCount)
            return;

        var command = script.Commands[index];
        var speed = session.Options.SpeedMultiplier <= 0 ? 1.0 : session.Options.SpeedMultiplier;

        var delayBefore = TimeSpan.FromMilliseconds(command.Delay.TotalMilliseconds / speed);
        if (delayBefore > TimeSpan.Zero)
            await _inputSimulator.DelayAsync(delayBefore);

        var executingArgs = new CommandExecutingEventArgs(command, session.Id, index);
        CommandExecuting?.Invoke(this, executingArgs);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();

            switch (command)
            {
                case MouseMoveCommand mm:
                    await _inputSimulator.SimulateMouseMoveAsync(mm.Position);
                    break;
                case MouseClickCommand mc:
                    await _inputSimulator.SimulateMouseClickAsync(mc.Button, mc.Type);
                    break;
                case KeyPressCommand kp:
                    await _inputSimulator.SimulateKeyPressAsync(kp.Key, kp.IsDown);
                    break;
                case KeyboardCommand kc:
                    if (!string.IsNullOrEmpty(kc.Text))
                        await _inputSimulator.SimulateKeyboardInputAsync(kc.Text);
                    else
                    {
                        foreach (var key in kc.Keys)
                        {
                            await _inputSimulator.SimulateKeyPressAsync(key, true);
                            await _inputSimulator.SimulateKeyPressAsync(key, false);
                        }
                    }
                    break;
                case SleepCommand sc:
                    var sleep = TimeSpan.FromMilliseconds(sc.Duration.TotalMilliseconds / speed);
                    await _inputSimulator.DelayAsync(sleep);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported command type: {command.GetType().Name}");
            }

            sw.Stop();

            lock (_lockObject)
            {
                context.CurrentCommandIndex = index + 1;
                session.UpdateProgress(context.CurrentCommandIndex);
                
                // 如果這是當前腳本，更新當前命令索引
                if (CurrentScript?.Id == script.Id)
                {
                    CurrentCommandIndex = context.CurrentCommandIndex;
                }
            }

            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, session.Id, index, true, sw.Elapsed));
            
            // 計算剩餘時間（僅針對該腳本）
            var remainingTime = CalculateRemainingTime(context);
            ProgressChanged?.Invoke(this, new ExecutionProgressEventArgs(session.Id, context.CurrentCommandIndex, script.CommandCount, session.ElapsedTime, remainingTime));
        }
        catch (Exception ex)
        {
            sw.Stop();
            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, session.Id, index, false, sw.Elapsed, ex));
            throw;
        }
    }

    private TimeSpan? CalculateRemainingTime(ScriptExecutionContext context)
    {
        var script = context.Script;
        var idx = context.CurrentCommandIndex;
        if (idx < 0 || idx >= script.CommandCount) return null;

        var remaining = TimeSpan.Zero;
        for (var i = idx; i < script.CommandCount; i++)
        {
            remaining = remaining.Add(script.Commands[i].Delay);
            if (script.Commands[i] is SleepCommand s)
                remaining = remaining.Add(s.Duration);
        }

        var speed = context.Session.Options.SpeedMultiplier <= 0 ? 1.0 : context.Session.Options.SpeedMultiplier;
        if (speed <= 0) speed = 1.0;

        return TimeSpan.FromMilliseconds(remaining.TotalMilliseconds / speed);
    }

    private void RaiseStateChanged(ExecutionState previous, ExecutionState current, Guid sessionId, string? reason)
    {
        try { StateChanged?.Invoke(this, new ExecutionStateChangedEventArgs(previous, current, sessionId, reason)); }
        catch { }
    }

    private void RaiseExecutionError(Exception error, Guid sessionId, string context, bool canContinue)
    {
        try { ExecutionError?.Invoke(this, new ExecutionErrorEventArgs(error, sessionId, context, canContinue)); }
        catch { }
    }

    private void RaiseCompleted(ExecutionSession session, ExecutionState finalState, bool success, string reason, Exception? error)
    {
        try
        {
            ExecutionCompleted?.Invoke(this, new ExecutionCompletedEventArgs(
                session.Id,
                finalState,
                session.ExecutedCommandCount,
                session.Script.CommandCount,
                session.ElapsedTime,
                success,
                reason,
                error));
        }
        catch { }
    }

    public void Dispose()
    {
        // 終止所有正在執行的腳本
        List<ScriptExecutionContext> contextsToDispose;
        lock (_lockObject)
        {
            contextsToDispose = _activeExecutions.Values.ToList();
            _activeExecutions.Clear();
        }

        foreach (var context in contextsToDispose)
        {
            try { context.Dispose(); } catch { }
        }

        try { _cts?.Cancel(); } catch { }
        try { _pauseEvent.Set(); } catch { }
        _pauseEvent.Dispose();
    }
}

