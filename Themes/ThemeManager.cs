using System.Windows;

namespace InputStats;

public static class ThemeManager
{
    private const string ThemeDictUri = "pack://application:,,,/InputStats;component/Themes/{0}Theme.xaml";
    private const string DatePickerThemeUri = "pack://application:,,,/InputStats;component/Themes/DatePickerTheme.xaml";

    private static bool IsPrimaryThemeDictionary(Uri? source)
    {
        if (source == null) return false;
        var s = source.OriginalString.Replace('\\', '/');
        return s.EndsWith("/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)
            || s.EndsWith("/LightTheme.xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatePickerThemeDictionary(Uri? source)
    {
        if (source == null) return false;
        var s = source.OriginalString.Replace('\\', '/');
        return s.EndsWith("/DatePickerTheme.xaml", StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyTheme(AppTheme theme)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        foreach (var d in app.Resources.MergedDictionaries.Where(d => IsPrimaryThemeDictionary(d.Source)).ToList())
            app.Resources.MergedDictionaries.Remove(d);

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

        foreach (var d in app.Resources.MergedDictionaries.Where(d => IsDatePickerThemeDictionary(d.Source)).ToList())
            app.Resources.MergedDictionaries.Remove(d);

        try
        {
            var datePickerDict = new ResourceDictionary { Source = new Uri(DatePickerThemeUri, UriKind.Absolute) };
            app.Resources.MergedDictionaries.Add(datePickerDict);
        }
        catch (Exception ex)
        {
            Logger.Error($"加载 DatePicker 主题失败: {DatePickerThemeUri}", ex);
        }
    }

    public static object? GetResource(string key)
    {
        return System.Windows.Application.Current?.TryFindResource(key);
    }
}
