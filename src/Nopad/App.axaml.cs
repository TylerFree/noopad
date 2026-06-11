using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nopad.Services;
using Nopad.ViewModels;
using Nopad.Views;

namespace Nopad;

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
            var recovery = new RecoveryService();
            var formatting = new FormattingService();
            var syntax = new SyntaxService();
            var markdown = new MarkdownPreviewService();
            var search = new SearchReplaceService();
            var settings = new UserSettingsService();

            var vm = new MainWindowViewModel(recovery, formatting, syntax, markdown, search, settings);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
