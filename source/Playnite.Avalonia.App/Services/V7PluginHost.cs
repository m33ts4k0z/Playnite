using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.API;
using Playnite.Common;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK;
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
    private readonly MethodInfo openLibraryClient;
    private readonly MethodInfo shutdownLibraryClient;
    private readonly MethodInfo createMetadataProvider;
    private readonly MethodInfo createLibraryMetadataProvider;
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
        applicationStarted = GetRequiredMethod(type, "InvokeApplicationStarted");
        applicationStopped = GetRequiredMethod(type, "InvokeApplicationStopped");
        publishDatabaseEvent = GetRequiredMethod(type, "PublishDatabaseEvent");
        getControllers = GetRequiredMethod(type, "GetControllers");
        invokeGameEvent = GetRequiredMethod(type, "InvokeGameEvent");
        getLibraryGames = GetRequiredMethod(type, "GetLibraryGames");
        importLibraryGames = GetRequiredMethod(type, "ImportLibraryGames");
        invokeLibraryUpdated = GetRequiredMethod(type, "InvokeLibraryUpdated");
        openLibraryClient = GetRequiredMethod(type, "OpenLibraryClient");
        shutdownLibraryClient = GetRequiredMethod(type, "ShutdownLibraryClient");
        createMetadataProvider = GetRequiredMethod(type, "CreateMetadataProvider");
        createLibraryMetadataProvider = GetRequiredMethod(type, "CreateLibraryMetadataProvider");
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
            (string)InvokeWithResult(getLibraryGames, cancellationToken)) ?? [];
    internal List<Game> ImportLibraryGames(CancellationToken cancellationToken) =>
        V7DatabaseTransport.Deserialize<List<Game>>(
            (string)InvokeWithResult(importLibraryGames, cancellationToken)) ?? [];
    internal void InvokeLibraryUpdated() => Invoke(invokeLibraryUpdated);
    internal void OpenLibraryClient() => Invoke(openLibraryClient);
    internal void ShutdownLibraryClient() => Invoke(shutdownLibraryClient);
    internal object CreateMetadataProvider(string gameJson, bool backgroundDownload) =>
        InvokeWithResult(createMetadataProvider, gameJson, backgroundDownload);
    internal object CreateLibraryMetadataProvider() =>
        InvokeWithResult(createLibraryMetadataProvider);

    private T ReadProperty<T>(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(type.FullName, name);
        return (T)property.GetValue(instance);
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
        public string Id { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
    }

    private sealed class DialogPayload
    {
        public string Message { get; set; }
        public string Caption { get; set; }
        public string Buttons { get; set; }
    }

    private readonly GameDatabase database;
    private readonly AvaloniaHostCallbacks callbacks;
    private readonly GameControllerFactory controllers;
    private readonly NotificationsAPI notifications;
    private readonly Func<GameActionRunner> actionRunner;
    private readonly Func<IEnumerable<string>> installedAddons;
    private readonly IPlayniteAPI pluginApi;
    private readonly V7DatabaseTransport databaseTransport;
    private readonly string hostBundlePath;
    private readonly List<PluginLoadHandle> handles = [];
    private readonly List<Action> unsubscribeEvents = [];
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
        IPlayniteAPI pluginApi = null)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
        this.controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        this.actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
        this.installedAddons = installedAddons ?? throw new ArgumentNullException(nameof(installedAddons));
        this.pluginApi = pluginApi;
        this.hostBundlePath = hostBundlePath ?? Path.Combine(AppContext.BaseDirectory, "SdkV7Host");
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
        try
        {
            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new InvalidDataException($"SDK v7 extension {manifest.Name} has no manifest ID.");
            }

            VerifyHostBundle();
            context = new V7PluginLoadContext(modulePath, hostBundlePath);
            var bridgeAssembly = context.LoadFromAssemblyPath(
                Path.Combine(hostBundlePath, "Playnite.SDK.V7.Host.dll"));
            var bridge = bridgeAssembly.GetType("Playnite.SDK.V7.Host.V7PluginBridge", true);
            var loadAll = bridge.GetMethod("LoadAll", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(bridge.FullName, "LoadAll");
            Func<string, string, string> hostCall = HostCall;
            var instances = (object[])loadAll.Invoke(null, [modulePath, hostCall]);
            constructedPlugins.AddRange(instances.Select(instance => new V7LoadedPlugin(instance, manifest)));
            if (constructedPlugins.Count == 0)
            {
                throw new InvalidDataException($"SDK v7 extension {manifest.Name} contains no plugin types.");
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
            case "ResourceString":
                return AvaloniaPluginApi.SharedResources.GetString(payload);
            case "NotificationAdd":
                AddNotification(JsonConvert.DeserializeObject<NotificationPayload>(payload));
                return string.Empty;
            case "NotificationRemove":
                notifications.Remove(payload);
                return string.Empty;
            case "NotificationRemoveAll":
                notifications.RemoveAll();
                return string.Empty;
            case "ShowError":
                ShowDialog(payload, true);
                return "OK";
            case "ShowMessage":
                return ShowDialog(payload, false);
            case "OpenPluginSettings":
                return Guid.TryParse(payload, out var pluginId) && callbacks.OpenPluginSettings(pluginId)
                    ? bool.TrueString
                    : bool.FalseString;
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
            case "AddSettingsSupport":
            case "AddConvertersSupport":
                callbacks.SetStatus($"SDK v7 registered {operation}.");
                return string.Empty;
            case "MarkdownToHtml":
                return new MarkupConverter().MarkdownToHtml(payload);
            default:
                return string.Empty;
        }
    }

    private void AddNotification(NotificationPayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Id))
        {
            return;
        }

        var type = Enum.TryParse<NotificationType>(payload.Type, true, out var notificationType)
            ? notificationType
            : NotificationType.Info;
        notifications.Add(payload.Id, payload.Text ?? string.Empty, type);
    }

    private string ShowDialog(string payload, bool error)
    {
        var dialog = JsonConvert.DeserializeObject<DialogPayload>(payload) ?? new DialogPayload();
        var options = error ? new List<string> { "OK" } : GetDialogOptions(dialog.Buttons);
        return callbacks.Dialogs.ShowMessage(
            dialog.Message ?? string.Empty,
            dialog.Caption ?? (error ? "Error" : "Playnite"),
            options,
            0,
            options.FindIndex(option => option == "Cancel"));
    }

    private static List<string> GetDialogOptions(string buttons) => buttons switch
    {
        "OKCancel" => ["OK", "Cancel"],
        "YesNo" => ["Yes", "No"],
        "YesNoCancel" => ["Yes", "No", "Cancel"],
        _ => ["OK"]
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
        LibraryPlugins.Clear();
        MetadataPlugins.Clear();
        Plugins.Clear();
    }
}
