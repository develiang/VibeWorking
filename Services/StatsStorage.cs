using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace InputStats;

public static class StatsStorage
{
    public static string BaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InputStats");

    public static string GetStatsPath(DateTime date) =>
        Path.Combine(BaseDir, $"stats_{date:yyyyMMdd}.json");

    public static string GetHeatMapPath(DateTime date) =>
        Path.Combine(BaseDir, $"heatmap_{date:yyyyMMdd}.bin");

    public static DailyStats Load(DateTime date)
    {
        try
        {
            var path = GetStatsPath(date);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<DailyStats>(json) ?? new DailyStats();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"加载 {date:yyyyMMdd} 统计数据失败", ex);
        }
        return new DailyStats();
    }

    public static void Save(DateTime date, DailyStats data)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            var path = GetStatsPath(date);
            File.WriteAllText(path, JsonSerializer.Serialize(data));
            Logger.Info($"{date:yyyyMMdd} 统计数据已保存");
        }
        catch (Exception ex)
        {
            Logger.Error($"保存 {date:yyyyMMdd} 统计数据失败", ex);
        }
    }

    public static int[,]? LoadHeatMap(DateTime date)
    {
        try
        {
            var path = GetHeatMapPath(date);
            if (!File.Exists(path)) return null;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);
            int w = reader.ReadInt32();
            int h = reader.ReadInt32();

            if (w != StatsService.HeatMapW || h != StatsService.HeatMapH)
            {
                Logger.Warn($"热力图尺寸不匹配：{path}");
                return null;
            }

            var flat = new int[w * h];
            for (int i = 0; i < flat.Length; i++)
                flat[i] = reader.ReadInt32();

            var map = new int[w, h];
            Buffer.BlockCopy(flat, 0, map, 0, flat.Length * sizeof(int));
            return map;
        }
        catch (Exception ex)
        {
            Logger.Error($"加载 {date:yyyyMMdd} 热力图失败", ex);
            return null;
        }
    }

    public static void SaveHeatMap(DateTime date, int[,] heatMap)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            var path = GetHeatMapPath(date);
            int w = heatMap.GetLength(0);
            int h = heatMap.GetLength(1);
            var flat = new int[w * h];
            Buffer.BlockCopy(heatMap, 0, flat, 0, flat.Length * sizeof(int));

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);
            writer.Write(w);
            writer.Write(h);
            foreach (var v in flat)
                writer.Write(v);

            Logger.Info($"{date:yyyyMMdd} 热力图已保存");
        }
        catch (Exception ex)
        {
            Logger.Error($"保存 {date:yyyyMMdd} 热力图失败", ex);
        }
    }

    public static List<DailyStats> LoadRange(DateTime start, DateTime end)
    {
        var result = new List<DailyStats>();
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            var stats = Load(d);
            result.Add(stats);
        }
        return result;
    }

    public static long GetTotalClicksBefore(DateTime date) => GetTotalsBefore(date).Clicks;
    public static long GetTotalKeysBefore(DateTime date) => GetTotalsBefore(date).Keys;
    public static double GetTotalCmBefore(DateTime date) => GetTotalsBefore(date).Cm;

    /// <summary>
    /// 获取 date（不含）之前所有日期累计。
    /// 优先使用 aggregate.json 缓存增量补差；缓存失效则全量扫描重建。
    /// </summary>
    public static Totals GetTotalsBefore(DateTime exclusiveDate)
    {
        var lastIncluded = exclusiveDate.Date.AddDays(-1);
        var cache = LoadAggregate();

        if (cache != null && cache.ThroughDate >= DateTime.MinValue.AddDays(1) && cache.ThroughDate <= lastIncluded)
        {
            // 增量补加 (cache.ThroughDate, lastIncluded]
            var totals = new Totals { Clicks = cache.Clicks, Keys = cache.Keys, Cm = cache.Cm };
            for (var d = cache.ThroughDate.AddDays(1); d <= lastIncluded; d = d.AddDays(1))
            {
                AddDayInto(totals, d);
            }
            totals.ThroughDate = lastIncluded;
            SaveAggregate(totals);
            return totals;
        }

        // 全量扫描
        return RebuildAggregate(lastIncluded);
    }

    /// <summary>
    /// 切日时调用：把刚结束的一天数据并入累计缓存。
    /// </summary>
    public static void AdvanceAggregate(DateTime justFinishedDate, long clicks, long keys, double cm)
    {
        var cache = LoadAggregate() ?? new Totals { ThroughDate = justFinishedDate.AddDays(-1) };

        // 缓存若已经覆盖到/超过这一天，跳过避免重复加
        if (cache.ThroughDate >= justFinishedDate) return;

        // 缓存若有缺口，先全量重建到 justFinishedDate-1
        if (cache.ThroughDate < justFinishedDate.AddDays(-1))
        {
            cache = RebuildAggregate(justFinishedDate.AddDays(-1));
        }

        cache.Clicks += clicks;
        cache.Keys += keys;
        cache.Cm += cm;
        cache.ThroughDate = justFinishedDate;
        SaveAggregate(cache);
    }

    private static Totals RebuildAggregate(DateTime lastIncluded)
    {
        var totals = new Totals { ThroughDate = lastIncluded };
        try
        {
            if (!Directory.Exists(BaseDir))
            {
                SaveAggregate(totals);
                return totals;
            }
            foreach (var file in Directory.EnumerateFiles(BaseDir, "stats_*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Length != 15) continue;
                if (!DateTime.TryParseExact(fileName.Substring(6), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    continue;
                if (fileDate > lastIncluded) continue;

                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<DailyStats>(json);
                    if (data != null)
                    {
                        totals.Clicks += data.Clicks;
                        totals.Keys += data.Keys;
                        totals.Cm += data.Cm;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"重建累计时跳过损坏文件 {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("重建累计缓存失败", ex);
        }
        SaveAggregate(totals);
        return totals;
    }

    private static void AddDayInto(Totals totals, DateTime date)
    {
        var path = GetStatsPath(date);
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<DailyStats>(json);
            if (data == null) return;
            totals.Clicks += data.Clicks;
            totals.Keys += data.Keys;
            totals.Cm += data.Cm;
        }
        catch (Exception ex)
        {
            Logger.Warn($"增量并入累计时跳过 {path}: {ex.Message}");
        }
    }

    private static string AggregatePath => Path.Combine(BaseDir, "aggregate.json");

    private static Totals? LoadAggregate()
    {
        try
        {
            if (!File.Exists(AggregatePath)) return null;
            var json = File.ReadAllText(AggregatePath);
            return JsonSerializer.Deserialize<Totals>(json);
        }
        catch (Exception ex)
        {
            Logger.Warn($"加载累计缓存失败: {ex.Message}");
            return null;
        }
    }

    private static void SaveAggregate(Totals totals)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            File.WriteAllText(AggregatePath, JsonSerializer.Serialize(totals));
        }
        catch (Exception ex)
        {
            Logger.Warn($"保存累计缓存失败: {ex.Message}");
        }
    }

    public static List<DateTime> GetAvailableDates()
    {
        var dates = new List<DateTime>();
        try
        {
            if (!Directory.Exists(BaseDir)) return dates;
            foreach (var file in Directory.GetFiles(BaseDir, "stats_*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Length != 15) continue;
                if (DateTime.TryParseExact(fileName.Substring(6), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    dates.Add(fileDate);
            }
        }
        catch { }
        dates.Sort();
        return dates;
    }
}

public class DailyStats
{
    /// <summary>当日 0:00–24:00 累计（与历史文件含义一致）。</summary>
    public long Clicks { get; set; }
    public long Keys { get; set; }
    public double Cm { get; set; }

    /// <summary>当日设定工作时间段内的累计（与设置中的上下班时间一致）。</summary>
    public long WorkClicks { get; set; }
    public long WorkKeys { get; set; }
    public double WorkCm { get; set; }

    public Dictionary<int, long> KeyCounts { get; set; } = new();
    public List<TimeBucket> TenMinutes { get; set; } = new();
    public int HeatMapMax { get; set; }
}

public class Totals
{
    public long Clicks { get; set; }
    public long Keys { get; set; }
    public double Cm { get; set; }
    /// <summary>已纳入累计的最后一天（含）。</summary>
    public DateTime ThroughDate { get; set; }
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

    /// <summary>登录 Windows 后是否自动启动本应用。</summary>
    public bool StartWithWindows { get; set; }

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
