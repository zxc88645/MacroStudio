using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;

namespace MacroStudio.Infrastructure.Adapters;

/// <summary>
/// Serial port implementation of IArduinoService for communicating with Arduino Leonardo.
/// </summary>
public sealed class ArduinoSerialService : IArduinoService, IDisposable
{
    private readonly ILogger<ArduinoSerialService> _logger;
    private readonly object _lockObject = new();
    private SerialPort? _serialPort;
    private ArduinoConnectionState _connectionState = ArduinoConnectionState.Disconnected;
    private string? _connectedPortName;
    private CancellationTokenSource? _readCancellationTokenSource;
    private Task? _readTask;
    private readonly List<byte> _receiveBuffer = new();
    private bool _isDisposed;

    // Serial port configuration
    private const int BaudRate = 115200;
    private const int DataBits = 8;
    private const System.IO.Ports.Parity SerialParity = System.IO.Ports.Parity.None;
    private const System.IO.Ports.StopBits SerialStopBits = System.IO.Ports.StopBits.One;
    private const System.IO.Ports.Handshake SerialHandshake = System.IO.Ports.Handshake.None;

    // Heartbeat configuration
    private const int HeartbeatIntervalMs = 5000; // 5 seconds
    private CancellationTokenSource? _heartbeatCancellationTokenSource;
    private Task? _heartbeatTask;

    public ArduinoSerialService(ILogger<ArduinoSerialService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ArduinoConnectionState ConnectionState
    {
        get
        {
            lock (_lockObject)
            {
                return _connectionState;
            }
        }
        private set
        {
            ArduinoConnectionState previous;
            lock (_lockObject)
            {
                previous = _connectionState;
                _connectionState = value;
            }

            if (previous != value)
            {
                ConnectionStateChanged?.Invoke(this, new ArduinoConnectionStateChangedEventArgs(previous, value, _connectedPortName));
            }
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_lockObject)
            {
                return _connectionState == ArduinoConnectionState.Connected && _serialPort?.IsOpen == true;
            }
        }
    }

    public string? ConnectedPortName
    {
        get
        {
            lock (_lockObject)
            {
                return _connectedPortName;
            }
        }
    }

    public event EventHandler<ArduinoConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<ArduinoEventReceivedEventArgs>? EventReceived;
    public event EventHandler<ArduinoErrorEventArgs>? ErrorOccurred;

    public Task<IReadOnlyList<string>> GetAvailablePortsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                return (IReadOnlyList<string>)ports.OrderBy(p => p).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available serial ports");
                RaiseError("Failed to get available serial ports", ex);
                return Array.Empty<string>();
            }
        });
    }

    public async Task ConnectAsync(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be null or empty.", nameof(portName));

        ThrowIfDisposed();

        lock (_lockObject)
        {
            if (_connectionState == ArduinoConnectionState.Connected || _connectionState == ArduinoConnectionState.Connecting)
                throw new InvalidOperationException($"Already connected or connecting. Current state: {_connectionState}");

            if (_serialPort != null)
                throw new InvalidOperationException("Serial port is already initialized. Disconnect first.");
        }

        ConnectionState = ArduinoConnectionState.Connecting;

        try
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _serialPort = new SerialPort(portName, BaudRate, SerialParity, DataBits, SerialStopBits)
                    {
                        Handshake = SerialHandshake,
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    _serialPort.Open();
                    _connectedPortName = portName;
                }
            });

            // Start reading task
            _readCancellationTokenSource = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_readCancellationTokenSource.Token), _readCancellationTokenSource.Token);

            // Start heartbeat task
            _heartbeatCancellationTokenSource = new CancellationTokenSource();
            _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCancellationTokenSource.Token), _heartbeatCancellationTokenSource.Token);

            ConnectionState = ArduinoConnectionState.Connected;
            _logger.LogInformation("Connected to Arduino on port {PortName}", portName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Arduino on port {PortName}", portName);
            ConnectionState = ArduinoConnectionState.Error;
            RaiseError($"Failed to connect to Arduino on port {portName}", ex);
            await DisconnectAsync();
            throw new InvalidOperationException($"Failed to connect to Arduino on port {portName}", ex);
        }
    }

    public async Task DisconnectAsync()
    {
        ThrowIfDisposed();

        SerialPort? portToClose = null;
        lock (_lockObject)
        {
            if (_connectionState == ArduinoConnectionState.Disconnected)
                return;

            portToClose = _serialPort;
            _serialPort = null;
            _connectedPortName = null;
        }

        // Stop heartbeat
        try
        {
            _heartbeatCancellationTokenSource?.Cancel();
            if (_heartbeatTask != null)
            {
                await _heartbeatTask.ConfigureAwait(false);
            }
            _heartbeatCancellationTokenSource?.Dispose();
            _heartbeatCancellationTokenSource = null;
            _heartbeatTask = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping heartbeat task");
        }

        // Stop reading
        try
        {
            _readCancellationTokenSource?.Cancel();
            if (_readTask != null)
            {
                await _readTask.ConfigureAwait(false);
            }
            _readCancellationTokenSource?.Dispose();
            _readCancellationTokenSource = null;
            _readTask = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping read task");
        }

        // Close port
        if (portToClose != null)
        {
            try
            {
                if (portToClose.IsOpen)
                {
                    portToClose.Close();
                }
                portToClose.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing serial port");
            }
        }

        lock (_lockObject)
        {
            _receiveBuffer.Clear();
        }

        ConnectionState = ArduinoConnectionState.Disconnected;
        _logger.LogInformation("Disconnected from Arduino");
    }

    public async Task SendCommandAsync(ArduinoCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        ThrowIfDisposed();

        SerialPort? port;
        lock (_lockObject)
        {
            if (_connectionState != ArduinoConnectionState.Connected || _serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("Arduino is not connected.");

            port = _serialPort;
        }

        try
        {
            var encoded = ArduinoProtocolEncoder.EncodeCommand(command);
            await Task.Run(() =>
            {
                lock (port)
                {
                    port.Write(encoded, 0, encoded.Length);
                }
            });

            _logger.LogTrace("Sent command {CommandType} to Arduino", command.CommandType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command {CommandType} to Arduino", command.CommandType);
            RaiseError($"Failed to send command {command.CommandType} to Arduino", ex);
            throw new ArduinoCommunicationException($"Failed to send command to Arduino", ex);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SerialPort? port;
                lock (_lockObject)
                {
                    port = _serialPort;
                }

                if (port == null || !port.IsOpen)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                int bytesRead;
                try
                {
                    bytesRead = port.Read(buffer, 0, buffer.Length);
                }
                catch (TimeoutException)
                {
                    // Timeout is expected when no data is available
                    continue;
                }

                if (bytesRead > 0)
                {
                    lock (_lockObject)
                    {
                        _receiveBuffer.AddRange(buffer.Take(bytesRead));
                    }

                    ProcessReceiveBuffer();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in read loop");
                RaiseError("Error reading from Arduino", ex);

                // If connection is lost, update state
                lock (_lockObject)
                {
                    if (_serialPort == null || !_serialPort.IsOpen)
                    {
                        ConnectionState = ArduinoConnectionState.Error;
                    }
                }

                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private void ProcessReceiveBuffer()
    {
        lock (_lockObject)
        {
            while (true)
            {
                if (_receiveBuffer.Count == 0)
                    break;

                if (ArduinoProtocolDecoder.TryDecodeEvent(_receiveBuffer.ToArray(), 0, out var decodedEvent) > 0)
                {
                    if (decodedEvent != null)
                    {
                        var eventArgs = new ArduinoEventReceivedEventArgs(
                            decodedEvent.EventType,
                            decodedEvent.Data,
                            decodedEvent.Timestamp
                        );

                        // Remove processed bytes
                        var consumed = ArduinoProtocolDecoder.TryDecodeEvent(_receiveBuffer.ToArray(), 0, out _);
                        _receiveBuffer.RemoveRange(0, consumed);

                        // Raise event outside of lock
                        Task.Run(() => EventReceived?.Invoke(this, eventArgs));
                    }
                    else
                    {
                        // Invalid data, skip one byte
                        _receiveBuffer.RemoveAt(0);
                    }
                }
                else
                {
                    // Not enough data yet
                    break;
                }
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(HeartbeatIntervalMs, cancellationToken);

                if (IsConnected)
                {
                    var command = new ArduinoStatusQueryCommand();
                    await SendCommandAsync(command);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in heartbeat loop");
            }
        }
    }

    private void RaiseError(string message, Exception? exception = null)
    {
        try
        {
            ErrorOccurred?.Invoke(this, new ArduinoErrorEventArgs(message, exception));
        }
        catch
        {
            // Ignore errors in event handlers
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ArduinoSerialService));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during disposal
        }
    }
}
