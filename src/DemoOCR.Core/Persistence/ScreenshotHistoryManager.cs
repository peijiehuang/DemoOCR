using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using DemoOCR.Core.Configuration;

namespace DemoOCR.Core.Persistence
{
    /// <summary>
    /// 截图历史与自动持久化管理器（自动存盘至图片目录并维护可配置数量的最近截图）
    /// </summary>
    public static class ScreenshotHistoryManager
    {
        private static readonly object _lock = new();
        private static readonly List<string> _recentFilePaths = new();

        /// <summary>获取当前生效的保留历史截图最大数量</summary>
        public static int MaxCount => Math.Clamp(AppSettings.Current.MaxHistoryCount, 1, 100);

        /// <summary>截图默认自动保存目录 (%USERPROFILE%\Pictures\DemoOCR)</summary>
        public static string SaveDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "DemoOCR");

        static ScreenshotHistoryManager()
        {
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                var dir = new DirectoryInfo(SaveDirectory);
                var existingFiles = dir.GetFiles("*.png")
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(MaxCount)
                    .Select(f => f.FullName);

                _recentFilePaths.AddRange(existingFiles);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 自动将截图保存为 PNG 文件并压入最近历史队列顶端
        /// </summary>
        public static string AutoSaveScreenshot(BitmapSource image)
        {
            string fileName = $"DemoOCR_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string filePath = Path.Combine(SaveDirectory, fileName);

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(filePath))
                {
                    encoder.Save(stream);
                }

                lock (_lock)
                {
                    _recentFilePaths.Insert(0, filePath);
                    while (_recentFilePaths.Count > MaxCount)
                    {
                        _recentFilePaths.RemoveAt(_recentFilePaths.Count - 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"自动保存截图失败: {ex.Message}");
            }

            return filePath;
        }

        /// <summary>获取当前所有有效的最近历史截图路径列表（按最新至最旧倒序）</summary>
        public static List<string> GetRecentFilePaths()
        {
            lock (_lock)
            {
                _recentFilePaths.RemoveAll(p => !File.Exists(p));
                return new List<string>(_recentFilePaths);
            }
        }

        /// <summary>按索引加载历史图片（0 为最新一张，1 为次新，超出返回 null）</summary>
        public static BitmapSource? LoadImageByIndex(int index)
        {
            var paths = GetRecentFilePaths();
            if (index < 0 || index >= paths.Count) return null;
            return LoadImageFromFile(paths[index]);
        }

        /// <summary>从本地文件安全读取 BitmapSource（内存缓存加载，避免锁定文件句柄）</summary>
        public static BitmapSource? LoadImageFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>在资源管理器中打开截图自动保存文件夹</summary>
        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                Process.Start(new ProcessStartInfo { FileName = SaveDirectory, UseShellExecute = true });
            }
            catch
            {
            }
        }
    }
}
