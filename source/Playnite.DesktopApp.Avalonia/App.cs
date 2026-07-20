using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

public sealed class App : Application
{
    private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();
    private DesktopLibrary library;
    private AvaloniaRuntimeHost runtimeHost;

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
            if (options.SelfTest)
            {
                try
                {
                    library.OpenTemporaryLibrary(1_000);
                }
                catch (Exception exception)
                {
                    startupError = exception.Message;
                }
            }

            var settingsStore = new DesktopSettingsStore(library.ActiveUserDataDirectory);
            var settings = LoadOrImportSettings(settingsStore, library.ActiveUserDataDirectory, options);
            global::Playnite.Avalonia.Controls.GameCoverImage.AsyncLoadingEnabled = settings.AsyncImageLoading;
            global::Playnite.Common.NLogLogger.IsTraceEnabled = settings.TraceLogEnabled;
            if (options.SelfTest && library.Database != null)
            {
                settings.DatabasePath = library.Database.DatabasePath;
            }
            else
            {
                var resolvedDatabasePath = string.IsNullOrWhiteSpace(settings.DatabasePath)
                    ? options.LibraryPath
                    : Path.GetFullPath(settings.DatabasePath);
                if (!string.Equals(settings.DatabasePath, resolvedDatabasePath, StringComparison.Ordinal))
                {
                    settings.DatabasePath = resolvedDatabasePath;
                    if (!options.PluginCompatibilityTest)
                    {
                        settingsStore.Save(settings);
                    }
                }
                library = new DesktopLibrary(options.UserDataDirectory, resolvedDatabasePath);
            }
            var backupCoordinator = new DesktopBackupCoordinator(
                settings,
                library.ActiveUserDataDirectory,
                settings.DatabasePath,
                () => settingsStore.Save(settings));
            if (!options.SelfTest)
            {
                if (!options.PluginCompatibilityTest && !RunRequestedBackupAction(options, ref startupError))
                {
                    desktop.Shutdown();
                    return;
                }

                if (!options.PluginCompatibilityTest)
                {
                    try
                    {
                        backupCoordinator.RunIfDueAsync(DateTime.Now).GetAwaiter().GetResult();
                    }
                    catch (Exception exception)
                    {
                        startupError = $"Automatic backup failed: {exception.Message}";
                    }
                }

                try
                {
                    library.OpenExistingLibrary();
                }
                catch (Exception exception)
                {
                    startupError = startupError == null
                        ? exception.Message
                        : $"{startupError} Library unavailable: {exception.Message}";
                }
            }
            if (!options.SelfTest && settings.ClearWebCacheOnNextStartup)
            {
                try
                {
                    DesktopWebCacheService.Clear();
                    settings.ClearWebCacheOnNextStartup = false;
                    if (!options.PluginCompatibilityTest)
                    {
                        settingsStore.Save(settings);
                    }
                }
                catch (Exception exception)
                {
                    startupError = startupError == null
                        ? $"Web cache could not be cleared: {exception.Message}"
                        : $"{startupError} Web cache could not be cleared: {exception.Message}";
                }
            }
            string extensionUpdateError = null;
            if (!options.SelfTest && !options.PluginCompatibilityTest)
            {
                try
                {
                    global::Playnite.Plugins.ExtensionInstaller.InstallExtensionQueue();
                }
                catch (Exception exception)
                {
                    extensionUpdateError = $"Queued add-on updates could not be installed: {exception.Message}";
                }
            }

            // Start-in-fullscreen hands off to the Fullscreen shell before any
            // desktop window is shown. The shells use separate single-instance
            // endpoints, so launching Fullscreen and shutting the desktop down is
            // safe; if the Fullscreen executable is not co-located (e.g. a dev
            // build) the handoff is skipped and the desktop opens normally.
            if (!options.SelfTest && !options.PluginCompatibilityTest &&
                settings.StartInFullscreen && TryStartFullscreen(options, settings.DatabasePath))
            {
                desktop.Shutdown();
                return;
            }

            var viewModel = new DesktopAppViewModel(
                library.Games,
                library.Database,
                settings,
                backupCoordinator,
                startupError);
            MainWindow window = null;
            if (library.IsOpen)
            {
                var dialogs = new DesktopDialogService(viewModel, () => window);
                viewModel.AttachDialogs(dialogs);
                var gameEditor = new DesktopGameEditorService(viewModel);
                runtimeHost = new AvaloniaRuntimeHost(library.Database, new AvaloniaHostCallbacks
                {
                    Mode = Playnite.SDK.ApplicationMode.Desktop,
                    Settings = settings,
                    Dialogs = dialogs,
                    CurrentWindow = () => window,
                    FilteredGames = () => viewModel.Games.Select(game => game.Game).ToList(),
                    SelectedGame = () => viewModel.SelectedGame?.Game,
                    SelectGame = viewModel.SelectGame,
                    OpenSearch = viewModel.OpenGlobalSearch,
                    OpenSearchContext = viewModel.PluginSearch.Open,
                    OpenPluginSettings = viewModel.OpenPluginSettings,
                    OpenEditDialog = gameEditor.Show,
                    ActiveDesktopView = () => viewModel.IsDetailsView
                        ? Playnite.SDK.DesktopView.Details
                        : viewModel.IsGridView
                            ? Playnite.SDK.DesktopView.Grid
                            : Playnite.SDK.DesktopView.List,
                    SetActiveDesktopView = value => viewModel.SelectedViewMode = value switch
                    {
                        Playnite.SDK.DesktopView.Grid => "Grid",
                        Playnite.SDK.DesktopView.List => "List",
                        Playnite.SDK.DesktopView.Details => "Details",
                        _ => throw new NotSupportedException($"Unsupported Desktop view {value}.")
                    },
                    SortOrder = () => viewModel.SelectedSortOrder,
                    SortDirection = () => viewModel.SelectedSortDirection,
                    Grouping = () => viewModel.SelectedGrouping,
                    SetSortDirection = value => viewModel.SelectedSortDirection = value,
                    SetGrouping = value => viewModel.SelectedGrouping = value,
                    ApplyFilterPreset = viewModel.ApplyFilterPreset,
                    ActiveFilterPreset = () => viewModel.SelectedFilterPreset?.Id ?? Guid.Empty,
                    CurrentFilterSettings = viewModel.GetCurrentFilterSettings,
                    FilterPresets = () => viewModel.FilterPresets.ToList(),
                    SwitchToLibraryView = viewModel.SwitchToLibraryView,
                    SetStatus = viewModel.SetStatusMessage,
                    SetPluginSummary = viewModel.SetPluginSummary,
                    RefreshGame = viewModel.RefreshGame
                });
                viewModel.AttachRuntime(runtimeHost);
            }

            if (extensionUpdateError != null)
            {
                viewModel.SetStatusMessage(extensionUpdateError);
            }

            window = new MainWindow(
                viewModel,
                library,
                runtimeHost,
                settings,
                options.SelfTest || options.PluginCompatibilityTest ? null : settingsStore,
                options);
            desktop.MainWindow = window;
            // Query the backend once the window has rendered a frame; the GPU
            // context is created lazily on first render, not at framework init.
            window.Opened += (_, _) => Dispatcher.UIThread.Post(
                () => LogRenderingBackend(settings),
                DispatcherPriority.Background);
            ConfigureCrashHandler(
                desktop,
                window,
                runtimeHost,
                settings,
                settingsStore,
                options);
            // Parse the loose theme before third-party assemblies enter the process. A plugin
            // with an incompatible dependency must not interfere with Avalonia's XAML discovery.
            var externalExtensions = (settings.DevelopmentExtensions ?? new List<DevelopmentExtensionPath>())
                .Where(extension => extension?.IsEnabled == true && !string.IsNullOrWhiteSpace(extension.Path))
                .Select(extension => extension.Path)
                .ToList();
            runtimeHost?.InitializePlugins(!options.SelfTest && !options.SafeStartup, externalExtensions);
            Program.InstanceCoordinator?.SetCommandHandler(command =>
                DispatchCommandWhenWindowReady(command, window, desktop, runtimeHost, viewModel));
            if (!string.IsNullOrWhiteSpace(options.UriData) &&
                !ProcessUri(options.UriData, runtimeHost, viewModel))
            {
                viewModel.SetStatusMessage($"No URI handler is registered for '{options.UriData}'.");
            }
            viewModel.RefreshPluginSurfaces();
            if (!options.SelfTest && !options.PluginCompatibilityTest)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (global::Playnite.PlayniteEnvironment.IsElevated && settings.ShowElevatedRightsWarning)
                    {
                        runtimeHost?.ShowMessage(
                            "Playnite is running with elevated rights. Games and extensions will inherit those privileges.",
                            true);
                    }
                    var result = viewModel.Scripts.RunApplicationScript(settings.AppStartupScript, "startup");
                    if (!result.Success)
                    {
                        runtimeHost?.ShowMessage(result.Message, true);
                    }
                }, DispatcherPriority.Background);
            }
            desktop.Exit += (_, _) =>
            {
                if (!options.SelfTest && !options.PluginCompatibilityTest)
                {
                    var result = viewModel.Scripts.RunApplicationScript(settings.AppShutdownScript, "shutdown");
                    if (!result.Success)
                    {
                        logger.Error(result.Message);
                    }
                }
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
        AvaloniaRuntimeHost host,
        DesktopAppViewModel viewModel)
    {
        switch (command.Command)
        {
            case CmdlineCommand.Focus:
                window.RestoreFromTray();
                break;
            case CmdlineCommand.UriRequest:
                window.RestoreFromTray();
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

    private static void DispatchCommandWhenWindowReady(
        CommandExecutedEventArgs command,
        MainWindow window,
        IClassicDesktopStyleApplicationLifetime desktop,
        AvaloniaRuntimeHost host,
        DesktopAppViewModel viewModel)
    {
        Dispatcher.UIThread.Post(() => window.RunForwardedCommandWhenReady(
            () => ProcessCommand(command, window, desktop, host, viewModel)));
    }

    private static bool ProcessUri(
        string uri,
        AvaloniaRuntimeHost host,
        DesktopAppViewModel viewModel)
    {
        try
        {
            var parsed = global::Playnite.PlayniteUriHandler.ParseUri(uri);
            if (string.Equals(parsed.source, "playnite", StringComparison.OrdinalIgnoreCase) &&
                parsed.arguments.Length > 0)
            {
                var command = parsed.arguments[0];
                if ((string.Equals(command, global::Playnite.UriCommands.StartGame, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(command, global::Playnite.UriCommands.ShowGame, StringComparison.OrdinalIgnoreCase)) &&
                    parsed.arguments.Length >= 2 &&
                    Guid.TryParse(parsed.arguments[1], out var gameId) &&
                    viewModel.LibraryGames.Any(game => game.Game.Id == gameId))
                {
                    if (string.Equals(command, global::Playnite.UriCommands.StartGame, StringComparison.OrdinalIgnoreCase))
                    {
                        viewModel.ActivateGame(gameId);
                    }
                    else
                    {
                        viewModel.SelectGame(gameId);
                    }

                    return true;
                }

                if (string.Equals(command, global::Playnite.UriCommands.Search, StringComparison.OrdinalIgnoreCase) &&
                    parsed.arguments.Length >= 2)
                {
                    viewModel.SearchText = parsed.arguments[1];
                    return true;
                }
            }

            return host?.ProcessUri(uri) == true;
        }
        catch (Exception exception)
        {
            viewModel.SetStatusMessage($"Invalid Playnite URI: {exception.Message}");
            return false;
        }
    }

    // Records the render backend Avalonia actually resolved. A registered
    // IPlatformGraphics means a GPU context was created; its absence means the
    // software rasterizer is in use — including a silent fallback when GPU
    // initialization fails despite hardware acceleration being requested.
    private static void LogRenderingBackend(DesktopSettings settings)
    {
        var requested = settings.DisableHwAcceleration
            ? "software (hardware acceleration disabled in settings)"
            : "hardware (auto-detect)";
        try
        {
            // Avalonia 12 keeps its service locator internal, so reach the
            // resolved IPlatformGraphics reflectively. A non-null instance means
            // a GPU context was created; null means the software rasterizer runs.
            const System.Reflection.BindingFlags staticFlags =
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            var resolver = typeof(global::Avalonia.AvaloniaLocator)
                .GetProperty("Current", staticFlags)?.GetValue(null);
            var graphics = resolver?.GetType()
                .GetMethod("GetService", new[] { typeof(Type) })?
                .Invoke(resolver, new object[] { typeof(global::Avalonia.Platform.IPlatformGraphics) });
            var resolved = graphics != null
                ? $"hardware-accelerated ({graphics.GetType().Name})"
                : "software rendering";
            logger.Info($"Avalonia rendering backend: requested={requested}; resolved={resolved}.");
        }
        catch (Exception exception)
        {
            logger.Error(exception, $"Could not determine the Avalonia rendering backend (requested={requested}).");
        }
    }

    private static bool TryStartFullscreen(StartupOptions options, string libraryPath)
    {
        var fullscreenExe = global::Playnite.PlaynitePaths.FullscreenExecutablePath;
        if (string.IsNullOrEmpty(fullscreenExe) || !File.Exists(fullscreenExe))
        {
            return false;
        }

        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.UserDataDirectory))
        {
            arguments.Add($"--userdatadir \"{options.UserDataDirectory}\"");
        }

        if (!string.IsNullOrWhiteSpace(libraryPath))
        {
            arguments.Add($"--library-path \"{libraryPath}\"");
        }

        try
        {
            Playnite.Common.ProcessStarter.StartProcess(fullscreenExe, string.Join(" ", arguments));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ConfigureCrashHandler(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window,
        AvaloniaRuntimeHost host,
        DesktopSettings settings,
        DesktopSettingsStore settingsStore,
        StartupOptions startupOptions)
    {
        AvaloniaCrashHandler.Configure(new AvaloniaCrashHandlerOptions
        {
            CurrentWindow = () => window,
            ExecutablePath = Environment.ProcessPath ?? global::Playnite.CoreRuntime.ApplicationExecutablePath(),
            RestartArguments = startupOptions.GetRestartArguments(),
            LogException = (exception, source) => logger.Error(exception, source),
            AttributeException = exception => AvaloniaPluginCrashAttribution.Resolve(exception, host),
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

    private static DesktopSettings LoadOrImportSettings(
        DesktopSettingsStore settingsStore, string userDataDirectory, StartupOptions options)
    {
        if (options.SelfTest)
        {
            return new DesktopSettings();
        }

        if (options.PluginCompatibilityTest)
        {
            return new DesktopSettings { EnableTray = false, CloseToTray = false };
        }

        if (settingsStore.Exists)
        {
            return settingsStore.Load();
        }

        // First launch against this profile: carry the WPF language and main
        // window placement across so the shell opens where the user left off.
        var settings = new DesktopSettings();
        ImportWpfDefaults(settings, userDataDirectory);
        settingsStore.Save(settings);
        return settings;
    }

    private static void ImportWpfDefaults(DesktopSettings settings, string userDataDirectory)
    {
        var defaults = WpfProfileImport.Read(userDataDirectory);
        if (!string.IsNullOrWhiteSpace(defaults.Language))
        {
            settings.Language = defaults.Language;
        }

        if (defaults.DisableHwAcceleration.HasValue)
        {
            settings.DisableHwAcceleration = defaults.DisableHwAcceleration.Value;
        }
        if (defaults.AsyncImageLoading.HasValue)
        {
            settings.AsyncImageLoading = defaults.AsyncImageLoading.Value;
        }
        if (defaults.ShowImagePerformanceWarning.HasValue)
        {
            settings.ShowImagePerformanceWarning = defaults.ShowImagePerformanceWarning.Value;
        }
        if (defaults.FirstTimeWizardComplete.HasValue)
        {
            settings.FirstTimeWizardComplete = defaults.FirstTimeWizardComplete.Value;
        }

        var placement = defaults.MainWindow;
        if (placement == null)
        {
            return;
        }

        if (placement.Width is > 0)
        {
            settings.WindowWidth = placement.Width.Value;
        }

        if (placement.Height is > 0)
        {
            settings.WindowHeight = placement.Height.Value;
        }

        settings.WindowX = placement.X;
        settings.WindowY = placement.Y;
        settings.WindowMaximized = placement.Maximized;
    }

    private static bool RunRequestedBackupAction(StartupOptions options, ref string startupError)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(options.BackupOptionsPath))
            {
                global::Playnite.Backup.BackupData(options.BackupOptionsPath, CancellationToken.None);
                global::Playnite.Common.FileSystem.DeleteFile(options.BackupOptionsPath);
            }
            else if (!string.IsNullOrWhiteSpace(options.RestoreBackupOptionsPath))
            {
                global::Playnite.Backup.RestoreBackup(options.RestoreBackupOptionsPath);
                global::Playnite.Common.FileSystem.DeleteFile(options.RestoreBackupOptionsPath);
            }

            return true;
        }
        catch (Exception exception)
        {
            startupError = $"Backup operation failed: {exception.Message}";
            return false;
        }
    }
}
