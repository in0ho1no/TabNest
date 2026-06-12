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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint format, IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    /// <summary>
    /// クリップボードに Unicode テキストを設定する。
    /// JIS 配列では SendKeys の記号(「"」等)が US スキャンコードで化けるため、
    /// 記号を含む文字列の入力は Ctrl+V 貼り付けで行う。
    /// </summary>
    public static void SetClipboardText(string text)
    {
        const uint cfUnicodeText = 13;
        const uint gmemMoveable = 0x0002;

        if (!OpenClipboard(IntPtr.Zero))
        {
            throw new InvalidOperationException("クリップボードを開けませんでした。");
        }

        try
        {
            EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var handle = GlobalAlloc(gmemMoveable, bytes);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("クリップボード用メモリの確保に失敗しました。");
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                GlobalFree(handle);
                throw new InvalidOperationException("クリップボード用メモリのロックに失敗しました。");
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            // 成功するとクリップボードがメモリの所有権を持つため解放しない(失敗時のみ解放する)
            if (SetClipboardData(cfUnicodeText, handle) == IntPtr.Zero)
            {
                GlobalFree(handle);
                throw new InvalidOperationException("クリップボードへの設定に失敗しました。");
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

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
