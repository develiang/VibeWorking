using System.Windows;

namespace InputStats;

public static class ThemeManager
{
    private const string ThemeDictUri = "pack://application:,,,/InputStats;component/Themes/{0}Theme.xaml";

    public static void ApplyTheme(AppTheme theme)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        // 移除旧的主题字典
        var oldDicts = app.Resources.MergedDictionaries
            .Where(d => d.Source?.OriginalString.Contains("Theme.xaml") == true)
            .ToList();
        foreach (var d in oldDicts)
            app.Resources.MergedDictionaries.Remove(d);

        // 加载新主题字典
        string themeFile = string.Format(ThemeDictUri, theme);
        try
        {
            var dict = new ResourceDictionary { Source = new Uri(themeFile, UriKind.Absolute) };
            app.Resources.MergedDictionaries.Add(dict);
            Logger.Info($"主题已应用: {theme}");
        }
        catch (Exception ex)
        {
            Logger.Error($"加载主题失败: {themeFile}", ex);
        }
    }

    public static object? GetResource(string key)
    {
        return System.Windows.Application.Current?.TryFindResource(key);
    }
}
