using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using DemoOCR.Core.Platform;

namespace DemoOCR.Core.Configuration
{
    /// <summary>
    /// 单个快捷键配置项模型（包含修饰键与主按键）
    /// </summary>
    public class HotkeySetting
    {
        /// <summary>是否包含 Ctrl 键</summary>
        public bool Ctrl { get; set; }
        /// <summary>是否包含 Alt 键</summary>
        public bool Alt { get; set; }
        /// <summary>是否包含 Shift 键</summary>
        public bool Shift { get; set; }
        /// <summary>是否包含 Win 徽标键</summary>
        public bool Win { get; set; }
        /// <summary>主键 (如 F1, A, C 等)</summary>
        public Key Key { get; set; }

        /// <summary>获取 Win32 RegisterHotKey 所需的修饰键掩码</summary>
        public uint GetWin32Modifiers()
        {
            uint mod = NativeMethods.MOD_NOREPEAT;
            if (Ctrl) mod |= NativeMethods.MOD_CONTROL;
            if (Alt) mod |= NativeMethods.MOD_ALT;
            if (Shift) mod |= NativeMethods.MOD_SHIFT;
            if (Win) mod |= NativeMethods.MOD_WIN;
            return mod;
        }

        /// <summary>获取 WPF Key 对应的 Win32 虚拟键码 (Virtual Key)</summary>
        public uint GetVirtualKey() => (uint)KeyInterop.VirtualKeyFromKey(Key);

        /// <summary>转为友好的快捷键展示文本 (如: Ctrl + Alt + A)</summary>
        public override string ToString()
        {
            string s = "";
            if (Ctrl) s += "Ctrl + ";
            if (Alt) s += "Alt + ";
            if (Shift) s += "Shift + ";
            if (Win) s += "Win + ";
            return s + Key;
        }
    }

    /// <summary>
    /// 全局应用程序配置管理类（支持 JSON 本地持久化存储）
    /// </summary>
    public class AppSettings
    {
        /// <summary>配置文件存储路径 (%LocalAppData%\DemoOCR\settings.json)</summary>
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DemoOCR", "settings.json");

        /// <summary>截图并OCR识别全局快捷键 (默认 F1)</summary>
        public HotkeySetting OcrHotkey { get; set; } = new() { Key = Key.F1 };

        /// <summary>仅截图复制到剪贴板全局快捷键 (默认 F2)</summary>
        public HotkeySetting CopyHotkey { get; set; } = new() { Key = Key.F2 };

        /// <summary>贴图全局快捷键 (默认 F3)</summary>
        public HotkeySetting PinHotkey { get; set; } = new() { Key = Key.F3 };

        /// <summary>框选后是否立即自动识别 (无需手动回车或点击按钮)</summary>
        public bool AutoOcrOnSelect { get; set; } = true;

        /// <summary>是否开机自启动 (默认 false)</summary>
        public bool AutoStartOnBoot { get; set; } = false;

        /// <summary>贴图历史回溯最大保留张数 (默认 10 张)</summary>
        public int MaxHistoryCount { get; set; } = 10;

        /// <summary>全局单例当前配置</summary>
        public static AppSettings Current { get; set; } = Load();

        /// <summary>从本地 JSON 文件读取配置，读取失败时自动使用默认配置</summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch
            {
                // 读取或解析异常时回退到默认设置
            }

            var def = new AppSettings();
            def.Save();
            return def;
        }

        /// <summary>将当前配置持久化保存到本地 JSON 文件</summary>
        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath)!;
                Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
            }
        }
    }
}
