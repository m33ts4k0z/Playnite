using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia;

public sealed class App : Application
{
    private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();
    private PlayniteLibrary library;
    private FullscreenRuntimeHost runtimeHost;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        AvaloniaCrashHandler.AttachDispatcherHandler();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = Program.Options;
            library = new PlayniteLibrary(options.UserDataDirectory, options.LibraryPath);
            string startupError = null;

            try
            {
                if (options.SelfTest)
                {
                    library.OpenTemporaryPilotLibrary(1_000);
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

            var settingsStore = new FullscreenSettingsStore(library.ActiveUserDataDirectory);
            var settings = LoadOrImportSettings(settingsStore, library.ActiveUserDataDirectory, options);
            var viewModel = new FullscreenAppViewModel(library.Games, settings, startupError);
            MainWindow window = null;
            if (library.IsOpen)
            {
                runtimeHost = new FullscreenRuntimeHost(library, viewModel, settings, () => window);
                viewModel.AttachRuntime(runtimeHost);
            }

            window = new MainWindow(
                viewModel,
                library,
                runtimeHost,
                settings,
                options.SelfTest ? null : settingsStore,
                options);
            viewModel.ExitRequested += (_, _) => window.Close();
            viewModel.ToggleFullscreenRequested += (_, _) =>
                window.WindowState = window.WindowState == global::Avalonia.Controls.WindowState.FullScreen
                    ? global::Avalonia.Controls.WindowState.Normal
                    : global::Avalonia.Controls.WindowState.FullScreen;

            desktop.MainWindow = window;
            ConfigureCrashHandler(
                desktop,
                window,
                runtimeHost,
                settings,
                settingsStore,
                options);
            // Parse the loose theme before third-party assemblies enter the process. A plugin
            // with an incompatible dependency must not interfere with Avalonia's XAML discovery.
            runtimeHost?.InitializePlugins(!options.SelfTest && !options.SafeStartup);
            Program.InstanceCoordinator?.SetCommandHandler(command =>
                Dispatcher.UIThread.Post(() =>
                    ProcessCommand(command, window, desktop, runtimeHost, viewModel)));
            if (!string.IsNullOrWhiteSpace(options.UriData) &&
                !ProcessUri(options.UriData, runtimeHost, viewModel))
            {
                viewModel.SetStatusMessage($"No URI handler is registered for '{options.UriData}'.");
            }
            desktop.Exit += (_, _) =>
            {
                runtimeHost?.Dispose();
                library.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ProcessCommand(
        CommandExecutedEventArgs command,
        MainWindow window,
        IClassicDesktopStyleApplicationLifetime desktop,
        FullscreenRuntimeHost host,
        FullscreenAppViewModel viewModel)
    {
        switch (command.Command)
        {
            case CmdlineCommand.Focus:
                window.Show();
                window.Activate();
                break;
            case CmdlineCommand.UriRequest:
                if (!ProcessUri(command.Args, host, viewModel))
                {
                    viewModel.SetStatusMessage($"No URI handler is registered for '{command.Args}'.");
                }
                break;
            case CmdlineCommand.Shutdown:
                desktop.Shutdown();
                break;
        }
    }

    private static bool ProcessUri(
        string uri,
        FullscreenRuntimeHost host,
        FullscreenAppViewModel viewModel)
    {
        try
        {
            return host?.ProcessUri(uri) == true;
        }
        catch (Exception exception)
        {
            viewModel.SetStatusMessage($"Invalid Playnite URI: {exception.Message}");
            return false;
        }
    }

    private static void ConfigureCrashHandler(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window,
        FullscreenRuntimeHost host,
        FullscreenSettings settings,
        FullscreenSettingsStore settingsStore,
        StartupOptions startupOptions)
    {
        AvaloniaCrashHandler.Configure(new AvaloniaCrashHandlerOptions
        {
            CurrentWindow = () => window,
            ExecutablePath = Environment.ProcessPath ?? global::Playnite.CoreRuntime.ApplicationExecutablePath(),
            RestartArguments = startupOptions.GetRestartArguments(),
            LogException = (exception, source) => logger.Error(exception, source),
            AttributeException = exception => AvaloniaPluginCrashAttribution.Resolve(
                exception,
                host?.Extensions,
                host?.V7Plugins),
            DisablePlugin = pluginId =>
            {
                if (!settings.DisabledPlugins.Contains(pluginId, StringComparer.OrdinalIgnoreCase))
                {
                    settings.DisabledPlugins.Add(pluginId);
                    settingsStore.Save(settings);
                }
            },
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

    private static FullscreenSettings LoadOrImportSettings(
        FullscreenSettingsStore settingsStore, string userDataDirectory, StartupOptions options)
    {
        if (options.SelfTest)
        {
            return new FullscreenSettings();
        }

        if (settingsStore.Exists)
        {
            var existing = settingsStore.Load();
            existing.ThemePath = ThemeCatalog.ResolveFullscreenThemeReference(
                existing.ThemePath,
                FullscreenThemeRoots());
            return existing;
        }

        // First launch against this profile: carry the WPF language and target
        // display across so fullscreen opens on the monitor the user chose.
        var settings = new FullscreenSettings();
        var defaults = WpfProfileImport.Read(userDataDirectory);
        if (!string.IsNullOrWhiteSpace(defaults.Language))
        {
            settings.Language = defaults.Language;
        }

        if (defaults.FullscreenMonitor is int monitor)
        {
            settings.Monitor = monitor;
        }

        settings.InterfaceVolume = defaults.FullscreenInterfaceVolume ?? settings.InterfaceVolume;
        settings.BackgroundVolume = defaults.FullscreenBackgroundVolume ?? settings.BackgroundVolume;
        settings.MuteInBackground = defaults.FullscreenMuteInBackground ?? settings.MuteInBackground;
        settings.UsePrimaryDisplay = defaults.FullscreenUsePrimaryDisplay ?? settings.UsePrimaryDisplay;
        settings.ShowClock = defaults.FullscreenShowClock ?? settings.ShowClock;
        settings.ShowBattery = defaults.FullscreenShowBattery ?? settings.ShowBattery;
        settings.ShowBatteryPercentage = defaults.FullscreenShowBatteryPercentage ?? settings.ShowBatteryPercentage;
        settings.MinimizeAfterGameStartup =
            defaults.FullscreenMinimizeAfterGameStartup ?? settings.MinimizeAfterGameStartup;
        settings.ThemePath = ThemeCatalog.ResolveFullscreenThemeReference(
            defaults.FullscreenTheme,
            FullscreenThemeRoots());
        settings.Rows = defaults.FullscreenRows ?? settings.Rows;
        settings.Columns = defaults.FullscreenColumns ?? settings.Columns;
        settings.HorizontalLayout = defaults.FullscreenHorizontalLayout ?? settings.HorizontalLayout;
        settings.FullscreenItemSpacing = defaults.FullscreenItemSpacing ?? settings.FullscreenItemSpacing;
        settings.SmoothScrolling = defaults.FullscreenSmoothScrolling ?? settings.SmoothScrolling;
        settings.DarkenUninstalledGamesGrid =
            defaults.FullscreenDarkenUninstalledGamesGrid ?? settings.DarkenUninstalledGamesGrid;
        settings.EnableMainBackgroundImage =
            defaults.FullscreenEnableMainBackgroundImage ?? settings.EnableMainBackgroundImage;
        settings.MainBackgroundImageBlurAmount =
            defaults.FullscreenMainBackgroundImageBlurAmount ?? settings.MainBackgroundImageBlurAmount;
        settings.MainBackgroundImageDarkAmount =
            defaults.FullscreenMainBackgroundImageDarkAmount ?? settings.MainBackgroundImageDarkAmount;
        settings.ShowGameTitles = defaults.FullscreenShowGameTitles ?? settings.ShowGameTitles;
        settings.FontSize = defaults.FullscreenFontSize ?? settings.FontSize;
        settings.FontSizeSmall = defaults.FullscreenFontSizeSmall ?? settings.FontSizeSmall;
        if (defaults.FullscreenButtonPrompts is int prompts &&
            Enum.IsDefined(typeof(FullscreenButtonPrompts), prompts))
        {
            settings.ButtonPrompts = (FullscreenButtonPrompts)prompts;
        }
        settings.MainMenuShowRestart = defaults.FullscreenMainMenuShowRestart ?? settings.MainMenuShowRestart;
        settings.MainMenuShowShutdown = defaults.FullscreenMainMenuShowShutdown ?? settings.MainMenuShowShutdown;
        settings.MainMenuShowSuspend = defaults.FullscreenMainMenuShowSuspend ?? settings.MainMenuShowSuspend;
        settings.MainMenuShowHibernate = defaults.FullscreenMainMenuShowHibernate ?? settings.MainMenuShowHibernate;
        settings.MainMenuShowMinimize = defaults.FullscreenMainMenuShowMinimize ?? settings.MainMenuShowMinimize;
        settings.MainMenuShowLogout = defaults.FullscreenMainMenuShowLogout ?? settings.MainMenuShowLogout;
        settings.MainMenuShowLock = defaults.FullscreenMainMenuShowLock ?? settings.MainMenuShowLock;
        settings.MainMenuShowTools = defaults.FullscreenMainMenuShowTools ?? settings.MainMenuShowTools;
        settings.MainMenuShowExtensions = defaults.FullscreenMainMenuShowExtensions ?? settings.MainMenuShowExtensions;
        settings.MainMenuShowClients = defaults.FullscreenMainMenuShowClients ?? settings.MainMenuShowClients;

        settingsStore.Save(settings);
        return settings;
    }

    internal static IReadOnlyList<string> FullscreenThemeRoots() => new[]
    {
        Path.Combine(AppContext.BaseDirectory, "Themes", "Fullscreen"),
        Path.Combine(global::Playnite.PlaynitePaths.ThemesUserDataPath, "Fullscreen")
    };
}
