using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using static MacroNex.Infrastructure.Win32.Win32Api;
using static MacroNex.Infrastructure.Win32.Win32Structures;

namespace MacroNex.Infrastructure.Adapters;

/// <summary>
/// WH_KEYBOARD_LL based recording hotkey listener (no RegisterHotKey).
/// </summary>
public sealed class Win32RecordingHotkeyHookService : IRecordingHotkeyHookService, IDisposable
{
    private readonly ILogger<Win32RecordingHotkeyHookService> _logger;
    private readonly object _lock = new();

    private IntPtr _keyboardHook = IntPtr.Zero;
    private readonly HookProc _keyboardProc;

    private HotkeyDefinition? _start;
    private HotkeyDefinition? _pause;
    private HotkeyDefinition? _stop;

    private volatile bool _isDisposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public Win32RecordingHotkeyHookService(ILogger<Win32RecordingHotkeyHookService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardProc = KeyboardHookCallback;

        InstallHook();
    }

    public void SetHotkeys(HotkeyDefinition? start, HotkeyDefinition? pause, HotkeyDefinition? stop)
    {
        lock (_lock)
        {
            _start = start;
            _pause = pause;
            _stop = stop;
        }
    }

    private void InstallHook()
    {
        lock (_lock)
        {
            if (_keyboardHook != IntPtr.Zero)
                return;

            var moduleHandle = GetModuleHandle(null);
            if (moduleHandle == IntPtr.Zero)
            {
                var error = (int)GetLastError();
                throw new InvalidOperationException($"GetModuleHandle failed (Win32Error={error}).");
            }

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            if (_keyboardHook == IntPtr.Zero)
            {
                var error = (int)GetLastError();
                throw new InvalidOperationException($"SetWindowsHookEx(WH_KEYBOARD_LL) failed (Win32Error={error}).");
            }

            _logger.LogInformation("Recording hotkey keyboard hook installed.");
        }
    }

    private static bool IsDownMessage(int msg) => msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
    private static bool IsUpMessage(int msg) => msg == WM_KEYUP || msg == WM_SYSKEYUP;

    private HotkeyModifiers GetCurrentModifiers()
    {
        var mods = HotkeyModifiers.None;

        // High bit indicates key is down.
        if ((GetAsyncKeyState((int)VirtualKey.VK_CONTROL) & 0x8000) != 0) mods |= HotkeyModifiers.Control;
        if ((GetAsyncKeyState((int)VirtualKey.VK_MENU) & 0x8000) != 0) mods |= HotkeyModifiers.Alt;
        if ((GetAsyncKeyState((int)VirtualKey.VK_SHIFT) & 0x8000) != 0) mods |= HotkeyModifiers.Shift;
        if ((GetAsyncKeyState((int)VirtualKey.VK_LWIN) & 0x8000) != 0 ||
            (GetAsyncKeyState((int)VirtualKey.VK_RWIN) & 0x8000) != 0) mods |= HotkeyModifiers.Windows;

        return mods;
    }

    private static bool Matches(HotkeyDefinition? hk, HotkeyModifiers mods, VirtualKey key)
        => hk != null && hk.Modifiers == mods && hk.Key == key;

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                if (IsDownMessage(msg) || IsUpMessage(msg))
                {
                    // Only act on key down to avoid double-trigger.
                    if (!IsDownMessage(msg))
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var key = (VirtualKey)data.vkCode;
                    var isInjected = (data.flags & KbdLlFlags.LLKHF_INJECTED) != 0;

                    // Ignore injected events (avoid recursive triggers).
                    if (isInjected)
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    HotkeyDefinition? matched = null;
                    lock (_lock)
                    {
                        var mods = GetCurrentModifiers();
                        if (Matches(_start, mods, key)) matched = _start;
                        else if (Matches(_pause, mods, key)) matched = _pause;
                        else if (Matches(_stop, mods, key)) matched = _stop;
                    }

                    if (matched != null)
                    {
                        try
                        {
                            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(matched, DateTime.Now));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in recording hotkey handler");
                        }

                        // Swallow to reduce interference/accidental recording.
                        return (IntPtr)1;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Recording hotkey hook callback error (ignored).");
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        lock (_lock)
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(_keyboardHook); } catch { }
                _keyboardHook = IntPtr.Zero;
            }
        }
    }
}

