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
}
