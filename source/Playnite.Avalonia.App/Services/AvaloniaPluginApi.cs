using Playnite.API;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

internal sealed class AvaloniaPluginApi : IPlayniteAPI
{
    private sealed class HostResourceProvider : IResourceProvider
    {
        public string GetString(string key) => key;
        public object GetResource(string key) => null;
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

        public void RegisterSource(string source, Action<PlayniteUriEventArgs> handler) => handlers[source] = handler;
        public void RemoveSource(string source) => handlers.Remove(source);
    }

    private readonly Func<Playnite.Controllers.GameActionRunner> actionRunner;
    private readonly Func<Playnite.Plugins.ExtensionFactory> extensions;
    private readonly AvaloniaHostCallbacks callbacks;

    public IMainViewAPI MainView { get; }
    public IGameDatabaseAPI Database { get; }
    public IDialogsFactory Dialogs { get; }
    public IPlaynitePathsAPI Paths { get; } = new PlaynitePathsAPI();
    public INotificationsAPI Notifications { get; }
    public IPlayniteInfoAPI ApplicationInfo { get; }
    public IWebViewFactory WebViews { get; }
    public IResourceProvider Resources { get; } = new HostResourceProvider();
    public IUriHandlerAPI UriHandler { get; } = new HostUriHandler();
    public IPlayniteSettingsAPI ApplicationSettings { get; }
    public IAddons Addons { get; }
    public IEmulationAPI Emulation { get; } = new Emulation();

    public AvaloniaPluginApi(
        Playnite.Database.GameDatabase database,
        INotificationsAPI notifications,
        Func<Playnite.Controllers.GameActionRunner> actionRunner,
        Func<Playnite.Plugins.ExtensionFactory> extensions,
        AvaloniaHostCallbacks callbacks)
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
        WebViews = InterfaceProxy.Create<IWebViewFactory>((method, _) =>
            throw new NotSupportedException(
                $"Plugin web view call '{method.Name}' requires the cross-platform CEF adapter."));
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
        if (method.Name == "ShowMessage" || method.Name == "ShowErrorMessage")
        {
            var message = args.OfType<string>().FirstOrDefault() ?? method.Name;
            var caption = args.OfType<string>().Skip(1).FirstOrDefault() ??
                (method.Name == "ShowErrorMessage" ? "Error" : "Playnite");
            var customOptions = args.OfType<List<MessageBoxOption>>().FirstOrDefault();
            if (customOptions != null)
            {
                var labels = customOptions.Select(option => option.Title).ToList();
                var defaultIndex = customOptions.FindIndex(option => option.IsDefault);
                var cancelIndex = customOptions.FindIndex(option => option.IsCancel);
                var selected = callbacks.Dialogs.ShowMessage(message, caption, labels, defaultIndex, cancelIndex);
                return customOptions.FirstOrDefault(option => option.Title == selected) ?? customOptions.FirstOrDefault();
            }

            if (method.ReturnType.IsEnum)
            {
                var available = GetMessageBoxResults(args);
                var selected = callbacks.Dialogs.ShowMessage(
                    message,
                    caption,
                    available,
                    0,
                    available.FindIndex(option => option == "Cancel"));
                return Enum.Parse(method.ReturnType, selected);
            }

            callbacks.Dialogs.ShowMessage(message, caption, new[] { "OK" });
            return null;
        }

        return InterfaceProxy.DefaultValue(method.ReturnType);
    }

    private static List<string> GetMessageBoxResults(object[] args)
    {
        var button = args.FirstOrDefault(argument => argument?.GetType().FullName == "System.Windows.MessageBoxButton");
        return button?.ToString() switch
        {
            "OKCancel" => new List<string> { "OK", "Cancel" },
            "YesNo" => new List<string> { "Yes", "No" },
            "YesNoCancel" => new List<string> { "Yes", "No", "Cancel" },
            _ => new List<string> { "OK" }
        };
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
            "get_FirstTimeWizardComplete" => true,
            "get_AsyncImageLoading" => true,
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
