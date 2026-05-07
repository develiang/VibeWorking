using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace InputStats;

/// <summary>
/// 通过当前用户「启动」注册表项（Run）实现开机自启动。
/// </summary>
public static class WindowsStartupRegistration
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "VibeWorking";

    /// <summary>
    /// 注册或移除启动项。失败时返回错误说明（已记录日志）。
    /// </summary>
    public static bool Apply(bool startWithWindows, out string? errorMessage)
    {
        errorMessage = null;
        if (!startWithWindows)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
                key?.DeleteValue(RegistryValueName, throwOnMissingValue: false);
                Logger.Debug("已移除开机自启动注册表项");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("移除开机自启动失败", ex);
                errorMessage = "无法移除开机自启动项，请稍后重试或以管理员身份排查注册表权限。";
                return false;
            }
        }

        var command = BuildStartupCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            errorMessage = "无法解析程序路径，无法启用开机自启动。";
            Logger.Warn("开机自启动：无法构建启动命令");
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true);
            key?.SetValue(RegistryValueName, command, RegistryValueKind.String);
            Logger.Info("已注册开机自启动: " + command);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("注册开机自启动失败", ex);
            errorMessage = "无法写入注册表，请检查权限或安全软件是否拦截。";
            return false;
        }
    }

    private static string? BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) &&
            !string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return QuoteIfNeeded(processPath);

        var dotnet = processPath;
        var dll = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrEmpty(dotnet) &&
            string.Equals(Path.GetFileName(dotnet), "dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(dll) &&
            dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return $"{QuoteIfNeeded(dotnet)} {QuoteIfNeeded(dll)}";

        var main = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(main) &&
            !string.Equals(Path.GetFileName(main), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return QuoteIfNeeded(main);

        return string.IsNullOrEmpty(dll) ? null : QuoteIfNeeded(dll);
    }

    private static string QuoteIfNeeded(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return trimmed;
        return trimmed.Contains(' ', StringComparison.Ordinal) ? $"\"{trimmed}\"" : trimmed;
    }
}
