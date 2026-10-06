using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using DemoOCR.Core.Platform;

namespace DemoOCR.Core.Capture
{
    /// <summary>
    /// 全屏截图抓取结果载体（包含物理位图及多显示器虚拟屏幕范围）
    /// </summary>
    public record CapturedScreen(
        BitmapSource FullBitmapSource,
        int VirtualLeft,
        int VirtualTop,
        int VirtualWidth,
        int VirtualHeight
    );

    /// <summary>
    /// 屏幕截图与高速图像格式转换工具类
    /// </summary>
    public static class ScreenCapturer
    {
        /// <summary>读取物理光标坐标，避免界面层直接调用 Win32 API。</summary>
        public static bool TryGetCursorPosition(out Point position)
        {
            bool success = NativeMethods.GetCursorPos(out var nativePosition);
            position = new Point(nativePosition.X, nativePosition.Y);
            return success;
        }

        /// <summary>
        /// 全屏幕冻结抓取（基于底层 GDI BitBlt 极速抓取全部显示器虚拟屏幕，耗时约 5~10ms）
        /// </summary>
        public static CapturedScreen CaptureVirtualScreen()
        {
            int vLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int vTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int vWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int vHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            IntPtr desktopDC = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memoryDC = NativeMethods.CreateCompatibleDC(desktopDC);
            IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(desktopDC, vWidth, vHeight);
            IntPtr oldBitmap = NativeMethods.SelectObject(memoryDC, hBitmap);

            NativeMethods.BitBlt(memoryDC, 0, 0, vWidth, vHeight, desktopDC, vLeft, vTop, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);

            NativeMethods.SelectObject(memoryDC, oldBitmap);
            NativeMethods.DeleteDC(memoryDC);
            NativeMethods.ReleaseDC(IntPtr.Zero, desktopDC);

            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            NativeMethods.DeleteObject(hBitmap);
            return new CapturedScreen(source, vLeft, vTop, vWidth, vHeight);
        }

        /// <summary>
        /// 依据像素矩形从全屏位图中裁剪选区（具备边界越界保护）
        /// </summary>
        public static BitmapSource CropBitmapSource(BitmapSource source, Int32Rect rect)
        {
            if (rect.X < 0) rect.X = 0;
            if (rect.Y < 0) rect.Y = 0;
            if (rect.X + rect.Width > source.PixelWidth) rect.Width = source.PixelWidth - rect.X;
            if (rect.Y + rect.Height > source.PixelHeight) rect.Height = source.PixelHeight - rect.Y;
            if (rect.Width <= 0 || rect.Height <= 0) return source;

            var cropped = new CroppedBitmap(source, rect);
            cropped.Freeze();
            return cropped;
        }

        /// <summary>
        /// 将 WPF BitmapSource 直接通过内存复制转换为 SkiaSharp 的 SKBitmap（耗时低于 0.2ms，免除 PNG 编码解码）
        /// </summary>
        public static SKBitmap ConvertBitmapSourceToSkBitmap(BitmapSource source)
        {
            BitmapSource formattedSource = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = source;
                converted.DestinationFormat = PixelFormats.Bgra32;
                converted.EndInit();
                converted.Freeze();
                formattedSource = converted;
            }

            int width = formattedSource.PixelWidth;
            int height = formattedSource.PixelHeight;
            var skBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

            int stride = width * 4;
            formattedSource.CopyPixels(new Int32Rect(0, 0, width, height), skBitmap.GetPixels(), stride * height, stride);
            return skBitmap;
        }
    }
}
