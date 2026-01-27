using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroNex.Application.Services;

/// <summary>
/// Application service for managing Arduino connection state and health monitoring.
/// </summary>
public sealed class ArduinoConnectionService : IDisposable
{
    private readonly IArduinoService _arduinoService;
    private readonly ILogger<ArduinoConnectionService> _logger;
    private bool _isDisposed;

    public ArduinoConnectionService(IArduinoService arduinoService, ILogger<ArduinoConnectionService> logger)
    {
        _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to connection state changes
        _arduinoService.ConnectionStateChanged += OnConnectionStateChanged;
        _arduinoService.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    public event EventHandler<ArduinoConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ArduinoConnectionState ConnectionState => _arduinoService.ConnectionState;

    /// <summary>
    /// Gets whether the Arduino is currently connected.
    /// </summary>
    public bool IsConnected => _arduinoService.IsConnected;

    /// <summary>
    /// Gets the currently connected port name, or null if not connected.
    /// </summary>
    public string? ConnectedPortName => _arduinoService.ConnectedPortName;

    /// <summary>
    /// Gets a list of available serial ports.
    /// </summary>
    public Task<IReadOnlyList<string>> GetAvailablePortsAsync() => _arduinoService.GetAvailablePortsAsync();

    /// <summary>
    /// Connects to the Arduino on the specified port.
    /// </summary>
    public Task ConnectAsync(string portName) => _arduinoService.ConnectAsync(portName);

    /// <summary>
    /// Disconnects from the Arduino.
    /// </summary>
    public Task DisconnectAsync() => _arduinoService.DisconnectAsync();

    /// <summary>
    /// Automatically detects and connects to an Arduino device.
    /// Tries each available port until a successful connection is established.
    /// </summary>
    /// <returns>True if connection was successful, false otherwise.</returns>
    public async Task<bool> AutoConnectAsync()
    {
        if (_arduinoService.IsConnected)
        {
            _logger.LogInformation("Arduino is already connected on port {PortName}", _arduinoService.ConnectedPortName);
            return true;
        }

        var ports = await GetAvailablePortsAsync();
        if (ports.Count == 0)
        {
            _logger.LogWarning("No serial ports available for Arduino connection");
            return false;
        }

        _logger.LogInformation("Attempting to auto-connect to Arduino. Checking {PortCount} available ports", ports.Count);

        foreach (var port in ports)
        {
            try
            {
                _logger.LogDebug("Attempting to connect to Arduino on port {PortName}", port);
                await ConnectAsync(port);
                
                // Wait a moment to see if connection succeeds
                await Task.Delay(500);
                
                if (_arduinoService.IsConnected)
                {
                    _logger.LogInformation("Successfully auto-connected to Arduino on port {PortName}", port);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to connect to Arduino on port {PortName}, trying next port", port);
                // Continue to next port
            }
        }

        _logger.LogWarning("Failed to auto-connect to Arduino on any available port");
        return false;
    }

    /// <summary>
    /// Validates that the Arduino is ready for use in hardware mode.
    /// </summary>
    /// <returns>True if ready, false otherwise.</returns>
    public bool ValidateConnection()
    {
        if (!_arduinoService.IsConnected)
        {
            _logger.LogWarning("Arduino is not connected");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates connection and throws if not ready.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when Arduino is not connected.</exception>
    public void EnsureConnected()
    {
        if (!_arduinoService.IsConnected)
        {
            throw new InvalidOperationException("Arduino is not connected. Please connect to an Arduino device before using hardware mode.");
        }
    }

    private void OnConnectionStateChanged(object? sender, ArduinoConnectionStateChangedEventArgs e)
    {
        _logger.LogInformation("Arduino connection state changed: {PreviousState} -> {NewState} (Port: {PortName})",
            e.PreviousState, e.NewState, e.PortName ?? "N/A");
        
        // Forward the event
        ConnectionStateChanged?.Invoke(this, e);
    }

    private void OnErrorOccurred(object? sender, ArduinoErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Arduino error: {Message}", e.Message);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ArduinoConnectionService));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _arduinoService.ConnectionStateChanged -= OnConnectionStateChanged;
        _arduinoService.ErrorOccurred -= OnErrorOccurred;
    }
}
