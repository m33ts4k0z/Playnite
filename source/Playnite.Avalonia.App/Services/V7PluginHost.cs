using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Playnite.API;
using Playnite.Avalonia.Controls;
using Playnite.Common;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Emulators;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;

namespace Playnite.Avalonia.App.Services;

public sealed class V7LoadedPlugin
{
    private readonly object instance;
    private readonly MethodInfo applicationStarted;
    private readonly MethodInfo applicationStopped;
    private readonly MethodInfo publishDatabaseEvent;
    private readonly MethodInfo getControllers;
    private readonly MethodInfo invokeGameEvent;
    private readonly MethodInfo getLibraryGames;
    private readonly MethodInfo importLibraryGames;
    private readonly MethodInfo invokeLibraryUpdated;
    private readonly MethodInfo invokeControllerButtonStateChanged;
    private readonly MethodInfo invokeControllerConnection;
    private readonly MethodInfo openLibraryClient;
    private readonly MethodInfo shutdownLibraryClient;
    private readonly MethodInfo createMetadataProvider;
    private readonly MethodInfo createLibraryMetadataProvider;
    private readonly MethodInfo beginSettingsEdit;
    private readonly MethodInfo verifySettings;
    private readonly MethodInfo endSettingsEdit;
    private readonly MethodInfo cancelSettingsEdit;
    private readonly MethodInfo getMenuItems;
    private readonly MethodInfo createGameViewControl;
    private readonly MethodInfo getConverter;
    private readonly MethodInfo getSidebarItems;
    private readonly MethodInfo getTopPanelItems;
    private readonly MethodInfo getSearches;
    private readonly MethodInfo invokeUri;
    private readonly MethodInfo invokeNotificationAction;
    private readonly MethodInfo dispose;

    public Guid Id { get; }
    public string Name { get; }
    public string Kind { get; }
    public bool HasSettings { get; }
    public bool CanShutdownLibraryClient { get; }
    public bool HasCustomizedGameImport { get; }
    public string LibraryIcon { get; }
    public string LibraryBackground { get; }
    public bool HasLibraryClient { get; }
    public bool IsLibraryClientInstalled { get; }
    public string LibraryClientIcon { get; }
    public string[] SupportedMetadataFields { get; }
    public Guid UriOwnerToken { get; }
    public ExtensionManifest Manifest { get; }

    internal V7LoadedPlugin(object instance, ExtensionManifest manifest)
    {
        this.instance = instance;
        Manifest = manifest;
        var type = instance.GetType();
        Id = ReadProperty<Guid>(type, nameof(Id));
        Name = ReadProperty<string>(type, nameof(Name));
        Kind = ReadProperty<string>(type, nameof(Kind));
        HasSettings = ReadProperty<bool>(type, nameof(HasSettings));
        CanShutdownLibraryClient = ReadProperty<bool>(type, nameof(CanShutdownLibraryClient));
        HasCustomizedGameImport = ReadProperty<bool>(type, nameof(HasCustomizedGameImport));
        LibraryIcon = ReadProperty<string>(type, nameof(LibraryIcon));
        LibraryBackground = ReadProperty<string>(type, nameof(LibraryBackground));
        HasLibraryClient = ReadProperty<bool>(type, nameof(HasLibraryClient));
        IsLibraryClientInstalled = ReadProperty<bool>(type, nameof(IsLibraryClientInstalled));
        LibraryClientIcon = ReadProperty<string>(type, nameof(LibraryClientIcon));
        SupportedMetadataFields = ReadProperty<string[]>(type, nameof(SupportedMetadataFields));
        UriOwnerToken = ReadProperty<Guid>(type, nameof(UriOwnerToken));
        applicationStarted = GetRequiredMethod(type, "InvokeApplicationStarted");
        applicationStopped = GetRequiredMethod(type, "InvokeApplicationStopped");
        publishDatabaseEvent = GetRequiredMethod(type, "PublishDatabaseEvent");
        getControllers = GetRequiredMethod(type, "GetControllers");
        invokeGameEvent = GetRequiredMethod(type, "InvokeGameEvent");
        getLibraryGames = GetRequiredMethod(type, "GetLibraryGames");
        importLibraryGames = GetRequiredMethod(type, "ImportLibraryGames");
        invokeLibraryUpdated = GetRequiredMethod(type, "InvokeLibraryUpdated");
        invokeControllerButtonStateChanged = GetRequiredMethod(type, "InvokeControllerButtonStateChanged");
        invokeControllerConnection = GetRequiredMethod(type, "InvokeControllerConnection");
        openLibraryClient = GetRequiredMethod(type, "OpenLibraryClient");
        shutdownLibraryClient = GetRequiredMethod(type, "ShutdownLibraryClient");
        createMetadataProvider = GetRequiredMethod(type, "CreateMetadataProvider");
        createLibraryMetadataProvider = GetRequiredMethod(type, "CreateLibraryMetadataProvider");
        beginSettingsEdit = GetRequiredMethod(type, "BeginSettingsEdit");
        verifySettings = GetRequiredMethod(type, "VerifySettings");
        endSettingsEdit = GetRequiredMethod(type, "EndSettingsEdit");
        cancelSettingsEdit = GetRequiredMethod(type, "CancelSettingsEdit");
        getMenuItems = GetRequiredMethod(type, "GetMenuItems");
        createGameViewControl = GetRequiredMethod(type, "CreateGameViewControl");
        getConverter = GetRequiredMethod(type, "GetConverter");
        getSidebarItems = GetRequiredMethod(type, "GetSidebarItems");
        getTopPanelItems = GetRequiredMethod(type, "GetTopPanelItems");
        getSearches = GetRequiredMethod(type, "GetSearches");
        invokeUri = GetRequiredMethod(type, "InvokeUri");
        invokeNotificationAction = GetRequiredMethod(type, "InvokeNotificationAction");
        dispose = GetRequiredMethod(type, nameof(IDisposable.Dispose));
    }

    internal void InvokeApplicationStarted() => Invoke(applicationStarted);
    internal void InvokeApplicationStopped() => Invoke(applicationStopped);
    internal void Dispose() => Invoke(dispose);
    internal void PublishDatabaseEvent(string collection, string eventName, string payload) =>
        Invoke(publishDatabaseEvent, collection, eventName, payload);
    internal object[] GetControllers(string kind, string gameJson) =>
        (object[])InvokeWithResult(getControllers, kind, gameJson);
    internal string InvokeGameEvent(string eventName, string payload) =>
        (string)InvokeWithResult(invokeGameEvent, eventName, payload);
    internal List<GameMetadata> GetLibraryGames(CancellationToken cancellationToken) =>
        V7DatabaseTransport.Deserialize<List<GameMetadata>>(
            (string)InvokeWithResult(getLibraryGames, cancellationToken))
        ?? throw new InvalidDataException(
            $"SDK v7 library plugin {Id} returned an invalid game sequence.");
    internal List<Game> ImportLibraryGames(CancellationToken cancellationToken) =>
        V7DatabaseTransport.Deserialize<List<Game>>(
            (string)InvokeWithResult(importLibraryGames, cancellationToken))
        ?? throw new InvalidDataException(
            $"SDK v7 library plugin {Id} returned an invalid custom-import sequence.");
    internal void InvokeLibraryUpdated() => Invoke(invokeLibraryUpdated);
    internal void InvokeControllerButtonStateChanged(int input, int state) =>
        Invoke(invokeControllerButtonStateChanged, input, state);
    internal void InvokeControllerConnection(
        bool connected,
        int instanceId,
        string path,
        string name,
        bool enabled) => Invoke(
            invokeControllerConnection,
            connected,
            instanceId,
            path,
            name,
            enabled);
    internal void OpenLibraryClient() => Invoke(openLibraryClient);
    internal void ShutdownLibraryClient() => Invoke(shutdownLibraryClient);
    internal object CreateMetadataProvider(string gameJson, bool backgroundDownload) =>
        InvokeWithResult(createMetadataProvider, gameJson, backgroundDownload);
    internal object CreateLibraryMetadataProvider() =>
        InvokeWithResult(createLibraryMetadataProvider);
    public Control BeginSettingsEdit() =>
        (Control)InvokeWithResult(beginSettingsEdit);
    public V7SettingsValidationResult VerifySettings()
    {
        var result = JObject.Parse((string)InvokeWithResult(verifySettings));
        return new V7SettingsValidationResult(
            result.Value<bool>("Valid"),
            result["Errors"]?.ToObject<List<string>>() ?? []);
    }
    public void EndSettingsEdit() => Invoke(endSettingsEdit);
    public void CancelSettingsEdit() => Invoke(cancelSettingsEdit);
    internal object[] GetMenuItems(string kind, string gamesJson, bool globalSearchRequest) =>
        (object[])InvokeWithResult(getMenuItems, kind, gamesJson, globalSearchRequest);
    internal object CreateGameViewControl(string name, ApplicationMode mode, string gameJson) =>
        InvokeWithResult(createGameViewControl, name, mode.ToString(), gameJson);
    internal IValueConverter GetConverter(string sourceName, string converterName) =>
        (IValueConverter)InvokeWithResult(getConverter, sourceName, converterName);
    internal object[] GetSidebarItems() => (object[])InvokeWithResult(getSidebarItems);
    internal object[] GetTopPanelItems() => (object[])InvokeWithResult(getTopPanelItems);
    public IReadOnlyList<V7SearchSupport> GetSearches() =>
        ((object[])InvokeWithResult(getSearches))
        .Select(search => new V7SearchSupport(
            ReadInstanceProperty<string>(search, "DefaultKeyword"),
            ReadInstanceProperty<string>(search, "Name"),
            ReadInstanceProperty<object>(search, "Context")))
        .ToList();
    internal void InvokeUri(string source, string[] arguments) => Invoke(invokeUri, source, arguments);
    internal void InvokeNotificationAction(Guid actionToken) =>
        Invoke(invokeNotificationAction, actionToken);

    private T ReadProperty<T>(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(type.FullName, name);
        return (T)property.GetValue(instance);
    }

    private static T ReadInstanceProperty<T>(object value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        var property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(value.GetType().FullName, name);
        return (T)property.GetValue(value);
    }

    private static MethodInfo GetRequiredMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new MissingMethodException(type.FullName, name);

    private void Invoke(MethodInfo method, params object[] arguments)
    {
        InvokeWithResult(method, arguments);
    }

    private object InvokeWithResult(MethodInfo method, params object[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}

public sealed record V7SettingsValidationResult(bool IsValid, IReadOnlyList<string> Errors);
public sealed record V7SearchSupport(string DefaultKeyword, string Name, object Context);

public sealed class V7PluginLoadFailure
{
    public ExtensionManifest Manifest { get; }
    public Exception Exception { get; }
    public string Message => Exception.Message;

    internal V7PluginLoadFailure(ExtensionManifest manifest, Exception exception)
    {
        Manifest = manifest;
        Exception = exception;
    }
}

internal sealed class V7PluginHost : IDisposable
{
    private static readonly ILogger logger = LogManager.GetLogger();

    private sealed class PluginLoadHandle
    {
        public V7PluginLoadContext Context { get; }
        public Func<string, string, string> HostCall { get; }
        public List<V7LoadedPlugin> Plugins { get; }

        public PluginLoadHandle(
            V7PluginLoadContext context,
            Func<string, string, string> hostCall,
            List<V7LoadedPlugin> plugins)
        {
            Context = context;
            HostCall = hostCall;
            Plugins = plugins;
        }
    }

    private sealed class V7PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver pluginResolver;
        private readonly string hostBundlePath;

        public V7PluginLoadContext(string pluginAssemblyPath, string hostBundlePath)
            : base($"Playnite SDK v7: {Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", true)
        {
            pluginResolver = new AssemblyDependencyResolver(pluginAssemblyPath);
            this.hostBundlePath = hostBundlePath;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, "Playnite.SDK", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName.Name, "Playnite.SDK.V7.Host", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(hostBundlePath, assemblyName.Name + ".dll");
                return LoadFromAssemblyPath(path);
            }

            if (ShouldShareWithDefaultContext(assemblyName.Name))
            {
                return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly =>
                    AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName))
                    ?? AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            }

            var resolvedPath = pluginResolver.ResolveAssemblyToPath(assemblyName);
            return resolvedPath == null ? null : LoadFromAssemblyPath(resolvedPath);
        }

        private static bool ShouldShareWithDefaultContext(string name) =>
            name.StartsWith("System.", StringComparison.Ordinal) ||
            name.StartsWith("Avalonia", StringComparison.Ordinal) ||
            string.Equals(name, "System", StringComparison.Ordinal) ||
            string.Equals(name, "netstandard", StringComparison.Ordinal) ||
            string.Equals(name, "Newtonsoft.Json", StringComparison.Ordinal) ||
            string.Equals(name, "YamlDotNet", StringComparison.Ordinal) ||
            string.Equals(name, "Nett", StringComparison.Ordinal);
    }

    private sealed class NotificationPayload
    {
        public Guid OwnerToken { get; set; }
        public Guid ActionToken { get; set; }
        public string Id { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
    }

    private sealed class NotificationMutationPayload
    {
        public Guid OwnerToken { get; set; }
        public string Id { get; set; }
    }

    private sealed class LogPayload
    {
        public string Level { get; set; }
        public string LoggerName { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
    }

    private sealed class DialogPayload
    {
        public string Message { get; set; }
        public string Caption { get; set; }
        public string Buttons { get; set; }
    }

    private sealed class ChoiceDialogPayload
    {
        public string Message { get; set; }
        public string Caption { get; set; }
        public List<string> Choices { get; set; }
        public int DefaultChoice { get; set; }
        public int CancelChoice { get; set; }
    }

    private sealed class FilePickerPayload
    {
        public string Filter { get; set; }
        public bool AllowMultiple { get; set; }
    }

    private sealed class PluginDescriptorPayload
    {
        public Guid Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
    }

    private sealed class UriRegistrationPayload
    {
        public Guid OwnerToken { get; set; }
        public string Source { get; set; }
    }

    private sealed class UiRegistrationPayload
    {
        public Guid PluginId { get; set; }
        public string SourceName { get; set; }
        public List<string> ElementList { get; set; }
        public List<string> ConverterNames { get; set; }
    }

    private sealed class VariableExpansionPayload
    {
        public Game Game { get; set; }
        public string Input { get; set; }
        public string EmulatorDirectory { get; set; }
        public GameAction Action { get; set; }
    }

    private sealed class GameImportExclusionPayload
    {
        public string GameId { get; set; }
        public Guid LibraryId { get; set; }
    }

    private readonly GameDatabase database;
    private readonly AvaloniaHostCallbacks callbacks;
    private readonly GameControllerFactory controllers;
    private readonly NotificationsAPI notifications;
    private readonly Func<GameActionRunner> actionRunner;
    private readonly Func<IEnumerable<string>> installedAddons;
    private readonly IPlayniteAPI pluginApi;
    private readonly IEmulationAPI emulation = new Emulation();
    private readonly AvaloniaWebViewFactory webViews;
    private readonly V7DatabaseTransport databaseTransport;
    private readonly string hostBundlePath;
    internal Version SdkVersion
    {
        get
        {
            var path = Path.Combine(hostBundlePath, "Playnite.SDK.dll");
            return File.Exists(path) ? AssemblyName.GetAssemblyName(path).Version : new Version(7, 0, 0);
        }
    }
    private readonly List<PluginLoadHandle> handles = [];
    private readonly List<Action> unsubscribeEvents = [];
    private readonly List<UiRegistrationPayload> customElementRegistrations = [];
    private readonly List<UiRegistrationPayload> converterRegistrations = [];
    private readonly Dictionary<string, Guid> uriSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, V7LoadedPlugin> uriOwners = [];
    private readonly Dictionary<string, Guid> notificationOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, HashSet<string>> notificationIdsByOwner = [];
    private readonly Dictionary<string, string> pluginLocalizationResources =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Sqlite> sqliteConnections = [];
    private readonly object sqliteSync = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, IV7ControllerAdapter>
        controllerAdapters = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, JObject>
        startingEventData = new();
    private bool loaded;
    private bool disposed;

    public List<V7LoadedPlugin> Plugins { get; } = [];
    public List<V7PluginLoadFailure> FailedPlugins { get; } = [];
    public List<LibraryPlugin> LibraryPlugins { get; } = [];
    public List<MetadataPlugin> MetadataPlugins { get; } = [];

    public V7PluginHost(
        GameDatabase database,
        GameControllerFactory controllers,
        AvaloniaHostCallbacks callbacks,
        NotificationsAPI notifications,
        Func<GameActionRunner> actionRunner,
        Func<IEnumerable<string>> installedAddons,
        string hostBundlePath = null,
        IPlayniteAPI pluginApi = null,
        AvaloniaWebViewFactory webViews = null)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
        this.controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        this.actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
        this.installedAddons = installedAddons ?? throw new ArgumentNullException(nameof(installedAddons));
        this.pluginApi = pluginApi;
        this.webViews = webViews;
        this.hostBundlePath = hostBundlePath ?? Path.Combine(AppContext.BaseDirectory, "SdkV7Host");
        PluginResourceRegistry.Clear();
        databaseTransport = new V7DatabaseTransport(database);
        SubscribeDatabaseEvents();
        SubscribeControllerEvents();
    }

    public IReadOnlyList<string> Load(
        IEnumerable<ExtensionManifest> manifests,
        IReadOnlyCollection<string> disabledManifestIds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (loaded)
        {
            throw new InvalidOperationException("SDK v7 plugins can only be loaded once.");
        }

        loaded = true;
        var claimedManifestIds = new List<string>();
        foreach (var manifest in manifests.Where(manifest => manifest.Type != ExtensionType.Script))
        {
            var modulePath = GetModulePath(manifest);
            if (!IsSdkV7Assembly(modulePath))
            {
                continue;
            }

            claimedManifestIds.Add(manifest.Id);
            if (disabledManifestIds?.Contains(manifest.Id) == true)
            {
                continue;
            }

            LoadManifest(manifest, modulePath);
        }

        LibraryPlugins.AddRange(Plugins
            .Where(plugin => plugin.Kind == "LibraryPlugin")
            .Select(plugin => new V7LibraryPluginAdapter(pluginApi, plugin)));
        MetadataPlugins.AddRange(Plugins
            .Where(plugin => plugin.Kind == "MetadataPlugin")
            .Select(plugin => new V7MetadataPluginAdapter(pluginApi, plugin)));

        return claimedManifestIds;
    }

    public bool ProcessUri(string uri)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var (source, arguments) = PlayniteUriHandler.ParseUri(uri);
        if (!uriSources.TryGetValue(source, out var ownerToken))
        {
            return false;
        }

        if (!uriOwners.TryGetValue(ownerToken, out var plugin))
        {
            throw new InvalidOperationException(
                $"SDK v7 URI source '{source}' has no live plugin context.");
        }

        plugin.InvokeUri(source, arguments);
        return true;
    }

    public static bool IsSdkV7Assembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return false;
            }

            var metadata = peReader.GetMetadataReader();
            foreach (var handle in metadata.AssemblyReferences)
            {
                var reference = metadata.GetAssemblyReference(handle);
                if (string.Equals(
                    metadata.GetString(reference.Name),
                    "Playnite.SDK",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return reference.Version.Major == 7;
                }
            }
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        return false;
    }

    private void LoadManifest(ExtensionManifest manifest, string modulePath)
    {
        V7PluginLoadContext context = null;
        var constructedPlugins = new List<V7LoadedPlugin>();
        var previousCustomRegistrations = customElementRegistrations.ToList();
        var previousConverterRegistrations = converterRegistrations.ToList();
        var previousUriSources = uriSources.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        var previousUriOwners = uriOwners.ToDictionary(item => item.Key, item => item.Value);
        var previousNotificationIds = notificationOwners.Keys.ToHashSet(StringComparer.Ordinal);
        var previousLocalizationResources = pluginLocalizationResources.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal);
        HashSet<Guid> previousSqliteHandles;
        lock (sqliteSync)
        {
            previousSqliteHandles = sqliteConnections.Keys.ToHashSet();
        }
        try
        {
            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new InvalidDataException($"SDK v7 extension {manifest.Name} has no manifest ID.");
            }

            VerifyHostBundle();
            foreach (var resource in PluginLocalizationCatalog.Load(
                manifest.DirectoryPath,
                callbacks.Settings.Language))
            {
                pluginLocalizationResources[resource.Key] = resource.Value;
            }
            PluginResourceRegistry.Replace(pluginLocalizationResources);
            context = new V7PluginLoadContext(modulePath, hostBundlePath);
            var bridgeAssembly = context.LoadFromAssemblyPath(
                Path.Combine(hostBundlePath, "Playnite.SDK.V7.Host.dll"));
            var bridge = bridgeAssembly.GetType("Playnite.SDK.V7.Host.V7PluginBridge", true);
            var loadAll = bridge.GetMethod("LoadAll", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(bridge.FullName, "LoadAll");
            Func<string, string, string> hostCall = HostCall;
            Func<string, string, object> hostObjectCall = HostObjectCall;
            Func<string, object, object> hostRequest = HostObjectRequest;
            var instances = (object[])loadAll.Invoke(
                null,
                [modulePath, hostCall, hostObjectCall, hostRequest]);
            constructedPlugins.AddRange(instances.Select(instance => new V7LoadedPlugin(instance, manifest)));
            if (constructedPlugins.Count == 0)
            {
                throw new InvalidDataException($"SDK v7 extension {manifest.Name} contains no plugin types.");
            }

            var uriOwnerToken = constructedPlugins[0].UriOwnerToken;
            if (uriOwnerToken == Guid.Empty || constructedPlugins.Any(plugin => plugin.UriOwnerToken != uriOwnerToken))
            {
                throw new InvalidDataException(
                    $"SDK v7 extension {manifest.Name} has inconsistent URI owner identity.");
            }
            if (!uriOwners.TryAdd(uriOwnerToken, constructedPlugins[0]))
            {
                throw new InvalidDataException(
                    $"SDK v7 extension {manifest.Name} duplicated a URI owner identity.");
            }

            foreach (var plugin in constructedPlugins)
            {
                if (Plugins.Any(existing => existing.Id == plugin.Id))
                {
                    throw new InvalidDataException($"SDK v7 plugin ID {plugin.Id} is already loaded.");
                }

                Plugins.Add(plugin);
                plugin.InvokeApplicationStarted();
            }

            handles.Add(new PluginLoadHandle(context, hostCall, constructedPlugins));
        }
        catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
        {
            var actualException = exception is TargetInvocationException { InnerException: not null }
                ? exception.InnerException
                : exception;
            FailedPlugins.Add(new V7PluginLoadFailure(manifest, actualException));
            callbacks.SetStatus($"SDK v7 plugin {manifest.Name} failed: {actualException.Message}");
            foreach (var plugin in constructedPlugins.AsEnumerable().Reverse())
            {
                plugin.Dispose();
                Plugins.Remove(plugin);
            }
            context?.Unload();
            customElementRegistrations.Clear();
            customElementRegistrations.AddRange(previousCustomRegistrations);
            converterRegistrations.Clear();
            converterRegistrations.AddRange(previousConverterRegistrations);
            uriSources.Clear();
            foreach (var registration in previousUriSources)
            {
                uriSources.Add(registration.Key, registration.Value);
            }
            uriOwners.Clear();
            foreach (var owner in previousUriOwners)
            {
                uriOwners.Add(owner.Key, owner.Value);
            }
            foreach (var notificationId in notificationOwners.Keys
                .Where(id => !previousNotificationIds.Contains(id))
                .ToList())
            {
                RemoveTrackedNotification(notificationId);
            }
            pluginLocalizationResources.Clear();
            foreach (var resource in previousLocalizationResources)
            {
                pluginLocalizationResources.Add(resource.Key, resource.Value);
            }
            PluginResourceRegistry.Replace(pluginLocalizationResources);
            DisposeSqliteConnections(handle => !previousSqliteHandles.Contains(handle));
        }
    }

    private string HostCall(string operation, string payload)
    {
        switch (operation)
        {
            case "ApplicationMode": return callbacks.Mode.ToString();
            case "ApplicationVersion": return CoreRuntime.ApplicationVersion().ToString();
            case "ApplicationPath": return PlaynitePaths.ProgramPath;
            case "ConfigurationPath": return PlaynitePaths.ConfigRootPath;
            case "ExtensionsDataPath": return PlaynitePaths.ExtensionsDataPath;
            case "DatabasePath": return database.DatabasePath;
            case "Database": return databaseTransport.Handle(payload);
            case "ControllerEvent":
                HandleControllerEvent(payload);
                return string.Empty;
            case "Language": return callbacks.Settings.Language;
            case "IsPortable": return PlaynitePaths.IsPortable.ToString();
            case "InOfflineMode": return PlayniteEnvironment.InOfflineMode.ToString();
            case "IsDebugBuild": return PlayniteEnvironment.IsDebugBuild.ToString();
            case "ThrowAllErrors": return PlayniteEnvironment.ThrowAllErrors.ToString();
            case "DisabledAddons":
                return JsonConvert.SerializeObject(callbacks.Settings.DisabledPlugins ?? []);
            case "Addons":
                return JsonConvert.SerializeObject(installedAddons().Distinct().ToList());
            case "LoadedPlugins":
                return JsonConvert.SerializeObject(GetLoadedPluginDescriptors());
            case "Uri.Register":
                RegisterUriSource(payload);
                return string.Empty;
            case "Uri.Remove":
                RemoveUriSource(payload);
                return string.Empty;
            case "Emulation.Platforms":
                return V7DatabaseTransport.Serialize(emulation.Platforms);
            case "Emulation.Regions":
                return V7DatabaseTransport.Serialize(emulation.Regions);
            case "Emulation.Emulators":
                return V7DatabaseTransport.Serialize(emulation.Emulators);
            case "Emulation.GetPlatform":
                return V7DatabaseTransport.Serialize(emulation.GetPlatform(payload));
            case "Emulation.GetRegion":
                return V7DatabaseTransport.Serialize(emulation.GetRegion(payload));
            case "Emulation.GetEmulator":
                return V7DatabaseTransport.Serialize(emulation.GetEmulator(payload));
            case "ResourceString":
                return ResolveResource(payload) as string ?? $"<!{payload}!>";
            case "Log":
                WritePluginLog(JsonConvert.DeserializeObject<LogPayload>(payload));
                return string.Empty;
            case "NotificationAdd":
                AddNotification(JsonConvert.DeserializeObject<NotificationPayload>(payload));
                return string.Empty;
            case "NotificationRemove":
                RemoveNotification(JsonConvert.DeserializeObject<NotificationMutationPayload>(payload));
                return string.Empty;
            case "NotificationRemoveAll":
                RemoveNotifications(JsonConvert.DeserializeObject<NotificationMutationPayload>(payload));
                return string.Empty;
            case "ShowError":
                ShowDialog(payload, true);
                return "OK";
            case "ShowMessage":
                return ShowDialog(payload, false);
            case "ShowChoice":
                return ShowChoiceDialog(payload);
            case "SelectFiles":
                var picker = JsonConvert.DeserializeObject<FilePickerPayload>(payload)
                    ?? throw new InvalidDataException("SDK v7 file-picker payload is empty.");
                var selectedFiles = callbacks.Dialogs.SelectFiles(picker.Filter, picker.AllowMultiple)
                    ?? throw new InvalidDataException("Avalonia dialog service returned no file-picker result.");
                return JsonConvert.SerializeObject(selectedFiles);
            case "SelectFolder":
                return callbacks.Dialogs.SelectFolder();
            case "OpenPluginSettings":
                return callbacks.OpenPluginSettings(ParseGuid(operation, payload))
                    ? bool.TrueString
                    : bool.FalseString;
            case "MainView.ActiveDesktopView": return callbacks.ActiveDesktopView().ToString();
            case "MainView.SetActiveDesktopView":
                callbacks.SetActiveDesktopView?.Invoke(ParseEnum<DesktopView>(operation, payload));
                if (callbacks.SetActiveDesktopView == null)
                {
                    throw new NotSupportedException(
                        "The current Avalonia application does not support changing its desktop view.");
                }
                return string.Empty;
            case "MainView.ActiveFullscreenView": return callbacks.ActiveFullscreenView().ToString();
            case "MainView.SortOrder": return callbacks.SortOrder().ToString();
            case "MainView.SortOrderDirection": return callbacks.SortDirection().ToString();
            case "MainView.SetSortOrderDirection":
                callbacks.SetSortDirection(ParseEnum<SortOrderDirection>(operation, payload));
                return string.Empty;
            case "MainView.Grouping": return callbacks.Grouping().ToString();
            case "MainView.SetGrouping":
                callbacks.SetGrouping(ParseEnum<GroupableField>(operation, payload));
                return string.Empty;
            case "MainView.SelectedGames":
                return V7DatabaseTransport.Serialize(
                    callbacks.SelectedGame() is { } selected ? new[] { selected } : []);
            case "MainView.FilteredGames":
                return V7DatabaseTransport.Serialize(callbacks.FilteredGames());
            case "MainView.SwitchToLibraryView":
                (callbacks.SwitchToLibraryView ?? throw new NotSupportedException(
                    "The current Avalonia application does not expose a library-view switch callback."))();
                return string.Empty;
            case "MainView.SelectGame":
                callbacks.SelectGame(ParseGuid(operation, payload));
                return string.Empty;
            case "MainView.SelectGames":
                var selectedIds = V7DatabaseTransport.Deserialize<List<Guid>>(payload) ?? [];
                if (callbacks.SelectGames != null)
                {
                    callbacks.SelectGames(selectedIds);
                }
                else if (selectedIds.Count <= 1)
                {
                    if (selectedIds.Count == 1)
                    {
                        callbacks.SelectGame(selectedIds[0]);
                    }
                }
                else
                {
                    throw new NotSupportedException(
                        "The current Avalonia application does not support selecting multiple games.");
                }
                return string.Empty;
            case "MainView.ApplyFilterPreset":
                callbacks.ApplyFilterPreset(ParseGuid(operation, payload));
                return string.Empty;
            case "MainView.ActiveFilterPreset": return callbacks.ActiveFilterPreset().ToString();
            case "MainView.CurrentFilterSettings":
                return V7DatabaseTransport.Serialize(callbacks.CurrentFilterSettings());
            case "MainView.OpenSearch":
                callbacks.OpenSearch(payload);
                return string.Empty;
            case "MainView.OpenEditDialog":
                return JsonConvert.SerializeObject(callbacks.OpenEditDialog(
                    V7DatabaseTransport.Deserialize<List<Guid>>(payload) ?? []));
            case "MainView.FilterPresets":
            case "MainView.FullscreenFilterPresets":
                return V7DatabaseTransport.Serialize(callbacks.FilterPresets());
            case "MainView.ToggleFullscreenView":
                (callbacks.ToggleFullscreenView ?? throw new NotSupportedException(
                    "The current Avalonia application does not expose a fullscreen-toggle callback."))();
                return string.Empty;
            case "Settings.Version": return callbacks.Settings.Version.ToString();
            case "Settings.GridItemWidthRatio": return callbacks.Settings.GridItemWidthRatio.ToString();
            case "Settings.GridItemHeightRatio": return callbacks.Settings.GridItemHeightRatio.ToString();
            case "Settings.FirstTimeWizardComplete": return callbacks.Settings.FirstTimeWizardComplete.ToString();
            case "Settings.DisableHwAcceleration": return callbacks.Settings.DisableHwAcceleration.ToString();
            case "Settings.AsyncImageLoading": return callbacks.Settings.AsyncImageLoading.ToString();
            case "Settings.DownloadMetadataOnImport": return callbacks.Settings.DownloadMetadataOnImport.ToString();
            case "Settings.StartInFullscreen": return callbacks.Settings.StartInFullscreen.ToString();
            case "Settings.MinimizeToTray": return callbacks.Settings.MinimizeToTray.ToString();
            case "Settings.CloseToTray": return callbacks.Settings.CloseToTray.ToString();
            case "Settings.EnableTray": return callbacks.Settings.EnableTray.ToString();
            case "Settings.UpdateLibStartup": return callbacks.Settings.UpdateLibStartup.ToString();
            case "Settings.DesktopTheme": return callbacks.Settings.DesktopTheme ?? string.Empty;
            case "Settings.FullscreenTheme": return callbacks.Settings.FullscreenTheme ?? string.Empty;
            case "Settings.StartMinimized": return callbacks.Settings.StartMinimized.ToString();
            case "Settings.StartOnBoot": return callbacks.Settings.StartOnBoot.ToString();
            case "Settings.PlaytimeImportMode": return callbacks.Settings.PlaytimeImportMode.ToString();
            case "Settings.FontFamilyName": return callbacks.Settings.FontFamilyName ?? string.Empty;
            case "Settings.DiscordPresenceEnabled": return callbacks.Settings.DiscordPresenceEnabled.ToString();
            case "Settings.AgeRatingOrgPriority": return callbacks.Settings.AgeRatingOrgPriority.ToString();
            case "Settings.SidebarVisible": return callbacks.Settings.SidebarVisible.ToString();
            case "Settings.SidebarPosition": return callbacks.Settings.SidebarPosition.ToString();
            case "Settings.Fullscreen.IsMusicMuted": return callbacks.Settings.IsMusicMuted.ToString();
            case "Settings.Fullscreen.SetIsMusicMuted":
                callbacks.Settings.IsMusicMuted = bool.TryParse(payload, out var muted)
                    ? muted
                    : throw new InvalidDataException($"Invalid Boolean payload for {operation}.");
                return string.Empty;
            case "Settings.Fullscreen.SwapConfirmCancelButtons":
                return callbacks.Settings.SwapConfirmCancelButtons.ToString();
            case "Settings.Fullscreen.SwapStartDetailsAction":
                return callbacks.Settings.SwapStartDetailsAction.ToString();
            case "Settings.Fullscreen.GuideButtonFocus": return callbacks.Settings.GuideButtonFocus.ToString();
            case "Settings.CompletionStatus.Default":
                return database.GetCompletionStatusSettings().DefaultStatus.ToString();
            case "Settings.CompletionStatus.Played":
                return database.GetCompletionStatusSettings().PlayedStatus.ToString();
            case "Settings.GameExcludedFromImport":
                var exclusion = V7DatabaseTransport.Deserialize<GameImportExclusionPayload>(payload)
                    ?? throw new InvalidDataException("SDK v7 import-exclusion payload is empty.");
                return (database.ImportExclusions[ImportExclusionItem.GetId(
                    exclusion.GameId,
                    exclusion.LibraryId)] != null).ToString();
            case "ExpandGameVariables":
                var variables = V7DatabaseTransport.Deserialize<VariableExpansionPayload>(payload)
                    ?? throw new InvalidDataException("SDK v7 variable-expansion payload is empty.");
                return variables.Game?.ExpandVariables(
                    variables.Input,
                    emulatorDir: variables.EmulatorDirectory);
            case "ExpandGameActionVariables":
                var actionVariables = V7DatabaseTransport.Deserialize<VariableExpansionPayload>(payload)
                    ?? throw new InvalidDataException("SDK v7 action-expansion payload is empty.");
                return V7DatabaseTransport.Serialize(actionVariables.Action?.ExpandVariables(actionVariables.Game));
            case "ConnectedControllers":
                return V7DatabaseTransport.Serialize(callbacks.ConnectedControllers());
            case "StartGame":
                RunGameOperation(payload, (runner, game) => runner.Play(game));
                return string.Empty;
            case "InstallGame":
                RunGameOperation(payload, (runner, game) => runner.Install(game));
                return string.Empty;
            case "UninstallGame":
                RunGameOperation(payload, (runner, game) => runner.Uninstall(game));
                return string.Empty;
            case "AddCustomElementSupport":
                RegisterUiSupport(customElementRegistrations, payload);
                PluginElementRuntime.NotifyRegistrationsChanged();
                callbacks.SetStatus($"SDK v7 registered {operation}.");
                return string.Empty;
            case "AddSettingsSupport":
                callbacks.SetStatus($"SDK v7 registered {operation}.");
                return string.Empty;
            case "AddConvertersSupport":
                RegisterUiSupport(converterRegistrations, payload);
                callbacks.SetStatus($"SDK v7 registered {operation}.");
                return string.Empty;
            case "MarkdownToHtml":
                return new MarkupConverter().MarkdownToHtml(payload);
            default:
                throw new NotSupportedException($"SDK v7 host operation {operation} is not supported.");
        }
    }

    private static TEnum ParseEnum<TEnum>(string operation, string payload) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(payload, true, out var value)
            ? value
            : throw new InvalidDataException($"Invalid {typeof(TEnum).Name} payload for {operation}.");

    private static Guid ParseGuid(string operation, string payload) =>
        Guid.TryParse(payload, out var value)
            ? value
            : throw new InvalidDataException($"Invalid GUID payload for {operation}.");

    private void RegisterUriSource(string payload)
    {
        var registration = ParseUriRegistration(payload);
        if (string.Equals(registration.Source, "playnite", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The 'playnite' URI source is reserved.");
        }

        if (uriSources.ContainsKey(registration.Source))
        {
            throw new InvalidOperationException(
                $"URI source '{registration.Source}' is already registered.");
        }

        uriSources.Add(registration.Source, registration.OwnerToken);
    }

    private void RemoveUriSource(string payload)
    {
        var registration = ParseUriRegistration(payload);
        if (uriSources.TryGetValue(registration.Source, out var ownerToken) &&
            ownerToken == registration.OwnerToken)
        {
            uriSources.Remove(registration.Source);
        }
    }

    private static UriRegistrationPayload ParseUriRegistration(string payload)
    {
        var registration = JsonConvert.DeserializeObject<UriRegistrationPayload>(payload)
            ?? throw new InvalidDataException("SDK v7 URI registration payload is empty.");
        if (registration.OwnerToken == Guid.Empty || string.IsNullOrWhiteSpace(registration.Source))
        {
            throw new InvalidDataException("SDK v7 URI registration has no owner or source.");
        }

        return registration;
    }

    private List<PluginDescriptorPayload> GetLoadedPluginDescriptors()
    {
        var descriptors = new List<PluginDescriptorPayload>();
        if (pluginApi?.Addons?.Plugins != null)
        {
            descriptors.AddRange(pluginApi.Addons.Plugins.Select(plugin => new PluginDescriptorPayload
            {
                Id = plugin.Id,
                Kind = plugin switch
                {
                    LibraryPlugin => "LibraryPlugin",
                    MetadataPlugin => "MetadataPlugin",
                    GenericPlugin => "GenericPlugin",
                    _ => "Plugin"
                },
                Name = plugin switch
                {
                    LibraryPlugin library => library.Name,
                    MetadataPlugin metadata => metadata.Name,
                    _ => plugin.GetType().Name
                }
            }));
        }

        descriptors.AddRange(Plugins.Select(plugin => new PluginDescriptorPayload
        {
            Id = plugin.Id,
            Kind = plugin.Kind,
            Name = plugin.Name
        }));
        return descriptors
            .GroupBy(plugin => plugin.Id)
            .Select(group => group.First())
            .ToList();
    }

    private object HostObjectCall(string operation, string payload) => operation switch
    {
        "CurrentAppWindow" => callbacks.CurrentWindow?.Invoke()
            ?? throw new NotSupportedException(
                "The current Avalonia application does not expose its active window."),
        "Resource" => ResolveResource(payload),
        "CreateWebView" => webViews?.CreateV7View(
            JsonConvert.DeserializeObject<V7WebViewCreationPayload>(payload)
            ?? throw new InvalidDataException("SDK v7 web-view creation payload is empty."))
            ?? throw new InvalidOperationException("The Avalonia web-view factory is unavailable."),
        _ => throw new NotSupportedException($"SDK v7 object host operation {operation} is not supported.")
    };

    private object ResolveResource(string key) =>
        pluginLocalizationResources.TryGetValue(key, out var localizedValue)
            ? localizedValue
            : callbacks.ResolveResource?.Invoke(key);

    private object HostObjectRequest(string operation, object payload)
    {
        switch (operation)
        {
            case "MainView.OpenSearchContext":
                var searchArguments = payload as object[]
                    ?? throw new InvalidDataException(
                        "SDK v7 custom-search payload is not an object array.");
                if (searchArguments.Length != 2 || searchArguments[0] == null ||
                    searchArguments[1] is not string searchTerm)
                {
                    throw new InvalidDataException(
                        "SDK v7 custom-search payload has an invalid shape.");
                }
                callbacks.OpenSearchContext(
                    V7SearchContextAdapter.Create(searchArguments[0]),
                    searchTerm);
                return null;
            case "SQLite.Open":
                var openArguments = payload as object[]
                    ?? throw new InvalidDataException("SDK v7 SQLite open payload is not an object array.");
                if (openArguments.Length != 2 || openArguments[0] is not string databasePath ||
                    openArguments[1] is not int openFlags)
                {
                    throw new InvalidDataException("SDK v7 SQLite open payload has an invalid shape.");
                }
                var handle = Guid.NewGuid();
                lock (sqliteSync)
                {
                    sqliteConnections.Add(handle, new Sqlite(databasePath, (SqliteOpenFlags)openFlags));
                }
                return handle;
            case "SQLite.Query":
                var queryArguments = payload as object[]
                    ?? throw new InvalidDataException("SDK v7 SQLite query payload is not an object array.");
                if (queryArguments.Length != 4 || queryArguments[0] is not Guid queryHandle ||
                    queryArguments[1] is not string query || queryArguments[2] is not object[] arguments ||
                    queryArguments[3] is not Type resultType)
                {
                    throw new InvalidDataException("SDK v7 SQLite query payload has an invalid shape.");
                }
                lock (sqliteSync)
                {
                    if (!sqliteConnections.TryGetValue(queryHandle, out var connection))
                    {
                        throw new ObjectDisposedException(
                            nameof(Sqlite),
                            $"SDK v7 SQLite handle {queryHandle} is not open.");
                    }
                    var queryMethod = typeof(Sqlite).GetMethod(nameof(Sqlite.Query))
                        ?? throw new MissingMethodException(typeof(Sqlite).FullName, nameof(Sqlite.Query));
                    try
                    {
                        return queryMethod.MakeGenericMethod(resultType).Invoke(
                            connection,
                            [query, arguments]);
                    }
                    catch (TargetInvocationException exception) when (exception.InnerException != null)
                    {
                        ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                        throw;
                    }
                }
            case "SQLite.Dispose":
                if (payload is not Guid disposeHandle)
                {
                    throw new InvalidDataException("SDK v7 SQLite dispose payload is not a GUID.");
                }
                lock (sqliteSync)
                {
                    if (sqliteConnections.Remove(disposeHandle, out var disposedConnection))
                    {
                        disposedConnection.Dispose();
                    }
                }
                return null;
            default:
                throw new NotSupportedException(
                    $"SDK v7 host request operation {operation} is not supported.");
        }
    }

    private void DisposeSqliteConnections(Func<Guid, bool> predicate)
    {
        lock (sqliteSync)
        {
            foreach (var handle in sqliteConnections.Keys.Where(predicate).ToList())
            {
                sqliteConnections.Remove(handle, out var connection);
                connection.Dispose();
            }
        }
    }

    internal Control ResolvePluginElement(string sourceName, string elementName, Game game)
    {
        var registration = customElementRegistrations.LastOrDefault(item =>
            string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase) &&
            item.ElementList?.Contains(elementName, StringComparer.Ordinal) == true);
        var plugin = registration == null
            ? null
            : Plugins.FirstOrDefault(item => item.Id == registration.PluginId);
        var instance = plugin?.CreateGameViewControl(
            elementName,
            callbacks.Mode,
            V7DatabaseTransport.Serialize(game));
        return instance == null ? null : new V7RemotePluginElementHost(instance);
    }

    internal IValueConverter ResolveConverter(string sourceName, string converterName)
    {
        var registration = converterRegistrations.LastOrDefault(item =>
            string.Equals(item.SourceName, sourceName, StringComparison.OrdinalIgnoreCase) &&
            item.ConverterNames?.Contains(converterName, StringComparer.Ordinal) == true);
        var plugin = registration == null
            ? null
            : Plugins.FirstOrDefault(item => item.Id == registration.PluginId);
        return plugin?.GetConverter(sourceName, converterName);
    }

    internal IReadOnlyList<AvaloniaPluginSidebarItem> GetSidebarItems()
    {
        var result = new List<AvaloniaPluginSidebarItem>();
        foreach (var plugin in Plugins)
        {
            try
            {
                result.AddRange(plugin.GetSidebarItems()
                    .Select(instance => new AvaloniaPluginSidebarItem(plugin.Id, plugin.Name, instance)));
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} sidebar discovery failed: {exception.Message}");
            }
        }

        return result
            .OrderByDescending(item => item.IsView)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal IReadOnlyList<AvaloniaPluginTopPanelItem> GetTopPanelItems()
    {
        var result = new List<AvaloniaPluginTopPanelItem>();
        foreach (var plugin in Plugins)
        {
            try
            {
                result.AddRange(plugin.GetTopPanelItems()
                    .Select(instance => new AvaloniaPluginTopPanelItem(plugin.Id, plugin.Name, instance)));
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} top-panel discovery failed: {exception.Message}");
            }
        }

        return result
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void RegisterUiSupport(
        List<UiRegistrationPayload> registrations,
        string payload)
    {
        var registration = JsonConvert.DeserializeObject<UiRegistrationPayload>(payload)
            ?? throw new InvalidDataException("SDK v7 UI registration payload is empty.");
        if (registration.PluginId == Guid.Empty || string.IsNullOrWhiteSpace(registration.SourceName))
        {
            throw new InvalidDataException("SDK v7 UI registration has no plugin or source identity.");
        }

        registrations.RemoveAll(item => item.PluginId == registration.PluginId);
        registrations.Add(registration);
    }

    private void AddNotification(NotificationPayload payload)
    {
        if (payload == null || payload.OwnerToken == Guid.Empty || string.IsNullOrWhiteSpace(payload.Id))
        {
            throw new InvalidDataException("SDK v7 notification payload has no owner or notification ID.");
        }

        var type = Enum.TryParse<NotificationType>(payload.Type, true, out var notificationType)
            ? notificationType
            : throw new InvalidDataException(
                $"SDK v7 notification payload has invalid type '{payload.Type}'.");
        if (notificationOwners.TryGetValue(payload.Id, out var existingOwner))
        {
            if (existingOwner != payload.OwnerToken)
            {
                throw new InvalidOperationException(
                    $"SDK v7 notification ID '{payload.Id}' is already owned by another plugin context.");
            }
            RemoveTrackedNotification(payload.Id);
        }
        else if (notifications.Messages.Any(message => message.Id == payload.Id))
        {
            throw new InvalidOperationException(
                $"SDK v7 notification ID '{payload.Id}' collides with a host notification.");
        }

        if (payload.ActionToken == Guid.Empty)
        {
            notifications.Add(payload.Id, payload.Text ?? string.Empty, type);
        }
        else
        {
            notifications.Add(new NotificationMessage(
                payload.Id,
                payload.Text ?? string.Empty,
                type,
                () => InvokeNotificationAction(payload.OwnerToken, payload.ActionToken, payload.Id)));
        }

        notificationOwners.Add(payload.Id, payload.OwnerToken);
        if (!notificationIdsByOwner.TryGetValue(payload.OwnerToken, out var ownerIds))
        {
            ownerIds = [];
            notificationIdsByOwner.Add(payload.OwnerToken, ownerIds);
        }
        ownerIds.Add(payload.Id);
    }

    private void RemoveNotification(NotificationMutationPayload payload)
    {
        if (payload == null || payload.OwnerToken == Guid.Empty || string.IsNullOrWhiteSpace(payload.Id))
        {
            throw new InvalidDataException("SDK v7 notification removal payload is incomplete.");
        }

        if (notificationOwners.TryGetValue(payload.Id, out var ownerToken) &&
            ownerToken == payload.OwnerToken)
        {
            RemoveTrackedNotification(payload.Id);
        }
    }

    private void RemoveNotifications(NotificationMutationPayload payload)
    {
        if (payload == null || payload.OwnerToken == Guid.Empty)
        {
            throw new InvalidDataException("SDK v7 notification removal payload has no owner.");
        }

        if (!notificationIdsByOwner.TryGetValue(payload.OwnerToken, out var notificationIds))
        {
            return;
        }
        foreach (var notificationId in notificationIds.ToList())
        {
            RemoveTrackedNotification(notificationId);
        }
    }

    private void InvokeNotificationAction(Guid ownerToken, Guid actionToken, string notificationId)
    {
        try
        {
            if (!uriOwners.TryGetValue(ownerToken, out var plugin))
            {
                throw new InvalidOperationException(
                    $"SDK v7 notification '{notificationId}' has no live plugin context.");
            }
            plugin.InvokeNotificationAction(actionToken);
        }
        finally
        {
            ForgetNotification(notificationId);
        }
    }

    private void RemoveTrackedNotification(string notificationId)
    {
        notifications.Remove(notificationId);
        ForgetNotification(notificationId);
    }

    private void ForgetNotification(string notificationId)
    {
        if (!notificationOwners.Remove(notificationId, out var ownerToken) ||
            !notificationIdsByOwner.TryGetValue(ownerToken, out var ownerIds))
        {
            return;
        }
        ownerIds.Remove(notificationId);
        if (ownerIds.Count == 0)
        {
            notificationIdsByOwner.Remove(ownerToken);
        }
    }

    private static void WritePluginLog(LogPayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.LoggerName) ||
            string.IsNullOrWhiteSpace(payload.Level))
        {
            throw new InvalidDataException("SDK v7 log payload is incomplete.");
        }

        var message = $"[SDK v7:{payload.LoggerName}] {payload.Message ?? string.Empty}";
        if (!string.IsNullOrWhiteSpace(payload.Exception))
        {
            message += Environment.NewLine + payload.Exception;
        }

        switch (payload.Level)
        {
            case "Trace": logger.Trace(message); break;
            case "Debug": logger.Debug(message); break;
            case "Info": logger.Info(message); break;
            case "Warn": logger.Warn(message); break;
            case "Error": logger.Error(message); break;
            default:
                throw new InvalidDataException(
                    $"SDK v7 log payload has unknown level '{payload.Level}'.");
        }
    }

    private string ShowDialog(string payload, bool error)
    {
        var dialog = JsonConvert.DeserializeObject<DialogPayload>(payload)
            ?? throw new InvalidDataException("SDK v7 message-dialog payload is empty.");
        var options = error ? new List<string> { "OK" } : GetDialogOptions(dialog.Buttons);
        return callbacks.Dialogs.ShowMessage(
            dialog.Message ?? string.Empty,
            dialog.Caption ?? (error ? "Error" : "Playnite"),
            options,
            0,
            options.FindIndex(option => option == "Cancel"));
    }

    private string ShowChoiceDialog(string payload)
    {
        var dialog = JsonConvert.DeserializeObject<ChoiceDialogPayload>(payload)
            ?? throw new InvalidDataException("SDK v7 choice-dialog payload is empty.");
        if (dialog.Choices == null || dialog.Choices.Count == 0)
        {
            throw new InvalidDataException("SDK v7 choice dialog has no choices.");
        }

        if (dialog.DefaultChoice < 0 || dialog.DefaultChoice >= dialog.Choices.Count)
        {
            throw new InvalidDataException("SDK v7 choice dialog has an invalid default choice index.");
        }

        if (dialog.CancelChoice < -1 || dialog.CancelChoice >= dialog.Choices.Count)
        {
            throw new InvalidDataException("SDK v7 choice dialog has an invalid cancel choice index.");
        }

        return callbacks.Dialogs.ShowMessage(
            dialog.Message ?? string.Empty,
            dialog.Caption ?? "Playnite",
            dialog.Choices,
            dialog.DefaultChoice,
            dialog.CancelChoice);
    }

    private static List<string> GetDialogOptions(string buttons) => buttons switch
    {
        "OK" => ["OK"],
        "OKCancel" => ["OK", "Cancel"],
        "YesNo" => ["Yes", "No"],
        "YesNoCancel" => ["Yes", "No", "Cancel"],
        _ => throw new InvalidDataException($"SDK v7 message dialog has invalid buttons '{buttons}'.")
    };

    private void RunGameOperation(
        string payload,
        Func<GameActionRunner, Playnite.SDK.Models.Game, GameOperationResult> operation)
    {
        if (!Guid.TryParse(payload, out var gameId) || database.Games[gameId] == null)
        {
            callbacks.SetStatus($"Game {payload} was not found.");
            return;
        }

        var result = operation(actionRunner(), database.Games[gameId]);
        if (!result.Success)
        {
            callbacks.SetStatus(result.Message);
        }
    }

    private static string GetModulePath(ExtensionManifest manifest) =>
        string.IsNullOrWhiteSpace(manifest?.Module) || string.IsNullOrWhiteSpace(manifest.DirectoryPath)
            ? string.Empty
            : Path.GetFullPath(Path.Combine(manifest.DirectoryPath, manifest.Module));

    private void VerifyHostBundle()
    {
        foreach (var fileName in new[] { "Playnite.SDK.V7.Host.dll", "Playnite.SDK.dll" })
        {
            var path = Path.Combine(hostBundlePath, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"SDK v7 host bundle is missing {fileName}.", path);
            }
        }
    }

    public IEnumerable<PlayController> GetPlayControllers(Playnite.SDK.Models.Game game) =>
        GetControllers<V7PlayControllerAdapter, PlayController>("Play", game);

    public IEnumerable<InstallController> GetInstallControllers(Playnite.SDK.Models.Game game) =>
        GetControllers<V7InstallControllerAdapter, InstallController>("Install", game);

    public IEnumerable<UninstallController> GetUninstallControllers(Playnite.SDK.Models.Game game) =>
        GetControllers<V7UninstallControllerAdapter, UninstallController>("Uninstall", game);

    private IEnumerable<TController> GetControllers<TAdapter, TController>(
        string kind,
        Playnite.SDK.Models.Game game)
        where TAdapter : TController, IV7ControllerAdapter
        where TController : ControllerBase
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var result = new List<TController>();
        foreach (var plugin in Plugins)
        {
            if (kind == "Play" && !game.IncludeLibraryPluginAction && plugin.Id == game.PluginId)
            {
                continue;
            }

            foreach (var instance in plugin.GetControllers(kind, V7DatabaseTransport.Serialize(game)))
            {
                var remote = new V7RemoteController(instance);
                if (!string.Equals(remote.Kind, kind, StringComparison.Ordinal))
                {
                    remote.Dispose();
                    throw new InvalidDataException(
                        $"SDK v7 plugin {plugin.Name} returned {remote.Kind} for a {kind} action request.");
                }

                TController adapter = kind switch
                {
                    "Play" => (TController)(ControllerBase)new V7PlayControllerAdapter(
                        game, remote, UnregisterController),
                    "Install" => (TController)(ControllerBase)new V7InstallControllerAdapter(
                        game, remote, UnregisterController),
                    "Uninstall" => (TController)(ControllerBase)new V7UninstallControllerAdapter(
                        game, remote, UnregisterController),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown controller kind.")
                };
                var v7Adapter = (IV7ControllerAdapter)(object)adapter;
                if (!controllerAdapters.TryAdd(v7Adapter.Token, v7Adapter))
                {
                    adapter.Dispose();
                    throw new InvalidDataException($"SDK v7 controller token {v7Adapter.Token} is duplicated.");
                }
                result.Add(adapter);
            }
        }
        return result;
    }

    private void HandleControllerEvent(string payload)
    {
        var data = JObject.Parse(payload);
        var token = data["Token"]?.ToObject<Guid>() ?? Guid.Empty;
        if (token == Guid.Empty || !controllerAdapters.TryGetValue(token, out var adapter))
        {
            throw new InvalidDataException($"SDK v7 controller event references unknown token {token}.");
        }
        adapter.Dispatch(data.Value<string>("Event"), data["Data"] as JObject);
    }

    private void UnregisterController(Guid token) => controllerAdapters.TryRemove(token, out _);

    private void SubscribeDatabaseEvents()
    {
        EventHandler opened = (_, _) => PublishDatabaseEvent("Database", "Opened", "null");
        database.DatabaseOpened += opened;
        unsubscribeEvents.Add(() => database.DatabaseOpened -= opened);
        SubscribeCollectionEvents(GameDatabaseCollection.Games, database.Games);
        SubscribeCollectionEvents(GameDatabaseCollection.Platforms, database.Platforms);
        SubscribeCollectionEvents(GameDatabaseCollection.Emulators, database.Emulators);
        SubscribeCollectionEvents(GameDatabaseCollection.Genres, database.Genres);
        SubscribeCollectionEvents(GameDatabaseCollection.Companies, database.Companies);
        SubscribeCollectionEvents(GameDatabaseCollection.Tags, database.Tags);
        SubscribeCollectionEvents(GameDatabaseCollection.Categories, database.Categories);
        SubscribeCollectionEvents(GameDatabaseCollection.Series, database.Series);
        SubscribeCollectionEvents(GameDatabaseCollection.AgeRatings, database.AgeRatings);
        SubscribeCollectionEvents(GameDatabaseCollection.Regions, database.Regions);
        SubscribeCollectionEvents(GameDatabaseCollection.Sources, database.Sources);
        SubscribeCollectionEvents(GameDatabaseCollection.Features, database.Features);
        SubscribeCollectionEvents(GameDatabaseCollection.GameScanners, database.GameScanners);
        SubscribeCollectionEvents(GameDatabaseCollection.CompletionStatuses, database.CompletionStatuses);
        SubscribeCollectionEvents(GameDatabaseCollection.ImportExclusions, database.ImportExclusions);
        SubscribeCollectionEvents(GameDatabaseCollection.FilterPresets, database.FilterPresets);
    }

    private void SubscribeCollectionEvents<TItem>(
        GameDatabaseCollection collectionType,
        IItemCollection<TItem> collection)
        where TItem : Playnite.SDK.Models.DatabaseObject
    {
        EventHandler<ItemCollectionChangedEventArgs<TItem>> changed = (_, args) =>
            PublishDatabaseEvent(collectionType.ToString(), "Changed", V7DatabaseTransport.Serialize(new
            {
                args.AddedItems,
                args.RemovedItems
            }));
        EventHandler<ItemUpdatedEventArgs<TItem>> updated = (_, args) =>
            PublishDatabaseEvent(collectionType.ToString(), "Updated", V7DatabaseTransport.Serialize(new
            {
                args.UpdatedItems
            }));
        collection.ItemCollectionChanged += changed;
        collection.ItemUpdated += updated;
        unsubscribeEvents.Add(() =>
        {
            collection.ItemCollectionChanged -= changed;
            collection.ItemUpdated -= updated;
        });
    }

    private void PublishDatabaseEvent(string collection, string eventName, string payload)
    {
        foreach (var plugin in Plugins.ToList())
        {
            try
            {
                plugin.PublishDatabaseEvent(collection, eventName, payload);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} database event failed: {exception.Message}");
            }
        }
    }

    public void NotifyLibraryUpdated()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (var plugin in Plugins.ToList())
        {
            try
            {
                plugin.InvokeLibraryUpdated();
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} library-updated event failed: {exception.Message}");
            }
        }
    }

    public void NotifyControllerButtonStateChanged(int input, int state)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (var plugin in Plugins.ToList())
        {
            try
            {
                plugin.InvokeControllerButtonStateChanged(input, state);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} controller event failed: {exception.Message}");
            }
        }
    }

    public void NotifyControllerConnection(
        bool connected,
        int instanceId,
        string path,
        string name,
        bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (var plugin in Plugins.ToList())
        {
            try
            {
                plugin.InvokeControllerConnection(connected, instanceId, path, name, enabled);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} controller connection event failed: {exception.Message}");
            }
        }
    }

    private void SubscribeControllerEvents()
    {
        EventHandler<OnGameStartingEventArgs> starting = (_, args) =>
        {
            startingEventData[args.Game.Id] = JObject.Parse(V7DatabaseTransport.Serialize(new
            {
                args.SourceAction,
                args.SelectedRomFile
            }));
            var result = PublishGameEvent("Starting", new
            {
                args.Game,
                args.SourceAction,
                args.SelectedRomFile
            }, true);
            args.CancelStartup |= result;
        };
        EventHandler<GameStartedEventArgs> started = (_, args) =>
            PublishControllerGameEvent("Started", args.Source?.Game, new
            {
                args.StartedProcessId
            });
        EventHandler<GameStoppedEventArgs> stopped = (_, args) =>
        {
            PublishControllerGameEvent("Stopped", args.Source?.Game, new
            {
                args.SessionLength,
                ElapsedSeconds = args.SessionLength,
                ManuallyStopped = false
            });
            if (args.Source?.Game != null)
            {
                startingEventData.TryRemove(args.Source.Game.Id, out var removedStartingData);
            }
        };
        EventHandler<GameInstalledEventArgs> installed = (_, args) =>
            PublishControllerGameEvent("Installed", args.Source?.Game);
        EventHandler<GameInstallationCancelledEventArgs> installCancelled = (_, args) =>
            PublishControllerGameEvent("InstallCancelled", args.Source?.Game);
        EventHandler<GameUninstalledEventArgs> uninstalled = (_, args) =>
            PublishControllerGameEvent("Uninstalled", args.Source?.Game);
        EventHandler<OnGameStartupCancelledEventArgs> startupCancelled = (_, args) =>
        {
            PublishGameEvent("StartupCancelled", new { args.Game });
            startingEventData.TryRemove(args.Game.Id, out var removedStartingData);
        };

        controllers.Starting += starting;
        controllers.Started += started;
        controllers.Stopped += stopped;
        controllers.Installed += installed;
        controllers.InstallationCancelled += installCancelled;
        controllers.Uninstalled += uninstalled;
        controllers.StartupCancelled += startupCancelled;
        unsubscribeEvents.Add(() =>
        {
            controllers.Starting -= starting;
            controllers.Started -= started;
            controllers.Stopped -= stopped;
            controllers.Installed -= installed;
            controllers.InstallationCancelled -= installCancelled;
            controllers.Uninstalled -= uninstalled;
            controllers.StartupCancelled -= startupCancelled;
        });
    }

    private void PublishControllerGameEvent(string eventName, Playnite.SDK.Models.Game source, object data = null)
    {
        if (source == null)
        {
            return;
        }
        var game = database.Games[source.Id] ?? source;
        var payload = data == null
            ? new JObject()
            : JObject.Parse(V7DatabaseTransport.Serialize(data));
        if (startingEventData.TryGetValue(source.Id, out var startingData))
        {
            foreach (var property in startingData.Properties())
            {
                if (payload[property.Name] == null)
                {
                    payload[property.Name] = property.Value.DeepClone();
                }
            }
        }
        payload["Game"] = JToken.Parse(V7DatabaseTransport.Serialize(game));
        PublishGameEvent(eventName, payload);
    }

    private bool PublishGameEvent(string eventName, object data, bool stopOnCancellation = false)
    {
        var cancelled = false;
        var payload = V7DatabaseTransport.Serialize(data);
        foreach (var plugin in Plugins.ToList())
        {
            try
            {
                var response = plugin.InvokeGameEvent(eventName, payload);
                if (!string.IsNullOrWhiteSpace(response) && response != "null")
                {
                    cancelled |= JObject.Parse(response).Value<bool>("CancelStartup");
                }
                if (cancelled && stopOnCancellation)
                {
                    break;
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus($"SDK v7 plugin {plugin.Name} {eventName} event failed: {exception.Message}");
            }
        }
        return cancelled;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var unsubscribe in unsubscribeEvents)
        {
            unsubscribe();
        }
        unsubscribeEvents.Clear();
        foreach (var adapter in controllerAdapters.Values.ToList())
        {
            if (adapter is PlayController play && controllers.PlayControllers.Contains(play))
            {
                controllers.RemoveController(play);
            }
            else if (adapter is InstallController install && controllers.InstallControllers.Contains(install))
            {
                controllers.RemoveController(install);
            }
            else if (adapter is UninstallController uninstall && controllers.UninstallControllers.Contains(uninstall))
            {
                controllers.RemoveController(uninstall);
            }
            else if (adapter is ControllerBase controller)
            {
                controller.Dispose();
            }
        }
        controllerAdapters.Clear();
        foreach (var handle in handles.AsEnumerable().Reverse())
        {
            foreach (var plugin in handle.Plugins.AsEnumerable().Reverse())
            {
                plugin.InvokeApplicationStopped();
                plugin.Dispose();
            }
            handle.Context.Unload();
        }

        handles.Clear();
        DisposeSqliteConnections(_ => true);
        foreach (var notificationId in notificationOwners.Keys.ToList())
        {
            RemoveTrackedNotification(notificationId);
        }
        notificationIdsByOwner.Clear();
        uriSources.Clear();
        uriOwners.Clear();
        LibraryPlugins.Clear();
        MetadataPlugins.Clear();
        Plugins.Clear();
        PluginResourceRegistry.Clear();
    }
}
