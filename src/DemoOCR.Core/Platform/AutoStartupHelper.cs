using System;
using Microsoft.Win32;

namespace DemoOCR.Core.Platform
{
    /// <summary>
    /// Windows 开机自启动辅助类（操作 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 注册表项）
    /// </summary>
    public static class AutoStartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DemoOCR";

        /// <summary>
        /// 检查当前是否已启用开机自启动
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 启用或禁用开机自启动
        /// </summary>
        /// <param name="enable">true: 写入当前 exe 完整路径至注册表; false: 从注册表删除</param>
        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置开机启动失败: {ex.Message}");
            }
        }
    }
}
