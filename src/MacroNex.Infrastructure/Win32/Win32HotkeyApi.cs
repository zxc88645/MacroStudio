using static MacroNex.Infrastructure.Win32.Win32Api;
using static MacroNex.Infrastructure.Win32.Win32Structures;

namespace MacroNex.Infrastructure.Win32;

/// <summary>
/// Production implementation of <see cref="IWin32HotkeyApi"/> that forwards to P/Invoke calls.
/// </summary>
internal sealed class Win32HotkeyApi : IWin32HotkeyApi
{
    public bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk) => Win32Api.RegisterHotKey(hWnd, id, fsModifiers, vk);
    public bool UnregisterHotKey(IntPtr hWnd, int id) => Win32Api.UnregisterHotKey(hWnd, id);
    public bool PeekMessage(out MSG msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg) => Win32Api.PeekMessage(out msg, hWnd, wMsgFilterMin, wMsgFilterMax, wRemoveMsg);
    public bool TranslateMessage(ref MSG msg) => Win32Api.TranslateMessage(ref msg);
    public IntPtr DispatchMessage(ref MSG msg) => Win32Api.DispatchMessage(ref msg);
    public bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam) => Win32Api.PostThreadMessage(idThread, msg, wParam, lParam);
    public uint GetCurrentThreadId() => Win32Api.GetCurrentThreadId();
    public uint GetLastError() => Win32Api.GetLastError();
}

