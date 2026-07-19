using Avalonia;
using Playnite.Common;
using Playnite.SDK;

namespace Playnite.FullscreenApp.Avalonia;

internal static class Program
{
    internal static StartupOptions Options { get; private set; }
    internal static SingleInstanceCoordinator InstanceCoordinator { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Options = StartupOptions.Parse(args);
        var logDirectory = Options.SelfTest ? AppContext.BaseDirectory : Options.UserDataDirectory;
        Directory.CreateDirectory(logDirectory);
        LogManager.Init(new NLogLogProvider(Path.Combine(logDirectory, "avaloniaFullscreen.log")));
        try
        {
            if (!Options.SelfTest)
            {
                var endpoint = SingleInstanceCoordinator.CreateEndpoint("fullscreen", Options.UserDataDirectory);
                InstanceCoordinator = new SingleInstanceCoordinator(endpoint);
                if (InstanceCoordinator.IsPrimary && Options.Shutdown)
                {
                    return 0;
                }

                if (!InstanceCoordinator.IsPrimary)
                {
                    var command = Options.Shutdown
                        ? CmdlineCommand.Shutdown
                        : string.IsNullOrWhiteSpace(Options.UriData)
                            ? CmdlineCommand.Focus
                            : CmdlineCommand.UriRequest;
                    InstanceCoordinator.SendToPrimary(command, Options.UriData);
                    return 0;
                }
            }

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            InstanceCoordinator?.Dispose();
            InstanceCoordinator = null;
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
