#if !WINDOWS
using Avalonia.Threading;
using Playnite.API;
using Playnite.Avalonia.Controls;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

/// <summary>
/// Portable SDK v7 implementation of the in-process API used by Core scripts
/// and the native host. SDK v6 remains isolated in the Windows implementation.
/// </summary>
internal sealed class AvaloniaPluginApi : IPlayniteAPI
{
    private sealed class HostResourceProvider : IResourceProvider
    {
        public string GetString(string key) => GetNativeResource(key) as string ?? key;
        public object GetResource(string key) => GetNativeResource(key);
    }

    private sealed class HostAddonsApi : IAddons
    {
        private readonly Func<Playnite.Plugins.ExtensionFactory> extensions;
        private readonly IAvaloniaHostSettings settings;

        public List<string> DisabledAddons => settings.DisabledPlugins.ToList();
        public List<string> Addons => extensions().Plugins.Keys.Select(id => id.ToString()).ToList();
        public List<Plugin> Plugins => extensions().Plugins.Values.Select(plugin => plugin.Plugin).ToList();

        public HostAddonsApi(
            Func<Playnite.Plugins.ExtensionFactory> extensions,
            IAvaloniaHostSettings settings)
        {
            this.extensions = extensions;
            this.settings = settings;
        }
    }

    private sealed class HostUriHandler : IUriHandlerAPI
    {
        private readonly Dictionary<string, Action<PlayniteUriEventArgs>> handlers =
            new(StringComparer.OrdinalIgnoreCase);

        public void RegisterSource(string source, Action<PlayniteUriEventArgs> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source);
            ArgumentNullException.ThrowIfNull(handler);
            if (string.Equals(source, "playnite", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The 'playnite' URI source is reserved.", nameof(source));
            }

            if (!handlers.TryAdd(source, handler))
            {
                throw new InvalidOperationException($"URI source '{source}' is already registered.");
            }
        }

        public void RemoveSource(string source) => handlers.Remove(source);

        public bool Process(string source, string[] arguments)
        {
            if (!handlers.TryGetValue(source, out var handler))
            {
                return false;
            }

            handler(new PlayniteUriEventArgs { Arguments = arguments ?? [] });
            return true;
        }
    }

    internal static IResourceProvider SharedResources { get; } = new HostResourceProvider();

    internal static object GetNativeResource(string key)
    {
        if (PluginResourceRegistry.TryGet(key, out var pluginResource))
        {
            return pluginResource;
        }

        var application = global::Avalonia.Application.Current;
        return application?.TryGetResource(key, null, out var resource) == true
            ? resource
            : null;
    }

    private readonly Func<Playnite.Controllers.GameActionRunner> actionRunner;
    private readonly Func<Playnite.Plugins.ExtensionFactory> extensions;
    private readonly AvaloniaHostCallbacks callbacks;
    private readonly HostUriHandler uriHandler = new();

    public IMainViewAPI MainView { get; }
    public IGameDatabaseAPI Database { get; }
    public IDialogsFactory Dialogs { get; }
    public IPlaynitePathsAPI Paths { get; } = new PlaynitePathsAPI();
    public INotificationsAPI Notifications { get; }
    public IPlayniteInfoAPI ApplicationInfo { get; }
    public IWebViewFactory WebViews { get; }
    public IResourceProvider Resources { get; } = SharedResources;
    public IUriHandlerAPI UriHandler => uriHandler;
    public IPlayniteSettingsAPI ApplicationSettings { get; }
    public IAddons Addons { get; }
    public IEmulationAPI Emulation { get; } = new Emulation();

    public AvaloniaPluginApi(
        Playnite.Database.GameDatabase database,
        INotificationsAPI notifications,
        Func<Playnite.Controllers.GameActionRunner> actionRunner,
        Func<Playnite.Plugins.ExtensionFactory> extensions,
        AvaloniaHostCallbacks callbacks,
        IWebViewFactory webViews)
    {
        this.actionRunner = actionRunner;
        this.extensions = extensions;
        this.callbacks = callbacks;

        Database = new DatabaseAPI(database);
        Notifications = notifications;
        ApplicationInfo = new PlayniteInfoAPI(callbacks.Mode);
        Addons = new HostAddonsApi(extensions, callbacks.Settings);
        Dialogs = InterfaceProxy.Create<IDialogsFactory>(HandleDialogCall);
        MainView = InterfaceProxy.Create<IMainViewAPI>(HandleMainViewCall);
        ApplicationSettings = InterfaceProxy.Create<IPlayniteSettingsAPI>((method, _) =>
            HandleSettingsCall(method, callbacks.Settings, database.DatabasePath));
        WebViews = webViews ?? throw new ArgumentNullException(nameof(webViews));
    }

    public string ExpandGameVariables(Game game, string inputString) => game?.ExpandVariables(inputString);
    public string ExpandGameVariables(Game game, string inputString, string emulatorDir) =>
        game?.ExpandVariables(inputString, emulatorDir: emulatorDir);
    public GameAction ExpandGameVariables(Game game, GameAction action) => action?.ExpandVariables(game);

    public Task StartGameAsync(Guid gameId)
    {
        Run(gameId, runner => runner.Play(GetGame(gameId)));
        return Task.CompletedTask;
    }

    public Task InstallGameAsync(Guid gameId)
    {
        Run(gameId, runner => runner.Install(GetGame(gameId)));
        return Task.CompletedTask;
    }

    public Task UninstallGameAsync(Guid gameId)
    {
        Run(gameId, runner => runner.Uninstall(GetGame(gameId)));
        return Task.CompletedTask;
    }

    public void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args)
    {
        extensions()?.AddCustomElementSupport(source, args);
        callbacks.AddCustomElementSupport(source, args);
        PluginElementRuntime.NotifyRegistrationsChanged();
    }

    public void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args)
    {
        extensions()?.AddSettingsSupport(source, args);
        callbacks.AddSettingsSupport(source, args);
    }

    public void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args)
    {
        extensions()?.AddConvertersSupport(source, args);
        callbacks.AddConvertersSupport(source, args);
    }

    public List<GamepadController> GetConnectedControllers() => callbacks.ConnectedControllers();
    internal bool ProcessUri(string source, string[] arguments) => uriHandler.Process(source, arguments);

    internal static bool SupportsDialogCall(MethodInfo method) => method?.Name is
        "ShowErrorMessageAsync" or
        "ShowMessageAsync" or
        "ShowChoiceAsync" or
        "SelectFileAsync" or
        "SelectFilesAsync" or
        "SelectFolderAsync" or
        "CreateWindow" or
        "GetCurrentAppWindow";

    private Game GetGame(Guid gameId) => Database.Games[gameId];

    private void Run(
        Guid gameId,
        Func<Playnite.Controllers.GameActionRunner, Playnite.Controllers.GameOperationResult> operation)
    {
        if (GetGame(gameId) == null)
        {
            callbacks.SetStatus($"Game {gameId} was not found.");
            return;
        }

        var result = operation(actionRunner());
        if (!result.Success)
        {
            callbacks.SetStatus(result.Message);
        }
    }

    private object HandleDialogCall(MethodInfo method, object[] args)
    {
        switch (method.Name)
        {
            case "ShowErrorMessageAsync":
                callbacks.Dialogs.ShowMessage(
                    (string)args[0],
                    args.ElementAtOrDefault(1) as string ?? "Playnite",
                    new[] { "OK" });
                return Task.CompletedTask;
            case "ShowMessageAsync":
                var buttons = args.ElementAtOrDefault(2) is MessageBoxButton requestedButtons
                    ? requestedButtons
                    : MessageBoxButton.OK;
                var selected = callbacks.Dialogs.ShowMessage(
                    (string)args[0],
                    args.ElementAtOrDefault(1) as string ?? "Playnite",
                    GetDialogOptions(buttons),
                    0,
                    GetCancelIndex(buttons));
                return Task.FromResult(ParseDialogResult(selected));
            case "ShowChoiceAsync":
                return Task.FromResult(callbacks.Dialogs.ShowMessage(
                    (string)args[0],
                    args[1] as string ?? "Playnite",
                    (IReadOnlyList<string>)args[2],
                    (int)args[3],
                    (int)args[4]));
            case "SelectFileAsync":
                return Task.FromResult(callbacks.Dialogs.SelectFiles(
                    args.ElementAtOrDefault(0) as string,
                    false).FirstOrDefault());
            case "SelectFilesAsync":
                return Task.FromResult(callbacks.Dialogs.SelectFiles(
                    args.ElementAtOrDefault(0) as string,
                    true));
            case "SelectFolderAsync":
                return Task.FromResult(callbacks.Dialogs.SelectFolder());
            case "CreateWindow":
                return callbacks.Dialogs.CreateWindow((WindowCreationOptions)args[0]);
            case "GetCurrentAppWindow":
                return callbacks.Dialogs.GetCurrentWindow();
            default:
                return InterfaceProxy.DefaultValue(method.ReturnType);
        }
    }

    private object HandleMainViewCall(MethodInfo method, object[] args)
    {
        switch (method.Name)
        {
            case "get_SelectedGames":
                return callbacks.SelectedGame() == null ? Array.Empty<Game>() : new[] { callbacks.SelectedGame() };
            case "get_FilteredGames":
                return callbacks.FilteredGames().ToList();
            case "get_ActiveFullscreenView":
                return callbacks.ActiveFullscreenView();
            case "get_ActiveDesktopView":
                return callbacks.ActiveDesktopView();
            case "set_ActiveDesktopView":
                callbacks.SetActiveDesktopView?.Invoke((DesktopView)args[0]);
                return null;
            case "get_SortOrder":
                return callbacks.SortOrder();
            case "get_SortOrderDirection":
                return callbacks.SortDirection();
            case "set_SortOrderDirection":
                callbacks.SetSortDirection((SortOrderDirection)args[0]);
                return null;
            case "get_Grouping":
                return callbacks.Grouping();
            case "set_Grouping":
                callbacks.SetGrouping((GroupableField)args[0]);
                return null;
            case "get_UIDispatcher":
                return Dispatcher.UIThread;
            case "SelectGame":
                callbacks.SelectGame((Guid)args[0]);
                return null;
            case "SelectGames":
                var gameIds = ((IEnumerable<Guid>)args[0]).ToList();
                if (callbacks.SelectGames != null)
                {
                    callbacks.SelectGames(gameIds);
                }
                else
                {
                    callbacks.SelectGame(gameIds.FirstOrDefault());
                }
                return null;
            case "OpenPluginSettingsAsync":
                return Task.FromResult(callbacks.OpenPluginSettings((Guid)args[0]));
            case "OpenEditDialogAsync":
                IReadOnlyList<Guid> ids = args[0] is Guid id
                    ? new List<Guid> { id }
                    : ((IEnumerable<Guid>)args[0]).ToList();
                return Task.FromResult(callbacks.OpenEditDialog(ids));
            case "ApplyFilterPreset":
                callbacks.ApplyFilterPreset(args[0] is Guid filterId
                    ? filterId
                    : ((FilterPreset)args[0]).Id);
                return null;
            case "GetActiveFilterPreset":
                return callbacks.ActiveFilterPreset();
            case "GetCurrentFilterSettings":
                return callbacks.CurrentFilterSettings();
            case "GetSortedFilterPresets":
            case "GetSortedFilterFullscreenPresets":
                return callbacks.FilterPresets();
            case "SwitchToLibraryView":
                callbacks.SwitchToLibraryView?.Invoke();
                return null;
            case "ToggleFullscreenView":
                callbacks.ToggleFullscreenView?.Invoke();
                return null;
            case "OpenSearch":
                if (args.Length == 2 && args[0] != null)
                {
                    callbacks.OpenSearchContext(
                        AvaloniaSearchContext.FromSdkV7(args[0]),
                        args[1] as string ?? string.Empty);
                }
                else
                {
                    callbacks.OpenSearch(args.OfType<string>().LastOrDefault() ?? string.Empty);
                }
                return null;
            default:
                return InterfaceProxy.DefaultValue(method.ReturnType);
        }
    }

    private static object HandleSettingsCall(
        MethodInfo method,
        IAvaloniaHostSettings settings,
        string databasePath)
    {
        return method.Name switch
        {
            "get_Version" => settings.Version,
            "get_FirstTimeWizardComplete" => settings.FirstTimeWizardComplete,
            "get_DisableHwAcceleration" => settings.DisableHwAcceleration,
            "get_AsyncImageLoading" => settings.AsyncImageLoading,
            "get_DownloadMetadataOnImport" => settings.DownloadMetadataOnImport,
            "get_StartInFullscreen" => settings.StartInFullscreen,
            "get_DatabasePath" => databasePath,
            "get_MinimizeToTray" => settings.MinimizeToTray,
            "get_CloseToTray" => settings.CloseToTray,
            "get_EnableTray" => settings.EnableTray,
            "get_Language" => settings.Language,
            "get_UpdateLibStartup" => settings.UpdateLibStartup,
            "get_DesktopTheme" => settings.DesktopTheme ?? string.Empty,
            "get_FullscreenTheme" => settings.FullscreenTheme ?? string.Empty,
            "get_StartMinimized" => settings.StartMinimized,
            "get_StartOnBoot" => settings.StartOnBoot,
            "get_GridItemWidthRatio" => settings.GridItemWidthRatio,
            "get_GridItemHeightRatio" => settings.GridItemHeightRatio,
            "get_PlaytimeImportMode" => settings.PlaytimeImportMode,
            "get_FontFamilyName" => settings.FontFamilyName,
            "get_DiscordPresenceEnabled" => settings.DiscordPresenceEnabled,
            "get_AgeRatingOrgPriority" => settings.AgeRatingOrgPriority,
            "get_SidebarVisible" => settings.SidebarVisible,
            "get_SidebarPosition" => settings.SidebarPosition,
            "GetGameExcludedFromImport" => false,
            "get_Fullscreen" => InterfaceProxy.Create<IFullscreenSettingsAPI>((nested, args) =>
                HandleFullscreenSettingsCall(nested, args, settings)),
            "get_CompletionStatus" => InterfaceProxy.Create<ICompletionStatusSettingsAPI>((nested, _) =>
                InterfaceProxy.DefaultValue(nested.ReturnType)),
            _ => InterfaceProxy.DefaultValue(method.ReturnType)
        };
    }

    private static object HandleFullscreenSettingsCall(
        MethodInfo method,
        object[] args,
        IAvaloniaHostSettings settings)
    {
        return method.Name switch
        {
            "get_IsMusicMuted" => settings.IsMusicMuted,
            "set_IsMusicMuted" => SetMusicMuted(settings, (bool)args[0]),
            "get_SwapConfirmCancelButtons" => settings.SwapConfirmCancelButtons,
            "get_SwapStartDetailsAction" => settings.SwapStartDetailsAction,
            "get_GuideButtonFocus" => settings.GuideButtonFocus,
            _ => InterfaceProxy.DefaultValue(method.ReturnType)
        };
    }

    private static IReadOnlyList<string> GetDialogOptions(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OKCancel => new[] { "OK", "Cancel" },
        MessageBoxButton.YesNo => new[] { "Yes", "No" },
        MessageBoxButton.YesNoCancel => new[] { "Yes", "No", "Cancel" },
        _ => new[] { "OK" }
    };

    private static int GetCancelIndex(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OKCancel => 1,
        MessageBoxButton.YesNoCancel => 2,
        _ => -1
    };

    private static MessageBoxResult ParseDialogResult(string selected) => selected switch
    {
        "OK" => MessageBoxResult.OK,
        "Cancel" => MessageBoxResult.Cancel,
        "Yes" => MessageBoxResult.Yes,
        "No" => MessageBoxResult.No,
        _ => MessageBoxResult.None
    };

    private static object SetMusicMuted(IAvaloniaHostSettings settings, bool value)
    {
        settings.IsMusicMuted = value;
        return null;
    }
}
#endif
