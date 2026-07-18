using Playnite.API;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;

namespace Playnite.FullscreenApp.Avalonia.Services;

internal sealed class LegacyPluginApi : IPlayniteAPI
{
    private sealed class PilotResourceProvider : IResourceProvider
    {
        public string GetString(string key) => key;
        public object GetResource(string key) => null;
    }

    private sealed class PilotAddonsApi : IAddons
    {
        private readonly Func<Playnite.Plugins.ExtensionFactory> extensions;
        private readonly FullscreenSettings settings;

        public List<string> DisabledAddons => settings.DisabledPlugins.ToList();
        public List<string> Addons => extensions().Plugins.Keys.Select(id => id.ToString()).ToList();
        public List<Plugin> Plugins => extensions().Plugins.Values.Select(plugin => plugin.Plugin).ToList();

        public PilotAddonsApi(Func<Playnite.Plugins.ExtensionFactory> extensions, FullscreenSettings settings)
        {
            this.extensions = extensions;
            this.settings = settings;
        }
    }

    private sealed class PilotUriHandler : IUriHandlerAPI
    {
        private readonly Dictionary<string, Action<PlayniteUriEventArgs>> handlers = new(StringComparer.OrdinalIgnoreCase);
        public void RegisterSource(string source, Action<PlayniteUriEventArgs> handler) => handlers[source] = handler;
        public void RemoveSource(string source) => handlers.Remove(source);
    }

    private readonly Func<Playnite.Controllers.GameActionRunner> actionRunner;
    private readonly Func<IReadOnlyList<Game>> filteredGames;
    private readonly Func<Game> selectedGame;
    private readonly Action<Guid> selectGame;
    private readonly Action<string, bool> showMessage;
    private readonly FullscreenDialogService dialogService;

    public IMainViewAPI MainView { get; }
    public IGameDatabaseAPI Database { get; }
    public IDialogsFactory Dialogs { get; }
    public IPlaynitePathsAPI Paths { get; } = new PlaynitePathsAPI();
    public INotificationsAPI Notifications { get; }
    public IPlayniteInfoAPI ApplicationInfo { get; } = new PlayniteInfoAPI(ApplicationMode.Fullscreen);
    public IWebViewFactory WebViews { get; }
    public IResourceProvider Resources { get; } = new PilotResourceProvider();
    public IUriHandlerAPI UriHandler { get; } = new PilotUriHandler();
    public IPlayniteSettingsAPI ApplicationSettings { get; }
    public IAddons Addons { get; }
    public IEmulationAPI Emulation { get; } = new Emulation();

    public LegacyPluginApi(
        Playnite.Database.GameDatabase database,
        INotificationsAPI notifications,
        FullscreenSettings settings,
        Func<Playnite.Controllers.GameActionRunner> actionRunner,
        Func<Playnite.Plugins.ExtensionFactory> extensions,
        Func<IReadOnlyList<Game>> filteredGames,
        Func<Game> selectedGame,
        Action<Guid> selectGame,
        Action<string, bool> showMessage,
        FullscreenDialogService dialogService)
    {
        this.actionRunner = actionRunner;
        this.filteredGames = filteredGames;
        this.selectedGame = selectedGame;
        this.selectGame = selectGame;
        this.showMessage = showMessage;
        this.dialogService = dialogService;

        Database = new DatabaseAPI(database);
        Notifications = notifications;
        Addons = new PilotAddonsApi(extensions, settings);
        Dialogs = InterfaceProxy.Create<IDialogsFactory>(HandleDialogCall);
        MainView = InterfaceProxy.Create<IMainViewAPI>(HandleMainViewCall);
        ApplicationSettings = InterfaceProxy.Create<IPlayniteSettingsAPI>((method, _) =>
            HandleSettingsCall(method, settings, database.DatabasePath));
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
    public void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args) { }
    public void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args) { }
    public void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args) { }
    public List<GamepadController> GetConnectedControllers() => new();

    private Game GetGame(Guid gameId) => Database.Games[gameId];

    private void Run(Guid gameId, Func<Playnite.Controllers.GameActionRunner, Playnite.Controllers.GameOperationResult> operation)
    {
        if (GetGame(gameId) == null)
        {
            showMessage($"Game {gameId} was not found.", true);
            return;
        }

        var result = operation(actionRunner());
        if (!result.Success)
        {
            showMessage(result.Message, true);
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
                var selected = dialogService.ShowMessage(message, caption, labels, defaultIndex, cancelIndex);
                return customOptions.FirstOrDefault(option => option.Title == selected) ?? customOptions.FirstOrDefault();
            }

            if (method.ReturnType.IsEnum)
            {
                var available = GetMessageBoxResults(args);
                var selected = dialogService.ShowMessage(
                    message,
                    caption,
                    available,
                    0,
                    available.FindIndex(option => option == "Cancel"));
                return Enum.Parse(method.ReturnType, selected);
            }

            dialogService.ShowMessage(message, caption, new[] { "OK" });
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
                return selectedGame() == null ? Array.Empty<Game>() : new[] { selectedGame() };
            case "get_FilteredGames":
                return filteredGames().ToList();
            case "get_ActiveFullscreenView":
                return FullscreenView.List;
            case "get_ActiveDesktopView":
                return DesktopView.Details;
            case "get_SortOrder":
                return SortOrder.Name;
            case "get_SortOrderDirection":
                return SortOrderDirection.Ascending;
            case "get_Grouping":
                return GroupableField.None;
            case "SelectGame":
                selectGame((Guid)args[0]);
                return null;
            case "OpenPluginSettings":
                return false;
            case "GetActiveFilterPreset":
                return Guid.Empty;
            case "GetCurrentFilterSettings":
                return new FilterPresetSettings();
            case "GetSortedFilterPresets":
            case "GetSortedFilterFullscreenPresets":
                return new List<FilterPreset>();
            default:
                return InterfaceProxy.DefaultValue(method.ReturnType);
        }
    }

    private static object HandleSettingsCall(MethodInfo method, FullscreenSettings settings, string databasePath)
    {
        return method.Name switch
        {
            "get_Version" => settings.Version,
            "get_FirstTimeWizardComplete" => true,
            "get_AsyncImageLoading" => true,
            "get_DatabasePath" => databasePath,
            "get_Language" => settings.Language,
            "get_FullscreenTheme" => settings.ThemePath ?? string.Empty,
            "get_GridItemWidthRatio" => 1,
            "get_GridItemHeightRatio" => 1,
            "get_PlaytimeImportMode" => PlaytimeImportMode.Always,
            _ => InterfaceProxy.DefaultValue(method.ReturnType)
        };
    }
}
