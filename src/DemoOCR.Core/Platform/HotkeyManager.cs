using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using DemoOCR.Core.Configuration;

namespace DemoOCR.Core.Platform
{
    /// <summary>
    /// 全局热键触发的操作类型
    /// </summary>
    public enum HotkeyAction
    {
        /// <summary>截图并进行 OCR 文字识别</summary>
        SnipAndOcr,
        /// <summary>仅截图复制图像到剪贴板</summary>
        SnipAndCopy,
        /// <summary>将剪贴板图像作为桌面贴图钉在屏幕上</summary>
        PinImage
    }

    /// <summary>
    /// Win32 全局热键注册与自动防冲突探测管理类
    /// </summary>
    public class HotkeyManager : IDisposable
    {
        private const int HOTKEY_ID_OCR = 9001;
        private const int HOTKEY_ID_COPY = 9002;
        private const int HOTKEY_ID_PIN = 9003;

        private HwndSource? _hwndSource;
        private IntPtr _hWnd = IntPtr.Zero;
        private bool _isDisposed;

        /// <summary>热键触发回调事件</summary>
        public event Action<HotkeyAction>? HotkeyPressed;
        /// <summary>当前已生效热键的总体描述文本</summary>
        public string RegisteredHotkeysDescription { get; private set; } = string.Empty;

        /// <summary>当前 OCR 识别实际生效的热键描述</summary>
        public string OcrHotkeyDescription { get; private set; } = string.Empty;
        /// <summary>当前截图复制实际生效的热键描述</summary>
        public string CopyHotkeyDescription { get; private set; } = string.Empty;
        /// <summary>当前贴图实际生效的热键描述</summary>
        public string PinHotkeyDescription { get; private set; } = string.Empty;

        /// <summary>备用快捷键候选结构体</summary>
        private record struct FallbackCandidate(uint Modifiers, uint VirtualKey, string DisplayText);

        /// <summary>初始化后台隐藏消息窗口句柄，用于监听 WM_HOTKEY 消息</summary>
        public void Initialize()
        {
            if (_hwndSource == null)
            {
                var parameters = new HwndSourceParameters("DemoOCR_Hotkey_Hwnd") { WindowStyle = 0x800000 };
                _hwndSource = new HwndSource(parameters);
                _hwndSource.AddHook(HwndHook);
                _hWnd = _hwndSource.Handle;
            }
        }

        /// <summary>应用并重新注册全局热键设置（若首选冲突则全自动搜寻备用热键）</summary>
        public (bool ocrOk, bool copyOk, bool pinOk) ApplySettings(AppSettings settings)
        {
            Initialize();
            UnregisterHotkeys();

            var usedKeys = new HashSet<string>();

            // 预设公共备选热键池（覆盖 F4~F12 及常用非冲突组合键）
            uint noRepeat = NativeMethods.MOD_NOREPEAT;
            uint ctrlMod = NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT;
            uint altMod = NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;

            var ocrFallbacks = new[]
            {
                new FallbackCandidate(noRepeat, 0x73, "F4"),
                new FallbackCandidate(noRepeat, 0x75, "F6"),
                new FallbackCandidate(noRepeat, 0x76, "F7"),
                new FallbackCandidate(noRepeat, 0x77, "F8"),
                new FallbackCandidate(noRepeat, 0x78, "F9"),
                new FallbackCandidate(noRepeat, 0x79, "F10"),
                new FallbackCandidate(noRepeat, 0x7A, "F11"),
                new FallbackCandidate(noRepeat, 0x7B, "F12"),
                new FallbackCandidate(ctrlMod, 0x70, "Ctrl + F1"),
                new FallbackCandidate(altMod, 0x51, "Alt + Q")
            };

            var copyFallbacks = new[]
            {
                new FallbackCandidate(noRepeat, 0x75, "F6"),
                new FallbackCandidate(noRepeat, 0x76, "F7"),
                new FallbackCandidate(noRepeat, 0x77, "F8"),
                new FallbackCandidate(noRepeat, 0x78, "F9"),
                new FallbackCandidate(noRepeat, 0x79, "F10"),
                new FallbackCandidate(noRepeat, 0x7A, "F11"),
                new FallbackCandidate(noRepeat, 0x7B, "F12"),
                new FallbackCandidate(noRepeat, 0x73, "F4"),
                new FallbackCandidate(ctrlMod, 0x71, "Ctrl + F2"),
                new FallbackCandidate(altMod, 0x57, "Alt + W")
            };

            var pinFallbacks = new[]
            {
                new FallbackCandidate(noRepeat, 0x77, "F8"),
                new FallbackCandidate(noRepeat, 0x78, "F9"),
                new FallbackCandidate(noRepeat, 0x79, "F10"),
                new FallbackCandidate(noRepeat, 0x7A, "F11"),
                new FallbackCandidate(noRepeat, 0x7B, "F12"),
                new FallbackCandidate(noRepeat, 0x76, "F7"),
                new FallbackCandidate(noRepeat, 0x75, "F6"),
                new FallbackCandidate(noRepeat, 0x73, "F4"),
                new FallbackCandidate(ctrlMod, 0x72, "Ctrl + F3"),
                new FallbackCandidate(altMod, 0x45, "Alt + E")
            };

            // 1. 自动探测并注册 OCR 热键
            var (ocrOk, ocrDesc) = TryRegisterWithFallback(HOTKEY_ID_OCR, settings.OcrHotkey, usedKeys, ocrFallbacks);
            OcrHotkeyDescription = ocrDesc;

            // 2. 自动探测并注册仅截图复制热键
            var (copyOk, copyDesc) = TryRegisterWithFallback(HOTKEY_ID_COPY, settings.CopyHotkey, usedKeys, copyFallbacks);
            CopyHotkeyDescription = copyDesc;

            // 3. 自动探测并注册贴图热键
            var (pinOk, pinDesc) = TryRegisterWithFallback(HOTKEY_ID_PIN, settings.PinHotkey, usedKeys, pinFallbacks);
            PinHotkeyDescription = pinDesc;

            RegisteredHotkeysDescription = $"OCR: {ocrDesc} | 截图: {copyDesc} | 贴图: {pinDesc}";
            return (ocrOk, copyOk, pinOk);
        }

        /// <summary>
        /// 尝试注册热键：优先使用用户配置，若冲突则依次搜寻按键修饰变体与备选热键池
        /// </summary>
        private (bool ok, string desc) TryRegisterWithFallback(
            int hotkeyId,
            HotkeySetting preferred,
            HashSet<string> usedKeys,
            FallbackCandidate[] fallbackPool)
        {
            // 阶段一：尝试用户首选快捷键
            uint prefMod = preferred.GetWin32Modifiers();
            uint prefVk = preferred.GetVirtualKey();
            string prefKeyId = $"{prefMod}_{prefVk}";

            if (!usedKeys.Contains(prefKeyId) && NativeMethods.RegisterHotKey(_hWnd, hotkeyId, prefMod, prefVk))
            {
                usedKeys.Add(prefKeyId);
                return (true, preferred.ToString());
            }

            // 阶段二：若首选冲突，优先针对该按键尝试常用修饰键组合变体 (Ctrl+Shift+Key, Alt+Shift+Key, Ctrl+Alt+Key)
            var modifierVariants = new[]
            {
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, $"Ctrl + Shift + {preferred.Key}"),
                (NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, $"Alt + Shift + {preferred.Key}"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, $"Ctrl + Alt + {preferred.Key}")
            };

            foreach (var (mod, name) in modifierVariants)
            {
                string keyId = $"{mod}_{prefVk}";
                if (!usedKeys.Contains(keyId) && NativeMethods.RegisterHotKey(_hWnd, hotkeyId, mod, prefVk))
                {
                    usedKeys.Add(keyId);
                    return (true, $"{name} (备用)");
                }
            }

            // 阶段三：循环遍历安全备用热键池
            foreach (var fb in fallbackPool)
            {
                string keyId = $"{fb.Modifiers}_{fb.VirtualKey}";
                if (usedKeys.Contains(keyId)) continue;

                if (NativeMethods.RegisterHotKey(_hWnd, hotkeyId, fb.Modifiers, fb.VirtualKey))
                {
                    usedKeys.Add(keyId);
                    return (true, $"{fb.DisplayText} (备用)");
                }
            }

            return (false, "未生效");
        }

        /// <summary>Win32 窗口消息钩子回调</summary>
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_OCR) { HotkeyPressed?.Invoke(HotkeyAction.SnipAndOcr); handled = true; }
                else if (id == HOTKEY_ID_COPY) { HotkeyPressed?.Invoke(HotkeyAction.SnipAndCopy); handled = true; }
                else if (id == HOTKEY_ID_PIN) { HotkeyPressed?.Invoke(HotkeyAction.PinImage); handled = true; }
            }
            return IntPtr.Zero;
        }

        /// <summary>注销所有已注册的热键</summary>
        public void UnregisterHotkeys()
        {
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID_OCR);
                NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID_COPY);
                NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID_PIN);
            }
        }

        /// <summary>释放热键与窗口消息资源</summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                UnregisterHotkeys();
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(HwndHook);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                    _hWnd = IntPtr.Zero;
                }
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
