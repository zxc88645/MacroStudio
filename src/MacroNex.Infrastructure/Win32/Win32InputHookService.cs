using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using static MacroNex.Infrastructure.Win32.Win32Api;
using static MacroNex.Infrastructure.Win32.Win32Structures;

namespace MacroNex.Infrastructure.Win32;

/// <summary>
/// Win32 implementation of global input capture using low-level hooks (WH_MOUSE_LL / WH_KEYBOARD_LL).
/// </summary>
public sealed class Win32InputHookService : IInputHookService, IDisposable
{
    private readonly ILogger<Win32InputHookService> _logger;
    private readonly object _lock = new();

    private RecordingOptions? _options;

    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;

    // Keep delegates alive (otherwise GC can collect them and crash)
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;

    private bool _isDisposed;

    public Win32InputHookService(ILogger<Win32InputHookService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public bool IsInstalled
    {
        get
        {
            lock (_lock)
            {
                return _mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero;
            }
        }
    }

    public event EventHandler<InputHookMouseMoveEventArgs>? MouseMoved;
    public event EventHandler<InputHookMouseClickEventArgs>? MouseClicked;
    public event EventHandler<InputHookKeyEventArgs>? KeyboardInput;

    public Task InstallHooksAsync(RecordingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        ThrowIfDisposed();

        lock (_lock)
        {
            _options = options;

            if (_mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero)
                return Task.CompletedTask;

            var moduleHandle = GetModuleHandle(null);
            if (moduleHandle == IntPtr.Zero)
            {
                var error = (int)GetLastError();
                throw new InvalidOperationException($"GetModuleHandle failed (Win32Error={error}).");
            }

            // Only install what we need (mouse and/or keyboard)
            if (options.RecordMouseMovements || options.RecordMouseClicks)
            {
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
                if (_mouseHook == IntPtr.Zero)
                {
                    var error = (int)GetLastError();
                    throw new InvalidOperationException($"SetWindowsHookEx(WH_MOUSE_LL) failed (Win32Error={error}).");
                }
            }

            if (options.RecordKeyboardInput)
            {
                _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
                if (_keyboardHook == IntPtr.Zero)
                {
                    var error = (int)GetLastError();
                    // Best effort cleanup if mouse hook already installed
                    if (_mouseHook != IntPtr.Zero)
                    {
                        try { UnhookWindowsHookEx(_mouseHook); } catch { /* ignore */ }
                        _mouseHook = IntPtr.Zero;
                    }
                    throw new InvalidOperationException($"SetWindowsHookEx(WH_KEYBOARD_LL) failed (Win32Error={error}).");
                }
            }

            _logger.LogInformation("Input hooks installed. Mouse={MouseInstalled}, Keyboard={KeyboardInstalled}", _mouseHook != IntPtr.Zero, _keyboardHook != IntPtr.Zero);
        }

        return Task.CompletedTask;
    }

    public Task UninstallHooksAsync()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            _options = null;
            _logger.LogInformation("Input hooks uninstalled.");
        }

        return Task.CompletedTask;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var position = new Point(data.pt.X, data.pt.Y);
                var isInjected = (data.flags & MsLlFlags.LLMHF_INJECTED) != 0;

                // Move
                if (msg == WM_MOUSEMOVE)
                {
                    MouseMoved?.Invoke(this, new InputHookMouseMoveEventArgs(position));
                }
                else
                {
                    if (TryMapMouseClick(msg, data.mouseData, out var button, out var clickType))
                    {
                        // Optional filtering of injected/system events
                        var opts = _options;
                        if (opts?.FilterSystemEvents == true && isInjected)
                            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                        MouseClicked?.Invoke(this, new InputHookMouseClickEventArgs(position, button, clickType, isInjected));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MouseHookCallback error (ignored).");
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool TryMapMouseClick(int msg, uint mouseData, out MouseButton button, out ClickType clickType)
    {
        button = MouseButton.Left;
        clickType = ClickType.Click;

        switch (msg)
        {
            case WM_LBUTTONDOWN:
                button = MouseButton.Left;
                clickType = ClickType.Down;
                return true;
            case WM_LBUTTONUP:
                button = MouseButton.Left;
                clickType = ClickType.Up;
                return true;
            case WM_RBUTTONDOWN:
                button = MouseButton.Right;
                clickType = ClickType.Down;
                return true;
            case WM_RBUTTONUP:
                button = MouseButton.Right;
                clickType = ClickType.Up;
                return true;
            case WM_MBUTTONDOWN:
                button = MouseButton.Middle;
                clickType = ClickType.Down;
                return true;
            case WM_MBUTTONUP:
                button = MouseButton.Middle;
                clickType = ClickType.Up;
                return true;
            case WM_XBUTTONDOWN:
            case WM_XBUTTONUP:
                {
                    // High word of mouseData indicates which X button.
                    var xButton = (mouseData >> 16) & 0xFFFF;
                    button = xButton == XBUTTON2 ? MouseButton.XButton2 : MouseButton.XButton1;
                    clickType = msg == WM_XBUTTONDOWN ? ClickType.Down : ClickType.Up;
                    return true;
                }
            default:
                return false;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var vk = (VirtualKey)data.vkCode;
                    var isInjected = (data.flags & KbdLlFlags.LLKHF_INJECTED) != 0;
                    var isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;

                    // Optional filtering of injected/system events
                    var opts = _options;
                    if (opts?.FilterSystemEvents == true && isInjected)
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    KeyboardInput?.Invoke(this, new InputHookKeyEventArgs(vk, isDown, isInjected));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "KeyboardHookCallback error (ignored).");
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    // NOTE: Keyboard capture intentionally records low-level down/up events instead of translating to text.

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(Win32InputHookService));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        try { UninstallHooksAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }
        _isDisposed = true;
    }
}

