using System.Runtime.InteropServices;

namespace TabNest.UiTests.Infrastructure;

/// <summary>ウィンドウサイズ検証用の Win32 API。</summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    /// <summary>
    /// ウィンドウの外接矩形(物理ピクセル)を取得する。
    /// AppWindow.Size / AppWindow.Resize と同じ座標系のため、サイズ復元の検証に使える。
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

    public const uint MouseEventLeftDown = 0x0002;

    public const uint MouseEventLeftUp = 0x0004;

    public const uint MouseEventRightDown = 0x0008;

    public const uint MouseEventRightUp = 0x0010;

    public const uint MouseEventMiddleDown = 0x0020;

    public const uint MouseEventMiddleUp = 0x0040;

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hWnd, uint attribute, out Rect rect, int size);

    /// <summary>DWMWA_EXTENDED_FRAME_BOUNDS: 見えないリサイズ枠を除いた可視ウィンドウ矩形。</summary>
    public const uint DwmwaExtendedFrameBounds = 9;

    /// <summary>
    /// ウィンドウの可視矩形(見えないリサイズ枠を除く・物理ピクセル)を取得する。
    /// WinAppDriver の要素座標はこの矩形の左上を原点とする相対座標のため、
    /// 物理クリックの座標変換に使う。取得失敗時は GetWindowRect にフォールバックする。
    /// </summary>
    public static Rect GetVisibleWindowRect(IntPtr hWnd)
    {
        if (DwmGetWindowAttribute(hWnd, DwmwaExtendedFrameBounds, out var rect, Marshal.SizeOf<Rect>()) == 0)
        {
            return rect;
        }

        GetWindowRect(hWnd, out var fallback);
        return fallback;
    }
}
