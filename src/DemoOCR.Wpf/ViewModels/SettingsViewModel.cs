using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemoOCR.Core.Configuration;
using DemoOCR.Core.Persistence;
using DemoOCR.Core.Platform;

namespace DemoOCR.ViewModels
{
    /// <summary>
    /// 设置窗口 ViewModel（管理快捷键配置、自动识别、开机自启动与贴图历史数量）
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private HotkeySetting _ocrHotkey;
        private HotkeySetting _copyHotkey;
        private HotkeySetting _pinHotkey;

        /// <summary>截图OCR快捷键显示文本</summary>
        [ObservableProperty]
        private string _ocrHotkeyText = string.Empty;

        /// <summary>截图复制快捷键显示文本</summary>
        [ObservableProperty]
        private string _copyHotkeyText = string.Empty;

        /// <summary>贴图快捷键显示文本</summary>
        [ObservableProperty]
        private string _pinHotkeyText = string.Empty;

        /// <summary>框选后是否自动识别（无需回车/点击按钮）</summary>
        [ObservableProperty]
        private bool _autoOcrOnSelect;

        /// <summary>开机自启动（默认不启用）</summary>
        [ObservableProperty]
        private bool _autoStartOnBoot;

        /// <summary>贴图历史记录保留最大张数 (默认 10 张)</summary>
        [ObservableProperty]
        private int _maxHistoryCount;

        /// <summary>通知视图关闭窗口事件</summary>
        public event Action? RequestClose;

        public SettingsViewModel()
        {
            // 从当前系统配置加载副本供用户修改
            _ocrHotkey = new HotkeySetting
            {
                Ctrl = AppSettings.Current.OcrHotkey.Ctrl,
                Alt = AppSettings.Current.OcrHotkey.Alt,
                Shift = AppSettings.Current.OcrHotkey.Shift,
                Win = AppSettings.Current.OcrHotkey.Win,
                Key = AppSettings.Current.OcrHotkey.Key
            };
            _copyHotkey = new HotkeySetting
            {
                Ctrl = AppSettings.Current.CopyHotkey.Ctrl,
                Alt = AppSettings.Current.CopyHotkey.Alt,
                Shift = AppSettings.Current.CopyHotkey.Shift,
                Win = AppSettings.Current.CopyHotkey.Win,
                Key = AppSettings.Current.CopyHotkey.Key
            };
            _pinHotkey = new HotkeySetting
            {
                Ctrl = AppSettings.Current.PinHotkey.Ctrl,
                Alt = AppSettings.Current.PinHotkey.Alt,
                Shift = AppSettings.Current.PinHotkey.Shift,
                Win = AppSettings.Current.PinHotkey.Win,
                Key = AppSettings.Current.PinHotkey.Key
            };

            OcrHotkeyText = _ocrHotkey.ToString();
            CopyHotkeyText = _copyHotkey.ToString();
            PinHotkeyText = _pinHotkey.ToString();
            AutoOcrOnSelect = AppSettings.Current.AutoOcrOnSelect;
            AutoStartOnBoot = AppSettings.Current.AutoStartOnBoot;
            MaxHistoryCount = Math.Clamp(AppSettings.Current.MaxHistoryCount, 1, 100);
        }

        /// <summary>更新 OCR 快捷键配置</summary>
        public void SetOcrHotkey(HotkeySetting setting)
        {
            _ocrHotkey = setting;
            OcrHotkeyText = _ocrHotkey.ToString();
        }

        /// <summary>更新仅截图复制快捷键配置</summary>
        public void SetCopyHotkey(HotkeySetting setting)
        {
            _copyHotkey = setting;
            CopyHotkeyText = _copyHotkey.ToString();
        }

        /// <summary>更新贴图快捷键配置</summary>
        public void SetPinHotkey(HotkeySetting setting)
        {
            _pinHotkey = setting;
            PinHotkeyText = _pinHotkey.ToString();
        }

        /// <summary>打开截图保存文件夹</summary>
        [RelayCommand]
        public void OpenSaveFolder() => ScreenshotHistoryManager.OpenFolder();

        /// <summary>保存配置并应用</summary>
        [RelayCommand]
        public void Save()
        {
            AppSettings.Current.OcrHotkey = _ocrHotkey;
            AppSettings.Current.CopyHotkey = _copyHotkey;
            AppSettings.Current.PinHotkey = _pinHotkey;
            AppSettings.Current.AutoOcrOnSelect = AutoOcrOnSelect;
            AppSettings.Current.AutoStartOnBoot = AutoStartOnBoot;
            AppSettings.Current.MaxHistoryCount = Math.Clamp(MaxHistoryCount, 1, 100);
            AppSettings.Current.Save();

            // 更新 Windows 注册表开机启动状态
            AutoStartupHelper.SetAutoStart(AutoStartOnBoot);

            // 热重载全局快捷键
            if (Application.Current is App app)
            {
                app.ApplyHotkeySettings();
            }

            RequestClose?.Invoke();
        }

        /// <summary>取消并关闭</summary>
        [RelayCommand]
        public void Cancel() => RequestClose?.Invoke();
    }
}
