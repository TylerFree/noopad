using Avalonia;
using System;
using Noopad.Services;

namespace Noopad;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var startupArgs = NormalizeStartupArgs(args);
        var singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.TryBecomePrimary())
        {
            var sent = singleInstance.TrySendLaunchRequestAsync(startupArgs).GetAwaiter().GetResult();
            singleInstance.Dispose();
            Environment.ExitCode = sent ? 0 : 1;
            return;
        }

        App.StartupArgs = startupArgs;
        App.SingleInstanceCoordinator = singleInstance;

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            App.SingleInstanceCoordinator = null;
            singleInstance.Dispose();
        }
    }

    private static string[] NormalizeStartupArgs(IEnumerable<string> args)
    {
        return args
            .Where(arg => !string.IsNullOrWhiteSpace(arg))
            .Select(NormalizeStartupPath)
            .ToArray();
    }

    private static string NormalizeStartupPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}