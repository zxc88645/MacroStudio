using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroStudio.Infrastructure.Adapters;

/// <summary>
/// Arduino-based implementation of IInputHookService that receives input events from Arduino via USB Host Shield.
/// </summary>
public sealed class ArduinoInputHookService : IInputHookService, IDisposable
{
    private readonly IArduinoService _arduinoService;
    private readonly ILogger<ArduinoInputHookService> _logger;
    private RecordingOptions? _options;
    private bool _isInstalled;
    private bool _isDisposed;

    public ArduinoInputHookService(IArduinoService arduinoService, ILogger<ArduinoInputHookService> logger)
    {
        _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to Arduino events
        _arduinoService.EventReceived += OnArduinoEventReceived;
    }

    public bool IsInstalled
    {
        get
        {
            lock (this)
            {
                return _isInstalled;
            }
        }
    }

    public event EventHandler<InputHookMouseMoveEventArgs>? MouseMoved;
    public event EventHandler<InputHookMouseClickEventArgs>? MouseClicked;
    public event EventHandler<InputHookKeyEventArgs>? KeyboardInput;

    public Task InstallHooksAsync(RecordingOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        ThrowIfDisposed();

        lock (this)
        {
            if (_isInstalled)
                throw new InvalidOperationException("Hooks are already installed.");

            if (!_arduinoService.IsConnected)
                throw new InvalidOperationException("Arduino is not connected. Cannot install hooks.");

            _options = options;
            _isInstalled = true;
        }

        _logger.LogInformation("Installed Arduino input hooks");

        // Send start recording command to Arduino
        return Task.Run(async () =>
        {
            try
            {
                var command = new ArduinoStartRecordingCommand();
                await _arduinoService.SendCommandAsync(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send start recording command to Arduino");
                throw;
            }
        });
    }

    public Task UninstallHooksAsync()
    {
        ThrowIfDisposed();

        lock (this)
        {
            if (!_isInstalled)
                return Task.CompletedTask;

            _isInstalled = false;
            _options = null;
        }

        _logger.LogInformation("Uninstalled Arduino input hooks");

        // Send stop recording command to Arduino
        return Task.Run(async () =>
        {
            try
            {
                if (_arduinoService.IsConnected)
                {
                    var command = new ArduinoStopRecordingCommand();
                    await _arduinoService.SendCommandAsync(command);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send stop recording command to Arduino");
                // Don't throw - we're uninstalling anyway
            }
        });
    }

    private void OnArduinoEventReceived(object? sender, ArduinoEventReceivedEventArgs e)
    {
        if (!_isInstalled)
            return;

        try
        {
            switch (e.EventType)
            {
                case ArduinoEventType.MouseMove:
                    HandleMouseMoveEvent(e.Data);
                    break;

                case ArduinoEventType.MouseClick:
                    HandleMouseClickEvent(e.Data);
                    break;

                case ArduinoEventType.KeyboardInput:
                    HandleKeyboardInputEvent(e.Data);
                    break;

                case ArduinoEventType.StatusResponse:
                    // Ignore status responses
                    break;

                case ArduinoEventType.Error:
                    _logger.LogWarning("Received error event from Arduino: {Data}", BitConverter.ToString(e.Data));
                    break;

                default:
                    _logger.LogWarning("Unknown event type received from Arduino: {EventType}", e.EventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Arduino event {EventType}", e.EventType);
        }
    }

    private void HandleMouseMoveEvent(byte[] data)
    {
        if (data == null || data.Length < 4)
        {
            _logger.LogWarning("Invalid mouse move event data: length {Length}", data?.Length ?? 0);
            return;
        }

        var options = _options;
        if (options == null || !options.RecordMouseMovements)
            return;

        // Decode position: [X: 2 bytes][Y: 2 bytes]
        int x = data[0] | (data[1] << 8);
        int y = data[2] | (data[3] << 8);

        var position = new Point(x, y);
        MouseMoved?.Invoke(this, new InputHookMouseMoveEventArgs(position));
    }

    private void HandleMouseClickEvent(byte[] data)
    {
        if (data == null || data.Length < 6)
        {
            _logger.LogWarning("Invalid mouse click event data: length {Length}", data?.Length ?? 0);
            return;
        }

        var options = _options;
        if (options == null || !options.RecordMouseClicks)
            return;

        // Decode click: [X: 2 bytes][Y: 2 bytes][Button: 1 byte][ClickType: 1 byte]
        int x = data[0] | (data[1] << 8);
        int y = data[2] | (data[3] << 8);
        var button = (MouseButton)data[4];
        var clickType = (ClickType)data[5];

        var position = new Point(x, y);
        // Arduino events are not injected (they come from real hardware)
        MouseClicked?.Invoke(this, new InputHookMouseClickEventArgs(position, button, clickType, isInjected: false));
    }

    private void HandleKeyboardInputEvent(byte[] data)
    {
        if (data == null || data.Length < 3)
        {
            _logger.LogWarning("Invalid keyboard input event data: length {Length}", data?.Length ?? 0);
            return;
        }

        var options = _options;
        if (options == null || !options.RecordKeyboardInput)
            return;

        // Decode keyboard: [Key: 2 bytes][IsDown: 1 byte]
        ushort keyCode = (ushort)(data[0] | (data[1] << 8));
        bool isDown = data[2] != 0;

        if (Enum.IsDefined(typeof(VirtualKey), (VirtualKey)keyCode))
        {
            var key = (VirtualKey)keyCode;
            // Arduino events are not injected (they come from real hardware)
            KeyboardInput?.Invoke(this, new InputHookKeyEventArgs(key, isDown, isInjected: false));
        }
        else
        {
            _logger.LogWarning("Invalid virtual key code received from Arduino: {KeyCode}", keyCode);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ArduinoInputHookService));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            UninstallHooksAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during disposal
        }

        _arduinoService.EventReceived -= OnArduinoEventReceived;
    }
}
