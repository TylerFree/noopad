using Avalonia;
using System;

namespace Noopad;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.StartupArgs = args;
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}