using Avalonia;

namespace Playnite.DesktopApp.Avalonia;

internal static class Program
{
    internal static StartupOptions Options { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Options = StartupOptions.Parse(args);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
