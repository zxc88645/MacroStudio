using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using static MacroNex.Infrastructure.Win32.Win32Api;
using static MacroNex.Infrastructure.Win32.Win32Structures;

namespace MacroNex.Infrastructure.Adapters;

/// <summary>
/// WH_KEYBOARD_LL based script hotkey listener (no RegisterHotKey).
/// Supports per-hotkey swallow behavior and RepeatWhileHeld throttling.
/// </summary>
public sealed class Win32ScriptHotkeyHookService : IScriptHotkeyHookService, IDisposable
{
    private readonly ILogger<Win32ScriptHotkeyHookService> _logger;
    private readonly object _lock = new();

    private IntPtr _keyboardHook = IntPtr.Zero;
    private readonly HookProc _keyboardProc;

    private Dictionary<Guid, HotkeyDefinition> _hotkeys = new();

    // RepeatWhileHeld throttling (per-script)
    private readonly Dictionary<Guid, DateTime> _lastFireByScript = new();
    private static readonly TimeSpan RepeatThrottle = TimeSpan.FromMilliseconds(120);

    private volatile bool _isDisposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public Win32ScriptHotkeyHookService(ILogger<Win32ScriptHotkeyHookService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardProc = KeyboardHookCallback;
        InstallHook();
    }

    public void SetScriptHotkeys(IReadOnlyDictionary<Guid, HotkeyDefinition> hotkeys)
    {
        if (hotkeys == null) throw new ArgumentNullException(nameof(hotkeys));
        lock (_lock)
        {
            _hotkeys = new Dictionary<Guid, HotkeyDefinition>(hotkeys);
            // prune last-fire cache
            var toRemove = _lastFireByScript.Keys.Where(id => !_hotkeys.ContainsKey(id)).ToList();
            foreach (var id in toRemove) _lastFireByScript.Remove(id);
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

            _logger.LogInformation("Script hotkey keyboard hook installed.");
        }
    }

    private static bool IsDownMessage(int msg) => msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
    private static bool IsUpMessage(int msg) => msg == WM_KEYUP || msg == WM_SYSKEYUP;

    private static HotkeyModifiers GetCurrentModifiers()
    {
        var mods = HotkeyModifiers.None;
        if ((GetAsyncKeyState((int)VirtualKey.VK_CONTROL) & 0x8000) != 0) mods |= HotkeyModifiers.Control;
        if ((GetAsyncKeyState((int)VirtualKey.VK_MENU) & 0x8000) != 0) mods |= HotkeyModifiers.Alt;
        if ((GetAsyncKeyState((int)VirtualKey.VK_SHIFT) & 0x8000) != 0) mods |= HotkeyModifiers.Shift;
        if ((GetAsyncKeyState((int)VirtualKey.VK_LWIN) & 0x8000) != 0 ||
            (GetAsyncKeyState((int)VirtualKey.VK_RWIN) & 0x8000) != 0) mods |= HotkeyModifiers.Windows;
        return mods;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                if (IsDownMessage(msg) || IsUpMessage(msg))
                {
                    // key down only
                    if (!IsDownMessage(msg))
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var key = (VirtualKey)data.vkCode;
                    var isInjected = (data.flags & KbdLlFlags.LLKHF_INJECTED) != 0;
                    if (isInjected)
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                    Guid? matchedScriptId = null;
                    HotkeyDefinition? matchedHotkey = null;
                    var mods = GetCurrentModifiers();

                    lock (_lock)
                    {
                        foreach (var kv in _hotkeys)
                        {
                            var hk = kv.Value;
                            if (hk.Modifiers == mods && hk.Key == key)
                            {
                                // repeat behavior
                                if (hk.TriggerMode == HotkeyTriggerMode.RepeatWhileHeld)
                                {
                                    if (_lastFireByScript.TryGetValue(kv.Key, out var last) &&
                                        DateTime.UtcNow - last < RepeatThrottle)
                                    {
                                        // still optionally swallow
                                        return hk.SwallowKeystroke ? (IntPtr)1 : CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                                    }
                                    _lastFireByScript[kv.Key] = DateTime.UtcNow;
                                }

                                matchedScriptId = kv.Key;
                                matchedHotkey = hk;
                                break;
                            }
                        }
                    }

                    if (matchedScriptId.HasValue && matchedHotkey != null)
                    {
                        // Pack ScriptId into Name for lookup without a new EventArgs type.
                        var evtHotkey = matchedHotkey with { Name = matchedScriptId.Value.ToString() };
                        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(evtHotkey, DateTime.Now));
                        return matchedHotkey.SwallowKeystroke ? (IntPtr)1 : CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Script hotkey hook callback error (ignored).");
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

