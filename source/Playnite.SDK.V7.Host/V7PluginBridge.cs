using Avalonia.Controls;
using Avalonia.Threading;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;

namespace Playnite.SDK.V7.Host;

public static class V7PluginBridge
{
    public static object[] LoadAll(
        string pluginAssemblyPath,
        Func<string, string, string> hostCall)
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

        var api = new V7PlayniteApi(hostCall);
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
        InvokeApplicationStopped();
        plugin.Dispose();
    }
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
    private readonly Func<string, string, string> hostCall;

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

    public V7PlayniteApi(Func<string, string, string> hostCall)
    {
        this.hostCall = hostCall;
        Paths = new HostPaths(hostCall);
        ApplicationInfo = new HostApplicationInfo(hostCall);
        Resources = new HostResources(hostCall);
        Notifications = new HostNotifications(hostCall);
        Dialogs = new HostDialogs(hostCall);
        MainView = V7InterfaceProxy.Create<IMainViewAPI>((method, args) =>
        {
            if (method.Name == "get_UIDispatcher")
            {
                return Dispatcher.UIThread;
            }

            if (method.Name == "OpenPluginSettingsAsync")
            {
                return Task.FromResult(bool.TryParse(
                    hostCall("OpenPluginSettings", args[0].ToString()),
                    out var opened) && opened);
            }

            return V7InterfaceProxy.DefaultValue(method.ReturnType);
        });
        HostDatabase = new HostGameDatabase(hostCall);
        Database = HostDatabase;
        ApplicationSettings = V7InterfaceProxy.Create<IPlayniteSettingsAPI>((method, _) => method.Name switch
        {
            "get_DatabasePath" => hostCall("DatabasePath", string.Empty),
            "get_Language" => hostCall("Language", string.Empty),
            "get_FirstTimeWizardComplete" => true,
            "get_AsyncImageLoading" => true,
            "get_DownloadMetadataOnImport" => true,
            "get_CompletionStatus" => V7InterfaceProxy.Create<ICompletionStatusSettingsAPI>(),
            "get_Fullscreen" => V7InterfaceProxy.Create<IFullscreenSettingsAPI>(),
            _ => V7InterfaceProxy.DefaultValue(method.ReturnType)
        });
        WebViews = V7InterfaceProxy.Create<IWebViewFactory>();
        UriHandler = V7InterfaceProxy.Create<IUriHandlerAPI>();
        Addons = new HostAddons(hostCall);
        Emulation = V7InterfaceProxy.Create<IEmulationAPI>();
    }

    public string ExpandGameVariables(Game game, string inputString) => inputString;
    public string ExpandGameVariables(Game game, string inputString, string emulatorDir) => inputString;
    public GameAction ExpandGameVariables(Game game, GameAction action) => action;
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

    public void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args) =>
        hostCall("AddConvertersSupport", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            PluginId = source.Id,
            args.SourceName,
            ConverterNames = args.Converters?.Select(converter => converter.GetType().FullName).ToList()
        }));

    public List<GamepadController> GetConnectedControllers() => [];

    private Task CallGameOperation(string operation, Guid gameId)
    {
        hostCall(operation, gameId.ToString());
        return Task.CompletedTask;
    }
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

internal class V7InterfaceProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler { get; set; }

    protected override object Invoke(MethodInfo targetMethod, object[] args) =>
        Handler?.Invoke(targetMethod, args) ?? DefaultValue(targetMethod.ReturnType);

    public static T Create<T>(Func<MethodInfo, object[], object> handler = null) where T : class
    {
        var proxy = DispatchProxy.Create<T, V7InterfaceProxy>();
        ((V7InterfaceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public static object DefaultValue(Type type)
    {
        if (type == typeof(void))
        {
            return null;
        }
        if (type == typeof(string))
        {
            return string.Empty;
        }
        if (type == typeof(Task))
        {
            return Task.CompletedTask;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = type.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { DefaultValue(resultType) });
        }
        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType(), 0);
        }
        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(IEnumerable<>) || generic == typeof(IReadOnlyList<>) ||
                generic == typeof(IList<>) || generic == typeof(List<>))
            {
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]));
            }
        }
        if (type.IsInterface)
        {
            return typeof(V7InterfaceProxy)
                .GetMethod(nameof(Create), BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(type)
                .Invoke(null, new object[] { null });
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
