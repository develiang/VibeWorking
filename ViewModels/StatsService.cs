using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace InputStats;

public sealed class StatsService : INotifyPropertyChanged
{
    public const int HeatMapW = 140;
    public const int HeatMapH = 78;

    private long _clicks;
    private long _keys;
    private double _cm;
    private double _earningsToday;
    private double _earningsMonth;
    private readonly ConcurrentDictionary<int, long> _keyCounts = new();
    private readonly int[,] _heatMapData = new int[HeatMapW, HeatMapH];
    private int _heatMapMax;
    private readonly object _lock = new();
    private System.Timers.Timer? _earningsTimer;
    private System.Timers.Timer? _historyTimer;

    private long _intervalClicks;
    private long _intervalKeys;
    private readonly TimeSeriesCollection _history = new();

    private AppSettings _settings;

    public long Clicks => Interlocked.Read(ref _clicks);
    public long Keys => Interlocked.Read(ref _keys);
    public double Cm
    {
        get { lock (_lock) return _cm; }
    }

    public int[,] HeatMapData => _heatMapData;
    public int HeatMapMax => _heatMapMax;

    public double EarningsToday
    {
        get { lock (_lock) return _earningsToday; }
        private set
        {
            lock (_lock) { _earningsToday = value; }
            OnPropertyChanged(nameof(EarningsToday));
        }
    }

    public double EarningsMonth
    {
        get { lock (_lock) return _earningsMonth; }
        private set
        {
            lock (_lock) { _earningsMonth = value; }
            OnPropertyChanged(nameof(EarningsMonth));
        }
    }

    public string TopKeysText
    {
        get
        {
            var top = _keyCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => $"{KeyName(kvp.Key)} {kvp.Value}")
                .ToList();
            return top.Count > 0 ? string.Join("  |  ", top) : "暂无数据";
        }
    }

    private static string KeyName(int vkCode)
    {
        try
        {
            var name = ((Keys)vkCode).ToString();
            return name switch
            {
                "LButton" => "左键",
                "RButton" => "右键",
                "MButton" => "中键",
                "Back" => "退格",
                "Tab" => "Tab",
                "Return" => "回车",
                "ShiftKey" => "Shift",
                "ControlKey" => "Ctrl",
                "Menu" => "Alt",
                "Pause" => "暂停",
                "Capital" => "Caps",
                "Escape" => "Esc",
                "Space" => "空格",
                "Prior" => "PgUp",
                "Next" => "PgDn",
                "End" => "End",
                "Home" => "Home",
                "Left" => "←",
                "Up" => "↑",
                "Right" => "→",
                "Down" => "↓",
                "Snapshot" => "截屏",
                "Insert" => "Ins",
                "Delete" => "Del",
                "D0" => "0",
                "D1" => "1",
                "D2" => "2",
                "D3" => "3",
                "D4" => "4",
                "D5" => "5",
                "D6" => "6",
                "D7" => "7",
                "D8" => "8",
                "D9" => "9",
                "LShiftKey" => "LShift",
                "RShiftKey" => "RShift",
                "LControlKey" => "LCtrl",
                "RControlKey" => "RCtrl",
                "LMenu" => "LAlt",
                "RMenu" => "RAlt",
                "Oemtilde" => "`",
                "OemMinus" => "-",
                "Oemplus" => "=",
                "OemOpenBrackets" => "[",
                "Oem6" => "]",
                "Oem1" => ";",
                "Oem7" => "'",
                "Oemcomma" => ",",
                "OemPeriod" => ".",
                "OemQuestion" => "/",
                "Oem5" => "\\",
                "OemBackslash" => "\\",
                "Oem3" => "`",
                "Oem102" => "\\",
                "OemSemicolon" => ";",
                "OemQuotes" => "'",
                "Oem8" => "?",
                "ProcessKey" => "IME",
                "Packet" => "Packet",
                "Attn" => "Attn",
                "Crsel" => "Crsel",
                "Exsel" => "Exsel",
                "EraseEof" => "EraseEOF",
                "Play" => "Play",
                "Zoom" => "Zoom",
                "NoName" => "NoName",
                "Pa1" => "Pa1",
                "OemClear" => "Clear",
                "LWin" => "Win",
                "RWin" => "Win",
                "Apps" => "菜单",
                "Sleep" => "Sleep",
                "Multiply" => "*",
                "Add" => "+",
                "Separator" => "分隔",
                "Subtract" => "-",
                "Decimal" => ".",
                "Divide" => "/",
                "NumLock" => "NumLock",
                "Scroll" => "Scroll",
                "PrintScreen" => "PrtSc",
                "BrowserBack" => "浏览器后退",
                "BrowserForward" => "浏览器前进",
                "BrowserRefresh" => "浏览器刷新",
                "BrowserStop" => "浏览器停止",
                "BrowserSearch" => "浏览器搜索",
                "BrowserFavorites" => "浏览器收藏",
                "BrowserHome" => "浏览器主页",
                "VolumeMute" => "静音",
                "VolumeDown" => "音量-",
                "VolumeUp" => "音量+",
                "MediaNextTrack" => "下一首",
                "MediaPreviousTrack" => "上一首",
                "MediaStop" => "停止",
                "MediaPlayPause" => "播放/暂停",
                "LaunchMail" => "邮件",
                "SelectMedia" => "媒体选择",
                "LaunchApplication1" => "App1",
                "LaunchApplication2" => "App2",
                _ => name
            };
        }
        catch
        {
            return $"VK{vkCode}";
        }
    }

    public void AddClick(int x, int y)
    {
        Interlocked.Increment(ref _clicks);
        Interlocked.Increment(ref _intervalClicks);

        var screen = Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        int gx = (int)((double)x / screen.Width * HeatMapW);
        int gy = (int)((double)y / screen.Height * HeatMapH);

        if (gx >= 0 && gx < HeatMapW && gy >= 0 && gy < HeatMapH)
        {
            const int radius = 4;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int nx = gx + dx;
                    int ny = gy + dy;
                    if (nx < 0 || nx >= HeatMapW || ny < 0 || ny >= HeatMapH) continue;

                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    int add = Math.Max(0, (int)((radius - dist + 0.5) * 8));
                    _heatMapData[nx, ny] += add;
                    if (_heatMapData[nx, ny] > _heatMapMax)
                        _heatMapMax = _heatMapData[nx, ny];
                }
            }
        }

        OnPropertyChanged(nameof(Clicks));
    }

    public void AddKey(int vkCode)
    {
        Interlocked.Increment(ref _keys);
        Interlocked.Increment(ref _intervalKeys);
        _keyCounts.AddOrUpdate(vkCode, 1, (_, count) => count + 1);
        OnPropertyChanged(nameof(Keys));
        OnPropertyChanged(nameof(TopKeysText));
    }

    public void AddCm(double value)
    {
        lock (_lock) { _cm += value; }
        OnPropertyChanged(nameof(Cm));
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _clicks, 0);
        Interlocked.Exchange(ref _keys, 0);
        lock (_lock) { _cm = 0; }
        _keyCounts.Clear();
        lock (_heatMapData)
        {
            Array.Clear(_heatMapData, 0, _heatMapData.Length);
            _heatMapMax = 1;
        }
        Logger.Info("统计数据已重置");
        UpdateEarnings();
        OnPropertyChanged(string.Empty);
    }

    public StatsService(AppSettings settings)
    {
        _settings = settings;

        var saved = StatsStorage.Load();
        Interlocked.Exchange(ref _clicks, saved.Clicks);
        Interlocked.Exchange(ref _keys, saved.Keys);
        lock (_lock) { _cm = saved.Cm; }
        foreach (var kvp in saved.KeyCounts)
        {
            _keyCounts[kvp.Key] = kvp.Value;
        }

        // 加载热力图
        if (saved.HeatMap != null && saved.HeatMap.Length > 0
            && saved.HeatMapW == HeatMapW && saved.HeatMapH == HeatMapH)
        {
            int idx = 0;
            for (int y = 0; y < HeatMapH; y++)
                for (int x = 0; x < HeatMapW; x++)
                    _heatMapData[x, y] = saved.HeatMap[idx++];
            _heatMapMax = saved.HeatMapMax > 0 ? saved.HeatMapMax : 1;
        }
        else
        {
            _heatMapMax = 1;
        }

        // 恢复历史数据
        _history.Restore(saved.History10Min, saved.HistoryHour, saved.HistoryDay);

        Logger.Info($"StatsService 初始化完成，已加载存档：点击 {saved.Clicks}，按键 {saved.Keys}，移动 {saved.Cm:F2} cm");
        UpdateEarnings();
        StartEarningsTimer();
        StartHistoryTimer();
    }

    private void StartEarningsTimer()
    {
        _earningsTimer?.Stop();
        _earningsTimer?.Dispose();

        int intervalMs = Math.Max(50, (int)(_settings.UpdateIntervalSeconds * 1000));
        _earningsTimer = new System.Timers.Timer(intervalMs);
        _earningsTimer.Elapsed += (_, _) => UpdateEarnings();
        _earningsTimer.AutoReset = true;
        _earningsTimer.Start();
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        Logger.Info("设置已应用");
        StartEarningsTimer();
        UpdateEarnings();
    }

    public void Save()
    {
        int max;
        lock (_heatMapData)
        {
            max = _heatMapMax;
        }
        FlushHistory();
        StatsStorage.Save(Clicks, Keys, Cm, _keyCounts, _heatMapData, max, _history);
    }

    public List<TimeBucket> GetHistory(TimeGranularity granularity)
    {
        var result = _history.Get(granularity);

        // 将当前尚未 flush 的 interval 数据合并到最后一个桶（或新建一个当前时间的桶）
        var currentClicks = Interlocked.Read(ref _intervalClicks);
        var currentKeys = Interlocked.Read(ref _intervalKeys);

        if (currentClicks > 0 || currentKeys > 0)
        {
            var now = DateTime.Now;
            DateTime currentBucketTime = granularity switch
            {
                TimeGranularity.TenMinutes => new DateTime(now.Year, now.Month, now.Day, now.Hour, (now.Minute / 10) * 10, 0),
                TimeGranularity.Hour => new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0),
                TimeGranularity.Day => now.Date,
                _ => now
            };

            if (result.Count > 0 && result[^1].StartTime == currentBucketTime)
            {
                result[^1] = new TimeBucket
                {
                    StartTime = result[^1].StartTime,
                    Clicks = result[^1].Clicks + currentClicks,
                    Keys = result[^1].Keys + currentKeys
                };
            }
            else
            {
                result.Add(new TimeBucket { StartTime = currentBucketTime, Clicks = currentClicks, Keys = currentKeys });
            }
        }

        return result;
    }

    private void StartHistoryTimer()
    {
        _historyTimer?.Stop();
        _historyTimer?.Dispose();

        _historyTimer = new System.Timers.Timer(60000); // 1 minute
        _historyTimer.Elapsed += (_, _) => FlushHistory();
        _historyTimer.AutoReset = true;
        _historyTimer.Start();
    }

    private void FlushHistory()
    {
        var clicks = Interlocked.Exchange(ref _intervalClicks, 0);
        var keys = Interlocked.Exchange(ref _intervalKeys, 0);

        if (clicks > 0 || keys > 0)
        {
            _history.AddSnapshot(clicks, keys);
            Logger.Debug($"历史数据已刷新：点击 +{clicks}，按键 +{keys}");
        }
    }

    private void UpdateEarnings()
    {
        var now = DateTime.Now;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        TimeSpan start = TimeSpan.Zero;
        TimeSpan end = TimeSpan.FromHours(24);
        try
        {
            if (!string.IsNullOrEmpty(_settings.WorkStartTime))
                start = TimeSpan.ParseExact(_settings.WorkStartTime, "hh\\:mm", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(_settings.WorkEndTime))
                end = TimeSpan.ParseExact(_settings.WorkEndTime, "hh\\:mm", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            Logger.Error($"工作时间解析失败: Start={_settings.WorkStartTime}, End={_settings.WorkEndTime}", ex);
        }

        double dailyWorkSeconds = (end - start).TotalSeconds;
        if (dailyWorkSeconds <= 0) dailyWorkSeconds = 24 * 60 * 60;

        double salaryPerSecond = _settings.MonthlySalary / daysInMonth / dailyWorkSeconds;

        // 今日已赚
        var todayStart = now.Date + start;
        var todayEnd = now.Date + end;

        double elapsedDaySeconds;
        if (now < todayStart)
            elapsedDaySeconds = 0;
        else if (now > todayEnd)
            elapsedDaySeconds = dailyWorkSeconds;
        else
            elapsedDaySeconds = (now - todayStart).TotalSeconds;

        EarningsToday = elapsedDaySeconds * salaryPerSecond;

        // 本月已赚：累加已过去的完整工作天 + 今天的工作时间
        var monthStart = new DateTime(now.Year, now.Month, 1);
        int fullWorkDays = 0;
        for (var d = monthStart; d < now.Date; d = d.AddDays(1))
        {
            fullWorkDays++;
        }

        double elapsedMonthSeconds = fullWorkDays * dailyWorkSeconds + elapsedDaySeconds;
        EarningsMonth = elapsedMonthSeconds * salaryPerSecond;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
