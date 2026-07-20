using Playnite.API;
using Playnite.Avalonia.Controls;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;
using LegacyFontFamily = System.Windows.Media.FontFamily;
using LegacySolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Playnite.Avalonia.App.Services;

internal sealed class AvaloniaPluginApi : IPlayniteAPI
{
    private sealed class HostResourceProvider : IResourceProvider
    {
        private static readonly LegacyFontFamily fallbackIconFont = new("Segoe MDL2 Assets");

        public string GetString(string key) => GetAvaloniaResource(key) as string ?? key;

        public object GetResource(string key)
        {
            var resource = GetAvaloniaResource(key);
            if (resource is global::Avalonia.Media.ISolidColorBrush brush)
            {
                var color = brush.Color;
                return new LegacySolidColorBrush(System.Windows.Media.Color.FromArgb(
                    color.A,
                    color.R,
                    color.G,
                    color.B));
            }

            if (resource is global::Avalonia.Media.FontFamily fontFamily)
            {
                return new LegacyFontFamily(fontFamily.Name);
            }

            return resource ?? (string.Equals(key, "FontIcoFont", StringComparison.Ordinal)
                ? fallbackIconFont
                : null);
        }

        private static object GetAvaloniaResource(string key)
        {
            var legacyResource = System.Windows.Application.Current?.TryFindResource(key);
            if (legacyResource != null)
            {
                return legacyResource;
            }

            return GetNativeResource(key);
        }
    }

    internal static IResourceProvider SharedResources { get; } = new HostResourceProvider();

    internal static object GetNativeResource(string key)
    {
        var application = global::Avalonia.Application.Current;
        return application?.TryGetResource(key, null, out var resource) == true
            ? resource
            : null;
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
    public void StartGame(Guid gameId) => Run(gameId, runner => runner.Play(GetGame(gameId)));
    public void InstallGame(Guid gameId) => Run(gameId, runner => runner.Install(GetGame(gameId)));
    public void UninstallGame(Guid gameId) => Run(gameId, runner => runner.Uninstall(GetGame(gameId)));
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
        return AvaloniaSdkDialogRouter.Invoke(callbacks.Dialogs, method, args);
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
            case "SelectGame":
                callbacks.SelectGame((Guid)args[0]);
                return null;
            case "SelectGames":
                callbacks.SelectGame(((IEnumerable<Guid>)args[0]).FirstOrDefault());
                return null;
            case "OpenPluginSettings":
                return callbacks.OpenPluginSettings((Guid)args[0]);
            case "OpenEditDialog":
                IReadOnlyList<Guid> ids = args[0] is Guid id
                    ? new List<Guid> { id }
                    : ((IEnumerable<Guid>)args[0]).ToList();
                return callbacks.OpenEditDialog(ids);
            case "ApplyFilterPreset":
                if (args[0] is Guid filterId)
                {
                    callbacks.ApplyFilterPreset(filterId);
                }
                else if (args[0] is FilterPreset preset)
                {
                    callbacks.ApplyFilterPreset(preset.Id);
                }

                return null;
            case "GetActiveFilterPreset":
                return callbacks.ActiveFilterPreset();
            case "GetCurrentFilterSettings":
                return callbacks.CurrentFilterSettings();
            case "GetSortedFilterPresets":
            case "GetSortedFilterFullscreenPresets":
                return callbacks.FilterPresets();
            case "OpenSearch":
                callbacks.OpenSearch(args.OfType<string>().LastOrDefault() ?? string.Empty);
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
            "get_AsyncImageLoading" => settings.AsyncImageLoading,
            "get_DatabasePath" => databasePath,
            "get_Language" => settings.Language,
            "get_DesktopTheme" => settings.DesktopTheme ?? string.Empty,
            "get_FullscreenTheme" => settings.FullscreenTheme ?? string.Empty,
            "get_GridItemWidthRatio" => 1,
            "get_GridItemHeightRatio" => 1,
            "get_PlaytimeImportMode" => PlaytimeImportMode.Always,
            "get_Fullscreen" => InterfaceProxy.Create<IFullscreenSettingsAPI>((nested, args) =>
                HandleFullscreenSettingsCall(nested, args, settings)),
            "get_CompletionStatus" => InterfaceProxy.Create<ICompletionStatusSettignsApi>((nested, _) =>
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

    private static object SetMusicMuted(IAvaloniaHostSettings settings, bool value)
    {
        settings.IsMusicMuted = value;
        return null;
    }
}
