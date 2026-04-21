using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace InputStats;

public static class StatsStorage
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InputStats",
        "stats.json");

    public static StatsData Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<StatsData>(json) ?? new StatsData();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("加载统计数据失败", ex);
        }
        return new StatsData();
    }

    public static void Save(long clicks, long keys, double cm, ConcurrentDictionary<int, long> keyCounts, int[,] heatMap, int heatMapMax, TimeSeriesCollection history)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);

            int w = heatMap.GetLength(0);
            int h = heatMap.GetLength(1);
            var flat = new int[w * h];
            int idx = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flat[idx++] = heatMap[x, y];

            var data = new StatsData
            {
                Clicks = clicks,
                Keys = keys,
                Cm = cm,
                KeyCounts = keyCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                HeatMap = flat,
                HeatMapW = w,
                HeatMapH = h,
                HeatMapMax = heatMapMax,
                History10Min = history.TenMinutes,
                HistoryHour = history.Hours,
                HistoryDay = history.Days,
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data));
            Logger.Info("统计数据已保存");
        }
        catch (Exception ex)
        {
            Logger.Error("保存统计数据失败", ex);
        }
    }

    public class StatsData
    {
        public long Clicks { get; set; }
        public long Keys { get; set; }
        public double Cm { get; set; }
        public Dictionary<int, long> KeyCounts { get; set; } = new();

        // 热力图数据（压扁的一维数组）
        public int[] HeatMap { get; set; } = Array.Empty<int>();
        public int HeatMapW { get; set; }
        public int HeatMapH { get; set; }
        public int HeatMapMax { get; set; }

        // 时间序列历史数据
        public List<TimeBucket> History10Min { get; set; } = new();
        public List<TimeBucket> HistoryHour { get; set; } = new();
        public List<TimeBucket> HistoryDay { get; set; } = new();
    }
}

public static class AppSettingsStorage
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InputStats",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("加载设置失败", ex);
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings));
            Logger.Info("设置已保存");
        }
        catch (Exception ex)
        {
            Logger.Error("保存设置失败", ex);
        }
    }
}

public class AppSettings
{
    public bool RememberCloseChoice { get; set; }
    public CloseAction CloseAction { get; set; }

    public double MonthlySalary { get; set; } = 23500;
    public double UpdateIntervalSeconds { get; set; } = 1;
    public string WorkStartTime { get; set; } = "09:00";
    public string WorkEndTime { get; set; } = "18:00";
}

public enum CloseAction
{
    Ask,
    MinimizeToTray,
    Exit
}
