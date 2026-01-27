using static MacroNex.Infrastructure.Win32.Win32Structures;

namespace MacroNex.Infrastructure.Win32;

/// <summary>
/// Abstraction over Win32 hotkey and message-loop APIs so they can be mocked in tests.
/// </summary>
internal interface IWin32HotkeyApi
{
    bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    bool UnregisterHotKey(IntPtr hWnd, int id);
    bool PeekMessage(out MSG msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    bool TranslateMessage(ref MSG msg);
    IntPtr DispatchMessage(ref MSG msg);
    bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    uint GetCurrentThreadId();
    uint GetLastError();
}

