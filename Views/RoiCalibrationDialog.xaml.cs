using System.Windows;
using System.Windows.Input;

namespace InputStats;

public partial class RoiCalibrationDialog : Window
{
    public double SelectedRoi { get; private set; }
    public bool Skipped { get; private set; }

    public RoiCalibrationDialog(long workClicks, long workKeys, double workCm, int currentCount)
    {
        InitializeComponent();

        CmValue.Text = $"{workCm:F2} cm";
        ClicksValue.Text = workClicks.ToString();
        KeysValue.Text = workKeys.ToString();
        ProgressText.Text = $"校准进度：{currentCount + 1} / 3";

        RoiSlider.ValueChanged += (_, _) =>
        {
            RoiValueText.Text = RoiSlider.Value.ToString("F2");
        };

        // 根据当前统计量给出一个预估的初始值（仅作为参考，用户可调整）
        if (currentCount >= 3)
        {
            DescriptionText.Text = "权重已校准完成。你可以重新评分以更新权重模型。";
            ProgressText.Text = "校准已完成";
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        SelectedRoi = RoiSlider.Value;
        DialogResult = true;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Skipped = true;
        DialogResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
