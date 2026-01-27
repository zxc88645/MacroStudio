using System.Runtime.InteropServices;
using static MacroStudio.Infrastructure.Win32.Win32Structures;

namespace MacroStudio.Infrastructure.Win32;

/// <summary>
/// Win32 API imports for input simulation.
/// </summary>
internal static class Win32Api
{
    /// <summary>
    /// Synthesizes keystrokes, mouse motions, and button clicks.
    /// </summary>
    /// <param name="nInputs">The number of structures in the pInputs array.</param>
    /// <param name="pInputs">An array of INPUT structures.</param>
    /// <param name="cbSize">The size, in bytes, of an INPUT structure.</param>
    /// <returns>The number of events that it successfully inserted into the keyboard or mouse input stream.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Retrieves the cursor's position, in screen coordinates.
    /// </summary>
    /// <param name="lpPoint">A pointer to a POINT structure that receives the screen coordinates of the cursor.</param>
    /// <returns>Returns nonzero if successful or zero otherwise.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// Sets the cursor position to the specified screen coordinates.
    /// </summary>
    /// <param name="x">The new x-coordinate of the cursor, in screen coordinates.</param>
    /// <param name="y">The new y-coordinate of the cursor, in screen coordinates.</param>
    /// <returns>Returns nonzero if successful or zero otherwise.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    /// <summary>
    /// Retrieves the dimensions of the bounding rectangle of the specified window.
    /// </summary>
    /// <param name="hWnd">A handle to the window.</param>
    /// <param name="lpRect">A pointer to a RECT structure that receives the screen coordinates.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Retrieves a handle to the desktop window.
    /// </summary>
    /// <returns>The return value is a handle to the desktop window.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDesktopWindow();

    /// <summary>
    /// Retrieves the width and height of the screen.
    /// </summary>
    /// <param name="nIndex">The system metric to be retrieved.</param>
    /// <returns>The requested system metric or configuration information.</returns>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // System metrics constants
    public const int SM_CXSCREEN = 0;   // Width of the primary screen
    public const int SM_CYSCREEN = 1;   // Height of the primary screen
    public const int SM_XVIRTUALSCREEN = 76;   // Left of virtual desktop
    public const int SM_YVIRTUALSCREEN = 77;   // Top of virtual desktop
    public const int SM_CXVIRTUALSCREEN = 78;  // Width of virtual desktop (all monitors)
    public const int SM_CYVIRTUALSCREEN = 79;  // Height of virtual desktop (all monitors)

    /// <summary>
    /// Rectangle structure for window bounds.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Gets the last Win32 error code.
    /// </summary>
    /// <returns>The calling thread's last-error code.</returns>
    [DllImport("kernel32.dll")]
    public static extern uint GetLastError();

    /// <summary>
    /// Retrieves a module handle for the specified module.
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // Hotkey registration APIs

    /// <summary>
    /// Defines a system-wide hot key.
    /// </summary>
    /// <param name="hWnd">A handle to the window that will receive WM_HOTKEY messages.</param>
    /// <param name="id">The identifier of the hot key.</param>
    /// <param name="fsModifiers">The keys that must be pressed in combination with the key specified by the uVirtKey parameter.</param>
    /// <param name="vk">The virtual-key code of the hot key.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// Frees a hot key previously registered by the calling thread.
    /// </summary>
    /// <param name="hWnd">A handle to the window associated with the hot key to be freed.</param>
    /// <param name="id">The identifier of the hot key to be freed.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Window message handling APIs

    /// <summary>
    /// Retrieves a message from the calling thread's message queue.
    /// </summary>
    /// <param name="lpMsg">A pointer to an MSG structure that receives message information.</param>
    /// <param name="hWnd">A handle to the window whose messages are to be retrieved.</param>
    /// <param name="wMsgFilterMin">The integer value of the lowest message value to be retrieved.</param>
    /// <param name="wMsgFilterMax">The integer value of the highest message value to be retrieved.</param>
    /// <returns>If the function retrieves a message other than WM_QUIT, the return value is nonzero.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    /// <summary>
    /// Checks a thread message queue for a message and places the message (if any) in the specified structure.
    /// </summary>
    /// <param name="lpMsg">A pointer to an MSG structure that receives message information.</param>
    /// <param name="hWnd">A handle to the window whose messages are to be examined.</param>
    /// <param name="wMsgFilterMin">The value of the first message in the range of messages to be examined.</param>
    /// <param name="wMsgFilterMax">The value of the last message in the range of messages to be examined.</param>
    /// <param name="wRemoveMsg">Specifies how messages are to be handled.</param>
    /// <returns>If a message is available, the return value is nonzero.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    /// <summary>
    /// Translates virtual-key messages into character messages.
    /// </summary>
    /// <param name="lpMsg">A pointer to an MSG structure that contains message information retrieved from the calling thread's message queue.</param>
    /// <returns>If the message is translated (that is, a character message is posted to the thread's message queue), the return value is nonzero.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    /// <summary>
    /// Dispatches a message to a window procedure.
    /// </summary>
    /// <param name="lpMsg">A pointer to a structure that contains the message.</param>
    /// <returns>The return value specifies the value returned by the window procedure.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref MSG lpMsg);

    /// <summary>
    /// Posts a message to the message queue of the specified thread.
    /// </summary>
    /// <param name="idThread">The identifier of the thread to which the message is to be posted.</param>
    /// <param name="Msg">The type of message to be posted.</param>
    /// <param name="wParam">Additional message-specific information.</param>
    /// <param name="lParam">Additional message-specific information.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Retrieves the thread identifier of the calling thread.
    /// </summary>
    /// <returns>The thread identifier of the calling thread.</returns>
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    // Window message constants
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_HOTKEY_REGISTER = 0x8000; // Custom message for registering hotkeys on message loop thread
    public const uint WM_HOTKEY_UNREGISTER = 0x8001; // Custom message for unregistering hotkeys on message loop thread
    public const uint PM_REMOVE = 0x0001;

    // Hotkey modifier constants
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Error codes
    public const uint ERROR_HOTKEY_ALREADY_REGISTERED = 1409;
    public const uint ERROR_HOTKEY_NOT_REGISTERED = 1419;

    // Low-level hook APIs

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;

    public const uint MAPVK_VK_TO_VSC = 0x0;


    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags,
        IntPtr dwhkl);
}