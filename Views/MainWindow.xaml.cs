using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace InputStats;

public partial class MainWindow : Window
{
    private readonly InputHook _hook;
    private readonly StatsService _stats;
    private readonly NotifyIcon _tray;
    private readonly AppSettings _settings;
    private AppTheme _theme;
    private int _lastX;
    private int _lastY;
    private bool _first = true;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettingsStorage.Load();
        if (!WindowsStartupRegistration.Apply(_settings.StartWithWindows, out var startupSyncErr) && _settings.StartWithWindows)
            Logger.Warn("开机自启动已开启但未能同步到注册表: " + (startupSyncErr ?? "未知原因"));

        _theme = ThemeStorage.Load();

        _stats = new StatsService(_settings);
        if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--seed-mock", StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Info("检测到 --seed-mock 参数，生成模拟数据");
            _stats.GenerateMockData();
        }
        DataContext = _stats;

        StatsPage.Attach(_stats);
        StatsPage.StatsReset += (_, _) => _first = true;
        TrendPage.Attach(_stats);
        SettingsPage.ReloadFrom(_settings, _theme);
        SettingsPage.Saved += SettingsPage_Saved;
        SettingsPage.Cancelled += (_, _) => SettingsPage.ReloadFrom(_settings, _theme);

        _hook = new InputHook();
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseClicked += (x, y) => _stats.AddClick(x, y);
        _hook.KeyPressed += vk => _stats.AddKey(vk);
        _hook.Start();

        _tray = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Input Stats",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示", null, (_, _) => ShowWindow());
        menu.Items.Add("-");
        menu.Items.Add("退出", null, (_, _) => CloseApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                Hide();
        };

        NavStats.IsChecked = true;

        Logger.Info("主窗口初始化完成");

        // 延迟 3 秒后检查是否需要 ROI 校准
        var calibrationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        calibrationTimer.Tick += (_, _) =>
        {
            calibrationTimer.Stop();
            CheckAndPromptCalibration();
        };
        calibrationTimer.Start();
    }

    private void SidebarNav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton { Tag: string tag, IsChecked: true })
            return;

        switch (tag)
        {
            case "Stats":
                StatsPage.Visibility = Visibility.Visible;
                TrendPage.Visibility = Visibility.Collapsed;
                SettingsPage.Visibility = Visibility.Collapsed;
                break;
            case "Trend":
                StatsPage.Visibility = Visibility.Collapsed;
                TrendPage.Visibility = Visibility.Visible;
                SettingsPage.Visibility = Visibility.Collapsed;
                break;
            case "Settings":
                StatsPage.Visibility = Visibility.Collapsed;
                TrendPage.Visibility = Visibility.Collapsed;
                SettingsPage.Visibility = Visibility.Visible;
                SettingsPage.ReloadFrom(_settings, _theme);
                break;
        }
    }

    private void SettingsPage_Saved(object? sender, SettingsSavedEventArgs e)
    {
        _settings.MonthlySalary = e.Settings.MonthlySalary;
        _settings.UpdateIntervalSeconds = e.Settings.UpdateIntervalSeconds;
        _settings.WorkStartTime = e.Settings.WorkStartTime;
        _settings.WorkEndTime = e.Settings.WorkEndTime;
        _settings.RememberCloseChoice = e.Settings.RememberCloseChoice;
        _settings.CloseAction = e.Settings.CloseAction;
        _settings.StartWithWindows = e.Settings.StartWithWindows;
        AppSettingsStorage.Save(_settings);
        _stats.ApplySettings(_settings);
        Logger.Debug("用户保存设置");

        if (_theme != e.Theme)
        {
            _theme = e.Theme;
            ThemeStorage.Save(_theme);
            ThemeManager.ApplyTheme(_theme);
            System.Windows.MessageBox.Show("主题已切换，重启后完全生效。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        NavStats.IsChecked = true;
    }

    private void OnMouseMoved(int x, int y)
    {
        if (_first)
        {
            _lastX = x;
            _lastY = y;
            _first = false;
            return;
        }

        double cm = DistanceCalculator.PixelsToCm(_lastX, _lastY, x, y);
        if (cm > 0)
            _stats.AddCm(cm);

        _lastX = x;
        _lastY = y;
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Logger.Debug("窗口从托盘恢复显示");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        WindowState = WindowState.Minimized;
        Hide();
        Logger.Debug("窗口关闭被拦截，最小化到托盘");
    }

    private void CloseApp()
    {
        Logger.Info("应用退出");
        StatsPage.StopHeatMapTimer();
        TrendPage.StopRefreshTimer();
        _stats.Save();
        _tray.Dispose();
        _hook.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.RememberCloseChoice)
        {
            if (_settings.CloseAction == CloseAction.MinimizeToTray)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
            else if (_settings.CloseAction == CloseAction.Exit)
            {
                CloseApp();
            }
            return;
        }

        var dialog = new ExitConfirmDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.RememberChoice)
            {
                _settings.RememberCloseChoice = true;
                _settings.CloseAction = dialog.MinimizeToTray ? CloseAction.MinimizeToTray : CloseAction.Exit;
                AppSettingsStorage.Save(_settings);
            }

            if (dialog.MinimizeToTray)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
            else
            {
                CloseApp();
            }
        }
    }

    private bool IsPastWorkEndTime()
    {
        try
        {
            if (!string.IsNullOrEmpty(_settings.WorkEndTime))
            {
                var endTime = TimeSpan.ParseExact(_settings.WorkEndTime, "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.Now.TimeOfDay >= endTime;
            }
        }
        catch
        {
            // 解析失败时默认允许校准
        }
        return true;
    }

    private void CheckAndPromptCalibration()
    {
        if (!_stats.NeedsCalibration)
            return;

        // 检查今天是否已经有校准记录
        var today = DateTime.Today;
        var weights = RoiStorage.Load();
        if (weights.History.Any(h => h.Date.Date == today))
            return;

        // 检查今天是否有工作时段数据
        if (_stats.WorkClicks == 0 && _stats.WorkKeys == 0 && _stats.WorkCm == 0)
            return;

        // 检查当前时间是否已过工作结束时间（避免工作时段中途打扰）
        if (!IsPastWorkEndTime())
            return;

        Logger.Info("触发 ROI 校准提示");
        var dialog = new RoiCalibrationDialog(_stats.WorkClicks, _stats.WorkKeys, _stats.WorkCm, _stats.CalibrationCount)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && !dialog.Skipped)
        {
            if (_stats.TryAddCalibration(dialog.SelectedRoi))
            {
                var msg = _stats.CalibrationCount >= 3
                    ? "ROI 权重校准完成！以后将自动计算工作强度。"
                    : $"校准记录已保存（{_stats.CalibrationCount}/3），还需 {3 - _stats.CalibrationCount} 次即可完成校准。";
                System.Windows.MessageBox.Show(msg, "校准成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("权重计算失败，可能是校准数据过于相似。请明天继续校准。", "校准失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
