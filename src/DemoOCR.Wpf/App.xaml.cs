using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using DemoOCR.Core.Capture;
using DemoOCR.Core.Configuration;
using DemoOCR.Core.Persistence;
using DemoOCR.Core.Platform;
using DemoOCR.Core.Ocr;
using DemoOCR.ViewModels;
using DemoOCR.Views;
using DemoOCR.Infrastructure;

namespace DemoOCR
{
    /// <summary>
    /// 应用程序入口与主生命周期管理（单实例防护、后台OCR预热、系统托盘、全局热键分发）
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "DemoOCR_SingleInstance_App_Mutex";
        private Mutex? _singleInstanceMutex;
        private HotkeyManager? _hotkeyManager;
        private TrayManager? _trayManager;
        private SettingsWindow? _settingsWindow;
        private bool _isSnipping;

        /// <summary>
        /// 应用并热重载全局热键配置，同步更新托盘提示与右键菜单
        /// </summary>
        public void ApplyHotkeySettings()
        {
            if (_hotkeyManager == null || _trayManager == null) return;

            var (ocrOk, copyOk, pinOk) = _hotkeyManager.ApplySettings(AppSettings.Current);
            _trayManager.UpdateTooltip($"DemoOCR ({_hotkeyManager.RegisteredHotkeysDescription})");
            _trayManager.UpdateMenuHeaders(
                _hotkeyManager.OcrHotkeyDescription,
                _hotkeyManager.CopyHotkeyDescription,
                _hotkeyManager.PinHotkeyDescription);

            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DemoOCR");
            File.AppendAllText(Path.Combine(logDir, "startup.log"), $"[{DateTime.Now}] Hotkeys updated: {_hotkeyManager.RegisteredHotkeysDescription} (OCR:{ocrOk}, Copy:{copyOk}, Pin:{pinOk})\n");
        }

        /// <summary>
        /// 唤起系统设置窗口（单窗口激活防护）
        /// </summary>
        private void ShowSettingsWindow()
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        /// <summary>
        /// 应用程序启动入口
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DemoOCR");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "startup.log"), $"[{DateTime.Now}] OnStartup invoked.\n");

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                File.AppendAllText(Path.Combine(logDir, "startup.log"), $"[{DateTime.Now}] AppDomain UnhandledException: {args.ExceptionObject}\n");
            };
            DispatcherUnhandledException += (s, args) =>
            {
                File.AppendAllText(Path.Combine(logDir, "startup.log"), $"[{DateTime.Now}] DispatcherUnhandledException: {args.Exception}\n");
            };

            // 1. 单实例互斥量保护，防止多进程冲突
            _singleInstanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                File.AppendAllText(Path.Combine(logDir, "startup.log"), $"[{DateTime.Now}] Another instance running. Exiting.\n");
                MessageBox.Show("DemoOCR 已经在后台运行中！\n可直接按截图快捷键，或通过右键托盘图标打开设置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 托盘常驻模式：关闭窗口不退出程序
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 2. 异步线程预加载 RapidOCR 离线模型并预热推理会话
            Task.Run(() =>
            {
                try
                {
                    RapidOcrService.Instance.Initialize();
                }
                catch (Exception ex)
                {
                    string errFile = Path.Combine(logDir, "error.log");
                    File.AppendAllText(errFile, $"[{DateTime.Now}] OCR Init Error: {ex}\n");
                }
            });

            // 3. 初始化任务栏托盘图标
            _trayManager = new TrayManager();
            _trayManager.RequestSnipAndOcr += () => StartSnip(HotkeyAction.SnipAndOcr);
            _trayManager.RequestSnipAndCopy += () => StartSnip(HotkeyAction.SnipAndCopy);
            _trayManager.RequestPinImage += PinFromClipboard;
            _trayManager.RequestOpenSettings += ShowSettingsWindow;
            _trayManager.RequestExit += () => Shutdown();
            _trayManager.Initialize();

            // 4. 注册全局快捷键并监听触发
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += action =>
            {
                if (action == HotkeyAction.PinImage) PinFromClipboard();
                else StartSnip(action);
            };
            ApplyHotkeySettings();

            // 5. 若配置了开机自启动，同步当前运行路径至注册表
            if (AppSettings.Current.AutoStartOnBoot)
            {
                AutoStartupHelper.SetAutoStart(true);
            }
        }

        /// <summary>
        /// 唤起贴图窗口：优先读取剪贴板图片，若剪贴板无图片则自动载入最近一张历史截图；若该图片已贴在桌面上，则直接激活该窗口避免重复弹出
        /// </summary>
        private void PinFromClipboard()
        {
            try
            {
                BitmapSource? targetImage = null;
                string? targetPath = null;

                if (Clipboard.ContainsImage())
                {
                    targetImage = Clipboard.GetImage();
                    var recentPaths = ScreenshotHistoryManager.GetRecentFilePaths();
                    if (recentPaths.Count > 0 && targetImage != null)
                    {
                        var latestBmp = ScreenshotHistoryManager.LoadImageByIndex(0);
                        if (latestBmp != null && latestBmp.PixelWidth == targetImage.PixelWidth && latestBmp.PixelHeight == targetImage.PixelHeight)
                        {
                            targetPath = recentPaths[0];
                        }
                    }
                }

                // 剪贴板无图片时，自动找回最近一张历史截图贴出
                if (targetImage == null)
                {
                    var recentPaths = ScreenshotHistoryManager.GetRecentFilePaths();
                    if (recentPaths.Count > 0)
                    {
                        targetPath = recentPaths[0];
                        targetImage = ScreenshotHistoryManager.LoadImageByIndex(0);
                    }
                }

                if (targetImage == null)
                {
                    _trayManager?.ShowNotification("DemoOCR 贴图提示", "暂无剪贴板图片或历史截图，请先截图后再按贴图键。");
                    return;
                }

                string targetFingerprint = PinViewModel.ComputeFingerprint(targetImage, targetPath);

                // 遍历当前已存在的贴图窗口：如果同一张图片已经在屏幕上显示，直接激活置顶，绝不重复弹出！
                foreach (Window win in Windows)
                {
                    if (win is PinWindow pinWin && pinWin.ViewModel.Fingerprint == targetFingerprint)
                    {
                        pinWin.Activate();
                        pinWin.ViewModel.ShowTip("该贴图已在屏幕上");
                        return;
                    }
                }

                // 屏幕上尚未显示该贴图，新建展示
                var newPin = new PinWindow(targetImage, filePath: targetPath);
                newPin.Show();
                newPin.Activate();
                if (targetPath != null)
                {
                    newPin.ViewModel.ShowTip("已贴出最新历史截图 (按←可看更早)");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"贴图失败: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 启动屏幕抓取并弹出全屏框选遮罩层
        /// </summary>
        private void StartSnip(HotkeyAction action)
        {
            if (_isSnipping) return;
            _isSnipping = true;

            try
            {
                var screen = ScreenCapturer.CaptureVirtualScreen();
                var overlay = new SnipOverlayWindow(screen, action);
                overlay.Closed += (s, e) => _isSnipping = false;
                overlay.Show();
                overlay.Activate();
            }
            catch (Exception ex)
            {
                _isSnipping = false;
                MessageBox.Show($"截图触发异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 应用程序退出时清理系统资源（热键、托盘、OCR引擎与互斥量）
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DemoOCR");
            File.AppendAllText(Path.Combine(logDir, "startup.log"), $"[{DateTime.Now}] OnExit called! ExitCode: {e.ApplicationExitCode}\nStackTrace:\n{Environment.StackTrace}\n");

            _hotkeyManager?.Dispose();
            _trayManager?.Dispose();
            RapidOcrService.Instance.Dispose();

            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}
