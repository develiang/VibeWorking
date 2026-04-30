using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace InputStats;

public enum DisplayMode
{
    Today,
    Total
}

public sealed class StatsService : INotifyPropertyChanged
{
    public const int HeatMapW = 140;
    public const int HeatMapH = 78;

    private long _clicks;
    private long _keys;
    private double _cm;
    private long _totalClicks;
    private long _totalKeys;
    private double _totalCm;
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
    private DateTime _currentDate;
    private DisplayMode _displayMode = DisplayMode.Today;

    public long Clicks => Interlocked.Read(ref _clicks);
    public long Keys => Interlocked.Read(ref _keys);
    public double Cm
    {
        get { lock (_lock) return _cm; }
    }

    public long DisplayClicks => _displayMode == DisplayMode.Today ? Clicks : (_totalClicks + Clicks);
    public long DisplayKeys => _displayMode == DisplayMode.Today ? Keys : (_totalKeys + Keys);
    public double DisplayCm => _displayMode == DisplayMode.Today ? Cm : (_totalCm + Cm);

    public int[,] HeatMapData => _heatMapData;
    public int HeatMapMax => _heatMapMax;

    private volatile bool _heatMapDirty = true;
    public bool HeatMapDirty => _heatMapDirty;
    public void ClearHeatMapDirty() => _heatMapDirty = false;

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

    public DisplayMode CurrentDisplayMode
    {
        get => _displayMode;
        set
        {
            _displayMode = value;
            OnPropertyChanged(nameof(CurrentDisplayMode));
            OnPropertyChanged(nameof(DisplayClicks));
            OnPropertyChanged(nameof(DisplayKeys));
            OnPropertyChanged(nameof(DisplayCm));
        }
    }

    public string ModeButtonText => _displayMode == DisplayMode.Today ? "切换累计" : "切换当日";

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

        var (gx, gy) = MapPointToHeatGrid(x, y, SystemInformation.VirtualScreen);

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

        _heatMapDirty = true;
        OnPropertyChanged(nameof(Clicks));
        OnPropertyChanged(nameof(DisplayClicks));
    }

    /// <summary>
    /// 把屏幕（虚拟桌面）坐标映射到热力图格子坐标。可单测。
    /// 越界时返回 (-1, -1)。
    /// </summary>
    internal static (int gx, int gy) MapPointToHeatGrid(int x, int y, System.Drawing.Rectangle virtualScreen)
    {
        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
            return (-1, -1);

        double nx = (double)(x - virtualScreen.Left) / virtualScreen.Width;
        double ny = (double)(y - virtualScreen.Top) / virtualScreen.Height;

        if (nx < 0 || nx >= 1.0 || ny < 0 || ny >= 1.0)
            return (-1, -1);

        int gx = (int)(nx * HeatMapW);
        int gy = (int)(ny * HeatMapH);
        if (gx >= HeatMapW) gx = HeatMapW - 1;
        if (gy >= HeatMapH) gy = HeatMapH - 1;
        return (gx, gy);
    }

    public void AddKey(int vkCode)
    {
        Interlocked.Increment(ref _keys);
        Interlocked.Increment(ref _intervalKeys);
        _keyCounts.AddOrUpdate(vkCode, 1, (_, count) => count + 1);
        OnPropertyChanged(nameof(Keys));
        OnPropertyChanged(nameof(DisplayKeys));
        OnPropertyChanged(nameof(TopKeysText));
    }

    public void AddCm(double value)
    {
        lock (_lock) { _cm += value; }
        OnPropertyChanged(nameof(Cm));
        OnPropertyChanged(nameof(DisplayCm));
    }

    public void Reset()
    {
        ResetDailyStats();
        _history.Reset();

        SaveForDate(_currentDate);
        SaveHeatMapForDate(_currentDate);

        Logger.Info("统计数据已重置");
        UpdateEarnings();
        OnPropertyChanged(string.Empty);
    }

    public StatsService(AppSettings settings)
    {
        _settings = settings;
        _currentDate = DateTime.Today;

        // 加载历史累计（当天之前的所有数据）
        var totals = StatsStorage.GetTotalsBefore(_currentDate);
        _totalClicks = totals.Clicks;
        _totalKeys = totals.Keys;
        _totalCm = totals.Cm;

        var saved = StatsStorage.Load(_currentDate);
        Interlocked.Exchange(ref _clicks, saved.Clicks);
        Interlocked.Exchange(ref _keys, saved.Keys);
        lock (_lock) { _cm = saved.Cm; }
        foreach (var kvp in saved.KeyCounts)
        {
            _keyCounts[kvp.Key] = kvp.Value;
        }

        // 加载热力图
        var heatMap = StatsStorage.LoadHeatMap(_currentDate);
        if (heatMap != null)
        {
            lock (_heatMapData)
            {
                Buffer.BlockCopy(heatMap, 0, _heatMapData, 0, heatMap.Length * sizeof(int));
            }
            _heatMapMax = saved.HeatMapMax > 0 ? saved.HeatMapMax : 1;
        }
        else
        {
            _heatMapMax = 1;
        }

        // 恢复当天10分钟历史
        _history.Restore(saved.TenMinutes, null, null);

        Logger.Info($"StatsService 初始化完成，日期 {_currentDate:yyyyMMdd}，已加载存档：点击 {saved.Clicks}，按键 {saved.Keys}，移动 {saved.Cm:F2} cm");
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
        var today = DateTime.Today;
        if (today != _currentDate)
        {
            FlushHistory();
            SaveForDate(_currentDate);
            SaveHeatMapForDate(_currentDate);

            var dayClicks = Clicks;
            var dayKeys = Keys;
            var dayCm = Cm;
            _totalClicks += dayClicks;
            _totalKeys += dayKeys;
            _totalCm += dayCm;
            StatsStorage.AdvanceAggregate(_currentDate, dayClicks, dayKeys, dayCm);

            ResetDailyStats();
            _currentDate = today;
        }

        FlushHistory();
        SaveForDate(today);
        SaveHeatMapForDate(today);
    }

    public void ToggleDisplayMode()
    {
        CurrentDisplayMode = _displayMode == DisplayMode.Today ? DisplayMode.Total : DisplayMode.Today;
        OnPropertyChanged(nameof(ModeButtonText));
    }

    public List<TimeBucket> GetHistory(TimeGranularity granularity)
    {
        return GetHistory(granularity, _currentDate, _currentDate);
    }

    public List<TimeBucket> GetHistory(TimeGranularity granularity, DateTime startDate, DateTime endDate)
    {
        if (granularity == TimeGranularity.TenMinutes && startDate == _currentDate && endDate == _currentDate)
        {
            return GetTodayTenMinutesWithInterval();
        }

        if (granularity == TimeGranularity.Hour && startDate == _currentDate && endDate == _currentDate)
        {
            return GetTodayHoursWithInterval();
        }

        var allStats = StatsStorage.LoadRange(startDate, endDate);

        if (granularity == TimeGranularity.Day || granularity == TimeGranularity.SevenDays || granularity == TimeGranularity.ThirtyDays)
        {
            var result = new List<TimeBucket>();
            for (int i = 0; i < allStats.Count; i++)
            {
                var date = startDate.AddDays(i);
                result.Add(new TimeBucket
                {
                    StartTime = date,
                    Clicks = allStats[i].Clicks,
                    Keys = allStats[i].Keys
                });
            }
            return result;
        }

        if (granularity == TimeGranularity.Hour)
        {
            return AggregateToHour(allStats);
        }

        if (granularity == TimeGranularity.ThirtyMinutes)
        {
            return AggregateToThirtyMinutes(allStats);
        }

        if (granularity == TimeGranularity.CustomRange)
        {
            return AggregateToDay(allStats);
        }

        return new List<TimeBucket>();
    }

    private List<TimeBucket> GetTodayTenMinutesWithInterval()
    {
        var result = _history.Get(TimeGranularity.TenMinutes);
        var currentClicks = Interlocked.Read(ref _intervalClicks);
        var currentKeys = Interlocked.Read(ref _intervalKeys);

        if (currentClicks > 0 || currentKeys > 0)
        {
            var now = DateTime.Now;
            var currentBucketTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, (now.Minute / 10) * 10, 0);

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

    private List<TimeBucket> GetTodayHoursWithInterval()
    {
        var result = _history.Get(TimeGranularity.Hour);
        var currentClicks = Interlocked.Read(ref _intervalClicks);
        var currentKeys = Interlocked.Read(ref _intervalKeys);

        if (currentClicks > 0 || currentKeys > 0)
        {
            var now = DateTime.Now;
            var currentBucketTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

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

    private static List<TimeBucket> AggregateToThirtyMinutes(List<DailyStats> allStats)
    {
        var buckets = new Dictionary<DateTime, TimeBucket>();
        foreach (var stats in allStats)
        {
            foreach (var tm in stats.TenMinutes)
            {
                var min = (tm.StartTime.Minute / 30) * 30;
                var thirtyMinStart = new DateTime(tm.StartTime.Year, tm.StartTime.Month, tm.StartTime.Day, tm.StartTime.Hour, min, 0);
                if (!buckets.TryGetValue(thirtyMinStart, out var bucket))
                {
                    bucket = new TimeBucket { StartTime = thirtyMinStart };
                    buckets[thirtyMinStart] = bucket;
                }
                bucket.Clicks += tm.Clicks;
                bucket.Keys += tm.Keys;
            }
        }
        return buckets.OrderBy(b => b.Key).Select(b => b.Value).ToList();
    }

    private static List<TimeBucket> AggregateToHour(List<DailyStats> allStats)
    {
        var buckets = new Dictionary<DateTime, TimeBucket>();
        foreach (var stats in allStats)
        {
            foreach (var tm in stats.TenMinutes)
            {
                var hourStart = new DateTime(tm.StartTime.Year, tm.StartTime.Month, tm.StartTime.Day, tm.StartTime.Hour, 0, 0);
                if (!buckets.TryGetValue(hourStart, out var bucket))
                {
                    bucket = new TimeBucket { StartTime = hourStart };
                    buckets[hourStart] = bucket;
                }
                bucket.Clicks += tm.Clicks;
                bucket.Keys += tm.Keys;
            }
        }
        return buckets.OrderBy(b => b.Key).Select(b => b.Value).ToList();
    }

    private static List<TimeBucket> AggregateToDay(List<DailyStats> allStats)
    {
        var result = new List<TimeBucket>();
        foreach (var stats in allStats)
        {
            if (stats.TenMinutes.Count > 0)
            {
                result.Add(new TimeBucket
                {
                    StartTime = stats.TenMinutes[0].StartTime.Date,
                    Clicks = stats.Clicks,
                    Keys = stats.Keys
                });
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
        var today = DateTime.Today;
        if (today != _currentDate)
        {
            var clicks = Interlocked.Exchange(ref _intervalClicks, 0);
            var keys = Interlocked.Exchange(ref _intervalKeys, 0);
            if (clicks > 0 || keys > 0)
            {
                _history.AddSnapshot(clicks, keys);
            }

            SaveForDate(_currentDate);
            SaveHeatMapForDate(_currentDate);

            var dayClicks = Clicks;
            var dayKeys = Keys;
            var dayCm = Cm;
            _totalClicks += dayClicks;
            _totalKeys += dayKeys;
            _totalCm += dayCm;
            StatsStorage.AdvanceAggregate(_currentDate, dayClicks, dayKeys, dayCm);

            ResetDailyStats();
            _currentDate = today;
            return;
        }

        var c = Interlocked.Exchange(ref _intervalClicks, 0);
        var k = Interlocked.Exchange(ref _intervalKeys, 0);
        if (c > 0 || k > 0)
        {
            _history.AddSnapshot(c, k);
            Logger.Debug($"历史数据已刷新：点击 +{c}，按键 +{k}");
        }
    }

    private void SaveForDate(DateTime date)
    {
        int max;
        lock (_heatMapData) { max = _heatMapMax; }

        var data = new DailyStats
        {
            Clicks = Clicks,
            Keys = Keys,
            Cm = Cm,
            KeyCounts = _keyCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            TenMinutes = _history.Get(TimeGranularity.TenMinutes),
            HeatMapMax = max,
        };
        StatsStorage.Save(date, data);
    }

    private void SaveHeatMapForDate(DateTime date)
    {
        lock (_heatMapData)
        {
            StatsStorage.SaveHeatMap(date, _heatMapData);
        }
    }

    private void ResetDailyStats()
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
        _history.ResetDaily();
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

        var monthStart = new DateTime(now.Year, now.Month, 1);
        int fullWorkDays = 0;
        for (var d = monthStart; d < now.Date; d = d.AddDays(1))
        {
            fullWorkDays++;
        }

        double elapsedMonthSeconds = fullWorkDays * dailyWorkSeconds + elapsedDaySeconds;
        EarningsMonth = elapsedMonthSeconds * salaryPerSecond;
    }

    public void GenerateMockData()
    {
        var baseDir = StatsStorage.BaseDir;
        var todayPath = Path.Combine(baseDir, $"stats_{DateTime.Today:yyyyMMdd}.json");
        if (File.Exists(todayPath)) return;

        var rnd = new Random();
        Logger.Info("开始生成模拟数据...");

        for (int i = 6; i >= 0; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            GenerateMockDay(date, rnd);
        }

        // 重新加载历史累计
        var totals = StatsStorage.GetTotalsBefore(_currentDate);
        _totalClicks = totals.Clicks;
        _totalKeys = totals.Keys;
        _totalCm = totals.Cm;

        Logger.Info("模拟数据生成完成");
    }

    private static void GenerateMockDay(DateTime date, Random rnd)
    {
        var dailyClicks = rnd.NextInt64(5000, 15000);
        var dailyKeys = rnd.NextInt64(10000, 30000);
        var dailyCm = rnd.NextDouble() * 1000 + 500;

        var tenMinutes = new List<TimeBucket>();
        long remainingClicks = dailyClicks;
        long remainingKeys = dailyKeys;

        for (int h = 0; h < 24; h++)
        {
            for (int m = 0; m < 60; m += 10)
            {
                bool isWorkTime = h >= 9 && h < 18;
                double factor = isWorkTime ? 1.5 : 0.2;

                var bucketClicks = Math.Min(remainingClicks, (long)(rnd.Next(50, 150) * factor));
                var bucketKeys = Math.Min(remainingKeys, (long)(rnd.Next(100, 300) * factor));

                if (h == 23 && m == 50)
                {
                    bucketClicks = remainingClicks;
                    bucketKeys = remainingKeys;
                }

                remainingClicks -= bucketClicks;
                remainingKeys -= bucketKeys;

                tenMinutes.Add(new TimeBucket
                {
                    StartTime = new DateTime(date.Year, date.Month, date.Day, h, m, 0),
                    Clicks = bucketClicks,
                    Keys = bucketKeys
                });

                if (remainingClicks <= 0 && remainingKeys <= 0) break;
            }
            if (remainingClicks <= 0 && remainingKeys <= 0) break;
        }

        var keyCounts = new Dictionary<int, long>
        {
            [(int)System.Windows.Forms.Keys.Space] = rnd.NextInt64(1000, 3000),
            [(int)System.Windows.Forms.Keys.Return] = rnd.NextInt64(500, 1500),
            [(int)System.Windows.Forms.Keys.Back] = rnd.NextInt64(300, 1000),
            [(int)System.Windows.Forms.Keys.LControlKey] = rnd.NextInt64(200, 800),
            [(int)System.Windows.Forms.Keys.LShiftKey] = rnd.NextInt64(200, 800),
        };

        var stats = new DailyStats
        {
            Clicks = dailyClicks,
            Keys = dailyKeys,
            Cm = dailyCm,
            TenMinutes = tenMinutes,
            KeyCounts = keyCounts,
            HeatMapMax = rnd.Next(100, 500)
        };
        StatsStorage.Save(date, stats);

        var heatMap = new int[HeatMapW, HeatMapH];
        int hotspots = rnd.Next(3, 8);
        for (int s = 0; s < hotspots; s++)
        {
            int cx = rnd.Next(20, HeatMapW - 20);
            int cy = rnd.Next(10, HeatMapH - 10);
            int radius = rnd.Next(5, 15);
            int intensity = rnd.Next(50, 200);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= HeatMapW || ny < 0 || ny >= HeatMapH) continue;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    int add = Math.Max(0, (int)((radius - dist) * intensity / radius));
                    heatMap[nx, ny] += add;
                }
            }
        }
        StatsStorage.SaveHeatMap(date, heatMap);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
