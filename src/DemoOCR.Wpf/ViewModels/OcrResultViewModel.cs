using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemoOCR.Core.Ocr;

namespace DemoOCR.ViewModels
{
    /// <summary>
    /// OCR 识别结果窗口 ViewModel（负责图像与文本展示、划词双向高亮、剪贴板交互等）
    /// </summary>
    public partial class OcrResultViewModel : ObservableObject
    {
        /// <summary>截图原图</summary>
        [ObservableProperty]
        private BitmapSource _croppedImage;

        /// <summary>拼接的全文内容（支持双向编辑）</summary>
        [ObservableProperty]
        private string _fullText = string.Empty;

        /// <summary>顶部耗时/状态角标文本</summary>
        [ObservableProperty]
        private string _statusBadgeText = "(正在识别...)";

        /// <summary>顶部状态角标颜色</summary>
        [ObservableProperty]
        private Brush _statusBadgeColor = new SolidColorBrush(Color.FromRgb(0, 120, 215));

        /// <summary>底部状态栏描述信息</summary>
        [ObservableProperty]
        private string _statusInfoText = "正在进行离线 OCR 模型分析...";

        /// <summary>右侧编辑区标题文本</summary>
        [ObservableProperty]
        private string _headerInfoText = "文本预览与编辑";

        /// <summary>轻量提示气泡文案</summary>
        [ObservableProperty]
        private string _toastMessage = string.Empty;

        /// <summary>轻量提示气泡是否可见</summary>
        [ObservableProperty]
        private bool _isToastVisible;

        /// <summary>窗口是否始终置顶</summary>
        [ObservableProperty]
        private bool _isTopmost = true;

        /// <summary>当前视图缩放比例</summary>
        [ObservableProperty]
        private double _scale = 1.0;

        /// <summary>当前选中的文本块</summary>
        [ObservableProperty]
        private TextBlockItemViewModel? _selectedBlock;

        /// <summary>OCR 识别出的所有文本块集合</summary>
        public ObservableCollection<TextBlockItemViewModel> TextBlocks { get; } = new();

        /// <summary>请求关闭窗口事件</summary>
        public event Action? RequestClose;
        /// <summary>请求滚动并在视图中高亮选中文本事件</summary>
        public event Action<TextBlockItemViewModel>? RequestScrollToAndSelectInView;
        /// <summary>请求保存图片事件</summary>
        public event Action? RequestSaveImage;

        private DispatcherTimer? _toastTimer;

        public OcrResultViewModel(BitmapSource croppedImage)
        {
            _croppedImage = croppedImage;
        }

        /// <summary>
        /// 异步执行 OCR 识别流水线
        /// </summary>
        public async Task StartOcrAsync()
        {
            StatusBadgeText = "(正在识别...)";
            StatusBadgeColor = new SolidColorBrush(Color.FromRgb(0, 120, 215));

            try
            {
                var result = await RapidOcrService.Instance.DetectAsync(CroppedImage);

                StatusBadgeText = $"(耗时 {result.ElapsedMilliseconds:F0} ms)";
                StatusBadgeColor = new SolidColorBrush(Color.FromRgb(40, 167, 69));

                TextBlocks.Clear();
                var sb = new StringBuilder();

                for (int i = 0; i < result.TextBlocks.Count; i++)
                {
                    var block = result.TextBlocks[i];
                    int start = sb.Length;
                    sb.AppendLine(block.Text);

                    TextBlocks.Add(new TextBlockItemViewModel
                    {
                        Index = i + 1,
                        Text = block.Text,
                        Score = block.Score,
                        BoundingBox = block.BoundingBox,
                        StartIndex = start,
                        Length = block.Text.Length,
                        LineIndex = i
                    });
                }

                FullText = sb.ToString();
                StatusInfoText = $"耗时: {result.ElapsedMilliseconds:F0} ms | 识别文本块: {TextBlocks.Count} 个 | 支持滚轮缩放与划词高亮";
            }
            catch (Exception ex)
            {
                StatusBadgeText = "(识别失败)";
                StatusBadgeColor = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                FullText = $"识别异常: {ex.Message}";
                StatusInfoText = "离线模型推理异常，请重试";
            }
        }

        /// <summary>
        /// 选中指定文本块（左图划词点击触发）
        /// </summary>
        [RelayCommand]
        public void SelectBlock(TextBlockItemViewModel? block)
        {
            if (block == null) return;
            SelectedBlock = block;

            foreach (var item in TextBlocks)
            {
                item.IsSelected = (item == block);
            }

            HeaderInfoText = $"文本预览与编辑 (已选中第 {block.Index} 行)";
            StatusInfoText = $"选中第 {block.Index}/{TextBlocks.Count} 行文字 (右侧已高亮同步)";

            // 自动复制该行文本到剪贴板并提示
            try
            {
                Clipboard.SetText(block.Text);
                ShowToast($"已复制第 {block.Index} 行: {block.Text}");
            }
            catch
            {
            }

            // 通知视图层在右侧 TextBox 划词选中该行
            RequestScrollToAndSelectInView?.Invoke(block);
        }

        /// <summary>
        /// 根据光标字符位置反向选中左侧对应的框（右侧编辑区光标变动触发）
        /// </summary>
        public void SelectBlockByCaret(int caretIndex)
        {
            foreach (var item in TextBlocks)
            {
                if (caretIndex >= item.StartIndex && caretIndex <= item.StartIndex + item.Length)
                {
                    if (SelectedBlock != item)
                    {
                        SelectedBlock = item;
                        foreach (var b in TextBlocks)
                        {
                            b.IsSelected = (b == item);
                        }
                        HeaderInfoText = $"文本预览与编辑 (已选中第 {item.Index} 行)";
                        StatusInfoText = $"选中第 {item.Index}/{TextBlocks.Count} 行: {item.Text}";
                    }
                    break;
                }
            }
        }

        /// <summary>切换窗口置顶状态</summary>
        [RelayCommand]
        public void TogglePin()
        {
            IsTopmost = !IsTopmost;
            ShowToast(IsTopmost ? "窗口已置顶" : "已取消置顶");
        }

        /// <summary>复制识别出的完整文本</summary>
        [RelayCommand]
        public void CopyFullText()
        {
            if (!string.IsNullOrEmpty(FullText))
            {
                Clipboard.SetText(FullText);
                ShowToast("全文已复制到剪贴板");
            }
        }

        /// <summary>复制截取的原图</summary>
        [RelayCommand]
        public void CopyImage()
        {
            if (CroppedImage != null)
            {
                Clipboard.SetImage(CroppedImage);
                ShowToast("截图图片已复制到剪贴板");
            }
        }

        /// <summary>保存图片</summary>
        [RelayCommand]
        public void SaveImage() => RequestSaveImage?.Invoke();

        /// <summary>关闭窗口</summary>
        [RelayCommand]
        public void Close() => RequestClose?.Invoke();

        /// <summary>
        /// 显示轻量 Toast 提示框
        /// </summary>
        public void ShowToast(string message)
        {
            ToastMessage = message;
            IsToastVisible = true;
            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _toastTimer.Tick += (s, e) =>
            {
                IsToastVisible = false;
                ToastMessage = string.Empty;
                _toastTimer?.Stop();
            };
            _toastTimer.Start();
        }
    }
}
