using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InputStats;

public partial class SettingsDialog : Window
{
    public AppSettings Settings { get; }
    public AppTheme Theme { get; set; }

    public SettingsDialog(AppSettings currentSettings, AppTheme currentTheme)
    {
        InitializeComponent();
        Theme = currentTheme;
        Settings = new AppSettings
        {
            RememberCloseChoice = currentSettings.RememberCloseChoice,
            CloseAction = currentSettings.CloseAction,
            MonthlySalary = currentSettings.MonthlySalary,
            UpdateIntervalSeconds = currentSettings.UpdateIntervalSeconds,
            WorkStartTime = currentSettings.WorkStartTime,
            WorkEndTime = currentSettings.WorkEndTime,
        };

        InitTimeBoxes();
        InitThemeBox();

        MonthlySalaryBox.Text = Settings.MonthlySalary.ToString("F0", CultureInfo.InvariantCulture);
        UpdateIntervalBox.Text = Settings.UpdateIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        SetTimeToBoxes(Settings.WorkStartTime, StartHourBox, StartMinuteBox);
        SetTimeToBoxes(Settings.WorkEndTime, EndHourBox, EndMinuteBox);
        ThemeBox.SelectedIndex = (int)Theme;
    }

    private void InitTimeBoxes()
    {
        for (int h = 0; h < 24; h++)
        {
            string text = h.ToString("D2", CultureInfo.InvariantCulture);
            StartHourBox.Items.Add(text);
            EndHourBox.Items.Add(text);
        }
        for (int m = 0; m < 60; m++)
        {
            string text = m.ToString("D2", CultureInfo.InvariantCulture);
            StartMinuteBox.Items.Add(text);
            EndMinuteBox.Items.Add(text);
        }
    }

    private void InitThemeBox()
    {
        ThemeBox.Items.Add("深色");
        ThemeBox.Items.Add("浅色");
    }

    private static void SetTimeToBoxes(string timeStr, System.Windows.Controls.ComboBox hourBox, System.Windows.Controls.ComboBox minuteBox)
    {
        if (TimeSpan.TryParseExact(timeStr, "hh\\:mm", CultureInfo.InvariantCulture, out var ts))
        {
            hourBox.SelectedIndex = ts.Hours;
            minuteBox.SelectedIndex = ts.Minutes;
        }
        else
        {
            hourBox.SelectedIndex = 0;
            minuteBox.SelectedIndex = 0;
        }
    }

    private static string GetTimeFromBoxes(System.Windows.Controls.ComboBox hourBox, System.Windows.Controls.ComboBox minuteBox)
    {
        string h = hourBox.SelectedIndex >= 0 ? hourBox.SelectedIndex.ToString("D2", CultureInfo.InvariantCulture) : "00";
        string m = minuteBox.SelectedIndex >= 0 ? minuteBox.SelectedIndex.ToString("D2", CultureInfo.InvariantCulture) : "00";
        return $"{h}:{m}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(MonthlySalaryBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var salary) || salary < 0)
        {
            System.Windows.MessageBox.Show("月薪资必须是有效的正数。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!double.TryParse(UpdateIntervalBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var interval) || interval < 0.05)
        {
            System.Windows.MessageBox.Show("更新时间必须大于等于 0.05 秒。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings.MonthlySalary = salary;
        Settings.UpdateIntervalSeconds = interval;
        Settings.WorkStartTime = GetTimeFromBoxes(StartHourBox, StartMinuteBox);
        Settings.WorkEndTime = GetTimeFromBoxes(EndHourBox, EndMinuteBox);
        Theme = (AppTheme)ThemeBox.SelectedIndex;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Logger.GetLogDirectory();
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer", dir) { UseShellExecute = true });
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
