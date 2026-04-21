using System.IO;

namespace InputStats;

public enum AppTheme
{
    Dark,
    Light,
}

public static class ThemeStorage
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InputStats",
        "theme.json");

    public static AppTheme Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (Enum.TryParse<AppTheme>(json.Trim('"'), out var theme))
                    return theme;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("加载主题设置失败", ex);
        }
        return AppTheme.Dark;
    }

    public static void Save(AppTheme theme)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_path, $"\"{theme}\"");
            Logger.Info("主题设置已保存");
        }
        catch (Exception ex)
        {
            Logger.Error("保存主题设置失败", ex);
        }
    }
}
