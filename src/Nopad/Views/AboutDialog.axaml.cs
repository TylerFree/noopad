using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Nopad.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void OkClick(object? sender, RoutedEventArgs e) => Close();
}
