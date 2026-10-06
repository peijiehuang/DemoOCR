using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DemoOCR.Core.Ocr
{
    /// <summary>
    /// 单个 OCR 识别文本块元数据模型
    /// </summary>
    public class OcrTextBlockInfo
    {
        /// <summary>识别文本内容</summary>
        public required string Text { get; set; }
        /// <summary>模型识别置信度 (0.0 ~ 1.0)</summary>
        public float Score { get; set; }
        /// <summary>轴对齐外接矩形范围</summary>
        public Rect BoundingBox { get; set; }
        /// <summary>四边形检测顶点坐标序列</summary>
        public Point[] PolygonPoints { get; set; } = [];
    }

    /// <summary>
    /// 单次 OCR 完整推理结果模型
    /// </summary>
    public class OcrAnalysisResult
    {
        /// <summary>识别出的所有文本块集合</summary>
        public List<OcrTextBlockInfo> TextBlocks { get; set; } = [];
        /// <summary>自然段拼接的全文内容</summary>
        public string FullText { get; set; } = string.Empty;
        /// <summary>裁剪后的原始输入图像源</summary>
        public BitmapSource CroppedImage { get; set; } = null!;
        /// <summary>OCR 模型推理总耗时 (毫秒)</summary>
        public double ElapsedMilliseconds { get; set; }
    }
}
