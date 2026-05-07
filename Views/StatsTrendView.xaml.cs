using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace InputStats;

public partial class StatsTrendView : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private StatsService? _stats;
    private TimeGranularity _currentGranularity = TimeGranularity.TenMinutes;
    private DateTime _startDate = DateTime.Today;
    private DateTime _endDate = DateTime.Today;
    private DispatcherTimer? _refreshTimer;
    private bool _wired;

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

    public StatsTrendView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public void Attach(StatsService stats)
    {
        if (_wired) return;
        _wired = true;
        _stats = stats;
        DataContext = this;

        StartDatePicker.SelectedDate = DateTime.Today.AddDays(-6);
        EndDatePicker.SelectedDate = DateTime.Today;

        RefreshData();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_wired || _stats == null) return;

        if (IsVisible)
        {
            _refreshTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal, (_, _) => RefreshData(), Dispatcher);
            _refreshTimer.Start();
            RefreshData();
            Chart.AnimateChart();
        }
        else
        {
            _refreshTimer?.Stop();
        }
    }

    private void RefreshData()
    {
        if (_stats == null) return;

        List<TimeBucket> newClickData;
        List<TimeBucket> newKeyData;

        if (_currentGranularity == TimeGranularity.CustomRange)
        {
            newClickData = _stats.GetHistory(TimeGranularity.CustomRange, _startDate, _endDate);
            newKeyData = newClickData;
        }
        else if (_currentGranularity == TimeGranularity.SevenDays)
        {
            var start = DateTime.Today.AddDays(-6);
            newClickData = _stats.GetHistory(TimeGranularity.Day, start, DateTime.Today);
            newKeyData = newClickData;
        }
        else if (_currentGranularity == TimeGranularity.ThirtyDays)
        {
            var start = DateTime.Today.AddDays(-29);
            newClickData = _stats.GetHistory(TimeGranularity.Day, start, DateTime.Today);
            newKeyData = newClickData;
        }
        else
        {
            newClickData = _stats.GetHistory(_currentGranularity);
            newKeyData = newClickData;
        }

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

        ResetButtonStyles();
        btn.Style = (Style)FindResource("GranularityActiveButtonStyle");

        _currentGranularity = btn.Name switch
        {
            "BtnTenMin" => TimeGranularity.TenMinutes,
            "BtnThirtyMin" => TimeGranularity.ThirtyMinutes,
            "BtnHour" => TimeGranularity.Hour,
            "BtnDay" => TimeGranularity.Day,
            "BtnSevenDays" => TimeGranularity.SevenDays,
            "BtnThirtyDays" => TimeGranularity.ThirtyDays,
            "BtnCustom" => TimeGranularity.CustomRange,
            _ => TimeGranularity.TenMinutes
        };

        CustomRangePanel.Visibility = _currentGranularity == TimeGranularity.CustomRange
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshData();
        Chart.AnimateChart();
    }

    private void ResetButtonStyles()
    {
        BtnTenMin.Style = (Style)FindResource("GranularityButtonStyle");
        BtnThirtyMin.Style = (Style)FindResource("GranularityButtonStyle");
        BtnHour.Style = (Style)FindResource("GranularityButtonStyle");
        BtnDay.Style = (Style)FindResource("GranularityButtonStyle");
        BtnSevenDays.Style = (Style)FindResource("GranularityButtonStyle");
        BtnThirtyDays.Style = (Style)FindResource("GranularityButtonStyle");
        BtnCustom.Style = (Style)FindResource("GranularityButtonStyle");
    }

    private void ApplyCustomRange_Click(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
        {
            _startDate = StartDatePicker.SelectedDate.Value;
            _endDate = EndDatePicker.SelectedDate.Value;
            if (_startDate > _endDate)
                (_startDate, _endDate) = (_endDate, _startDate);
            RefreshData();
            Chart.AnimateChart();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void StopRefreshTimer()
    {
        _refreshTimer?.Stop();
    }
}
