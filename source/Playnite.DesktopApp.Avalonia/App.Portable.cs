using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Playnite.Avalonia.Services;

namespace Playnite.DesktopApp.Avalonia;

public sealed class App : Application
{
    private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();
    private DesktopLibrary library;
    private PortableTrayService trayService;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        AvaloniaCrashHandler.AttachDispatcherHandler();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var options = Program.Options;
            library = new DesktopLibrary(options.UserDataDirectory, options.LibraryPath);
            string startupError = null;
            try
            {
                if (options.SelfTest)
                {
                    library.OpenTemporaryLibrary(1_000);
                }
                else
                {
                    library.OpenExistingLibrary();
                }
            }
            catch (Exception exception)
            {
                startupError = exception.Message;
            }

            var window = new MainWindow(library, startupError, options);
            desktop.MainWindow = window;
            ConfigureCrashHandler(desktop, window, options);
            Program.InstanceCoordinator?.SetCommandHandler(command =>
                Dispatcher.UIThread.Post(() => ProcessCommand(command, window, desktop)));

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "applogo.png");
            trayService = new PortableTrayService(
                iconPath,
                window.RestoreAndActivate,
                () => desktop.Shutdown(),
                !options.SelfTest);
            window.TrayService = trayService;

            if (!string.IsNullOrWhiteSpace(options.UriData))
            {
                window.ProcessUri(options.UriData);
            }

            desktop.Exit += (_, _) =>
            {
                trayService.Dispose();
                library.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureCrashHandler(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window,
        StartupOptions startupOptions)
    {
        AvaloniaCrashHandler.Configure(new AvaloniaCrashHandlerOptions
        {
            CurrentWindow = () => window,
            ExecutablePath = Environment.ProcessPath ?? global::Playnite.CoreRuntime.ApplicationExecutablePath(),
            RestartArguments = startupOptions.GetRestartArguments(),
            LogException = (exception, source) => logger.Error(exception, source),
            SaveLogPackage = global::Playnite.Diagnostic.CreateLogPackage,
            SaveDiagnosticPackage = (path, description) =>
                global::Playnite.Diagnostic.CreateDiagPackage(
                    path,
                    description,
                    new global::Playnite.DiagnosticPackageInfo
                    {
                        IsCrashPackage = true,
                        PlayniteVersion = global::Playnite.CoreRuntime.ApplicationVersion().ToString()
                    }),
            ReportIssue = () => Playnite.Common.ProcessStarter.StartUrl(global::Playnite.UrlConstants.Issues),
            RequestShutdown = () => desktop.Shutdown()
        });
    }

    private static void ProcessCommand(
        CommandExecutedEventArgs command,
        MainWindow window,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        switch (command.Command)
        {
            case CmdlineCommand.Focus:
                window.RestoreAndActivate();
                break;
            case CmdlineCommand.UriRequest:
                window.ProcessUri(command.Args);
                break;
            case CmdlineCommand.Shutdown:
                desktop.Shutdown();
                break;
        }
    }
}
