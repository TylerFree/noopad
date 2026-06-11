using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Noopad.Services;

namespace Noopad.Views;

public partial class SettingsDialog : Window
{
    private readonly IUserSettingsService _settings;

    public SettingsDialog() : this(new UserSettingsService()) { }

    public SettingsDialog(IUserSettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _settings.Settings;

        var themeCombo = this.FindControl<ComboBox>("ThemeCombo")!;
        themeCombo.SelectedIndex = s.ThemeVariant switch { "Light" => 1, "Default" => 2, _ => 0 };

        this.FindControl<CheckBox>("WordWrapCheck")!.IsChecked = s.WordWrap;
        this.FindControl<CheckBox>("LineNumbersCheck")!.IsChecked = s.ShowLineNumbers;
        this.FindControl<TextBox>("FontFamilyBox")!.Text = s.FontFamily;
        this.FindControl<NumericUpDown>("FontSizeBox")!.Value = (decimal)s.FontSize;
    }

    private void ApplyClick(object? sender, RoutedEventArgs e)
    {
        var s = _settings.Settings;

        var themeCombo = this.FindControl<ComboBox>("ThemeCombo")!;
        s.ThemeVariant = (themeCombo.SelectedIndex) switch { 1 => "Light", 2 => "Default", _ => "Dark" };
        s.WordWrap = this.FindControl<CheckBox>("WordWrapCheck")!.IsChecked == true;
        s.ShowLineNumbers = this.FindControl<CheckBox>("LineNumbersCheck")!.IsChecked == true;
        s.FontFamily = this.FindControl<TextBox>("FontFamilyBox")!.Text ?? s.FontFamily;
        s.FontSize = (double)(this.FindControl<NumericUpDown>("FontSizeBox")!.Value ?? (decimal)s.FontSize);

        _settings.Save();
        ApplyTheme(s.ThemeVariant);

        Close(true);
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private static void ApplyTheme(string variant)
    {
        Application.Current!.RequestedThemeVariant = variant switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}