using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace InputStats;

public sealed class SettingsSavedEventArgs : EventArgs
{
    public AppSettings Settings { get; }
    public AppTheme Theme { get; }

    public SettingsSavedEventArgs(AppSettings settings, AppTheme theme)
    {
        Settings = settings;
        Theme = theme;
    }
}

public partial class SettingsPanelView : System.Windows.Controls.UserControl
{
    public AppSettings Settings { get; private set; } = null!;
    public AppTheme Theme { get; set; }

    public event EventHandler<SettingsSavedEventArgs>? Saved;
    public event EventHandler? Cancelled;

    private bool _boxesInitialized;

    public SettingsPanelView()
    {
        InitializeComponent();
    }

    public void ReloadFrom(AppSettings currentSettings, AppTheme currentTheme)
    {
        Theme = currentTheme;
        Settings = new AppSettings
        {
            RememberCloseChoice = currentSettings.RememberCloseChoice,
            CloseAction = currentSettings.CloseAction,
            StartWithWindows = currentSettings.StartWithWindows,
            MonthlySalary = currentSettings.MonthlySalary,
            UpdateIntervalSeconds = currentSettings.UpdateIntervalSeconds,
            WorkStartTime = currentSettings.WorkStartTime,
            WorkEndTime = currentSettings.WorkEndTime,
        };

        if (!_boxesInitialized)
        {
            InitTimeBoxes();
            InitThemeBox();
            InitCloseActionBox();
            _boxesInitialized = true;
        }

        MonthlySalaryBox.Text = Settings.MonthlySalary.ToString("F0", CultureInfo.InvariantCulture);
        UpdateIntervalBox.Text = Settings.UpdateIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        SetTimeToBoxes(Settings.WorkStartTime, StartHourBox, StartMinuteBox);
        SetTimeToBoxes(Settings.WorkEndTime, EndHourBox, EndMinuteBox);
        ThemeBox.SelectedIndex = (int)Theme;
        CloseActionBox.SelectedIndex = Settings.RememberCloseChoice ? (int)Settings.CloseAction : 0;
        StartWithWindowsCheck.IsChecked = Settings.StartWithWindows;
    }

    private void InitTimeBoxes()
    {
        StartHourBox.Items.Clear();
        EndHourBox.Items.Clear();
        StartMinuteBox.Items.Clear();
        EndMinuteBox.Items.Clear();
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
        ThemeBox.Items.Clear();
        ThemeBox.Items.Add("深色");
        ThemeBox.Items.Add("浅色");
    }

    private void InitCloseActionBox()
    {
        CloseActionBox.Items.Clear();
        CloseActionBox.Items.Add("每次询问");
        CloseActionBox.Items.Add("最小化到托盘");
        CloseActionBox.Items.Add("直接退出");
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

        int closeIndex = CloseActionBox.SelectedIndex;
        Settings.RememberCloseChoice = closeIndex != 0;
        Settings.CloseAction = closeIndex == 0 ? CloseAction.Ask :
                               closeIndex == 1 ? CloseAction.MinimizeToTray : CloseAction.Exit;

        Settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        if (!WindowsStartupRegistration.Apply(Settings.StartWithWindows, out var startupErr))
        {
            System.Windows.MessageBox.Show(startupErr ?? "无法更新开机自启动设置。", "开机自启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Saved?.Invoke(this, new SettingsSavedEventArgs(Settings, Theme));
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Logger.GetLogDirectory();
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer", dir) { UseShellExecute = true });
    }
}
