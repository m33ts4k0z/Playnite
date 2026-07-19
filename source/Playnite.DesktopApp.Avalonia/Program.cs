using Avalonia;
using Playnite.Common;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia;

internal static class Program
{
    internal static StartupOptions Options { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Options = StartupOptions.Parse(args);
        var logDirectory = Options.SelfTest ? AppContext.BaseDirectory : Options.UserDataDirectory;
        Directory.CreateDirectory(logDirectory);
        LogManager.Init(new NLogLogProvider(Path.Combine(logDirectory, "avaloniaDesktop.log")));
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
