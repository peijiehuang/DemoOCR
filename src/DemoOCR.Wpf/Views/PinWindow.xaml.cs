using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DemoOCR.Core.Capture;
using DemoOCR.ViewModels;
using Microsoft.Win32;

namespace DemoOCR.Views
{
    /// <summary>
    /// 桌面贴图窗口交互逻辑（无边框阴影置顶窗口，支持双击关闭、拖拽移动、滚轮缩放与历史截图回溯）
    /// </summary>
    public partial class PinWindow : Window
    {
        /// <summary>强类型 ViewModel 引用</summary>
        public PinViewModel ViewModel { get; }

        public PinWindow(BitmapSource image, double? initialLeft = null, double? initialTop = null, string? filePath = null)
        {
            InitializeComponent();

            ViewModel = new PinViewModel(image, filePath);
            DataContext = ViewModel;

            // 订阅 ViewModel 交互事件
            ViewModel.RequestClose += Close;
            ViewModel.RequestOcr += OnRequestOcr;
            ViewModel.RequestSaveImage += OnRequestSaveImage;
            ViewModel.ImageChanged += OnImageChanged;

            // 初始尺寸适配（包含外圈 Margin 8 * 2 = 16）
            Width = ViewModel.OrigWidth + 16;
            Height = ViewModel.OrigHeight + 16;

            if (initialLeft.HasValue && initialTop.HasValue)
            {
                Left = initialLeft.Value - 8;
                Top = initialTop.Value - 8;
            }
            else
            {
                Point mouse = ScreenCapturer.TryGetCursorPosition(out var position)
                    ? position
                    : new Point(SystemParameters.PrimaryScreenWidth / 2, SystemParameters.PrimaryScreenHeight / 2);

                double targetLeft = mouse.X - Width / 2;
                double targetTop = mouse.Y - Height / 2;
                Left = Math.Clamp(targetLeft, 10, Math.Max(10, SystemParameters.VirtualScreenWidth - Width - 10));
                Top = Math.Clamp(targetTop, 10, Math.Max(10, SystemParameters.VirtualScreenHeight - Height - 10));
            }
        }

        /// <summary>切换历史截图时自适应调整窗口大小并保持中心对齐</summary>
        private void OnImageChanged()
        {
            double oldW = Width;
            double oldH = Height;
            double newW = ViewModel.OrigWidth * ViewModel.Scale + 16;
            double newH = ViewModel.OrigHeight * ViewModel.Scale + 16;
            Width = newW;
            Height = newH;
            Left -= (newW - oldW) / 2;
            Top -= (newH - oldH) / 2;
        }

        /// <summary>打开 OCR 识别窗口分析当前贴图</summary>
        private void OnRequestOcr()
        {
            var ocrWindow = new OcrResultWindow(ViewModel.Image, Left + 8, Top + 8);
            ocrWindow.Show();
        }

        /// <summary>弹出文件保存对话框保存贴图图片</summary>
        private void OnRequestSaveImage()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg)|*.jpg|所有文件 (*.*)|*.*",
                FileName = $"Pin_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(ViewModel.Image));
                    using var stream = File.Create(dialog.FileName);
                    encoder.Save(stream);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存贴图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>鼠标左键按下：双击快速关闭，单击按住可拖动</summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ViewModel.CloseCommand.Execute(null);
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        /// <summary>滚轮平滑缩放贴图（以中心点对齐缩放）</summary>
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double oldW = Width;
            double oldH = Height;

            ViewModel.ZoomCommand.Execute(e.Delta > 0);

            double newW = ViewModel.OrigWidth * ViewModel.Scale + 16;
            double newH = ViewModel.OrigHeight * ViewModel.Scale + 16;
            Width = newW;
            Height = newH;

            Left -= (newW - oldW) / 2;
            Top -= (newH - oldH) / 2;
        }

        /// <summary>键盘快捷键支持：Esc关闭、Ctrl+C复制、Ctrl+S保存、←/A或→/D翻阅历史截图</summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ViewModel.CloseCommand.Execute(null);
            }
            else if (e.Key == Key.Left || e.Key == Key.A)
            {
                ViewModel.PreviousHistoryCommand.Execute(null);
            }
            else if (e.Key == Key.Right || e.Key == Key.D)
            {
                ViewModel.NextHistoryCommand.Execute(null);
            }
            else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ViewModel.CopyCommand.Execute(null);
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ViewModel.SaveCommand.Execute(null);
            }
        }
    }
}
