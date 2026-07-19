using Avalonia;
using Playnite.Common;
using Playnite.SDK;

namespace Playnite.FullscreenApp.Avalonia;

internal static class Program
{
    internal static StartupOptions Options { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Options = StartupOptions.Parse(args);
        var logDirectory = Options.SelfTest ? AppContext.BaseDirectory : Options.UserDataDirectory;
        Directory.CreateDirectory(logDirectory);
        LogManager.Init(new NLogLogProvider(Path.Combine(logDirectory, "avaloniaFullscreen.log")));
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
