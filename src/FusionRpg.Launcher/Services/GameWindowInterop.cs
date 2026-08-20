using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace FusionRpg.Launcher.Services;

/// <summary>
/// Win32 helpers for the game/browser overlay switch: global hotkey registration,
/// locating the game window, and moving focus/geometry between it and the overlay.
/// </summary>
public static class GameWindowInterop
{
    public const int WmHotKey = 0x0312;
    public const int OverlayHotKeyId = 0x5250; // 'RP'
    public const Key DefaultOverlayKey = Key.F10;

    const uint ModNoRepeat = 0x4000;
    const int SwRestore = 9;
    const int SwMinimize = 6;
    static readonly IntPtr HwndTopmost = new(-1);
    const uint SwpShowWindow = 0x0040;
    const int QunsRunningD3dFullScreen = 2; // SHQueryUserNotificationState: exclusive-fullscreen D3D app

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("shell32.dll")]
    static extern int SHQueryUserNotificationState(out int state);

    /// <summary>Main window handle of the running game process, or IntPtr.Zero.</summary>
    public static IntPtr FindGameWindow()
    {
        foreach (var p in Process.GetProcessesByName(ProcessSupervisor.GameProcessName))
        {
            try
            {
                if (!p.HasExited && p.MainWindowHandle != IntPtr.Zero)
                    return p.MainWindowHandle;
            }
            catch { /* process died between enumerate and read */ }
            finally { p.Dispose(); }
        }
        return IntPtr.Zero;
    }

    public static bool TryGetGameWindowRect(out Rect rect)
    {
        rect = default;
        var hwnd = FindGameWindow();
        if (hwnd == IntPtr.Zero || IsIconic(hwnd)) return false; // minimized rect is off-screen garbage
        return GetWindowRect(hwnd, out rect) && rect.Width > 0 && rect.Height > 0;
    }

    /// <summary>
    /// True when the game is the foreground window in exclusive-fullscreen D3D mode —
    /// topmost windows cannot render over it, so the overlay must fall back to a window switch.
    /// </summary>
    public static bool IsGameExclusiveFullscreen()
    {
        var hwnd = FindGameWindow();
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            if (SHQueryUserNotificationState(out var state) != 0) return false;
            return state == QunsRunningD3dFullScreen && GetForegroundWindow() == hwnd;
        }
        catch
        {
            return false;
        }
    }

    public static void MinimizeGame()
    {
        var hwnd = FindGameWindow();
        if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SwMinimize);
    }

    /// <summary>Place <paramref name="hwnd"/> exactly over the given device-pixel rect, topmost.</summary>
    public static void MoveWindowOver(IntPtr hwnd, Rect rect) =>
        SetWindowPos(hwnd, HwndTopmost, rect.Left, rect.Top, rect.Width, rect.Height, SwpShowWindow);

    /// <summary>Bring the game window back to the foreground (restore if minimized).</summary>
    public static bool FocusGame()
    {
        var hwnd = FindGameWindow();
        if (hwnd == IntPtr.Zero) return false;
        if (IsIconic(hwnd)) ShowWindow(hwnd, SwRestore);
        return SetForegroundWindow(hwnd);
    }

    public static bool TryRegisterOverlayHotKey(IntPtr hwnd, Key key) =>
        RegisterHotKey(hwnd, OverlayHotKeyId, ModNoRepeat, (uint)KeyInterop.VirtualKeyFromKey(key));

    public static void UnregisterOverlayHotKey(IntPtr hwnd)
    {
        try { UnregisterHotKey(hwnd, OverlayHotKeyId); }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Parse a settings hotkey name ("F10", "F9", "Pause", …) into a WPF Key.
    /// Falls back to <see cref="DefaultOverlayKey"/> for null/unknown/modifier-only names.
    /// </summary>
    public static Key ParseOverlayKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DefaultOverlayKey;
        if (!Enum.TryParse<Key>(name.Trim(), ignoreCase: true, out var key))
            return DefaultOverlayKey;
        return key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin
            ? DefaultOverlayKey
            : key;
    }
}
