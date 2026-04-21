using System.Diagnostics;
using System.IO;

namespace InputStats;

public static class Logger
{
    private static readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InputStats",
        "logs");

    private static readonly object _lock = new();

    public static string GetLogDirectory() => _logDir;

    public static void Debug(string message) => Write("DEBUG", message, null);
    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message) => Write("ERROR", message, null);
    public static void Error(string message, Exception ex) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(_logDir);
            var file = Path.Combine(_logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{level}] {message}";
            if (ex != null)
            {
                line += $"{Environment.NewLine}[Exception] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            }

            lock (_lock)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // 日志系统自身不能抛异常
        }
    }
}
