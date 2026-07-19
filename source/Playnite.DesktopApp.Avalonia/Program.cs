using Avalonia;
using Playnite.Common;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia;

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
        LogManager.Init(new NLogLogProvider(Path.Combine(logDirectory, "avaloniaDesktop.log")));
        try
        {
            if (Options.HostLaunchSelfTest)
            {
                if (!OperatingSystem.IsLinux())
                {
                    throw new PlatformNotSupportedException(
                        "The host-launch self-test is only available on Linux.");
                }

                var exitCode = ProcessStarter.StartProcessWait(
                    "/bin/true",
                    string.Empty,
                    Environment.CurrentDirectory,
                    true);
                Console.WriteLine(exitCode == 0
                    ? "Flatpak host process launch passed."
                    : $"Flatpak host process launch failed with exit code {exitCode}.");
                return exitCode;
            }

            if (Options.IntegrationCommand != StartupOptions.LinuxIntegrationCommand.None)
            {
                if (!OperatingSystem.IsLinux())
                {
                    throw new PlatformNotSupportedException(
                        "The desktop-integration command is only available on Linux.");
                }

                ApplyLinuxIntegrationCommand(Options.IntegrationCommand);
                return 0;
            }

            if (!Options.SelfTest && !Options.PluginCompatibilityTest)
            {
                var endpoint = SingleInstanceCoordinator.CreateEndpoint("desktop", Options.UserDataDirectory);
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

            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            InstanceCoordinator?.Dispose();
            InstanceCoordinator = null;
        }
    }

    private static void ApplyLinuxIntegrationCommand(StartupOptions.LinuxIntegrationCommand command)
    {
        switch (command)
        {
            case StartupOptions.LinuxIntegrationCommand.Register:
                SystemIntegration.RegisterPlayniteUriProtocol();
                break;
            case StartupOptions.LinuxIntegrationCommand.EnableAutostart:
                SystemIntegration.SetBootupStateRegistration(true, false);
                break;
            case StartupOptions.LinuxIntegrationCommand.EnableClosedAutostart:
                SystemIntegration.SetBootupStateRegistration(true, true);
                break;
            case StartupOptions.LinuxIntegrationCommand.DisableAutostart:
                SystemIntegration.SetBootupStateRegistration(false, false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }
}
