using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace DemoOCR;

public static class AppInfo
{
    private static readonly Assembly AppAssembly = typeof(AppInfo).Assembly;

    public static string Name { get; } = AppAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? throw new InvalidOperationException("缺少应用产品名称。");

    public static string Version { get; } = "V" + (AppAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("缺少应用版本信息。"));

    public static string DisplayName => $"{Name} {Version}";

    public static string RepositoryUrl { get; } = AppAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "RepositoryUrl").Value
        ?? throw new InvalidOperationException("缺少项目地址。");

    public static Uri RepositoryUri { get; } = new(RepositoryUrl, UriKind.Absolute);

    public static void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = RepositoryUrl, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show($"无法打开项目地址：{exception.Message}\n{RepositoryUrl}", Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
