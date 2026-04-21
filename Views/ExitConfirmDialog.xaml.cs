using System.Windows;

namespace InputStats;

public partial class ExitConfirmDialog : Window
{
    public bool MinimizeToTray { get; private set; }
    public bool RememberChoice => RememberCheckBox.IsChecked == true;

    public ExitConfirmDialog()
    {
        InitializeComponent();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTray = true;
        DialogResult = true;
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTray = false;
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
