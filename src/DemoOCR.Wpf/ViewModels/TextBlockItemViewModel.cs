using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DemoOCR.ViewModels
{
    /// <summary>
    /// OCR 识别文本块数据项模型（对应图像上的单个检测框及文本）
    /// </summary>
    public partial class TextBlockItemViewModel : ObservableObject
    {
        /// <summary>行号序号（从1开始）</summary>
        public int Index { get; set; }
        /// <summary>识别出的文本内容</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>识别置信度 (0~1)</summary>
        public float Score { get; set; }
        /// <summary>图像物理坐标下的检测框范围</summary>
        public Rect BoundingBox { get; set; }

        /// <summary>在拼接全文本中的起始字符索引（用于双向高亮联动）</summary>
        public int StartIndex { get; set; }
        /// <summary>在拼接全文本中的字符长度</summary>
        public int Length { get; set; }
        /// <summary>行索引（0-based）</summary>
        public int LineIndex { get; set; }

        /// <summary>当前文本块是否处于选中状态</summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>悬浮提示文案</summary>
        public string ToolTipContent => $"第 {Index} 行 | 置信度: {Score:P0}\n{Text}\n(点击选中并联动高亮右侧对应文本)";

        /// <summary>WPF 画布绑定的 X 坐标</summary>
        public double Left => BoundingBox.X;
        /// <summary>WPF 画布绑定的 Y 坐标</summary>
        public double Top => BoundingBox.Y;
        /// <summary>WPF 画布绑定的宽度（最小保护8像素）</summary>
        public double Width => Math.Max(8, BoundingBox.Width);
        /// <summary>WPF 画布绑定的高度（最小保护8像素）</summary>
        public double Height => Math.Max(8, BoundingBox.Height);
    }
}
