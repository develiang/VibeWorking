using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace InputStats;

public partial class StatsChartDialog : Window, INotifyPropertyChanged
{
    private readonly StatsService _stats;
    private TimeGranularity _currentGranularity = TimeGranularity.TenMinutes;
    private readonly DispatcherTimer _refreshTimer;

    private List<TimeBucket> _clickData = new();
    private List<TimeBucket> _keyData = new();

    public List<TimeBucket> ClickData
    {
        get => _clickData;
        set
        {
            _clickData = value;
            OnPropertyChanged(nameof(ClickData));
        }
    }

    public List<TimeBucket> KeyData
    {
        get => _keyData;
        set
        {
            _keyData = value;
            OnPropertyChanged(nameof(KeyData));
        }
    }

    public StatsChartDialog(StatsService stats)
    {
        InitializeComponent();
        _stats = stats;
        DataContext = this;
        RefreshData();

        _refreshTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal, (_, _) => RefreshData(), Dispatcher);
        _refreshTimer.Start();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _refreshTimer?.Stop();
        base.OnClosing(e);
    }

    private void RefreshData()
    {
        var newClickData = _stats.GetHistory(_currentGranularity);
        var newKeyData = _stats.GetHistory(_currentGranularity);

        bool clickChanged = !AreEqual(ClickData, newClickData);
        bool keyChanged = !AreEqual(KeyData, newKeyData);
        if (!clickChanged && !keyChanged) return;

        if (clickChanged) ClickData = newClickData;
        if (keyChanged) KeyData = newKeyData;
        Chart.DrawChart();
    }

    private static bool AreEqual(List<TimeBucket>? a, List<TimeBucket> b)
    {
        if (a == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].StartTime != b[i].StartTime || a[i].Clicks != b[i].Clicks || a[i].Keys != b[i].Keys)
                return false;
        }
        return true;
    }

    private void GranularityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;

        BtnTenMin.Style = (Style)FindResource("GranularityButtonStyle");
        BtnHour.Style = (Style)FindResource("GranularityButtonStyle");
        BtnDay.Style = (Style)FindResource("GranularityButtonStyle");

        btn.Style = (Style)FindResource("GranularityActiveButtonStyle");

        _currentGranularity = btn.Name switch
        {
            "BtnTenMin" => TimeGranularity.TenMinutes,
            "BtnHour" => TimeGranularity.Hour,
            "BtnDay" => TimeGranularity.Day,
            _ => TimeGranularity.TenMinutes
        };

        RefreshData();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
