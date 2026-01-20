using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using static MacroStudio.Infrastructure.Win32.Win32Api;
using static MacroStudio.Infrastructure.Win32.Win32Structures;

namespace MacroStudio.Infrastructure.Adapters;

/// <summary>
/// Win32-based implementation of input simulation using SendInput API.
/// Provides mouse and keyboard automation capabilities with coordinate transformation and timing utilities.
/// </summary>
public class Win32InputSimulator : IInputSimulator
{
    private readonly ILogger<Win32InputSimulator> _logger;
    private readonly object _lockObject = new();
    private bool _isDisposed = false;

    /// <summary>
    /// Initializes a new instance of the Win32InputSimulator class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public Win32InputSimulator(ILogger<Win32InputSimulator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("Win32InputSimulator initialized");
    }

    /// <inheritdoc />
    public async Task SimulateMouseMoveAsync(Point position)
    {
        ThrowIfDisposed();
        ValidatePosition(position);

        _logger.LogDebug("Simulating mouse move to {Position}", position);

        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                // Use SetCursorPos for direct cursor positioning
                if (!SetCursorPos(position.X, position.Y))
                {
                    var error = GetLastError();
                    _logger.LogError("Failed to move cursor to {Position}. Win32 error: {Error}", position, error);
                    throw new InputSimulationException($"Failed to move cursor to {position}", (int)error);
                }

                _logger.LogTrace("Successfully moved cursor to {Position}", position);
            }
        });
    }

    /// <inheritdoc />
    public async Task SimulateMouseClickAsync(MouseButton button, ClickType type)
    {
        ThrowIfDisposed();
        ValidateMouseButton(button);
        ValidateClickType(type);

        _logger.LogDebug("Simulating {ClickType} {Button} click at current cursor position", type, button);

        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                // Perform the click action at the current cursor position
                var inputs = CreateMouseClickInputs(button, type);
                SendInputs(inputs);

                _logger.LogTrace("Successfully performed {ClickType} {Button} click at current cursor position", type, button);
            }
        });
    }

    /// <inheritdoc />
    public async Task SimulateKeyboardInputAsync(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        ThrowIfDisposed();

        if (string.IsNullOrEmpty(text))
        {
            _logger.LogDebug("Skipping empty text input");
            return;
        }

        _logger.LogDebug("Simulating keyboard input: {Text}", text);

        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                // Use Unicode input for text
                var inputs = new List<INPUT>();

                foreach (char c in text)
                {
                    // Key down
                    inputs.Add(new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    });

                    // Key up
                    inputs.Add(new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    });
                }

                SendInputs(inputs.ToArray());
                _logger.LogTrace("Successfully sent keyboard input: {Text}", text);
            }
        });
    }

    /// <inheritdoc />
    public async Task SimulateKeyPressAsync(VirtualKey key, bool isDown)
    {
        ThrowIfDisposed();
        ValidateVirtualKey(key);

        _logger.LogDebug("Simulating key {Action}: {Key}", isDown ? "press" : "release", key);

        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                var input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (ushort)key,
                            wScan = 0,
                            dwFlags = isDown ? 0 : KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                SendInputs(new[] { input });
                _logger.LogTrace("Successfully {Action} key: {Key}", isDown ? "pressed" : "released", key);
            }
        });
    }

    /// <inheritdoc />
    public async Task SimulateKeyComboAsync(IEnumerable<VirtualKey> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var keyList = keys.ToList();
        if (keyList.Count == 0)
            throw new ArgumentException("Keys collection cannot be empty", nameof(keys));

        ThrowIfDisposed();

        foreach (var key in keyList)
        {
            ValidateVirtualKey(key);
        }

        _logger.LogDebug("Simulating key combination: {Keys}", string.Join(" + ", keyList));

        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                var inputs = new List<INPUT>();

                // Press all keys down
                foreach (var key in keyList)
                {
                    inputs.Add(new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = (ushort)key,
                                wScan = 0,
                                dwFlags = 0,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    });
                }

                // Release all keys in reverse order
                foreach (var key in keyList.AsEnumerable().Reverse())
                {
                    inputs.Add(new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = (ushort)key,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = IntPtr.Zero
                            }
                        }
                    });
                }

                SendInputs(inputs.ToArray());
                _logger.LogTrace("Successfully sent key combination: {Keys}", string.Join(" + ", keyList));
            }
        });
    }

    /// <inheritdoc />
    public async Task DelayAsync(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Duration cannot be negative", nameof(duration));

        ThrowIfDisposed();

        if (duration == TimeSpan.Zero)
        {
            return;
        }

        _logger.LogTrace("Delaying for {Duration}", duration);
        await Task.Delay(duration);
    }

    /// <inheritdoc />
    public async Task<Point> GetCursorPositionAsync()
    {
        ThrowIfDisposed();

        return await Task.Run(() =>
        {
            lock (_lockObject)
            {
                if (!GetCursorPos(out POINT point))
                {
                    var error = GetLastError();
                    _logger.LogError("Failed to get cursor position. Win32 error: {Error}", error);
                    throw new InputSimulationException("Failed to get cursor position", (int)error);
                }

                var position = new Point(point.X, point.Y);
                _logger.LogTrace("Current cursor position: {Position}", position);
                return position;
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync()
    {
        if (_isDisposed)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                // Test if we can get cursor position as a basic readiness check
                return GetCursorPos(out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Input simulator readiness check failed");
                return false;
            }
        });
    }

    /// <summary>
    /// Creates mouse click input structures for the specified button and click type.
    /// </summary>
    /// <param name="button">The mouse button to click.</param>
    /// <param name="type">The type of click action.</param>
    /// <returns>Array of INPUT structures for the click action.</returns>
    private INPUT[] CreateMouseClickInputs(MouseButton button, ClickType type)
    {
        var inputs = new List<INPUT>();
        var (downFlag, upFlag, mouseData) = GetMouseEventFlags(button);

        switch (type)
        {
            case ClickType.Down:
                inputs.Add(CreateMouseInput(downFlag, mouseData));
                break;

            case ClickType.Up:
                inputs.Add(CreateMouseInput(upFlag, mouseData));
                break;

            case ClickType.Click:
                inputs.Add(CreateMouseInput(downFlag, mouseData));
                inputs.Add(CreateMouseInput(upFlag, mouseData));
                break;
        }

        return inputs.ToArray();
    }

    /// <summary>
    /// Creates a mouse input structure.
    /// </summary>
    /// <param name="flags">Mouse event flags.</param>
    /// <param name="mouseData">Additional mouse data.</param>
    /// <returns>INPUT structure for mouse input.</returns>
    private INPUT CreateMouseInput(uint flags, uint mouseData = 0)
    {
        return new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    /// <summary>
    /// Gets the Win32 mouse event flags for the specified button.
    /// </summary>
    /// <param name="button">The mouse button.</param>
    /// <returns>Tuple containing down flag, up flag, and mouse data.</returns>
    private (uint downFlag, uint upFlag, uint mouseData) GetMouseEventFlags(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, 0),
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, 0),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP, 0),
            MouseButton.XButton1 => (MOUSEEVENTF_XDOWN, MOUSEEVENTF_XUP, XBUTTON1),
            MouseButton.XButton2 => (MOUSEEVENTF_XDOWN, MOUSEEVENTF_XUP, XBUTTON2),
            _ => throw new ArgumentException($"Unsupported mouse button: {button}", nameof(button))
        };
    }

    /// <summary>
    /// Sends input events using the Win32 SendInput API.
    /// </summary>
    /// <param name="inputs">Array of input structures to send.</param>
    private void SendInputs(INPUT[] inputs)
    {
        if (inputs.Length == 0)
            return;

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            var error = GetLastError();
            _logger.LogError("SendInput failed. Expected {Expected} inputs, sent {Sent}. Win32 error: {Error}", 
                inputs.Length, sent, error);
            throw new InputSimulationException($"SendInput failed. Expected {inputs.Length} inputs, sent {sent}", (int)error);
        }
    }

    /// <summary>
    /// Validates that the position coordinates are valid.
    /// </summary>
    /// <param name="position">The position to validate.</param>
    private void ValidatePosition(Point position)
    {
        if (position.X < 0 || position.Y < 0)
        {
            throw new ArgumentException($"Position coordinates must be non-negative. Got: {position}", nameof(position));
        }

        // Optional: Validate against screen bounds
        var screenWidth = GetSystemMetrics(SM_CXSCREEN);
        var screenHeight = GetSystemMetrics(SM_CYSCREEN);

        if (position.X >= screenWidth || position.Y >= screenHeight)
        {
            _logger.LogWarning("Position {Position} is outside screen bounds ({Width}x{Height})", 
                position, screenWidth, screenHeight);
        }
    }

    /// <summary>
    /// Validates that the mouse button is supported.
    /// </summary>
    /// <param name="button">The mouse button to validate.</param>
    private void ValidateMouseButton(MouseButton button)
    {
        if (!Enum.IsDefined(typeof(MouseButton), button))
        {
            throw new ArgumentException($"Invalid mouse button: {button}", nameof(button));
        }
    }

    /// <summary>
    /// Validates that the click type is supported.
    /// </summary>
    /// <param name="type">The click type to validate.</param>
    private void ValidateClickType(ClickType type)
    {
        if (!Enum.IsDefined(typeof(ClickType), type))
        {
            throw new ArgumentException($"Invalid click type: {type}", nameof(type));
        }
    }

    /// <summary>
    /// Validates that the virtual key is supported.
    /// </summary>
    /// <param name="key">The virtual key to validate.</param>
    private void ValidateVirtualKey(VirtualKey key)
    {
        if (!Enum.IsDefined(typeof(VirtualKey), key))
        {
            throw new ArgumentException($"Invalid virtual key: {key}", nameof(key));
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(Win32InputSimulator));
        }
    }

    /// <summary>
    /// Disposes the input simulator and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _logger.LogDebug("Disposing Win32InputSimulator");
            _isDisposed = true;
        }
    }
}