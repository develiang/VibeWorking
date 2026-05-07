using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace InputStats;

public partial class StatsOverviewView : System.Windows.Controls.UserControl
{
    private StatsService? _stats;
    private WriteableBitmap? _heatMapBitmap;
    private byte[]? _heatMapPixels;
    private DispatcherTimer? _heatMapTimer;

    public event EventHandler? StatsReset;

    public StatsOverviewView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public void Attach(StatsService stats)
    {
        _stats = stats;
        DataContext = stats;
        InitializeHeatMap();
    }

    public void StopHeatMapTimer()
    {
        _heatMapTimer?.Stop();
        _heatMapTimer = null;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_heatMapTimer == null || _stats == null) return;
        if (IsVisible)
        {
            _heatMapTimer.Start();
            if (_stats.HeatMapDirty) RenderHeatMap();
        }
        else
            _heatMapTimer.Stop();
    }

    private void InitializeHeatMap()
    {
        if (_stats == null) return;
        if (_heatMapTimer != null)
        {
            RenderHeatMap();
            return;
        }

        _heatMapBitmap = new WriteableBitmap(StatsService.HeatMapW, StatsService.HeatMapH, 96, 96, PixelFormats.Bgra32, null);
        _heatMapPixels = new byte[StatsService.HeatMapW * StatsService.HeatMapH * 4];
        HeatMapImage.Source = _heatMapBitmap;
        RenderHeatMap();

        _heatMapTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _heatMapTimer.Tick += (_, _) =>
        {
            if (_stats.HeatMapDirty) RenderHeatMap();
        };
        if (IsVisible)
            _heatMapTimer.Start();
    }

    private void RenderHeatMap()
    {
        if (_heatMapBitmap == null || _heatMapPixels == null || _stats == null) return;

        int w = StatsService.HeatMapW;
        int h = StatsService.HeatMapH;
        int stride = w * 4;
        var pixels = _heatMapPixels;
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
        _stats.ClearHeatMapDirty();
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

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _stats?.Reset();
        StatsReset?.Invoke(this, EventArgs.Empty);
        RenderHeatMap();
        Logger.Debug("用户点击重置统计");
    }
}
