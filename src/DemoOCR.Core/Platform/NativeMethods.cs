using System;
using System.Runtime.InteropServices;

namespace DemoOCR.Core.Platform
{
    /// <summary>
    /// Win32 底层 API 与常量封装（用于全局热键、桌面物理尺寸获取与 GDI+ 极速位图抓取）
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>系统全局热键通知消息</summary>
        public const int WM_HOTKEY = 0x0312;

        /// <summary>热键修饰键常量</summary>
        public const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        /// <summary>GDI 光栅操作码：直接复制源矩形至目标</summary>
        public const int SRCCOPY = 0x00CC0020;
        /// <summary>包含半透明或分层窗口 (Layered Windows) 的内容</summary>
        public const int CAPTUREBLT = 0x40000000;

        /// <summary>多显示器虚拟屏幕坐标与尺寸常量</summary>
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        /// <summary>注册全局热键</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>注销全局热键</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>获取系统屏幕指标（支持跨屏虚拟屏幕测量）</summary>
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        /// <summary>获取桌面窗口句柄</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        /// <summary>获取指定窗口的设备上下文 DC</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        /// <summary>释放设备上下文 DC</summary>
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        /// <summary>创建与指定设备兼容的内存 DC</summary>
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        /// <summary>创建与指定设备兼容的位图</summary>
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        /// <summary>将 GDI 对象选入指定设备上下文</summary>
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        /// <summary>删除内存设备上下文</summary>
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        /// <summary>删除 GDI 逻辑对象（如位图句柄）</summary>
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        /// <summary>执行位块传输（从源 DC 极速传输位图数据到目标 DC）</summary>
        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        /// <summary>物理光标坐标结构体</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>获取当前物理光标坐标</summary>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
    }
}
