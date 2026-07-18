using Playnite.API;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenRuntimeHost : IDisposable
{
    private readonly PlayniteLibrary library;
    private readonly FullscreenAppViewModel viewModel;
    private readonly FullscreenSettings settings;
    private readonly GameControllerFactory controllers;
    private readonly ExtensionFactory extensions;
    private readonly GameActionRunner actionRunner;
    private readonly NotificationsAPI notifications;
    private readonly IPlayniteAPI globalApi;
    private readonly FullscreenDialogService dialogs;

    public GameActionRunner Actions => actionRunner;
    public ExtensionFactory Extensions => extensions;
    public NotificationsAPI Notifications => notifications;
    internal FullscreenDialogService Dialogs => dialogs;
    public int LoadedPluginCount => extensions.Plugins.Count;
    public int FailedPluginCount => extensions.FailedExtensions.Count;

    public FullscreenRuntimeHost(
        PlayniteLibrary library,
        FullscreenAppViewModel viewModel,
        FullscreenSettings settings)
    {
        this.library = library;
        this.viewModel = viewModel;
        this.settings = settings;

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
        dialogs = new FullscreenDialogService(viewModel);
        controllers = new GameControllerFactory(library.Database);

        GameActionRunner runner = null;
        ExtensionFactory factory = null;
        IPlayniteAPI CreateApi() => new LegacyPluginApi(
            library.Database,
            notifications,
            settings,
            () => runner,
            () => factory,
            () => viewModel.Games.Select(item => item.Game).ToList(),
            () => viewModel.SelectedGame?.Game,
            viewModel.SelectGame,
            ShowMessage,
            dialogs);

        extensions = factory = new ExtensionFactory(library.Database, controllers, _ => CreateApi());
        actionRunner = runner = new GameActionRunner(library.Database, controllers, extensions, () => globalApi);
        globalApi = CreateApi();

        actionRunner.StatusChanged += (_, message) => viewModel.SetStatusMessage(message);
        actionRunner.OperationFailed += (_, message) => ShowMessage(message, true);
        actionRunner.GameStateChanged += (_, game) => viewModel.RefreshGame(game.Id);
        GameControllerDialogs.ShowError = (message, caption) => ShowMessage(
            string.IsNullOrWhiteSpace(caption) ? message : $"{caption}: {message}",
            true);
    }

    public void InitializePlugins(bool loadUserPlugins)
    {
        if (!loadUserPlugins)
        {
            viewModel.SetPluginSummary("Plugin loading disabled for isolated self-test");
            return;
        }

        ExtensionFactory.CreatePluginFolders();
        extensions.LoadPlugins(settings.DisabledPlugins, false, new List<string>());
        extensions.LoadScripts(settings.DisabledPlugins, false, new List<string>());
        viewModel.SetPluginSummary(
            $"{LoadedPluginCount} plugins loaded" +
            (FailedPluginCount == 0 ? string.Empty : $", {FailedPluginCount} failed"));
    }

    public GameOperationResult Play(Game game, int choiceIndex = -1) => actionRunner.Play(game, choiceIndex);
    public GameOperationResult Install(Game game, int choiceIndex = -1) => actionRunner.Install(game, choiceIndex);
    public GameOperationResult Uninstall(Game game, int choiceIndex = -1) => actionRunner.Uninstall(game, choiceIndex);

    public void Dispose()
    {
        GameControllerDialogs.ShowError = (_, _) => { };
        actionRunner.Dispose();
        extensions.Dispose();
        controllers.Dispose();
    }

    private void ShowMessage(string message, bool error)
    {
        notifications.Add(
            $"avalonia-host-{Guid.NewGuid():N}",
            message,
            error ? NotificationType.Error : NotificationType.Info);
        viewModel.SetStatusMessage(message);
    }
}
