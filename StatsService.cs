using System.ComponentModel;
using System.Threading;
using System.Timers;

namespace InputStats;

public sealed class StatsService : INotifyPropertyChanged
{
    private long _clicks;
    private long _keys;
    private double _cm;
    private double _earningsToday;
    private double _earningsMonth;
    private readonly object _lock = new();
    private readonly System.Timers.Timer _earningsTimer;

    private const double MonthlySalary = 23500;
    private const int WorkDaysPerMonth = 22;
    private const int WorkHoursPerDay = 8;
    private const int WorkStartHour = 9;
    private const int WorkEndHour = 17;

    public long Clicks => Interlocked.Read(ref _clicks);
    public long Keys => Interlocked.Read(ref _keys);
    public double Cm
    {
        get { lock (_lock) return _cm; }
    }

    public double EarningsToday
    {
        get { lock (_lock) return _earningsToday; }
        private set
        {
            lock (_lock)
            {
                _earningsToday = value;
            }
            OnPropertyChanged(nameof(EarningsToday));
        }
    }

    public double EarningsMonth
    {
        get { lock (_lock) return _earningsMonth; }
        private set
        {
            lock (_lock)
            {
                _earningsMonth = value;
            }
            OnPropertyChanged(nameof(EarningsMonth));
        }
    }

    public void AddClick()
    {
        Interlocked.Increment(ref _clicks);
        OnPropertyChanged(nameof(Clicks));
    }

    public void AddKey()
    {
        Interlocked.Increment(ref _keys);
        OnPropertyChanged(nameof(Keys));
    }

    public void AddCm(double value)
    {
        lock (_lock)
        {
            _cm += value;
        }
        OnPropertyChanged(nameof(Cm));
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _clicks, 0);
        Interlocked.Exchange(ref _keys, 0);
        lock (_lock)
        {
            _cm = 0;
        }
        UpdateEarnings();
        OnPropertyChanged(string.Empty);
    }

    public StatsService()
    {
        var saved = StatsStorage.Load();
        Interlocked.Exchange(ref _clicks, saved.Clicks);
        Interlocked.Exchange(ref _keys, saved.Keys);
        lock (_lock) { _cm = saved.Cm; }

        UpdateEarnings();
        _earningsTimer = new System.Timers.Timer(1000);
        _earningsTimer.Elapsed += (_, _) => UpdateEarnings();
        _earningsTimer.AutoReset = true;
        _earningsTimer.Start();
    }

    public void Save()
    {
        StatsStorage.Save(Clicks, Keys, Cm);
    }

    private void UpdateEarnings()
    {
        var now = DateTime.Now;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        double salaryPerSecond = MonthlySalary / daysInMonth / 24 / 60 / 60;

        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
        double elapsedDaySeconds = (now - dayStart).TotalSeconds;
        EarningsToday = elapsedDaySeconds * salaryPerSecond;

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0);
        double elapsedMonthSeconds = (now - monthStart).TotalSeconds;
        EarningsMonth = elapsedMonthSeconds * salaryPerSecond;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
