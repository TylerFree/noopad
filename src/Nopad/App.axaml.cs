using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Noopad.Services;
using Noopad.ViewModels;
using Noopad.Views;

namespace Noopad;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new UserSettingsService();
            ApplyTheme(settings.Settings.ThemeVariant);

            var recovery = new RecoveryService();
            var formatting = new FormattingService();
            var syntax = new SyntaxService();
            var markdown = new MarkdownPreviewService();
            var search = new SearchReplaceService();

            var vm = new MainWindowViewModel(recovery, formatting, syntax, markdown, search, settings);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyTheme(string variant)
    {
        if (Current == null) return;
        Current.RequestedThemeVariant = variant switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}