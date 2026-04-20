using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace InputStats;

public partial class MainWindow : Window
{
    private readonly InputHook _hook;
    private readonly StatsService _stats;
    private readonly NotifyIcon _tray;
    private int _lastX;
    private int _lastY;
    private bool _first = true;

    public MainWindow()
    {
        InitializeComponent();

        _stats = new StatsService();
        DataContext = _stats;

        _hook = new InputHook();
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseClicked += _stats.AddClick;
        _hook.KeyPressed += _stats.AddKey;
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
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _stats.Reset();
        _first = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void CloseApp()
    {
        _stats.Save();
        _tray.Dispose();
        _hook.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
