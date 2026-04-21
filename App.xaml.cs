using System.Windows;
using System.Windows.Threading;

namespace InputStats;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Error("未捕获的域异常", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString() ?? "未知异常"));
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("未捕获的 UI 线程异常", args.Exception);
            args.Handled = true;
        };

        Logger.Info("应用启动");
        ThemeManager.ApplyTheme(ThemeStorage.Load());
    }
}
