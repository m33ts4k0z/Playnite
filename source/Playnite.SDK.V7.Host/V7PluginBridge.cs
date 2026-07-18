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

            results.Add(new V7PluginInstance(plugin));
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
    private readonly Plugin plugin;
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

    internal V7PluginInstance(Plugin plugin)
    {
        this.plugin = plugin;
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

    public void Dispose()
    {
        InvokeApplicationStopped();
        plugin.Dispose();
    }
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
        Database = V7InterfaceProxy.Create<IGameDatabaseAPI>((method, _) =>
            method.Name == "get_DatabasePath"
                ? hostCall("DatabasePath", string.Empty)
                : V7InterfaceProxy.DefaultValue(method.ReturnType));
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
