using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DemoOCR.Infrastructure
{
    /// <summary>
    /// Windows 系统任务栏托盘图标与右键菜单管理器（纯 Win32 Shell_NotifyIcon 实现）
    /// </summary>
    public class TrayManager : IDisposable
    {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1024;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONUP = 0x0202;

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;
        private const int NIIF_INFO = 0x00000001;
        private const int TrayIconSize = 32;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public uint uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private HwndSource? _hwndSource;
        private IntPtr _hWnd;
        private NOTIFYICONDATA _nid;
        private bool _isCreated;
        private readonly ContextMenu _contextMenu;
        private readonly Icon _icon;

        /// <summary>触发截图识别操作</summary>
        public event Action? RequestSnipAndOcr;
        /// <summary>触发截图复制操作</summary>
        public event Action? RequestSnipAndCopy;
        /// <summary>触发剪贴板贴图操作</summary>
        public event Action? RequestPinImage;
        /// <summary>打开设置面板</summary>
        public event Action? RequestOpenSettings;
        /// <summary>退出应用程序</summary>
        public event Action? RequestExit;

        private MenuItem? _itemOcr;
        private MenuItem? _itemCopy;
        private MenuItem? _itemPin;

        public TrayManager()
        {
            var iconUri = new Uri("/DemoOCR;component/Assets/App.ico", UriKind.Relative);
            using var stream = Application.GetResourceStream(iconUri)?.Stream
                ?? throw new InvalidOperationException("未找到应用图标资源。");
            _icon = new Icon(stream, TrayIconSize, TrayIconSize);
            _contextMenu = CreateContextMenu();
        }

        /// <summary>初始化系统托盘图标并注册消息钩子</summary>
        public void Initialize()
        {
            var parameters = new HwndSourceParameters("DemoOCR_Tray_Hwnd") { WindowStyle = 0x800000 };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndHook);
            _hWnd = _hwndSource.Handle;

            _nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _icon.Handle,
                szTip = "DemoOCR 截图文字识别"
            };

            Shell_NotifyIcon(NIM_ADD, ref _nid);
            _isCreated = true;
        }

        /// <summary>更新鼠标悬浮提示文本</summary>
        public void UpdateTooltip(string tip)
        {
            if (_isCreated)
            {
                _nid.szTip = tip.Length > 120 ? tip[..120] : tip;
                _nid.uFlags = NIF_TIP;
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
        }

        /// <summary>弹出系统托盘气泡通知</summary>
        public void ShowNotification(string title, string message)
        {
            if (_isCreated)
            {
                _nid.uFlags = NIF_INFO | NIF_TIP;
                _nid.szInfoTitle = title.Length > 63 ? title[..63] : title;
                _nid.szInfo = message.Length > 255 ? message[..255] : message;
                _nid.dwInfoFlags = NIIF_INFO;
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
        }

        /// <summary>动态更新托盘右键菜单中显示的快捷键文本</summary>
        public void UpdateMenuHeaders(string ocrText, string copyText, string pinText)
        {
            if (_itemOcr != null) _itemOcr.Header = $"🔍 截屏识别 ({ocrText})";
            if (_itemCopy != null) _itemCopy.Header = $"📋 截屏复制 ({copyText})";
            if (_itemPin != null) _itemPin.Header = $"📌 贴图置顶 ({pinText})";
        }

        /// <summary>构造托盘右键菜单</summary>
        private ContextMenu CreateContextMenu()
        {
            var menu = new ContextMenu();

            _itemOcr = new MenuItem { Header = "🔍 截屏识别 (F1)" };
            _itemOcr.Click += (s, e) => RequestSnipAndOcr?.Invoke();
            menu.Items.Add(_itemOcr);

            _itemCopy = new MenuItem { Header = "📋 截屏复制 (F2)" };
            _itemCopy.Click += (s, e) => RequestSnipAndCopy?.Invoke();
            menu.Items.Add(_itemCopy);

            _itemPin = new MenuItem { Header = "📌 贴图置顶 (F3)" };
            _itemPin.Click += (s, e) => RequestPinImage?.Invoke();
            menu.Items.Add(_itemPin);

            menu.Items.Add(new Separator());

            var itemSettings = new MenuItem { Header = "⚙️ 设置 (自定义快捷键)" };
            itemSettings.Click += (s, e) => RequestOpenSettings?.Invoke();
            menu.Items.Add(itemSettings);

            var itemProject = new MenuItem { Header = "🌐 GitHub 项目 · DemoOCR" };
            itemProject.Click += (s, e) => AppInfo.OpenRepository();
            menu.Items.Add(itemProject);

            var itemAbout = new MenuItem { Header = "ℹ️ 关于 DemoOCR" };
            itemAbout.Click += (s, e) => MessageBox.Show(
                $"{AppInfo.DisplayName}\n截图与离线 OCR 工具\n\n" +
                "• 框选完成后自动识别文字，可点击文字框直接复制\n" +
                "• 支持自定义截图与 OCR 快捷键\n" +
                "• 引擎: RapidAI/RapidOCR (PP-OCRv6 离线内嵌)\n" +
                "• 基于: .NET 10 WPF + 单文件免安装\n\n" +
                $"项目地址：{AppInfo.RepositoryUrl}",
                "关于 DemoOCR", MessageBoxButton.OK, MessageBoxImage.Information);
            menu.Items.Add(itemAbout);

            menu.Items.Add(new Separator());

            var itemExit = new MenuItem { Header = "✕ 退出程序" };
            itemExit.Click += (s, e) => RequestExit?.Invoke();
            menu.Items.Add(itemExit);

            return menu;
        }

        /// <summary>托盘鼠标点击消息监听钩子</summary>
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int mouseMsg = lParam.ToInt32();
                if (mouseMsg == WM_RBUTTONUP)
                {
                    SetForegroundWindow(_hWnd);
                    _contextMenu.IsOpen = true;
                    handled = true;
                }
                else if (mouseMsg == WM_LBUTTONUP)
                {
                    RequestSnipAndOcr?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>释放托盘图标资源</summary>
        public void Dispose()
        {
            if (_isCreated)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                _isCreated = false;
            }
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
            _icon.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
