using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Threading;
using Playnite.SDK.Controls;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;

namespace Playnite.SDK.V7.Host;

public static class V7PluginBridge
{
    public static object[] LoadAll(
        string pluginAssemblyPath,
        Func<string, string, string> hostCall,
        Func<string, string, object> hostObjectCall = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAssemblyPath);
        ArgumentNullException.ThrowIfNull(hostCall);

        Serialization.Init(new V7DataSerializer());
        Markup.Init(new HostMarkupConverter(hostCall));
        SQLite.Init((_, _) => throw new NotSupportedException(
            "SDK v7 SQLite bridging is not available in this host milestone."));

        var context = AssemblyLoadContext.GetLoadContext(typeof(V7PluginBridge).Assembly)
            ?? AssemblyLoadContext.Default;
        var fullPluginPath = Path.GetFullPath(pluginAssemblyPath);
        var assembly = context.Assemblies.FirstOrDefault(candidate =>
            string.Equals(candidate.Location, fullPluginPath, StringComparison.OrdinalIgnoreCase))
            ?? context.LoadFromAssemblyPath(fullPluginPath);
        var sdkReference = assembly.GetReferencedAssemblies()
            .FirstOrDefault(reference => string.Equals(
                reference.Name,
                "Playnite.SDK",
                StringComparison.OrdinalIgnoreCase));
        if (sdkReference?.Version?.Major != 7)
        {
            throw new InvalidDataException(
                $"Plugin assembly must reference Playnite.SDK major 7, but references {sdkReference?.Version}." );
        }

        var api = new V7PlayniteApi(hostCall, hostObjectCall);
        API.Instance = api;
        var results = new List<object>();
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(Plugin).IsAssignableFrom(type))
            {
                continue;
            }

            var ignore = type.IsDefined(typeof(IgnorePluginAttribute));
            var load = type.IsDefined(typeof(LoadPluginAttribute));
            if (ignore && !load)
            {
                continue;
            }

            var plugin = (Plugin)Activator.CreateInstance(type, api);
            if (plugin.Id == Guid.Empty)
            {
                plugin.Dispose();
                throw new InvalidDataException($"SDK v7 plugin type {type.FullName} has no plugin ID.");
            }

            results.Add(new V7PluginInstance(plugin, api, hostCall));
        }

        return results.ToArray();
    }

    private sealed class HostMarkupConverter : IMarkupConverter
    {
        private readonly Func<string, string, string> hostCall;

        public HostMarkupConverter(Func<string, string, string> hostCall)
        {
            this.hostCall = hostCall;
        }

        public string MarkdownToHtml(string markdown) => hostCall("MarkdownToHtml", markdown) ?? markdown;
    }
}

public sealed class V7PluginInstance : IDisposable
{
    private sealed class GameEventPayload
    {
        public Game Game { get; set; }
        public GameAction SourceAction { get; set; }
        public string SelectedRomFile { get; set; }
        public int StartedProcessId { get; set; }
        public ulong ElapsedSeconds { get; set; }
        public bool ManuallyStopped { get; set; }
    }

    private readonly Plugin plugin;
    private readonly V7PlayniteApi api;
    private readonly Func<string, string, string> hostCall;
    private ISettings activeSettings;
    private bool applicationStarted;

    public Guid Id => plugin.Id;
    public string TypeName => plugin.GetType().FullName;
    public string Kind => plugin switch
    {
        LibraryPlugin => "LibraryPlugin",
        MetadataPlugin => "MetadataPlugin",
        GenericPlugin => "GenericPlugin",
        _ => "Plugin"
    };
    public string Name => plugin switch
    {
        LibraryPlugin library => library.Name,
        MetadataPlugin metadata => metadata.Name,
        _ => plugin.GetType().Name
    };
    public bool HasSettings => plugin.GetSettings(false) != null;
    public bool CanShutdownLibraryClient =>
        plugin is LibraryPlugin library && library.Properties?.CanShutdownClient == true;
    public bool HasCustomizedGameImport =>
        plugin is LibraryPlugin library && library.Properties?.HasCustomizedGameImport == true;
    public string LibraryIcon => (plugin as LibraryPlugin)?.LibraryIcon;
    public string LibraryBackground => (plugin as LibraryPlugin)?.LibraryBackground;
    public bool HasLibraryClient => (plugin as LibraryPlugin)?.Client != null;
    public bool IsLibraryClientInstalled => (plugin as LibraryPlugin)?.Client?.IsInstalled == true;
    public string LibraryClientIcon => (plugin as LibraryPlugin)?.Client?.Icon;
    public string[] SupportedMetadataFields => plugin is MetadataPlugin metadata
        ? metadata.SupportedFields?.Select(metadataField => metadataField.ToString()).ToArray() ?? []
        : [];

    internal V7PluginInstance(
        Plugin plugin,
        V7PlayniteApi api,
        Func<string, string, string> hostCall)
    {
        this.plugin = plugin;
        this.api = api;
        this.hostCall = hostCall;
    }

    public void InvokeApplicationStarted()
    {
        if (applicationStarted)
        {
            return;
        }

        plugin.OnApplicationStarted(new OnApplicationStartedEventArgs());
        applicationStarted = true;
    }

    public void InvokeApplicationStopped()
    {
        if (!applicationStarted)
        {
            return;
        }

        plugin.OnApplicationStopped(new OnApplicationStoppedEventArgs());
        applicationStarted = false;
    }

    public object GetSettings() => plugin.GetSettings(false);
    public Control GetSettingsView() => plugin.GetSettingsView(false);

    public Control BeginSettingsEdit()
    {
        if (activeSettings != null)
        {
            throw new InvalidOperationException($"Settings for plugin {Name} are already being edited.");
        }

        var settings = plugin.GetSettings(false);
        var view = plugin.GetSettingsView(false);
        if (settings == null || view == null)
        {
            return null;
        }

        settings.BeginEdit();
        activeSettings = settings;
        return view;
    }

    public string VerifySettings()
    {
        if (activeSettings == null)
        {
            throw new InvalidOperationException($"Settings for plugin {Name} are not being edited.");
        }

        var valid = activeSettings.VerifySettings(out var errors);
        return V7RpcJson.Serialize(new
        {
            Valid = valid,
            Errors = errors ?? []
        });
    }

    public void EndSettingsEdit()
    {
        if (activeSettings == null)
        {
            throw new InvalidOperationException($"Settings for plugin {Name} are not being edited.");
        }

        var settings = activeSettings;
        activeSettings = null;
        settings.EndEdit();
    }

    public void CancelSettingsEdit()
    {
        if (activeSettings == null)
        {
            return;
        }

        var settings = activeSettings;
        activeSettings = null;
        settings.CancelEdit();
    }

    public string GetLibraryGames(CancellationToken cancellationToken)
    {
        if (plugin is not LibraryPlugin library)
        {
            return "[]";
        }

        var games = library.GetGames(new LibraryGetGamesArgs
        {
            CancelToken = cancellationToken
        })?.ToList() ?? [];
        return V7RpcJson.Serialize(games);
    }

    public string ImportLibraryGames(CancellationToken cancellationToken)
    {
        if (plugin is not LibraryPlugin library)
        {
            return "[]";
        }

        var games = library.ImportGames(new LibraryImportGamesArgs
        {
            CancelToken = cancellationToken
        })?.ToList() ?? [];
        return V7RpcJson.Serialize(games);
    }

    public void InvokeLibraryUpdated() =>
        plugin.OnLibraryUpdated(new OnLibraryUpdatedEventArgs());

    public void OpenLibraryClient() => (plugin as LibraryPlugin)?.Client?.Open();
    public void ShutdownLibraryClient() => (plugin as LibraryPlugin)?.Client?.Shutdown();

    public object CreateMetadataProvider(string gameJson, bool backgroundDownload)
    {
        if (plugin is not MetadataPlugin metadata)
        {
            return null;
        }

        var game = V7RpcJson.Deserialize<Game>(gameJson);
        var provider = metadata.GetMetadataProvider(new MetadataRequestOptions(game, backgroundDownload));
        return provider == null ? null : new V7MetadataProviderInstance(provider);
    }

    public object CreateLibraryMetadataProvider()
    {
        var provider = (plugin as LibraryPlugin)?.GetMetadataDownloader();
        return provider == null ? null : new V7LibraryMetadataProviderInstance(provider);
    }

    public object[] GetMenuItems(string kind, string gamesJson, bool globalSearchRequest)
    {
        if (string.Equals(kind, "Main", StringComparison.Ordinal))
        {
            return (plugin.GetMainMenuItems(new GetMainMenuItemsArgs
            {
                IsGlobalSearchRequest = globalSearchRequest
            }) ?? [])
                .Select(item => (object)new V7MenuItemInstance(item))
                .ToArray();
        }

        if (string.Equals(kind, "Game", StringComparison.Ordinal))
        {
            var games = V7RpcJson.Deserialize<List<Game>>(gamesJson) ?? [];
            return (plugin.GetGameMenuItems(new GetGameMenuItemsArgs
            {
                Games = games,
                IsGlobalSearchRequest = globalSearchRequest
            }) ?? [])
                .Select(item => (object)new V7MenuItemInstance(item, games))
                .ToArray();
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown plugin menu kind.");
    }

    public object CreateGameViewControl(string name, string mode, string gameJson)
    {
        var control = plugin.GetGameViewControl(new GetGameViewControlArgs
        {
            Name = name,
            Mode = Enum.Parse<ApplicationMode>(mode)
        });
        if (control == null)
        {
            return null;
        }

        var instance = new V7PluginElementInstance(control);
        instance.SetGameContext(gameJson);
        return instance;
    }

    public object GetConverter(string sourceName, string converterName) =>
        api.GetConverter(plugin.Id, sourceName, converterName);

    public object[] GetSidebarItems() => (plugin.GetSidebarItems() ?? [])
        .Select(item => (object)new V7SidebarItemInstance(item))
        .ToArray();

    public object[] GetTopPanelItems() => (plugin.GetTopPanelItems() ?? [])
        .Select(item => (object)new V7TopPanelItemInstance(item))
        .ToArray();

    public void PublishDatabaseEvent(string collection, string eventName, string payload) =>
        api.HostDatabase.Publish(collection, eventName, payload);

    public object[] GetControllers(string kind, string gameJson)
    {
        var game = V7RpcJson.Deserialize<Game>(gameJson);
        IEnumerable<ControllerBase> controllers = kind switch
        {
            "Play" => plugin.GetPlayActions(new GetPlayActionsArgs { Game = game }) ?? [],
            "Install" => plugin.GetInstallActions(new GetInstallActionsArgs { Game = game }) ?? [],
            "Uninstall" => plugin.GetUninstallActions(new GetUninstallActionsArgs { Game = game }) ?? [],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown controller kind.")
        };
        return controllers
            .Select(controller => (object)new V7ControllerInstance(controller, hostCall))
            .ToArray();
    }

    public string InvokeGameEvent(string eventName, string payload)
    {
        var data = V7RpcJson.Deserialize<GameEventPayload>(payload) ?? new GameEventPayload();
        switch (eventName)
        {
            case "Starting":
                var starting = new OnGameStartingEventArgs
                {
                    Game = data.Game,
                    SourceAction = data.SourceAction,
                    SelectedRomFile = data.SelectedRomFile
                };
                plugin.OnGameStarting(starting);
                return V7RpcJson.Serialize(new { starting.CancelStartup });
            case "Started":
                plugin.OnGameStarted(new OnGameStartedEventArgs
                {
                    Game = data.Game,
                    SourceAction = data.SourceAction,
                    SelectedRomFile = data.SelectedRomFile,
                    StartedProcessId = data.StartedProcessId
                });
                break;
            case "StartupCancelled":
                plugin.OnGameStartupCancelled(new OnGameStartupCancelledEventArgs { Game = data.Game });
                break;
            case "Stopped":
                plugin.OnGameStopped(new OnGameStoppedEventArgs
                {
                    Game = data.Game,
                    ElapsedSeconds = data.ElapsedSeconds,
                    ManuallyStopped = data.ManuallyStopped
                });
                break;
            case "Installed":
                plugin.OnGameInstalled(new OnGameInstalledEventArgs { Game = data.Game });
                break;
            case "InstallCancelled":
                plugin.OnGameInstallationCancelled(
                    new OnGameInstallationCancelledEventArgs { Game = data.Game });
                break;
            case "Uninstalled":
                plugin.OnGameUninstalled(new OnGameUninstalledEventArgs { Game = data.Game });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventName), eventName, "Unknown game event.");
        }
        return "null";
    }

    public void Dispose()
    {
        CancelSettingsEdit();
        InvokeApplicationStopped();
        plugin.Dispose();
    }
}

public sealed class V7MenuItemInstance
{
    private readonly MainMenuItem mainItem;
    private readonly GameMenuItem gameItem;
    private readonly List<Game> games;

    public string Description => mainItem?.Description ?? gameItem?.Description;
    public string MenuSection => mainItem?.MenuSection ?? gameItem?.MenuSection;
    public string Icon => mainItem?.Icon ?? gameItem?.Icon;

    public V7MenuItemInstance(MainMenuItem item) =>
        mainItem = item ?? throw new ArgumentNullException(nameof(item));

    public V7MenuItemInstance(GameMenuItem item, List<Game> games)
    {
        gameItem = item ?? throw new ArgumentNullException(nameof(item));
        this.games = games ?? [];
    }

    public void Invoke()
    {
        if (mainItem != null)
        {
            mainItem.Action?.Invoke(new MainMenuItemActionArgs { SourceItem = mainItem });
        }
        else
        {
            gameItem.Action?.Invoke(new GameMenuItemActionArgs
            {
                Games = games,
                SourceItem = gameItem
            });
        }
    }
}

public sealed class V7PluginElementInstance
{
    public Control Control { get; }

    public V7PluginElementInstance(Control control) =>
        Control = control ?? throw new ArgumentNullException(nameof(control));

    public void SetGameContext(string gameJson)
    {
        var game = V7RpcJson.Deserialize<Game>(gameJson);
        if (Control is PluginUserControl pluginControl)
        {
            pluginControl.GameContext = game;
        }
        else
        {
            Control.DataContext = game;
        }
    }
}

public sealed class V7SidebarItemInstance : INotifyPropertyChanged
{
    private readonly SidebarItem item;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Type => item.Type.ToString();
    public object Icon => item.Icon;
    public string Title => item.Title;
    public bool Visible => item.Visible;
    public double ProgressValue => item.ProgressValue;
    public double ProgressMaximum => item.ProgressMaximum;
    public Avalonia.Thickness IconPadding => item.IconPadding;

    public V7SidebarItemInstance(SidebarItem item)
    {
        this.item = item ?? throw new ArgumentNullException(nameof(item));
        item.PropertyChanged += (_, args) => PropertyChanged?.Invoke(this, args);
    }

    public void Activate() => item.Activated?.Invoke();
    public Control Open() => item.Opened?.Invoke();
    public void Close() => item.Closed?.Invoke();
}

public sealed class V7TopPanelItemInstance : INotifyPropertyChanged
{
    private readonly TopPanelItem item;

    public event PropertyChangedEventHandler PropertyChanged;
    public object Icon => item.Icon;
    public string Title => item.Title;
    public bool Visible => item.Visible;

    public V7TopPanelItemInstance(TopPanelItem item)
    {
        this.item = item ?? throw new ArgumentNullException(nameof(item));
        item.PropertyChanged += (_, args) => PropertyChanged?.Invoke(this, args);
    }

    public void Activate() => item.Activated?.Invoke();
}

public sealed class V7MetadataProviderInstance : IDisposable
{
    private readonly OnDemandMetadataProvider provider;

    public string[] AvailableFields =>
        provider.AvailableFields?.Select(metadataField => metadataField.ToString()).ToArray() ?? [];

    public V7MetadataProviderInstance(OnDemandMetadataProvider provider) =>
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public string GetField(string fieldName, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MetadataField>(fieldName, out var field))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown metadata field.");
        }

        var args = new GetMetadataFieldArgs { CancelToken = cancellationToken };
        object value = field switch
        {
            MetadataField.Name => provider.GetName(args),
            MetadataField.Genres => provider.GetGenres(args)?.ToList(),
            MetadataField.ReleaseDate => provider.GetReleaseDate(args),
            MetadataField.Developers => provider.GetDevelopers(args)?.ToList(),
            MetadataField.Publishers => provider.GetPublishers(args)?.ToList(),
            MetadataField.Tags => provider.GetTags(args)?.ToList(),
            MetadataField.Description => provider.GetDescription(args),
            MetadataField.Links => provider.GetLinks(args)?.ToList(),
            MetadataField.CriticScore => provider.GetCriticScore(args),
            MetadataField.CommunityScore => provider.GetCommunityScore(args),
            MetadataField.Icon => provider.GetIcon(args),
            MetadataField.CoverImage => provider.GetCoverImage(args),
            MetadataField.BackgroundImage => provider.GetBackgroundImage(args),
            MetadataField.Features => provider.GetFeatures(args)?.ToList(),
            MetadataField.AgeRating => provider.GetAgeRatings(args)?.ToList(),
            MetadataField.Series => provider.GetSeries(args)?.ToList(),
            MetadataField.Region => provider.GetRegions(args)?.ToList(),
            MetadataField.Platform => provider.GetPlatforms(args)?.ToList(),
            MetadataField.InstallSize => provider.GetInstallSize(args),
            _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown metadata field.")
        };
        return V7RpcJson.Serialize(value);
    }

    public void Dispose() => provider.Dispose();
}

public sealed class V7LibraryMetadataProviderInstance : IDisposable
{
    private readonly LibraryMetadataProvider provider;

    public V7LibraryMetadataProviderInstance(LibraryMetadataProvider provider) =>
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public string GetMetadata(string gameJson) =>
        V7RpcJson.Serialize(provider.GetMetadata(V7RpcJson.Deserialize<Game>(gameJson)));

    public void Dispose() => provider.Dispose();
}

internal sealed class V7PlayniteApi : IPlayniteAPI
{
    private sealed record ConverterRegistration(
        Guid PluginId,
        string SourceName,
        IReadOnlyList<IValueConverter> Converters);

    private readonly Func<string, string, string> hostCall;
    private readonly List<ConverterRegistration> converterRegistrations = [];

    public IMainViewAPI MainView { get; }
    public IGameDatabaseAPI Database { get; }
    public IDialogsFactory Dialogs { get; }
    public IPlaynitePathsAPI Paths { get; }
    public INotificationsAPI Notifications { get; }
    public IPlayniteInfoAPI ApplicationInfo { get; }
    public IWebViewFactory WebViews { get; }
    public IResourceProvider Resources { get; }
    public IUriHandlerAPI UriHandler { get; }
    public IPlayniteSettingsAPI ApplicationSettings { get; }
    public IAddons Addons { get; }
    public IEmulationAPI Emulation { get; }
    internal HostGameDatabase HostDatabase { get; }

    public V7PlayniteApi(
        Func<string, string, string> hostCall,
        Func<string, string, object> hostObjectCall = null)
    {
        this.hostCall = hostCall;
        Paths = new HostPaths(hostCall);
        ApplicationInfo = new HostApplicationInfo(hostCall);
        Resources = new HostResources(hostCall);
        Notifications = new HostNotifications(hostCall);
        Dialogs = new HostDialogs(hostCall);
        MainView = new HostMainView(hostCall);
        HostDatabase = new HostGameDatabase(hostCall);
        Database = HostDatabase;
        ApplicationSettings = new HostApplicationSettings(hostCall);
        WebViews = new HostWebViewFactory(hostObjectCall);
        UriHandler = new UnsupportedUriHandler();
        Addons = new HostAddons(hostCall);
        Emulation = new UnsupportedEmulationApi();
    }

    public string ExpandGameVariables(Game game, string inputString) =>
        ExpandGameVariables(game, inputString, null);

    public string ExpandGameVariables(Game game, string inputString, string emulatorDir) =>
        hostCall("ExpandGameVariables", V7RpcJson.Serialize(new
        {
            Game = game,
            Input = inputString,
            EmulatorDirectory = emulatorDir
        }));

    public GameAction ExpandGameVariables(Game game, GameAction action) =>
        V7RpcJson.Deserialize<GameAction>(hostCall(
            "ExpandGameActionVariables",
            V7RpcJson.Serialize(new { Game = game, Action = action })));
    public Task StartGameAsync(Guid gameId) => CallGameOperation("StartGame", gameId);
    public Task InstallGameAsync(Guid gameId) => CallGameOperation("InstallGame", gameId);
    public Task UninstallGameAsync(Guid gameId) => CallGameOperation("UninstallGame", gameId);

    public void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args) =>
        hostCall("AddCustomElementSupport", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            PluginId = source.Id,
            args.SourceName,
            args.ElementList
        }));

    public void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args) =>
        hostCall("AddSettingsSupport", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            PluginId = source.Id,
            args.SourceName,
            args.SettingsRoot
        }));

    public void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args)
    {
        converterRegistrations.RemoveAll(item => item.PluginId == source.Id);
        converterRegistrations.Add(new ConverterRegistration(
            source.Id,
            args.SourceName,
            args.Converters?.ToList() ?? []));
        hostCall("AddConvertersSupport", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            PluginId = source.Id,
            args.SourceName,
            ConverterNames = args.Converters?.Select(converter => converter.GetType().Name).ToList()
        }));
    }

    internal IValueConverter GetConverter(Guid pluginId, string sourceName, string converterName)
    {
        var registration = converterRegistrations.FirstOrDefault(item =>
            item.PluginId == pluginId &&
            string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        return registration?.Converters.FirstOrDefault(converter =>
            string.Equals(converter.GetType().Name, converterName, StringComparison.Ordinal));
    }

    public List<GamepadController> GetConnectedControllers() =>
        V7RpcJson.Deserialize<List<GamepadController>>(
            hostCall("ConnectedControllers", string.Empty)) ?? [];

    private Task CallGameOperation(string operation, Guid gameId)
    {
        hostCall(operation, gameId.ToString());
        return Task.CompletedTask;
    }
}

internal sealed class HostMainView : IMainViewAPI
{
    private readonly Func<string, string, string> hostCall;

    public DesktopView ActiveDesktopView
    {
        get => ReadEnum<DesktopView>("MainView.ActiveDesktopView");
        set => hostCall("MainView.SetActiveDesktopView", value.ToString());
    }

    public FullscreenView ActiveFullscreenView => ReadEnum<FullscreenView>("MainView.ActiveFullscreenView");
    public SortOrder SortOrder => ReadEnum<SortOrder>("MainView.SortOrder");

    public SortOrderDirection SortOrderDirection
    {
        get => ReadEnum<SortOrderDirection>("MainView.SortOrderDirection");
        set => hostCall("MainView.SetSortOrderDirection", value.ToString());
    }

    public GroupableField Grouping
    {
        get => ReadEnum<GroupableField>("MainView.Grouping");
        set => hostCall("MainView.SetGrouping", value.ToString());
    }

    public Dispatcher UIDispatcher => Dispatcher.UIThread;
    public IEnumerable<Game> SelectedGames => Read<List<Game>>("MainView.SelectedGames") ?? [];
    public List<Game> FilteredGames => Read<List<Game>>("MainView.FilteredGames") ?? [];

    public HostMainView(Func<string, string, string> hostCall) =>
        this.hostCall = hostCall ?? throw new ArgumentNullException(nameof(hostCall));

    public Task<bool> OpenPluginSettingsAsync(Guid pluginId) => Task.FromResult(
        bool.TryParse(hostCall("OpenPluginSettings", pluginId.ToString()), out var opened) && opened);

    public void SwitchToLibraryView() => hostCall("MainView.SwitchToLibraryView", string.Empty);
    public void SelectGame(Guid gameId) => hostCall("MainView.SelectGame", gameId.ToString());
    public void SelectGames(IEnumerable<Guid> gameIds) =>
        hostCall("MainView.SelectGames", V7RpcJson.Serialize(gameIds?.ToList() ?? []));
    public void ApplyFilterPreset(Guid filterId) =>
        hostCall("MainView.ApplyFilterPreset", filterId.ToString());
    public void ApplyFilterPreset(FilterPreset preset) =>
        ApplyFilterPreset((preset ?? throw new ArgumentNullException(nameof(preset))).Id);
    public Guid GetActiveFilterPreset()
    {
        var value = hostCall("MainView.ActiveFilterPreset", string.Empty);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                $"Avalonia host returned invalid active filter preset GUID '{value}'.");
    }
    public FilterPresetSettings GetCurrentFilterSettings() =>
        Read<FilterPresetSettings>("MainView.CurrentFilterSettings");
    public void OpenSearch(string searchTerm) =>
        hostCall("MainView.OpenSearch", searchTerm ?? string.Empty);

    public void OpenSearch(SearchContext context, string searchTerm)
    {
        if (context != null)
        {
            throw new NotSupportedException(
                "SDK v7 custom search contexts cannot cross the isolated Avalonia plugin boundary yet.");
        }

        OpenSearch(searchTerm);
    }

    public Task<bool?> OpenEditDialogAsync(Guid gameId) => OpenEditDialogAsync([gameId]);

    public Task<bool?> OpenEditDialogAsync(IReadOnlyList<Guid> gameIds) => Task.FromResult(
        V7RpcJson.Deserialize<bool?>(hostCall(
            "MainView.OpenEditDialog",
            V7RpcJson.Serialize(gameIds ?? []))));

    public List<FilterPreset> GetSortedFilterPresets() =>
        Read<List<FilterPreset>>("MainView.FilterPresets") ?? [];
    public List<FilterPreset> GetSortedFilterFullscreenPresets() =>
        Read<List<FilterPreset>>("MainView.FullscreenFilterPresets") ?? [];
    public void ToggleFullscreenView() => hostCall("MainView.ToggleFullscreenView", string.Empty);

    private T Read<T>(string operation) =>
        V7RpcJson.Deserialize<T>(hostCall(operation, string.Empty));

    private TEnum ReadEnum<TEnum>(string operation) where TEnum : struct, Enum
    {
        var value = hostCall(operation, string.Empty);
        return Enum.TryParse<TEnum>(value, true, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                $"Avalonia host returned invalid {typeof(TEnum).Name} value '{value}' for {operation}.");
    }
}

internal sealed class HostApplicationSettings : IPlayniteSettingsAPI
{
    private readonly Func<string, string, string> hostCall;

    public int Version => ReadInteger("Settings.Version");
    public int GridItemWidthRatio => ReadInteger("Settings.GridItemWidthRatio");
    public int GridItemHeightRatio => ReadInteger("Settings.GridItemHeightRatio");
    public bool FirstTimeWizardComplete => ReadBoolean("Settings.FirstTimeWizardComplete");
    public bool DisableHwAcceleration => ReadBoolean("Settings.DisableHwAcceleration");
    public bool AsyncImageLoading => ReadBoolean("Settings.AsyncImageLoading");
    public bool DownloadMetadataOnImport => ReadBoolean("Settings.DownloadMetadataOnImport");
    public bool StartInFullscreen => ReadBoolean("Settings.StartInFullscreen");
    public string DatabasePath => hostCall("DatabasePath", string.Empty);
    public bool MinimizeToTray => ReadBoolean("Settings.MinimizeToTray");
    public bool CloseToTray => ReadBoolean("Settings.CloseToTray");
    public bool EnableTray => ReadBoolean("Settings.EnableTray");
    public string Language => hostCall("Language", string.Empty);
    public bool UpdateLibStartup => ReadBoolean("Settings.UpdateLibStartup");
    public string DesktopTheme => hostCall("Settings.DesktopTheme", string.Empty);
    public string FullscreenTheme => hostCall("Settings.FullscreenTheme", string.Empty);
    public bool StartMinimized => ReadBoolean("Settings.StartMinimized");
    public bool StartOnBoot => ReadBoolean("Settings.StartOnBoot");
    public PlaytimeImportMode PlaytimeImportMode => ReadEnum<PlaytimeImportMode>("Settings.PlaytimeImportMode");
    public string FontFamilyName => hostCall("Settings.FontFamilyName", string.Empty);
    public bool DiscordPresenceEnabled => ReadBoolean("Settings.DiscordPresenceEnabled");
    public AgeRatingOrg AgeRatingOrgPriority => ReadEnum<AgeRatingOrg>("Settings.AgeRatingOrgPriority");
    public bool SidebarVisible => ReadBoolean("Settings.SidebarVisible");
    public Dock SidebarPosition => ReadEnum<Dock>("Settings.SidebarPosition");
    public IFullscreenSettingsAPI Fullscreen { get; }
    public ICompletionStatusSettingsAPI CompletionStatus { get; }

    public HostApplicationSettings(Func<string, string, string> hostCall)
    {
        this.hostCall = hostCall ?? throw new ArgumentNullException(nameof(hostCall));
        Fullscreen = new HostFullscreenSettings(hostCall);
        CompletionStatus = new HostCompletionStatusSettings(hostCall);
    }

    public bool GetGameExcludedFromImport(string gameId, Guid libraryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        if (libraryId == Guid.Empty)
        {
            throw new ArgumentException("A library ID must be specified.", nameof(libraryId));
        }

        return bool.TryParse(hostCall(
            "Settings.GameExcludedFromImport",
            V7RpcJson.Serialize(new { GameId = gameId, LibraryId = libraryId })), out var excluded) && excluded;
    }

    private bool ReadBoolean(string operation)
    {
        var value = hostCall(operation, string.Empty);
        return bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                $"Avalonia host returned invalid Boolean value '{value}' for {operation}.");
    }

    private int ReadInteger(string operation)
    {
        var value = hostCall(operation, string.Empty);
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                $"Avalonia host returned invalid integer value '{value}' for {operation}.");
    }

    private TEnum ReadEnum<TEnum>(string operation) where TEnum : struct, Enum
    {
        var value = hostCall(operation, string.Empty);
        return Enum.TryParse<TEnum>(value, true, out var parsed)
            ? parsed
            : throw new InvalidDataException(
                $"Avalonia host returned invalid {typeof(TEnum).Name} value '{value}' for {operation}.");
    }
}

internal sealed class HostFullscreenSettings : IFullscreenSettingsAPI
{
    private readonly Func<string, string, string> hostCall;
    public bool IsMusicMuted
    {
        get => ReadBoolean("Settings.Fullscreen.IsMusicMuted");
        set => hostCall("Settings.Fullscreen.SetIsMusicMuted", value.ToString());
    }
    public bool SwapConfirmCancelButtons => ReadBoolean("Settings.Fullscreen.SwapConfirmCancelButtons");
    public bool SwapStartDetailsAction => ReadBoolean("Settings.Fullscreen.SwapStartDetailsAction");
    public bool GuideButtonFocus => ReadBoolean("Settings.Fullscreen.GuideButtonFocus");

    public HostFullscreenSettings(Func<string, string, string> hostCall) => this.hostCall = hostCall;
    private bool ReadBoolean(string operation) =>
        bool.TryParse(hostCall(operation, string.Empty), out var value)
            ? value
            : throw new InvalidDataException($"Avalonia host returned an invalid Boolean for {operation}.");
}

internal sealed class HostCompletionStatusSettings : ICompletionStatusSettingsAPI
{
    private readonly Func<string, string, string> hostCall;
    public Guid DefaultStatus => ReadGuid("Settings.CompletionStatus.Default");
    public Guid PlayedStatus => ReadGuid("Settings.CompletionStatus.Played");

    public HostCompletionStatusSettings(Func<string, string, string> hostCall) => this.hostCall = hostCall;
    private Guid ReadGuid(string operation) =>
        Guid.TryParse(hostCall(operation, string.Empty), out var value)
            ? value
            : throw new InvalidDataException($"Avalonia host returned an invalid GUID for {operation}.");
}

internal sealed class UnsupportedUriHandler : IUriHandlerAPI
{
    public void RegisterSource(string source, Action<PlayniteUriEventArgs> handler) =>
        throw new NotSupportedException(
            "SDK v7 URI handlers cannot cross the isolated Avalonia plugin boundary yet.");
    public void RemoveSource(string source) =>
        throw new NotSupportedException(
            "SDK v7 URI handlers cannot cross the isolated Avalonia plugin boundary yet.");
}

internal sealed class UnsupportedEmulationApi : IEmulationAPI
{
    private const string Message =
        "SDK v7 emulation definitions are not bridged into the isolated Avalonia plugin host yet.";

    public IList<EmulatedPlatform> Platforms => throw new NotSupportedException(Message);
    public IList<EmulatedRegion> Regions => throw new NotSupportedException(Message);
    public IList<EmulatorDefinition> Emulators => throw new NotSupportedException(Message);
    public EmulatedPlatform GetPlatform(string platformId) => throw new NotSupportedException(Message);
    public EmulatedRegion GetRegion(string regionId) => throw new NotSupportedException(Message);
    public EmulatorDefinition GetEmulator(string emulatorDefinitionId) => throw new NotSupportedException(Message);
}

internal sealed class HostPaths : IPlaynitePathsAPI
{
    private readonly Func<string, string, string> hostCall;
    public bool IsPortable => bool.TryParse(hostCall("IsPortable", string.Empty), out var value) && value;
    public string ApplicationPath => hostCall("ApplicationPath", string.Empty);
    public string ConfigurationPath => hostCall("ConfigurationPath", string.Empty);
    public string ExtensionsDataPath => hostCall("ExtensionsDataPath", string.Empty);

    public HostPaths(Func<string, string, string> hostCall) => this.hostCall = hostCall;
}

internal sealed class HostApplicationInfo : IPlayniteInfoAPI
{
    private readonly Func<string, string, string> hostCall;
    public Version ApplicationVersion => Version.TryParse(
        hostCall("ApplicationVersion", string.Empty), out var version) ? version : new Version(0, 0);
    public ApplicationMode Mode => Enum.TryParse<ApplicationMode>(
        hostCall("ApplicationMode", string.Empty), true, out var mode) ? mode : ApplicationMode.Desktop;
    public bool IsPortable => ReadBoolean("IsPortable");
    public bool InOfflineMode => ReadBoolean("InOfflineMode");
    public bool IsDebugBuild => ReadBoolean("IsDebugBuild");
    public bool ThrowAllErrors => ReadBoolean("ThrowAllErrors");

    public HostApplicationInfo(Func<string, string, string> hostCall) => this.hostCall = hostCall;
    private bool ReadBoolean(string operation) =>
        bool.TryParse(hostCall(operation, string.Empty), out var value) && value;
}

internal sealed class HostResources : IResourceProvider
{
    private readonly Func<string, string, string> hostCall;
    public HostResources(Func<string, string, string> hostCall) => this.hostCall = hostCall;
    public string GetString(string key) => hostCall("ResourceString", key) ?? key;
    public object GetResource(string key) => GetString(key);
}

internal sealed class HostNotifications : INotificationsAPI
{
    private readonly Func<string, string, string> hostCall;
    public ObservableCollection<NotificationMessage> Messages { get; } = [];
    public int Count => Messages.Count;

    public HostNotifications(Func<string, string, string> hostCall) => this.hostCall = hostCall;

    public void Add(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Remove(message.Id);
        Messages.Add(message);
        hostCall("NotificationAdd", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            message.Id,
            message.Text,
            Type = message.Type.ToString()
        }));
    }

    public void Add(string id, string text, NotificationType type) => Add(new NotificationMessage(id, text, type));

    public void Remove(string id)
    {
        var existing = Messages.FirstOrDefault(message => message.Id == id);
        if (existing != null)
        {
            Messages.Remove(existing);
        }
        hostCall("NotificationRemove", id);
    }

    public void RemoveAll()
    {
        Messages.Clear();
        hostCall("NotificationRemoveAll", string.Empty);
    }
}

internal sealed class HostDialogs : IDialogsFactory
{
    private readonly Func<string, string, string> hostCall;
    public HostDialogs(Func<string, string, string> hostCall) => this.hostCall = hostCall;
    public Task ShowErrorMessageAsync(string message, string caption = null)
    {
        hostCall("ShowError", Newtonsoft.Json.JsonConvert.SerializeObject(new { message, caption }));
        return Task.CompletedTask;
    }
    public Task<MessageBoxResult> ShowMessageAsync(
        string message,
        string caption = null,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None)
    {
        var response = hostCall("ShowMessage", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            message,
            caption,
            Buttons = buttons.ToString(),
            Image = image.ToString()
        }));
        return Task.FromResult(Enum.TryParse<MessageBoxResult>(response, true, out var result)
            ? result
            : MessageBoxResult.OK);
    }
    public Task<string> ShowChoiceAsync(
        string message,
        string caption,
        IReadOnlyList<string> choices,
        int defaultChoice = 0,
        int cancelChoice = -1) => Task.FromResult(choices.ElementAtOrDefault(defaultChoice));
    public Task<string> SelectFileAsync(string filter = null) => Task.FromResult<string>(null);
    public Task<IReadOnlyList<string>> SelectFilesAsync(string filter = null) =>
        Task.FromResult<IReadOnlyList<string>>([]);
    public Task<string> SelectFolderAsync() => Task.FromResult<string>(null);
    public Window CreateWindow(WindowCreationOptions options) => new()
    {
        Title = options?.Title,
        Width = options?.Width ?? 900,
        Height = options?.Height ?? 650,
        ShowInTaskbar = options?.ShowInTaskbar ?? true,
        CanResize = options?.CanResize ?? true
    };
    public Window GetCurrentAppWindow() => null;
}

internal sealed class HostAddons : IAddons
{
    private readonly Func<string, string, string> hostCall;
    public List<string> DisabledAddons => ReadList("DisabledAddons");
    public List<string> Addons => ReadList("Addons");
    public List<Plugin> Plugins => [];
    public HostAddons(Func<string, string, string> hostCall) => this.hostCall = hostCall;
    private List<string> ReadList(string operation) =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(hostCall(operation, string.Empty) ?? "[]") ?? [];
}
