using Avalonia;
using Playnite.Common;
using Playnite.Avalonia.Services;
using Playnite.SDK;
using System.Text.Json;

namespace Playnite.DesktopApp.Avalonia;

internal static class Program
{
    internal static StartupOptions Options { get; private set; }
    internal static SingleInstanceCoordinator InstanceCoordinator { get; private set; }
    internal static string RuntimeOutputDirectory { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Options = StartupOptions.Parse(args);
        AvaloniaCrashHandler.AttachProcessHandlers();
        RuntimeOutputDirectory = Options.SelfTest
            ? Path.Combine(Path.GetTempPath(), "Playnite", "SelfTests")
            : Options.UserDataDirectory;
        Directory.CreateDirectory(RuntimeOutputDirectory);
        LogManager.Init(new NLogLogProvider(
            Path.Combine(RuntimeOutputDirectory, "avaloniaDesktop.log"),
            replaceExisting: true));
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

            var disableHwAcceleration = Options.SelfTest || Options.PluginCompatibilityTest ||
                ReadDisableHwAcceleration(Options.UserDataDirectory);
            return ConfigureAppBuilder(disableHwAcceleration)
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            InstanceCoordinator?.Dispose();
            InstanceCoordinator = null;
        }
    }

    internal static AppBuilder ConfigureAppBuilder(bool disableHwAcceleration)
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect();
        if (disableHwAcceleration)
        {
            builder = builder
                .With(new Win32PlatformOptions
                {
                    RenderingMode = new[] { Win32RenderingMode.Software }
                })
                .With(new X11PlatformOptions
                {
                    RenderingMode = new[] { X11RenderingMode.Software }
                });
        }

        return builder.LogToTrace();
    }

    internal static bool ReadDisableHwAcceleration(string userDataDirectory)
    {
        foreach (var fileName in new[] { "avaloniaDesktop.json", "config.json" })
        {
            try
            {
                var path = Path.Combine(userDataDirectory, fileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty(
                        "DisableHwAcceleration",
                        out var value) &&
                    value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return value.GetBoolean();
                }

                // Once an Avalonia settings file exists it is authoritative;
                // do not revive an older WPF preference when the value is absent.
                if (fileName == "avaloniaDesktop.json")
                {
                    return false;
                }
            }
            catch (Exception exception) when (
                exception is IOException or JsonException or UnauthorizedAccessException)
            {
                // Startup remains usable with a locked or malformed profile and
                // follows the normal hardware-accelerated default.
                return false;
            }
        }

        return false;
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
