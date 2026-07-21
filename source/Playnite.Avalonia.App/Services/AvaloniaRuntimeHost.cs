using Playnite.API;
using Playnite.Avalonia.Controls;
using Playnite.Avalonia.Markup;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.Avalonia.App.Services;

public sealed partial class AvaloniaRuntimeHost : IDisposable
{
    private List<string> externalExtensionDirectories = new();
    private readonly AvaloniaHostCallbacks callbacks;
    private readonly GameDatabase database;
    private readonly GameControllerFactory controllers;
    private readonly ExtensionFactory extensions;
    private readonly GameActionRunner actionRunner;
    private readonly V7PluginHost v7Plugins;
    private readonly NotificationsAPI notifications;
    private readonly AvaloniaWebViewFactory webViews;
    private readonly IPluginCompatibilityHost pluginCompatibility;
    private readonly IPlayniteAPI globalApi;
    private readonly Func<WebViewSettings, IWebView> previousOffscreenWebViewFactory;
    private readonly Func<WebViewSettings, IWebView> offscreenWebViewFactory;
    private readonly Func<string, string, global::Avalonia.Data.Converters.IValueConverter> pluginConverterResolver;
    private readonly Func<string, string, object, global::Avalonia.Controls.Control> pluginElementResolver;
    private readonly IResourceProvider previousResourceProvider;

    public GameActionRunner Actions => actionRunner;
    public ExtensionFactory Extensions => extensions;
    public NotificationsAPI Notifications => notifications;
    public IAvaloniaDialogService Dialogs => callbacks.Dialogs;
    public Version V7SdkVersion => v7Plugins.SdkVersion;
    public IPlayniteAPI PluginApi => globalApi;
    public IReadOnlyList<V7LoadedPlugin> V7Plugins => v7Plugins.Plugins;
    public IReadOnlyList<V7PluginLoadFailure> V7PluginFailures => v7Plugins.FailedPlugins;
    public IReadOnlyList<LibraryPlugin> LibraryPlugins =>
        extensions.LibraryPlugins.Concat(v7Plugins.LibraryPlugins).ToList();
    public IReadOnlyList<MetadataPlugin> MetadataPlugins =>
        extensions.MetadataPlugins.Concat(v7Plugins.MetadataPlugins).ToList();
    public IReadOnlyList<AvaloniaPluginSidebarItem> PluginSidebarItems => v7Plugins.GetSidebarItems();
    public IReadOnlyList<AvaloniaPluginTopPanelItem> PluginTopPanelItems => v7Plugins.GetTopPanelItems();
    public int LoadedPluginCount => extensions.Plugins.Count + v7Plugins.Plugins.Count;
    public int FailedPluginCount => extensions.FailedExtensions.Count + v7Plugins.FailedPlugins.Count;

    public AvaloniaRuntimeHost(GameDatabase database, AvaloniaHostCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.database = database;
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        ArgumentNullException.ThrowIfNull(callbacks.Settings);
        ArgumentNullException.ThrowIfNull(callbacks.Dialogs);

        GameDatabase.ExpandGameVariables = (game, input, fixSeparators, emulatorDirectory) =>
            game.ExpandVariables(input, fixSeparators, emulatorDirectory);

        notifications = new NotificationsAPI();
        notifications.ActivationRequested += (_, args) =>
        {
            if (args.Message == null)
            {
                return;
            }
            try
            {
                args.Message.ActivationAction?.Invoke();
            }
            finally
            {
                notifications.Remove(args.Message.Id);
            }
        };
        notifications.CloseRequested += (_, args) =>
        {
            if (args.Message != null)
            {
                notifications.Remove(args.Message.Id);
            }
        };
        controllers = new GameControllerFactory(database);
        webViews = new AvaloniaWebViewFactory();
        previousOffscreenWebViewFactory = GoogleImageDownloader.CreateOffscreenView;
        offscreenWebViewFactory = webViews.CreateOffscreenView;
        GoogleImageDownloader.CreateOffscreenView = offscreenWebViewFactory;

        GameActionRunner runner = null;
        ExtensionFactory factory = null;
        IPlayniteAPI CreateApi() => new AvaloniaPluginApi(
            database,
            notifications,
            () => runner,
            () => factory,
            callbacks,
            webViews);

        pluginCompatibility = PluginCompatibilityHost.Create();
        pluginCompatibility.Initialize();
        extensions = factory = new ExtensionFactory(
            database,
            controllers,
            _ => CreateApi(),
            extensionDirectory => pluginCompatibility.LoadPluginResources(
                extensionDirectory,
                callbacks.Settings.Language));
        var actionPolicy = new GameActionRunnerPolicy
        {
            GlobalPreScript = () => callbacks.Settings.GlobalPreScript,
            GlobalGameStartedScript = () => callbacks.Settings.GlobalGameStartedScript,
            GlobalPostScript = () => callbacks.Settings.GlobalPostScript,
            ShutdownClients = () => callbacks.Settings.ShutdownLibraryClients,
            ClientShutdownGraceSeconds = () => callbacks.Settings.ClientShutdownGraceSeconds,
            ClientShutdownMinimumSessionSeconds = () =>
                callbacks.Settings.ClientShutdownMinimumSessionSeconds,
            ClientShutdownPluginIds = () => callbacks.Settings.ClientShutdownPluginIds,
            AdditionalLibraryPlugins = () => v7Plugins?.LibraryPlugins ?? [],
            AdditionalPlayControllers = game => v7Plugins?.GetPlayControllers(game) ?? [],
            AdditionalInstallControllers = game => v7Plugins?.GetInstallControllers(game) ?? [],
            AdditionalUninstallControllers = game => v7Plugins?.GetUninstallControllers(game) ?? []
        };
        actionRunner = runner = new GameActionRunner(
            database,
            controllers,
            extensions,
            () => globalApi,
            actionPolicy);
        globalApi = CreateApi();
        v7Plugins = new V7PluginHost(
            database,
            controllers,
            callbacks,
            notifications,
            () => actionRunner,
            () => extensions.Plugins.Keys.Select(id => id.ToString())
                .Concat(v7Plugins?.Plugins.Select(plugin => plugin.Id.ToString()) ?? []),
            pluginApi: globalApi,
            webViews: webViews);
        previousResourceProvider = pluginCompatibility.InstallResourceProvider(globalApi.Resources);
        pluginConverterResolver = (pluginSource, converterName) =>
            v7Plugins.ResolveConverter(pluginSource, converterName) ??
            pluginCompatibility.ResolveConverter(extensions, pluginSource, converterName);
        PluginConverterRuntime.Resolver = pluginConverterResolver;
        pluginElementResolver = (pluginSource, elementName, gameContext) =>
        {
            try
            {
                return v7Plugins.ResolvePluginElement(
                    pluginSource,
                    elementName,
                    gameContext as Game) ?? pluginCompatibility.CreateElement(
                    extensions,
                    callbacks.Mode,
                    pluginSource,
                    elementName,
                    gameContext);
            }
            catch (Exception exception)
            {
                var message = $"Plugin element {pluginSource}_{elementName} failed: {exception.Message}";
                ShowMessage(message, true);
                return new global::Avalonia.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    Foreground = global::Avalonia.Media.Brushes.IndianRed
                };
            }
        };
        PluginElementRuntime.Resolver = pluginElementResolver;

        actionRunner.StatusChanged += (_, message) => callbacks.SetStatus(message);
        actionRunner.OperationFailed += (_, message) => ShowMessage(message, true);
        actionRunner.GameStateChanged += (_, game) => callbacks.RefreshGame(game.Id);
        GameControllerDialogs.ShowError = (message, caption) => ShowMessage(
            string.IsNullOrWhiteSpace(caption) ? message : $"{caption}: {message}",
            true);
    }

    public void InitializePlugins(
        bool loadUserPlugins,
        IReadOnlyList<string> externalExtensionDirectories = null)
    {
        if (!loadUserPlugins)
        {
            callbacks.SetPluginSummary("Plugin loading disabled for isolated self-test");
            return;
        }

        ExtensionFactory.CreatePluginFolders();
        var disabled = callbacks.Settings.DisabledPlugins ?? new List<string>();
        var externals = (externalExtensionDirectories ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        this.externalExtensionDirectories = externals;
        var manifests = ExtensionFactory.GetInstalledManifests(externals);
        var v7ManifestIds = v7Plugins.Load(manifests, disabled);
        var v6IgnoreList = disabled.Concat(v7ManifestIds).Distinct().ToList();
        pluginCompatibility.LoadLegacyPlugins(extensions, v6IgnoreList, externals);
        extensions.LoadScripts(disabled, false, externals);
        callbacks.SetPluginSummary(
            $"{LoadedPluginCount} plugins loaded" +
            (FailedPluginCount == 0 ? string.Empty : $", {FailedPluginCount} failed"));
    }

    public void ReloadScripts()
    {
        var disabled = callbacks.Settings.DisabledPlugins ?? new List<string>();
        extensions.LoadScripts(disabled, false, externalExtensionDirectories);
        callbacks.SetPluginSummary(
            $"{LoadedPluginCount} plugins loaded" +
            (FailedPluginCount == 0 ? string.Empty : $", {FailedPluginCount} failed"));
    }

    public GameOperationResult Play(Game game, int choiceIndex = -1) => actionRunner.Play(game, choiceIndex);
    public GameOperationResult Install(Game game, int choiceIndex = -1) => actionRunner.Install(game, choiceIndex);
    public GameOperationResult Uninstall(Game game, int choiceIndex = -1) => actionRunner.Uninstall(game, choiceIndex);

    public void NotifyLibraryUpdated()
    {
        extensions.NotifiyOnLibraryUpdated();
        v7Plugins.NotifyLibraryUpdated();
    }

    public bool ProcessUri(string uri)
    {
        if (v7Plugins.ProcessUri(uri))
        {
            return true;
        }

        var (source, arguments) = PlayniteUriHandler.ParseUri(uri);
        return ((AvaloniaPluginApi)globalApi).ProcessUri(source, arguments);
    }

    public void ShowMessage(string message, bool error)
    {
        notifications.Add(
            $"avalonia-host-{Guid.NewGuid():N}",
            message,
            error ? NotificationType.Error : NotificationType.Info);
        callbacks.SetStatus(message);
    }

    public void Dispose()
    {
        if (ReferenceEquals(PluginConverterRuntime.Resolver, pluginConverterResolver))
        {
            PluginConverterRuntime.Resolver = (_, _) => null;
        }

        if (ReferenceEquals(PluginElementRuntime.Resolver, pluginElementResolver))
        {
            PluginElementRuntime.Resolver = (_, _, _) => null;
            PluginElementRuntime.NotifyRegistrationsChanged();
        }

        GameControllerDialogs.ShowError = (_, _) => { };
        actionRunner.Dispose();
        extensions.Dispose();
        controllers.Dispose();
        v7Plugins.Dispose();
        if (ReferenceEquals(GoogleImageDownloader.CreateOffscreenView, offscreenWebViewFactory))
        {
            GoogleImageDownloader.CreateOffscreenView = previousOffscreenWebViewFactory;
        }

        webViews.Dispose();
        pluginCompatibility.RestoreResourceProvider(previousResourceProvider);
        pluginCompatibility.Shutdown();
    }
}
