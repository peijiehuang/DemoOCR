using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DemoOCR.ViewModels;
using Microsoft.Win32;

namespace DemoOCR.Views
{
    /// <summary>
    /// OCR 识别结果展示与划词编辑窗口交互逻辑
    /// </summary>
    public partial class OcrResultWindow : Window
    {
        /// <summary>强类型 ViewModel 引用</summary>
        public OcrResultViewModel ViewModel { get; }

        public OcrResultWindow(BitmapSource croppedImage, double screenX, double screenY)
        {
            InitializeComponent();

            // 绑定 MVVM 数据上下文
            ViewModel = new OcrResultViewModel(croppedImage);
            DataContext = ViewModel;

            // 依据截图大小和当前屏幕自适应窗口尺寸与弹出位置（设置保底最小宽高 620x400，防止小截图导致标题栏按钮被遮挡）
            double minW = 620;
            double minH = 400;
            double calcWidth = Math.Clamp(Math.Max(croppedImage.PixelWidth + 280, minW), minW, SystemParameters.WorkArea.Width - 60);
            double calcHeight = Math.Clamp(Math.Max(croppedImage.PixelHeight + 130, minH), minH, SystemParameters.WorkArea.Height - 60);
            Width = calcWidth;
            Height = calcHeight;
            Left = Math.Clamp(screenX, 10, SystemParameters.WorkArea.Width - calcWidth - 10);
            Top = Math.Clamp(screenY, 10, SystemParameters.WorkArea.Height - calcHeight - 10);

            // 订阅 ViewModel 触发的视图层事件（关闭、保存文件、滚动划词）
            ViewModel.RequestClose += Close;
            ViewModel.RequestSaveImage += SaveImageDialog;
            ViewModel.RequestScrollToAndSelectInView += OnBlockSelected;

            // 窗口加载完成后异步启动 OCR 模型推理
            Loaded += async (s, e) => await ViewModel.StartOcrAsync();
        }

        /// <summary>左侧图像点击文本框时，联动聚焦并高亮右侧对应文本行</summary>
        private void OnBlockSelected(TextBlockItemViewModel block)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    TxtFullContent.Focus();
                    TxtFullContent.Select(block.StartIndex, block.Length);
                    int line = TxtFullContent.GetLineIndexFromCharacterIndex(block.StartIndex);
                    if (line >= 0) TxtFullContent.ScrollToLine(line);
                }
                catch
                {
                }
            }), DispatcherPriority.Input);
        }

        /// <summary>右侧文本框光标位置变动时，反向高亮左侧图像对应边框</summary>
        private void TxtFullContent_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (TxtFullContent.IsFocused)
            {
                ViewModel.SelectBlockByCaret(TxtFullContent.CaretIndex);
            }
        }

        /// <summary>标题栏鼠标拖动窗口</summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        /// <summary>图像区域滚轮缩放支持 (0.25x ~ 4.0x)</summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control || !TxtFullContent.IsFocused)
            {
                double zoomDelta = e.Delta > 0 ? 1.15 : 0.85;
                double newScale = Math.Clamp(ViewportScale.ScaleX * zoomDelta, 0.25, 4.0);
                ViewportScale.ScaleX = newScale;
                ViewportScale.ScaleY = newScale;
                ViewModel.Scale = newScale;
                e.Handled = true;
            }
        }

        /// <summary>弹出文件保存对话框保存截图</summary>
        private void SaveImageDialog()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg)|*.jpg|所有文件 (*.*)|*.*",
                FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true && ViewModel.CroppedImage != null)
            {
                try
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(ViewModel.CroppedImage));
                    using var stream = File.Create(dialog.FileName);
                    encoder.Save(stream);
                    ViewModel.ShowToast("图片保存成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>Esc 键快速关闭窗口</summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
