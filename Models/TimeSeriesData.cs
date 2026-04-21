namespace InputStats;

public enum TimeGranularity
{
    TenMinutes,
    Hour,
    Day
}

public class TimeBucket
{
    public DateTime StartTime { get; set; }
    public long Clicks { get; set; }
    public long Keys { get; set; }
}

public class TimeSeriesCollection
{
    public List<TimeBucket> TenMinutes { get; set; } = new();
    public List<TimeBucket> Hours { get; set; } = new();
    public List<TimeBucket> Days { get; set; } = new();

    private readonly object _lock = new();
    private const int MaxTenMinutes = 144; // 24 hours
    private const int MaxHours = 168;      // 7 days
    private const int MaxDays = 30;        // 30 days

    public void AddSnapshot(long clicks, long keys)
    {
        var now = DateTime.Now;

        var tenMinStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, (now.Minute / 10) * 10, 0);
        var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
        var dayStart = now.Date;

        lock (_lock)
        {
            AddToBucket(TenMinutes, tenMinStart, clicks, keys, MaxTenMinutes);
            AddToBucket(Hours, hourStart, clicks, keys, MaxHours);
            AddToBucket(Days, dayStart, clicks, keys, MaxDays);
        }
    }

    private static void AddToBucket(List<TimeBucket> buckets, DateTime startTime, long clicks, long keys, int maxCount)
    {
        if (buckets.Count > 0 && buckets[^1].StartTime == startTime)
        {
            buckets[^1].Clicks += clicks;
            buckets[^1].Keys += keys;
        }
        else
        {
            buckets.Add(new TimeBucket { StartTime = startTime, Clicks = clicks, Keys = keys });
        }

        while (buckets.Count > maxCount)
        {
            buckets.RemoveAt(0);
        }
    }

    public List<TimeBucket> Get(TimeGranularity granularity)
    {
        lock (_lock)
        {
            return granularity switch
            {
                TimeGranularity.TenMinutes => new List<TimeBucket>(TenMinutes),
                TimeGranularity.Hour => new List<TimeBucket>(Hours),
                TimeGranularity.Day => new List<TimeBucket>(Days),
                _ => new List<TimeBucket>()
            };
        }
    }

    public void Restore(List<TimeBucket>? tenMin, List<TimeBucket>? hours, List<TimeBucket>? days)
    {
        lock (_lock)
        {
            if (tenMin != null) TenMinutes = Trim(tenMin, MaxTenMinutes);
            if (hours != null) Hours = Trim(hours, MaxHours);
            if (days != null) Days = Trim(days, MaxDays);
        }
    }

    private static List<TimeBucket> Trim(List<TimeBucket> list, int max)
    {
        if (list.Count <= max) return list;
        return list.Skip(list.Count - max).ToList();
    }
}
