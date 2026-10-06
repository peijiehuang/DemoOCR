using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DemoOCR.Core.Capture;
using DemoOCR.Core.Configuration;
using DemoOCR.Core.Persistence;
using DemoOCR.Core.Platform;

namespace DemoOCR.Views
{
    /// <summary>
    /// 全屏截图框选遮罩层窗口交互逻辑（无缝覆盖所有显示器，支持框选、尺寸标注、双击/回车确认与快捷键）
    /// </summary>
    public partial class SnipOverlayWindow : Window
    {
        private readonly CapturedScreen _capturedScreen;
        private readonly HotkeyAction _action;
        private readonly double _scaleX;
        private readonly double _scaleY;
        private Point _startPoint;
        private bool _isSelecting;
        private Rect _selectedRect;

        public SnipOverlayWindow(CapturedScreen capturedScreen, HotkeyAction action)
        {
            InitializeComponent();
            _capturedScreen = capturedScreen;
            _action = action;

            // 预先计算物理像素与 WPF 设备无关像素 (DIPs) 之间的缩放比率
            _scaleX = (double)_capturedScreen.FullBitmapSource.PixelWidth / SystemParameters.VirtualScreenWidth;
            _scaleY = (double)_capturedScreen.FullBitmapSource.PixelHeight / SystemParameters.VirtualScreenHeight;

            // 窗口尺寸精准覆盖全部显示器区域
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            FreezeImage.Source = _capturedScreen.FullBitmapSource;
            ScreenGeometry.Rect = new Rect(0, 0, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            SelectionGeometry.Rect = Rect.Empty;

            if (_action == HotkeyAction.SnipAndCopy)
            {
                BtnConfirm.Content = "📋 复制 (Enter)";
                BtnCopy.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>鼠标按下：右键取消退出，左键启动矩形选区或双击选区确认</summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                Close();
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2 && _selectedRect.Contains(e.GetPosition(this)))
                {
                    Confirm();
                    return;
                }

                _startPoint = e.GetPosition(this);
                _isSelecting = true;
                ActionToolbar.Visibility = Visibility.Collapsed;
                SelectionBorder.Visibility = Visibility.Visible;
                SizeTipBorder.Visibility = Visibility.Visible;
            }
        }

        /// <summary>鼠标移动：动态计算选区矩形、更新挖空遮罩与物理分辨率尺寸浮标</summary>
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            Point currentPoint = e.GetPosition(this);
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            _selectedRect = new Rect(x, y, w, h);
            SelectionGeometry.Rect = _selectedRect;

            Canvas.SetLeft(SelectionBorder, x);
            Canvas.SetTop(SelectionBorder, y);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;

            // 显示真实物理像素分辨率 (如 640 × 480)
            int actualW = (int)Math.Round(w * _scaleX);
            int actualH = (int)Math.Round(h * _scaleY);
            SizeTipText.Text = $"{actualW} × {actualH}";

            double tipTop = y - 28 >= 0 ? y - 28 : y + 6;
            Canvas.SetLeft(SizeTipBorder, x);
            Canvas.SetTop(SizeTipBorder, tipTop);
        }

        /// <summary>鼠标松开：若配置自动识别则直接触发，否则展示右下角悬浮操作栏</summary>
        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            if (_selectedRect.Width < 10 || _selectedRect.Height < 10)
            {
                SelectionGeometry.Rect = Rect.Empty;
                SelectionBorder.Visibility = Visibility.Collapsed;
                SizeTipBorder.Visibility = Visibility.Collapsed;
                ActionToolbar.Visibility = Visibility.Collapsed;
                return;
            }

            if (AppSettings.Current.AutoOcrOnSelect)
            {
                Confirm();
                return;
            }

            // 定位操作悬浮工具栏在选区右下角
            ActionToolbar.Visibility = Visibility.Visible;
            ActionToolbar.UpdateLayout();

            double tbLeft = _selectedRect.Right - ActionToolbar.ActualWidth;
            double tbTop = _selectedRect.Bottom + 6;

            if (tbTop + ActionToolbar.ActualHeight > Height) tbTop = _selectedRect.Bottom - ActionToolbar.ActualHeight - 6;
            if (tbLeft < 0) tbLeft = 6;
            if (tbLeft + ActionToolbar.ActualWidth > Width) tbLeft = Width - ActionToolbar.ActualWidth - 6;

            Canvas.SetLeft(ActionToolbar, tbLeft);
            Canvas.SetTop(ActionToolbar, tbTop);
        }

        /// <summary>按键快捷操作：Esc 取消，Enter 确认，F3 贴图</summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Enter) Confirm();
            else if (e.Key == Key.F3) PinSelectionAndClose();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e) => Confirm();
        private void BtnCopy_Click(object sender, RoutedEventArgs e) => CopyToClipboardAndClose();
        private void BtnPin_Click(object sender, RoutedEventArgs e) => PinSelectionAndClose();
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>裁剪当前选区并作为桌面贴图固定</summary>
        private void PinSelectionAndClose()
        {
            if (_selectedRect.Width < 10 || _selectedRect.Height < 10) { Close(); return; }
            var wpfCrop = CalculatePhysicalCropRect();
            var croppedWpf = ScreenCapturer.CropBitmapSource(_capturedScreen.FullBitmapSource, wpfCrop);
            string savedPath = ScreenshotHistoryManager.AutoSaveScreenshot(croppedWpf);
            double screenX = Left + _selectedRect.X;
            double screenY = Top + _selectedRect.Y;
            var pinWin = new PinWindow(croppedWpf, screenX, screenY, savedPath);
            pinWin.Show();
            Close();
        }

        /// <summary>确认当前选区操作（OCR 或复制）</summary>
        private void Confirm()
        {
            if (_selectedRect.Width < 10 || _selectedRect.Height < 10) { Close(); return; }
            if (_action == HotkeyAction.SnipAndCopy) CopyToClipboardAndClose();
            else StartOcrAndClose();
        }

        /// <summary>根据 DPI 缩放比例计算底图物理裁剪像素矩形</summary>
        private Int32Rect CalculatePhysicalCropRect()
        {
            int cropX = Math.Max(0, (int)Math.Round(_selectedRect.X * _scaleX));
            int cropY = Math.Max(0, (int)Math.Round(_selectedRect.Y * _scaleY));
            int cropW = Math.Min(_capturedScreen.FullBitmapSource.PixelWidth - cropX, (int)Math.Round(_selectedRect.Width * _scaleX));
            int cropH = Math.Min(_capturedScreen.FullBitmapSource.PixelHeight - cropY, (int)Math.Round(_selectedRect.Height * _scaleY));
            return new Int32Rect(cropX, cropY, Math.Max(1, cropW), Math.Max(1, cropH));
        }

        /// <summary>复制选区图片至系统剪贴板并退出</summary>
        private void CopyToClipboardAndClose()
        {
            var wpfCrop = CalculatePhysicalCropRect();
            var cropped = ScreenCapturer.CropBitmapSource(_capturedScreen.FullBitmapSource, wpfCrop);
            ScreenshotHistoryManager.AutoSaveScreenshot(cropped);
            Clipboard.SetImage(cropped);
            Close();
        }

        /// <summary>裁剪选区图片，启动 OCR 识别展示窗口并退出遮罩</summary>
        private void StartOcrAndClose()
        {
            var wpfCrop = CalculatePhysicalCropRect();
            var croppedWpf = ScreenCapturer.CropBitmapSource(_capturedScreen.FullBitmapSource, wpfCrop);
            ScreenshotHistoryManager.AutoSaveScreenshot(croppedWpf);
            double screenX = Left + _selectedRect.X;
            double screenY = Top + _selectedRect.Y;
            var resultWindow = new OcrResultWindow(croppedWpf, screenX, screenY);
            resultWindow.Show();
            Close();
        }
    }
}
