using Avalonia;

namespace Playnite.FullscreenApp.Avalonia;

internal static class Program
{
    internal static StartupOptions Options { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Options = StartupOptions.Parse(args);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
