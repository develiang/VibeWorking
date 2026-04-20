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
        catch { }
        return new StatsData();
    }

    public static void Save(long clicks, long keys, double cm)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            var data = new StatsData { Clicks = clicks, Keys = keys, Cm = cm };
            File.WriteAllText(_path, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    public class StatsData
    {
        public long Clicks { get; set; }
        public long Keys { get; set; }
        public double Cm { get; set; }
    }
}
