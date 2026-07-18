using Playnite.API;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Playnite.Avalonia.App.Services;

public sealed class AvaloniaRuntimeHost : IDisposable
{
    private readonly AvaloniaHostCallbacks callbacks;
    private readonly GameControllerFactory controllers;
    private readonly ExtensionFactory extensions;
    private readonly GameActionRunner actionRunner;
    private readonly NotificationsAPI notifications;
    private readonly IPlayniteAPI globalApi;

    public GameActionRunner Actions => actionRunner;
    public ExtensionFactory Extensions => extensions;
    public NotificationsAPI Notifications => notifications;
    public IAvaloniaDialogService Dialogs => callbacks.Dialogs;
    public int LoadedPluginCount => extensions.Plugins.Count;
    public int FailedPluginCount => extensions.FailedExtensions.Count;

    public AvaloniaRuntimeHost(GameDatabase database, AvaloniaHostCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        ArgumentNullException.ThrowIfNull(callbacks.Settings);
        ArgumentNullException.ThrowIfNull(callbacks.Dialogs);

        GameDatabase.ExpandGameVariables = (game, input, fixSeparators, emulatorDirectory) =>
            game.ExpandVariables(input, fixSeparators, emulatorDirectory);

        notifications = new NotificationsAPI();
        notifications.ActivationRequested += (_, args) =>
        {
            args.Message?.ActivationAction?.Invoke();
            if (args.Message != null)
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

        GameActionRunner runner = null;
        ExtensionFactory factory = null;
        IPlayniteAPI CreateApi() => new AvaloniaPluginApi(
            database,
            notifications,
            () => runner,
            () => factory,
            callbacks);

        extensions = factory = new ExtensionFactory(database, controllers, _ => CreateApi());
        actionRunner = runner = new GameActionRunner(database, controllers, extensions, () => globalApi);
        globalApi = CreateApi();

        actionRunner.StatusChanged += (_, message) => callbacks.SetStatus(message);
        actionRunner.OperationFailed += (_, message) => ShowMessage(message, true);
        actionRunner.GameStateChanged += (_, game) => callbacks.RefreshGame(game.Id);
        GameControllerDialogs.ShowError = (message, caption) => ShowMessage(
            string.IsNullOrWhiteSpace(caption) ? message : $"{caption}: {message}",
            true);
    }

    public void InitializePlugins(bool loadUserPlugins)
    {
        if (!loadUserPlugins)
        {
            callbacks.SetPluginSummary("Plugin loading disabled for isolated self-test");
            return;
        }

        ExtensionFactory.CreatePluginFolders();
        var disabled = callbacks.Settings.DisabledPlugins ?? new List<string>();
        extensions.LoadPlugins(disabled, false, new List<string>());
        extensions.LoadScripts(disabled, false, new List<string>());
        callbacks.SetPluginSummary(
            $"{LoadedPluginCount} plugins loaded" +
            (FailedPluginCount == 0 ? string.Empty : $", {FailedPluginCount} failed"));
    }

    public GameOperationResult Play(Game game, int choiceIndex = -1) => actionRunner.Play(game, choiceIndex);
    public GameOperationResult Install(Game game, int choiceIndex = -1) => actionRunner.Install(game, choiceIndex);
    public GameOperationResult Uninstall(Game game, int choiceIndex = -1) => actionRunner.Uninstall(game, choiceIndex);

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
        GameControllerDialogs.ShowError = (_, _) => { };
        actionRunner.Dispose();
        extensions.Dispose();
        controllers.Dispose();
    }
}
