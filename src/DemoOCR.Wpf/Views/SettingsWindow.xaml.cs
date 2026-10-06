using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using DemoOCR.Core.Configuration;
using DemoOCR.ViewModels;

namespace DemoOCR.Views
{
    /// <summary>
    /// 系统设置窗口交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        /// <summary>强类型 ViewModel 引用</summary>
        public SettingsViewModel ViewModel { get; }

        public SettingsWindow()
        {
            InitializeComponent();

            ViewModel = new SettingsViewModel();
            DataContext = ViewModel;

            // 订阅 ViewModel 的关闭通知
            ViewModel.RequestClose += Close;
        }

        private void GitHubProjectLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            AppInfo.OpenRepository();
            e.Handled = true;
        }

        /// <summary>标题栏鼠标拖动窗口</summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        /// <summary>录制 OCR 快捷键输入</summary>
        private void TxtOcrHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var setting = ExtractHotkey(e);
            if (setting != null) ViewModel.SetOcrHotkey(setting);
        }

        /// <summary>录制仅截图复制快捷键输入</summary>
        private void TxtCopyHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var setting = ExtractHotkey(e);
            if (setting != null) ViewModel.SetCopyHotkey(setting);
        }

        /// <summary>录制贴图快捷键输入</summary>
        private void TxtPinHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var setting = ExtractHotkey(e);
            if (setting != null) ViewModel.SetPinHotkey(setting);
        }

        /// <summary>
        /// 从按键事件中提取修饰键及主按键
        /// </summary>
        private static HotkeySetting? ExtractHotkey(KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // 过滤单独按下的 Ctrl / Alt / Shift / Win 键
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return null;
            }

            var modifiers = Keyboard.Modifiers;
            return new HotkeySetting
            {
                Ctrl = (modifiers & ModifierKeys.Control) != 0,
                Alt = (modifiers & ModifierKeys.Alt) != 0,
                Shift = (modifiers & ModifierKeys.Shift) != 0,
                Win = (modifiers & ModifierKeys.Windows) != 0,
                Key = key
            };
        }
    }
}
