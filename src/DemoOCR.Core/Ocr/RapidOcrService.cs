using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using DemoOCR.Core.Capture;
using RapidOcrNet;
using SkiaSharp;

namespace DemoOCR.Core.Ocr
{
    /// <summary>
    /// RapidAI/RapidOCR 引擎服务封装（负责嵌入式 PP-OCRv6 模型解压、线程池管理、预热与异步推理）
    /// </summary>
    public class RapidOcrService : IDisposable
    {
        private static readonly Lazy<RapidOcrService> _instance = new(() => new RapidOcrService());
        /// <summary>服务单例实例</summary>
        public static RapidOcrService Instance => _instance.Value;

        private RapidOcr? _ocr;
        private bool _isInitialized;
        private readonly object _lock = new();

        private string _detModelPath = string.Empty;
        private string _clsModelPath = string.Empty;
        private string _recModelPath = string.Empty;
        private string _dictPath = string.Empty;

        /// <summary>引擎是否已初始化完成</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 同步初始化 OCR 引擎：释放嵌入式模型至本地并预热推理会话
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            lock (_lock)
            {
                if (_isInitialized) return;

                string baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DemoOCR", "models");
                Directory.CreateDirectory(baseDir);

                string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DemoOCR", "app.log");
                File.AppendAllText(logFile, $"[{DateTime.Now}] Starting model extraction...\n");

                // 提取内嵌的 PP-OCRv6 离线模型文件
                _detModelPath = ExtractEmbeddedModelIfNeeded("PP-OCRv6_det_tiny.onnx", baseDir);
                _clsModelPath = ExtractEmbeddedModelIfNeeded("ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx", baseDir);
                _recModelPath = ExtractEmbeddedModelIfNeeded("PP-OCRv6_rec_tiny.onnx", baseDir);
                _dictPath = ExtractEmbeddedModelIfNeeded("ppocrv6_tiny_dict.txt", baseDir);

                File.AppendAllText(logFile, $"[{DateTime.Now}] Models extracted. Initializing RapidOcr engine...\n");

                _ocr = new RapidOcr();
                int threads = Math.Clamp(Environment.ProcessorCount, 2, 8);
                _ocr.InitModels(_detModelPath, _clsModelPath, _recModelPath, _dictPath, threads);

                // 预热 ONNX Runtime 推理会话，提前触发 JIT 编译和内存分配，消除首次截图卡顿
                try
                {
                    using var warmUpBmp = new SKBitmap(48, 48, SKColorType.Bgra8888, SKAlphaType.Premul);
                    _ocr.Detect(warmUpBmp, RapidOcrOptions.PPOCRv6);
                    File.AppendAllText(logFile, $"[{DateTime.Now}] RapidOcr engine initialized & warmed up successfully!\n");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logFile, $"[{DateTime.Now}] Warm-up note: {ex.Message}\n");
                }

                _isInitialized = true;
            }
        }

        /// <summary>
        /// 异步执行 OCR 识别，完全在后台线程运算，绝不阻塞 UI 渲染
        /// </summary>
        /// <param name="croppedSource">裁剪后的屏幕位图源</param>
        public async Task<OcrAnalysisResult> DetectAsync(BitmapSource croppedSource)
        {
            if (!_isInitialized) await Task.Run(Initialize);

            var sw = Stopwatch.StartNew();

            OcrResult ocrResult = await Task.Run(() =>
            {
                // 将 WPF 位图直接转为 SKBitmap，执行 RapidOCR 推理
                using var skBitmap = ScreenCapturer.ConvertBitmapSourceToSkBitmap(croppedSource);
                lock (_lock)
                {
                    return _ocr!.Detect(skBitmap, RapidOcrOptions.PPOCRv6);
                }
            });

            sw.Stop();

            var textBlocks = new List<OcrTextBlockInfo>();
            if (ocrResult.TextBlocks != null)
            {
                foreach (var block in ocrResult.TextBlocks)
                {
                    if (string.IsNullOrWhiteSpace(block.Text)) continue;

                    var pts = new List<Point>();
                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;

                    if (block.BoxPoints != null && block.BoxPoints.Length > 0)
                    {
                        foreach (var p in block.BoxPoints)
                        {
                            pts.Add(new Point(p.X, p.Y));
                            if (p.X < minX) minX = p.X;
                            if (p.X > maxX) maxX = p.X;
                            if (p.Y < minY) minY = p.Y;
                            if (p.Y > maxY) maxY = p.Y;
                        }
                    }
                    else
                    {
                        minX = 0; minY = 0; maxX = 10; maxY = 10;
                    }

                    textBlocks.Add(new OcrTextBlockInfo
                    {
                        Text = block.Text,
                        Score = block.BoxScore,
                        BoundingBox = new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY)),
                        PolygonPoints = pts.ToArray()
                    });
                }
            }

            return new OcrAnalysisResult
            {
                TextBlocks = textBlocks,
                FullText = ocrResult.StrRes ?? string.Join(Environment.NewLine, textBlocks.Select(b => b.Text)),
                CroppedImage = croppedSource,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds
            };
        }

        /// <summary>
        /// 检查本地文件是否存在且完整，若不存在则从程序集嵌入资源中释放到本地
        /// </summary>
        private static string ExtractEmbeddedModelIfNeeded(string filename, string targetDirectory)
        {
            string targetPath = Path.Combine(targetDirectory, filename);
            var asm = Assembly.GetExecutingAssembly();

            string? resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(filename, StringComparison.OrdinalIgnoreCase));

            if (resName == null)
            {
                // 开发环境直接回退检查本地项目目录
                string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", filename);
                if (File.Exists(devPath)) return devPath;
                throw new FileNotFoundException($"未找到嵌入式模型资源: '{filename}'");
            }

            using var stream = asm.GetManifestResourceStream(resName);
            if (stream == null) throw new InvalidOperationException($"无法打开嵌入式资源流: '{resName}'");

            if (File.Exists(targetPath))
            {
                var fi = new FileInfo(targetPath);
                if (fi.Length == stream.Length) return targetPath;
            }

            using (var fileStream = File.Create(targetPath))
            {
                stream.CopyTo(fileStream);
            }
            return targetPath;
        }

        /// <summary>释放 OCR 引擎句柄与底层模型资源</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _ocr?.Dispose();
                _ocr = null;
                _isInitialized = false;
            }
            GC.SuppressFinalize(this);
        }
    }
}
