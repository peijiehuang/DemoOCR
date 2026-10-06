using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemoOCR.Core.Persistence;

namespace DemoOCR.ViewModels
{
    /// <summary>
    /// 桌面贴图窗口 ViewModel（管理贴图缩放、置顶、透明度、保存、OCR与最近历史截图回溯）
    /// </summary>
    public partial class PinViewModel : ObservableObject
    {
        /// <summary>贴图图像源</summary>
        [ObservableProperty]
        private BitmapSource _image;

        /// <summary>原始图像像素宽度</summary>
        [ObservableProperty]
        private double _origWidth;

        /// <summary>原始图像像素高度</summary>
        [ObservableProperty]
        private double _origHeight;

        /// <summary>当前缩放比率 (默认 1.0)</summary>
        [ObservableProperty]
        private double _scale = 1.0;

        /// <summary>浮标文案 (如: "115%" 或 "历史截图 (2/10)")</summary>
        [ObservableProperty]
        private string _zoomTipText = "100%";

        /// <summary>浮标提示是否可见</summary>
        [ObservableProperty]
        private bool _isZoomTipVisible;

        /// <summary>窗口是否始终置顶</summary>
        [ObservableProperty]
        private bool _isTopmost = true;

        /// <summary>窗口不透明度 (0.1 ~ 1.0)</summary>
        [ObservableProperty]
        private double _opacity = 1.0;

        /// <summary>当前浏览的历史记录索引 (0为最新，往大为更早的历史)</summary>
        [ObservableProperty]
        private int _historyIndex;

        /// <summary>当前贴图对应文件路径（若有）</summary>
        public string? FilePath { get; private set; }

        /// <summary>当前贴图图像指纹（用于贴图唯一性判断，防重复弹出）</summary>
        [ObservableProperty]
        private string _fingerprint = string.Empty;

        /// <summary>通知视图关闭窗口事件</summary>
        public event Action? RequestClose;
        /// <summary>通知视图弹出保存文件对话框事件</summary>
        public event Action? RequestSaveImage;
        /// <summary>通知视图打开 OCR 识别窗口事件</summary>
        public event Action? RequestOcr;
        /// <summary>切换历史图片导致尺寸变动时通知视图调整窗口尺寸</summary>
        public event Action? ImageChanged;

        private DispatcherTimer? _tipTimer;

        public PinViewModel(BitmapSource image, string? filePath = null)
        {
            _image = image;
            _image.Freeze();
            _origWidth = Math.Max(10, image.PixelWidth);
            _origHeight = Math.Max(10, image.PixelHeight);
            FilePath = filePath;
            Fingerprint = ComputeFingerprint(image, filePath);
        }

        /// <summary>
        /// 快速计算图像的唯一特征指纹（用于贴图去重）
        /// </summary>
        public static string ComputeFingerprint(BitmapSource bmp, string? filePath = null)
        {
            if (!string.IsNullOrEmpty(filePath)) return Path.GetFullPath(filePath);
            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            byte[] sample = new byte[Math.Min(64, w * 4)];
            try
            {
                bmp.CopyPixels(new Int32Rect(0, 0, Math.Min(w, 16), 1), sample, sample.Length, sample.Length);
            }
            catch
            {
            }
            return $"{w}x{h}_{Convert.ToHexString(sample)}";
        }

        /// <summary>滚轮平滑缩放贴图</summary>
        [RelayCommand]
        public void Zoom(bool zoomIn)
        {
            double factor = zoomIn ? 1.15 : 0.85;
            double newScale = Math.Clamp(Scale * factor, 0.15, 6.0);
            if (Math.Abs(newScale - Scale) < 0.001) return;

            Scale = newScale;
            ShowTip($"{(int)Math.Round(Scale * 100)}%");
        }

        /// <summary>回溯切换至更早一张历史截图 (按键 ← 或 A)</summary>
        [RelayCommand]
        public void PreviousHistory()
        {
            var history = ScreenshotHistoryManager.GetRecentFilePaths();
            if (history.Count == 0)
            {
                ShowTip("暂无历史截图");
                return;
            }

            int nextIndex = HistoryIndex + 1;
            if (nextIndex >= history.Count)
            {
                ShowTip($"已是最后一张 ({history.Count}/{history.Count})");
                return;
            }

            var bmp = ScreenshotHistoryManager.LoadImageByIndex(nextIndex);
            if (bmp != null)
            {
                HistoryIndex = nextIndex;
                SwitchImage(bmp, history[nextIndex]);
                ShowTip($"历史截图 ({HistoryIndex + 1}/{history.Count})");
            }
        }

        /// <summary>前进切换至更新一张历史截图 (按键 → 或 D)</summary>
        [RelayCommand]
        public void NextHistory()
        {
            var history = ScreenshotHistoryManager.GetRecentFilePaths();
            if (history.Count == 0 || HistoryIndex <= 0)
            {
                ShowTip("已是最新截图 (1/" + Math.Max(1, history.Count) + ")");
                return;
            }

            int prevIndex = HistoryIndex - 1;
            var bmp = ScreenshotHistoryManager.LoadImageByIndex(prevIndex);
            if (bmp != null)
            {
                HistoryIndex = prevIndex;
                SwitchImage(bmp, history[prevIndex]);
                ShowTip($"历史截图 ({HistoryIndex + 1}/{history.Count})");
            }
        }

        /// <summary>切换贴图并重置原始尺寸与图像指纹</summary>
        private void SwitchImage(BitmapSource newBmp, string? newPath)
        {
            Image = newBmp;
            FilePath = newPath;
            OrigWidth = Math.Max(10, newBmp.PixelWidth);
            OrigHeight = Math.Max(10, newBmp.PixelHeight);
            Fingerprint = ComputeFingerprint(newBmp, newPath);
            ImageChanged?.Invoke();
        }

        /// <summary>弹出居中悬浮气泡提示</summary>
        public void ShowTip(string message)
        {
            ZoomTipText = message;
            IsZoomTipVisible = true;
            _tipTimer?.Stop();
            _tipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
            _tipTimer.Tick += (s, e) =>
            {
                IsZoomTipVisible = false;
                _tipTimer?.Stop();
            };
            _tipTimer.Start();
        }

        /// <summary>打开本地截图存储文件夹</summary>
        [RelayCommand]
        public void OpenFolder() => ScreenshotHistoryManager.OpenFolder();

        /// <summary>复制贴图到剪贴板</summary>
        [RelayCommand]
        public void Copy()
        {
            if (Image != null) Clipboard.SetImage(Image);
        }

        /// <summary>保存贴图</summary>
        [RelayCommand]
        public void Save() => RequestSaveImage?.Invoke();

        /// <summary>对当前贴图执行 OCR 识别</summary>
        [RelayCommand]
        public void Ocr() => RequestOcr?.Invoke();

        /// <summary>关闭贴图窗口</summary>
        [RelayCommand]
        public void Close() => RequestClose?.Invoke();

        /// <summary>设置贴图透明度</summary>
        [RelayCommand]
        public void SetOpacity(string? opStr)
        {
            if (double.TryParse(opStr, out double op)) Opacity = op;
        }

        /// <summary>切换窗口置顶状态</summary>
        [RelayCommand]
        public void ToggleTopmost() => IsTopmost = !IsTopmost;
    }
}
