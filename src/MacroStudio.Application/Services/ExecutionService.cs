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
    private readonly ILogger<ExecutionService> _logger;
    private readonly object _lockObject = new();

    private CancellationTokenSource? _cts;
    private Task? _executionTask;
    private readonly ManualResetEventSlim _pauseEvent = new(true);

    private ExecutionSession? _session;
    private Script? _currentScript;
    private int _currentCommandIndex;
    private ExecutionState _state = ExecutionState.Idle;

    private readonly HotkeyDefinition _killSwitchHotkey =
        HotkeyDefinition.Create("Kill Switch", HotkeyModifiers.Control | HotkeyModifiers.Shift, VirtualKey.VK_ESCAPE);

    public ExecutionService(IInputSimulator inputSimulator, IGlobalHotkeyService globalHotkeyService, ISafetyService safetyService, ILogger<ExecutionService> logger)
    {
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
        _globalHotkeyService = globalHotkeyService ?? throw new ArgumentNullException(nameof(globalHotkeyService));
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _globalHotkeyService.HotkeyPressed += OnHotkeyPressed;
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

        lock (_lockObject)
        {
            if (_executionTask is { IsCompleted: false })
                throw new InvalidOperationException("Execution is already active.");
        }

        var validation = await ValidateScriptForExecutionAsync(script);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors);
            throw new InvalidOperationException($"Script is not valid for execution: {errors}");
        }

        _logger.LogInformation("Starting execution for script {ScriptId} ({ScriptName})", script.Id, script.Name);

        _cts = new CancellationTokenSource();
        _pauseEvent.Set();

        CurrentScript = script;
        CurrentCommandIndex = 0;
        var session = new ExecutionSession(script, options);
        CurrentSession = session;

        // Register kill switch hotkey (best-effort)
        try
        {
            if (await _globalHotkeyService.IsReadyAsync())
            {
                await _globalHotkeyService.RegisterHotkeyAsync(_killSwitchHotkey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register kill switch hotkey");
        }

        var prev = State;
        State = ExecutionState.Running;
        RaiseStateChanged(prev, State, session.Id, "Execution started");

        _executionTask = Task.Run(() => ExecutionLoopAsync(session, _cts.Token), _cts.Token);
    }

    public Task PauseExecutionAsync()
    {
        var session = CurrentSession;
        if (session == null || State != ExecutionState.Running)
            throw new InvalidOperationException("Execution is not running.");

        _pauseEvent.Reset();
        var prev = State;
        State = ExecutionState.Paused;
        session.ChangeState(ExecutionState.Paused);
        RaiseStateChanged(prev, State, session.Id, "Paused");
        return Task.CompletedTask;
    }

    public Task ResumeExecutionAsync()
    {
        var session = CurrentSession;
        if (session == null || State != ExecutionState.Paused)
            throw new InvalidOperationException("Execution is not paused.");

        _pauseEvent.Set();
        var prev = State;
        State = ExecutionState.Running;
        session.ChangeState(ExecutionState.Running);
        RaiseStateChanged(prev, State, session.Id, "Resumed");
        return Task.CompletedTask;
    }

    public Task StopExecutionAsync()
    {
        var session = CurrentSession;
        if (session == null)
        {
            State = ExecutionState.Stopped;
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping execution (session {SessionId})", session.Id);
        _cts?.Cancel();
        _pauseEvent.Set();

        var prev = State;
        State = ExecutionState.Stopped;
        session.ChangeState(ExecutionState.Stopped);
        RaiseStateChanged(prev, State, session.Id, "Stopped");

        CurrentCommandIndex = 0;
        return Task.CompletedTask;
    }

    public async Task StepExecutionAsync()
    {
        var session = CurrentSession;
        if (session == null || CurrentScript == null)
            throw new InvalidOperationException("No script is loaded.");

        if (State == ExecutionState.Running)
            throw new InvalidOperationException("Cannot step while execution is running.");

        var prev = State;
        State = ExecutionState.Stepping;
        session.ChangeState(ExecutionState.Stepping);
        RaiseStateChanged(prev, State, session.Id, "Step");

        await ExecuteSingleCommandAsync(session, CurrentCommandIndex, CancellationToken.None);

        // After one step, pause
        prev = State;
        State = ExecutionState.Paused;
        session.ChangeState(ExecutionState.Paused);
        RaiseStateChanged(prev, State, session.Id, "Paused after step");
    }

    public Task TerminateExecutionAsync()
    {
        var session = CurrentSession;
        _logger.LogWarning("Terminate requested (session {SessionId})", session?.Id);

        _cts?.Cancel();
        _pauseEvent.Set();

        if (session != null)
        {
            var prev = State;
            State = ExecutionState.Terminated;
            session.ChangeState(ExecutionState.Terminated);
            RaiseStateChanged(prev, State, session.Id, "Terminated");
            RaiseCompleted(session, ExecutionState.Terminated, false, "Terminated by user/kill switch", null);
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
        var script = CurrentScript;
        if (script == null) return null;

        var idx = CurrentCommandIndex;
        if (idx < 0 || idx >= script.CommandCount) return null;

        var remaining = TimeSpan.Zero;
        for (var i = idx; i < script.CommandCount; i++)
        {
            remaining = remaining.Add(script.Commands[i].Delay);
            if (script.Commands[i] is SleepCommand s)
                remaining = remaining.Add(s.Duration);
        }

        var session = CurrentSession;
        var speed = session?.Options.SpeedMultiplier ?? 1.0;
        if (speed <= 0) speed = 1.0;

        return TimeSpan.FromMilliseconds(remaining.TotalMilliseconds / speed);
    }

    private async Task ExecutionLoopAsync(ExecutionSession session, CancellationToken ct)
    {
        try
        {
            var script = session.Script;

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

            for (var i = CurrentCommandIndex; i < script.CommandCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                _pauseEvent.Wait(ct);

                if (_safetyService.IsKillSwitchActive)
                    throw new InvalidOperationException("Kill switch is active.");

                // Safety: max execution time
                if (DateTime.UtcNow - started > session.Options.MaxExecutionTime)
                    throw new InvalidOperationException("Execution time limit exceeded.");

                await ExecuteSingleCommandAsync(session, i, ct);
            }

            var prev = State;
            State = ExecutionState.Completed;
            session.ChangeState(ExecutionState.Completed);
            RaiseStateChanged(prev, State, session.Id, "Completed");
            RaiseCompleted(session, ExecutionState.Completed, true, "Completed successfully", null);
        }
        catch (OperationCanceledException)
        {
            // Stop/Terminate paths handle events/state separately.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution failed (session {SessionId})", session.Id);
            session.SetError(ex);

            var prev = State;
            State = ExecutionState.Failed;
            RaiseStateChanged(prev, State, session.Id, "Failed");
            RaiseExecutionError(ex, session.Id, "Execution loop", canContinue: false);
            RaiseCompleted(session, ExecutionState.Failed, false, "Execution failed", ex);
        }
        finally
        {
            // Unregister kill switch hotkey best-effort
            try { await _globalHotkeyService.UnregisterHotkeyAsync(_killSwitchHotkey); } catch { }
        }
    }

    private async Task ExecuteSingleCommandAsync(ExecutionSession session, int index, CancellationToken ct)
    {
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

            CurrentCommandIndex = index + 1;
            session.UpdateProgress(CurrentCommandIndex);

            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, session.Id, index, true, sw.Elapsed));
            ProgressChanged?.Invoke(this, new ExecutionProgressEventArgs(session.Id, CurrentCommandIndex, script.CommandCount, session.ElapsedTime, GetEstimatedRemainingTime()));
        }
        catch (Exception ex)
        {
            sw.Stop();
            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, session.Id, index, false, sw.Elapsed, ex));
            throw;
        }
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
        try { _cts?.Cancel(); } catch { }
        try { _pauseEvent.Set(); } catch { }
        try { _globalHotkeyService.HotkeyPressed -= OnHotkeyPressed; } catch { }
        _pauseEvent.Dispose();
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        try
        {
            if (e.Hotkey.Modifiers == _killSwitchHotkey.Modifiers && e.Hotkey.Key == _killSwitchHotkey.Key)
            {
                _ = _safetyService.ActivateKillSwitchAsync("Kill switch hotkey pressed");
                _ = TerminateExecutionAsync();
            }
        }
        catch
        {
            // never throw from event handler
        }
    }
}

