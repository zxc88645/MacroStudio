using MacroNex.Domain.Entities;
using MacroNex.Domain.Events;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroNex.Application.Services;

/// <summary>
/// Application service for recording user input and converting it to automation commands.
/// Handles event capture, filtering, and command generation with Win32 hook integration.
/// </summary>
public class RecordingService : IRecordingService
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IInputHookServiceFactory _inputHookServiceFactory;
    private readonly ArduinoConnectionService _arduinoConnectionService;
    private readonly ISettingsService _settingsService;
    private readonly IInputSimulatorFactory _inputSimulatorFactory;
    private readonly ILogger<RecordingService> _logger;
    private readonly object _stateLock = new();

    private RecordingSession? _currentSession;
    private DateTime _lastEventTime;
    private Point _lastMousePosition;
    private bool _isFirstEventInSegment = true;
    private bool _hooksSubscribed;
    private IInputHookService? _currentInputHookService;
    private HotkeyDefinition? _ignoreStart;
    private HotkeyDefinition? _ignorePause;
    private HotkeyDefinition? _ignoreStop;
    
    /// <summary>
    /// Tracks keys that are currently pressed to filter out key repeat events.
    /// When a key is held down, Windows sends repeated key down events which should be ignored.
    /// </summary>
    private readonly HashSet<VirtualKey> _pressedKeys = new();

    /// <summary>
    /// Initializes a new instance of the RecordingService class.
    /// </summary>
    /// <param name="hotkeyService">Global hotkey service for recording control.</param>
    /// <param name="inputHookServiceFactory">Factory for creating input hook services based on input mode.</param>
    /// <param name="arduinoConnectionService">Arduino connection service for validating hardware mode.</param>
    /// <param name="settingsService">Settings service for reading recording hotkey configuration (to avoid recording control keys).</param>
    /// <param name="inputSimulatorFactory">Factory for creating input simulators (for getting cursor position).</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public RecordingService(IGlobalHotkeyService hotkeyService, IInputHookServiceFactory inputHookServiceFactory, ArduinoConnectionService arduinoConnectionService, ISettingsService settingsService, IInputSimulatorFactory inputSimulatorFactory, ILogger<RecordingService> logger)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _inputHookServiceFactory = inputHookServiceFactory ?? throw new ArgumentNullException(nameof(inputHookServiceFactory));
        _arduinoConnectionService = arduinoConnectionService ?? throw new ArgumentNullException(nameof(arduinoConnectionService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _inputSimulatorFactory = inputSimulatorFactory ?? throw new ArgumentNullException(nameof(inputSimulatorFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("RecordingService initialized");
    }

    /// <inheritdoc />
    public bool IsRecording
    {
        get
        {
            lock (_stateLock)
            {
                return _currentSession != null && _currentSession.State == RecordingState.Active;
            }
        }
    }

    /// <inheritdoc />
    public RecordingSession? CurrentSession
    {
        get
        {
            lock (_stateLock)
            {
                return _currentSession;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<CommandRecordedEventArgs>? CommandRecorded;

    /// <inheritdoc />
    public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    /// <inheritdoc />
    public event EventHandler<RecordingErrorEventArgs>? RecordingError;

    /// <inheritdoc />
    public async Task StartRecordingAsync(RecordingOptions? options = null)
    {
        lock (_stateLock)
        {
            if (_currentSession != null && _currentSession.State == RecordingState.Active)
            {
                throw new InvalidOperationException("Recording is already active.");
            }
        }

        try
        {
            _logger.LogInformation("Starting recording session");

            var recordingOptions = options ?? RecordingOptions.Default();
            var previousState = _currentSession?.State ?? RecordingState.Inactive;

            // Load current recording-control hotkeys so we can ignore them during capture.
            try
            {
                var settings = await _settingsService.LoadAsync();
                settings.EnsureDefaults();
                _ignoreStart = settings.RecordingStartHotkey;
                _ignorePause = settings.RecordingPauseHotkey;
                _ignoreStop = settings.RecordingStopHotkey;
            }
            catch
            {
                // Best effort; if settings cannot be loaded we just won't ignore.
                _ignoreStart = _ignorePause = _ignoreStop = null;
            }

            // Validate hardware mode connection
            if (recordingOptions.InputMode == InputMode.Hardware)
            {
                _arduinoConnectionService.EnsureConnected();
            }

            // Validate recording setup BEFORE creating a session (otherwise IsRecording becomes true).
            var validationResult = await ValidateRecordingSetupAsync();
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                throw new InvalidOperationException($"Recording setup validation failed: {errors}");
            }

            // Create new recording session
            var session = new RecordingSession(recordingOptions);

            // Get the appropriate input hook service based on input mode
            var inputHookService = _inputHookServiceFactory.GetInputHookService(recordingOptions.InputMode);

            // If using relative mouse movement, get current cursor position to initialize
            Point initialMousePosition = Point.Zero;
            if (recordingOptions.UseRelativeMouseMove && recordingOptions.RecordMouseMovements)
            {
                try
                {
                    var inputSimulator = _inputSimulatorFactory.GetInputSimulator(recordingOptions.InputMode);
                    initialMousePosition = await inputSimulator.GetCursorPositionAsync();
                    _logger.LogDebug("Initialized relative mouse movement with current position: {Position}", initialMousePosition);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get current cursor position for relative movement initialization. Using Point.Zero.");
                    initialMousePosition = Point.Zero;
                }
            }

            lock (_stateLock)
            {
                _currentSession = session;
                _currentInputHookService = inputHookService;
                _lastEventTime = DateTime.UtcNow;
                _lastMousePosition = initialMousePosition;
                _isFirstEventInSegment = true;
                _pressedKeys.Clear();
            }

            // Install hooks for mouse and keyboard events
            EnsureHookSubscriptions(inputHookService);
            await inputHookService.InstallHooksAsync(recordingOptions);

            // Raise state changed event
            RaiseStateChanged(previousState, RecordingState.Active, session.Id, "Recording started");

            _logger.LogInformation("Recording session {SessionId} started", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting recording session");
            RaiseError(ex, "Starting recording");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopRecordingAsync()
    {
        RecordingSession? session;
        RecordingState previousState;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || (session.State != RecordingState.Active && session.State != RecordingState.Paused))
            {
                throw new InvalidOperationException("No active or paused recording session to stop.");
            }

            previousState = session.State;
        }

        try
        {
            _logger.LogInformation("Stopping recording session {SessionId}", session.Id);

            // Uninstall hooks (best effort)
            IInputHookService? hookService;
            lock (_stateLock)
            {
                hookService = _currentInputHookService;
                _currentInputHookService = null;
            }

            if (hookService != null)
            {
                try
                {
                    await hookService.UninstallHooksAsync();
                }
                catch (Exception hookEx)
                {
                    _logger.LogWarning(hookEx, "Failed to uninstall input hooks");
                }
            }

            // Change state to stopped
            session.ChangeState(RecordingState.Stopped);

            lock (_stateLock)
            {
                _currentSession = null;
            }

            // Raise state changed event
            RaiseStateChanged(previousState, RecordingState.Stopped, session.Id, "Recording stopped");

            _logger.LogInformation("Recording session {SessionId} stopped. Recorded {CommandCount} commands.",
                session.Id, session.Commands.Count);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping recording session {SessionId}", session.Id);
            RaiseError(ex, "Stopping recording", session.Id);
            throw;
        }
    }

    private void EnsureHookSubscriptions(IInputHookService inputHookService)
    {
        if (_hooksSubscribed)
        {
            // Unsubscribe from previous service if different
            // Note: In practice, we should track which service we're subscribed to
            // For simplicity, we'll just subscribe to the new one
        }

        inputHookService.MouseMoved += OnMouseMoved;
        inputHookService.MouseClicked += OnMouseClicked;
        inputHookService.KeyboardInput += OnKeyboardInput;
        _hooksSubscribed = true;
    }

    private void OnMouseMoved(object? sender, InputHookMouseMoveEventArgs e)
    {
        if (e.IsRelative)
        {
            // Hardware input: use original relative values for 100% accurate replay
            HandleMouseMoveRelative(e.DeltaX, e.DeltaY, e.Position);
        }
        else
        {
            HandleMouseMove(e.Position);
        }
    }

    private void OnMouseClicked(object? sender, InputHookMouseClickEventArgs e)
    {
        HandleMouseClick(e.Position, e.Button, e.ClickType);
    }

    private void OnKeyboardInput(object? sender, InputHookKeyEventArgs e)
    {
        HandleKeyPress(e.Key, e.IsDown);
    }

    internal void HandleKeyPress(VirtualKey key, bool isDown)
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Active)
                return;

            if (!session.Options.RecordKeyboardInput)
                return;
            
            // Filter out key repeat events:
            // - If key down and key is already pressed, this is a repeat event - ignore
            // - If key up and key is not in pressed set, ignore (key was pressed before recording started)
            if (isDown)
            {
                if (_pressedKeys.Contains(key))
                {
                    // Key repeat event - ignore
                    return;
                }
                _pressedKeys.Add(key);
            }
            else
            {
                if (!_pressedKeys.Contains(key))
                {
                    // Key was not tracked as pressed - ignore
                    return;
                }
                _pressedKeys.Remove(key);
            }
        }

        try
        {
            // Do not record recording-control hotkeys (Start/Pause/Stop).
            // We ignore both down/up to keep scripts clean.
            // NOTE: we only get VirtualKey here (no modifier state), so we ignore by Key.
            bool MatchesIgnore(HotkeyDefinition? hk) => hk != null && hk.Key == key;
            if (MatchesIgnore(_ignoreStart) || MatchesIgnore(_ignorePause) || MatchesIgnore(_ignoreStop))
                return;

            var now = DateTime.UtcNow;
            var delay = now - _lastEventTime;

            if (_isFirstEventInSegment)
                delay = TimeSpan.Zero;

            if (!_isFirstEventInSegment && delay < session.Options.MinimumDelay)
                delay = session.Options.MinimumDelay;

            if (delay > session.Options.MaximumDelay)
                delay = session.Options.MaximumDelay;

            var command = new KeyPressCommand(key, isDown)
            {
                Delay = delay
            };

            session.AddCommand(command);
            _lastEventTime = now;
            _isFirstEventInSegment = false;

            RaiseCommandRecorded(command, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key press event");
            RaiseError(ex, "Handling key press", session.Id);
        }
    }

    /// <inheritdoc />
    public async Task PauseRecordingAsync()
    {
        RecordingSession? session;
        RecordingState previousState;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Active)
            {
                throw new InvalidOperationException("No active recording session to pause.");
            }

            previousState = session.State;
        }

        try
        {
            _logger.LogDebug("Pausing recording session {SessionId}", session.Id);

            session.ChangeState(RecordingState.Paused);

            // Raise state changed event
            RaiseStateChanged(previousState, RecordingState.Paused, session.Id, "Recording paused");

            _logger.LogDebug("Recording session {SessionId} paused", session.Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing recording session {SessionId}", session.Id);
            RaiseError(ex, "Pausing recording", session.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ResumeRecordingAsync()
    {
        RecordingSession? session;
        RecordingState previousState;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Paused)
            {
                throw new InvalidOperationException("No paused recording session to resume.");
            }

            previousState = session.State;
        }

        try
        {
            _logger.LogDebug("Resuming recording session {SessionId}", session.Id);

            session.ChangeState(RecordingState.Active);
            _lastEventTime = DateTime.UtcNow;
            _isFirstEventInSegment = true;

            // Raise state changed event
            RaiseStateChanged(previousState, RecordingState.Active, session.Id, "Recording resumed");

            _logger.LogDebug("Recording session {SessionId} resumed", session.Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming recording session {SessionId}", session.Id);
            RaiseError(ex, "Resuming recording", session.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Command>> GetRecordedCommandsAsync()
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null)
            {
                throw new InvalidOperationException("No active recording session.");
            }
        }

        return await Task.FromResult(session.Commands);
    }

    /// <inheritdoc />
    public async Task ClearRecordedCommandsAsync()
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null)
            {
                throw new InvalidOperationException("No active recording session.");
            }
        }

        try
        {
            _logger.LogDebug("Clearing recorded commands from session {SessionId}", session.Id);

            session.ClearCommands();

            _logger.LogDebug("Cleared recorded commands from session {SessionId}", session.Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing recorded commands from session {SessionId}", session.Id);
            RaiseError(ex, "Clearing recorded commands", session.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<RecordingValidationResult> ValidateRecordingSetupAsync()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Check if hotkey service is ready
            var isReady = await _hotkeyService.IsReadyAsync();
            if (!isReady)
            {
                errors.Add("Global hotkey service is not ready. Cannot start recording.");
            }

            // TODO: Check if Win32 hooks can be installed
            // This would check system permissions and hook availability
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating recording setup: {ex.Message}");
        }

        return errors.Count > 0
            ? RecordingValidationResult.Failure(errors, warnings)
            : RecordingValidationResult.Success(warnings);
    }

    /// <inheritdoc />
    public RecordingStatistics? GetRecordingStatistics()
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null)
            {
                return null;
            }
        }

        var commands = session.Commands;
        var stats = new RecordingStatistics
        {
            TotalCommands = commands.Count,
            MouseMoveCommands = commands.Count(c => c is MouseMoveCommand || c is MouseMoveRelativeCommand),
            MouseClickCommands = commands.Count(c => c is MouseClickCommand),
            KeyboardCommands = commands.Count(c => c is KeyboardCommand),
            SleepCommands = commands.Count(c => c is SleepCommand),
            SessionDuration = DateTime.UtcNow - session.StartedAt
        };

        // Calculate estimated execution time
        var totalTime = TimeSpan.Zero;
        foreach (var command in commands)
        {
            totalTime = totalTime.Add(command.Delay);
            if (command is SleepCommand sleepCommand)
            {
                totalTime = totalTime.Add(sleepCommand.Duration);
            }
        }
        stats.EstimatedExecutionTime = totalTime;

        return stats;
    }

    /// <summary>
    /// Handles a mouse move event (called by Win32 hook).
    /// </summary>
    /// <param name="position">The new mouse position.</param>
    internal void HandleMouseMove(Point position)
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Active)
            {
                return;
            }

            if (!session.Options.RecordMouseMovements)
            {
                return;
            }
        }

        try
        {
            var now = DateTime.UtcNow;
            var delay = now - _lastEventTime;

            // Apply minimum delay filter
            if (_isFirstEventInSegment)
                delay = TimeSpan.Zero;

            if (!_isFirstEventInSegment && delay < session.Options.MinimumDelay)
            {
                return; // Skip this event
            }

            // Apply maximum delay cap
            if (delay > session.Options.MaximumDelay)
            {
                delay = session.Options.MaximumDelay;
            }

            // Only record if position changed significantly (optional optimization)
            if (position.X == _lastMousePosition.X && position.Y == _lastMousePosition.Y)
            {
                return;
            }

            Command command;
            // Unified commands - actual execution mode depends on InputMode setting
            if (session.Options.UseRelativeMouseMove)
            {
                // Calculate relative displacement
                var deltaX = position.X - _lastMousePosition.X;
                var deltaY = position.Y - _lastMousePosition.Y;

                command = new MouseMoveRelativeCommand(deltaX, deltaY);
            }
            else
            {
                command = new MouseMoveCommand(position);
            }
            command.Delay = delay;

            session.AddCommand(command);
            _lastEventTime = now;
            _lastMousePosition = position;
            _isFirstEventInSegment = false;

            RaiseCommandRecorded(command, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mouse move event");
            RaiseError(ex, "Handling mouse move", session.Id);
        }
    }

    /// <summary>
    /// Handles a relative mouse move event (from hardware input via USB Host Shield).
    /// Preserves the original ?X, ?Y values for 100% accurate replay.
    /// </summary>
    /// <param name="deltaX">The relative X displacement.</param>
    /// <param name="deltaY">The relative Y displacement.</param>
    /// <param name="accumulatedPosition">The accumulated position for reference.</param>
    internal void HandleMouseMoveRelative(int deltaX, int deltaY, Point accumulatedPosition)
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Active)
            {
                return;
            }

            if (!session.Options.RecordMouseMovements)
            {
                return;
            }
        }

        try
        {
            var now = DateTime.UtcNow;
            var delay = now - _lastEventTime;

            // Apply minimum delay filter
            if (_isFirstEventInSegment)
                delay = TimeSpan.Zero;

            if (!_isFirstEventInSegment && delay < session.Options.MinimumDelay)
            {
                return; // Skip this event
            }

            // Apply maximum delay cap
            if (delay > session.Options.MaximumDelay)
            {
                delay = session.Options.MaximumDelay;
            }

            // Skip zero movement
            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            // For hardware input, always create relative move command
            // This preserves the exact USB Host Shield data for 100% accurate replay
            // The command will be executed differently based on the playback mode:
            // - Hardware mode: send relative move directly to Arduino
            // - HighLevel/LowLevel mode: IInputSimulator converts to GetCursorPos + delta
            Command command = new MouseMoveRelativeCommand(deltaX, deltaY);
            
            command.Delay = delay;

            session.AddCommand(command);
            _lastEventTime = now;
            _lastMousePosition = accumulatedPosition;
            _isFirstEventInSegment = false;

            RaiseCommandRecorded(command, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling relative mouse move event");
            RaiseError(ex, "Handling relative mouse move", session.Id);
        }
    }

    /// <summary>
    /// Handles a mouse click event (called by Win32 hook).
    /// </summary>
    /// <param name="position">The click position.</param>
    /// <param name="button">The mouse button.</param>
    /// <param name="clickType">The click type.</param>
    internal void HandleMouseClick(Point position, MouseButton button, ClickType clickType)
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Active)
            {
                return;
            }

            if (!session.Options.RecordMouseClicks)
            {
                return;
            }
        }

        try
        {
            var now = DateTime.UtcNow;
            var delay = now - _lastEventTime;

            // Apply minimum delay filter
            if (_isFirstEventInSegment)
                delay = TimeSpan.Zero;

            if (!_isFirstEventInSegment && delay < session.Options.MinimumDelay)
            {
                delay = session.Options.MinimumDelay;
            }

            // Apply maximum delay cap
            if (delay > session.Options.MaximumDelay)
            {
                delay = session.Options.MaximumDelay;
            }

            var command = new MouseClickCommand(button, clickType)
            {
                Delay = delay
            };

            session.AddCommand(command);
            _lastEventTime = now;
            _lastMousePosition = position;
            _isFirstEventInSegment = false;

            RaiseCommandRecorded(command, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling mouse click event");
            RaiseError(ex, "Handling mouse click", session.Id);
        }
    }

    /// <summary>
    /// Handles a keyboard input event (called by Win32 hook).
    /// </summary>
    /// <param name="text">The text to type, or null for key combinations.</param>
    /// <param name="keys">The virtual keys to press, if text is null.</param>
    internal void HandleKeyboardInput(string? text, IEnumerable<VirtualKey>? keys = null)
    {
        RecordingSession? session;

        lock (_stateLock)
        {
            session = _currentSession;
            if (session == null || session.State != RecordingState.Active)
            {
                return;
            }

            if (!session.Options.RecordKeyboardInput)
            {
                return;
            }
        }

        try
        {
            var now = DateTime.UtcNow;
            var delay = now - _lastEventTime;

            // Apply minimum delay filter
            if (_isFirstEventInSegment)
                delay = TimeSpan.Zero;

            if (!_isFirstEventInSegment && delay < session.Options.MinimumDelay)
            {
                delay = session.Options.MinimumDelay;
            }

            // Apply maximum delay cap
            if (delay > session.Options.MaximumDelay)
            {
                delay = session.Options.MaximumDelay;
            }

            Command command;
            if (!string.IsNullOrEmpty(text))
            {
                command = new KeyboardCommand(text)
                {
                    Delay = delay
                };
            }
            else if (keys != null && keys.Any())
            {
                command = new KeyboardCommand(keys)
                {
                    Delay = delay
                };
            }
            else
            {
                return; // No valid input
            }

            session.AddCommand(command);
            _lastEventTime = now;
            _isFirstEventInSegment = false;

            RaiseCommandRecorded(command, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling keyboard input event");
            RaiseError(ex, "Handling keyboard input", session.Id);
        }
    }

    /// <summary>
    /// Raises the CommandRecorded event.
    /// </summary>
    private void RaiseCommandRecorded(Command command, Guid sessionId)
    {
        try
        {
            var eventArgs = new CommandRecordedEventArgs(command, sessionId);
            CommandRecorded?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising CommandRecorded event");
        }
    }

    /// <summary>
    /// Raises the RecordingStateChanged event.
    /// </summary>
    private void RaiseStateChanged(RecordingState previousState, RecordingState newState, Guid sessionId, string? reason = null)
    {
        try
        {
            var eventArgs = new RecordingStateChangedEventArgs(previousState, newState, sessionId, reason);
            RecordingStateChanged?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising RecordingStateChanged event");
        }
    }

    /// <summary>
    /// Raises the RecordingError event.
    /// </summary>
    private void RaiseError(Exception error, string context, Guid? sessionId = null)
    {
        try
        {
            var id = sessionId ?? _currentSession?.Id ?? Guid.Empty;
            var eventArgs = new RecordingErrorEventArgs(error, id, context);
            RecordingError?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising RecordingError event");
        }
    }
}
