using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    private WriteableBitmap? _heatMapBitmap;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettingsStorage.Load();
        _theme = ThemeStorage.Load();

        _stats = new StatsService(_settings);
        DataContext = _stats;

        _hook = new InputHook();
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseClicked += (x, y) =>
        {
            _stats.AddClick(x, y);
            Dispatcher.Invoke(RenderHeatMap);
        };
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

        InitializeHeatMap();
        Logger.Info("主窗口初始化完成");
    }

    private void InitializeHeatMap()
    {
        _heatMapBitmap = new WriteableBitmap(StatsService.HeatMapW, StatsService.HeatMapH, 96, 96, PixelFormats.Bgra32, null);
        HeatMapImage.Source = _heatMapBitmap;
        RenderHeatMap();
    }

    private void RenderHeatMap()
    {
        if (_heatMapBitmap == null) return;

        int w = StatsService.HeatMapW;
        int h = StatsService.HeatMapH;
        int stride = w * 4;
        byte[] pixels = new byte[h * stride];
        int max = _stats.HeatMapMax;
        var data = _stats.HeatMapData;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = HeatColor(data[x, y], max);
                int idx = y * stride + x * 4;
                pixels[idx] = c.B;
                pixels[idx + 1] = c.G;
                pixels[idx + 2] = c.R;
                pixels[idx + 3] = 255;
            }
        }

        _heatMapBitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
    }

    private static System.Windows.Media.Color HeatColor(int value, int max)
    {
        if (max <= 0 || value <= 0) return System.Windows.Media.Colors.Black;

        double t = Math.Log(1 + value) / Math.Log(1 + max);
        t = Math.Min(1.0, t * 1.5);

        byte r, g, b;
        if (t < 0.2)
        {
            double s = t / 0.2;
            r = 0; g = 0; b = (byte)(s * 50);
        }
        else if (t < 0.4)
        {
            double s = (t - 0.2) / 0.2;
            r = 0; g = 0; b = (byte)(50 + s * 205);
        }
        else if (t < 0.6)
        {
            double s = (t - 0.4) / 0.2;
            r = 0; g = (byte)(s * 255); b = 255;
        }
        else if (t < 0.8)
        {
            double s = (t - 0.6) / 0.2;
            r = (byte)(s * 255); g = 255; b = (byte)((1 - s) * 255);
        }
        else
        {
            double s = (t - 0.8) / 0.2;
            r = 255; g = (byte)((1 - s) * 255); b = 0;
        }

        return System.Windows.Media.Color.FromRgb(r, g, b);
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

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _stats.Reset();
        _first = true;
        RenderHeatMap();
        Logger.Debug("用户点击重置统计");
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

    private void ViewButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new StatsChartDialog(_stats) { Owner = this };
        dialog.ShowDialog();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_settings, _theme) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _settings.MonthlySalary = dialog.Settings.MonthlySalary;
            _settings.UpdateIntervalSeconds = dialog.Settings.UpdateIntervalSeconds;
            _settings.WorkStartTime = dialog.Settings.WorkStartTime;
            _settings.WorkEndTime = dialog.Settings.WorkEndTime;
            AppSettingsStorage.Save(_settings);
            _stats.ApplySettings(_settings);
            Logger.Debug("用户保存设置");

            if (_theme != dialog.Theme)
            {
                _theme = dialog.Theme;
                ThemeStorage.Save(_theme);
                ThemeManager.ApplyTheme(_theme);
                System.Windows.MessageBox.Show("主题已切换，重启后完全生效。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
