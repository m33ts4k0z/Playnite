using Playnite.Avalonia.App.Services;
using Avalonia.Controls;
using Playnite.Controllers;
using Playnite.Plugins;
using Playnite.SDK.Models;
using Playnite.FullscreenApp.Avalonia.ViewModels;
using Playnite.SDK.Plugins;
using Playnite.Scripting.PowerShell;
using Playnite.Common;
using Playnite.Avalonia.Input;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenRuntimeHost : IDisposable
{
    private readonly AvaloniaRuntimeHost host;
    private readonly FullscreenDialogService dialogs;

    public GameActionRunner Actions => host.Actions;
    public ExtensionFactory Extensions => host.Extensions;
    public Playnite.API.NotificationsAPI Notifications => host.Notifications;
    internal FullscreenDialogService Dialogs => dialogs;
    public int LoadedPluginCount => host.LoadedPluginCount;
    public int FailedPluginCount => host.FailedPluginCount;
    public IReadOnlyList<V7LoadedPlugin> V7Plugins => host.V7Plugins;
    public IReadOnlyList<LibraryPlugin> LibraryPlugins => host.LibraryPlugins;
    public Playnite.SDK.IPlayniteAPI PluginApi => host.PluginApi;

    public FullscreenRuntimeHost(
        PlayniteLibrary library,
        FullscreenAppViewModel viewModel,
        FullscreenSettings settings,
        Func<Window> currentWindow)
    {
        dialogs = new FullscreenDialogService(viewModel, currentWindow);
        host = new AvaloniaRuntimeHost(library.Database, new AvaloniaHostCallbacks
        {
            Mode = Playnite.SDK.ApplicationMode.Fullscreen,
            Settings = settings,
            Dialogs = dialogs,
            CurrentWindow = currentWindow,
            FilteredGames = () => viewModel.Games.Select(item => item.Game).ToList(),
            SelectedGame = () => viewModel.SelectedGame?.Game,
            SelectGame = viewModel.SelectGame,
            OpenSearch = term =>
            {
                viewModel.OpenSearchCommand.Execute(null);
                viewModel.SearchText = term;
            },
            OpenSearchContext = viewModel.PluginSearch.Open,
            ActiveFullscreenView = () =>
                viewModel.IsDetailsVisible
                    ? Playnite.SDK.FullscreenView.Details
                    : Playnite.SDK.FullscreenView.List,
            SwitchToLibraryView = viewModel.SwitchToLibraryView,
            ToggleFullscreenView = () => viewModel.ToggleFullscreenCommand.Execute(null),
            SetStatus = viewModel.SetStatusMessage,
            SetPluginSummary = viewModel.SetPluginSummary,
            RefreshGame = viewModel.RefreshGame
        });
    }

    public void InitializePlugins(bool loadUserPlugins) => host.InitializePlugins(loadUserPlugins);
    public bool ProcessUri(string uri) => host.ProcessUri(uri);
    public GameOperationResult Play(Game game, int choiceIndex = -1) => host.Play(game, choiceIndex);
    public GameOperationResult Install(Game game, int choiceIndex = -1) => host.Install(game, choiceIndex);
    public GameOperationResult Uninstall(Game game, int choiceIndex = -1) => host.Uninstall(game, choiceIndex);
    public IReadOnlyList<PluginMenuAction> GetMainMenuActions() => host.GetMainMenuActions();
    public IReadOnlyList<PluginMenuAction> GetGameMenuActions(Game game) =>
        host.GetGameMenuActions(game == null ? Array.Empty<Game>() : new[] { game });
    public string ActivateGameAction(Game game, GameAction action) => host.ActivateGameAction(game, action);
    public void NotifyLibraryUpdated() => host.NotifyLibraryUpdated();
    public void NotifyControllerButtonStateChanged(GamepadButton button, bool isPressed) =>
        host.NotifyControllerButtonStateChanged(button, isPressed);
    public void NotifyControllerConnected(Playnite.Avalonia.Input.SdlGameControllerDevice device) =>
        host.NotifyControllerConnected(device);
    public void NotifyControllerDisconnected(Playnite.Avalonia.Input.SdlGameControllerDevice device) =>
        host.NotifyControllerDisconnected(device);

    public void StartSoftwareTool(AppSoftware app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.AppType == AppSoftwareType.Standard)
        {
            ProcessStarter.StartProcess(
                PlaynitePaths.ExpandVariables(app.Path, fixSeparators: true),
                PlaynitePaths.ExpandVariables(app.Arguments),
                PlaynitePaths.ExpandVariables(app.WorkingDir, fixSeparators: true));
            return;
        }

        using var runtime = new PowerShellRuntime($"Software tool {app.Name} runtime");
        runtime.Execute(
            PlaynitePaths.ExpandVariables(app.Script),
            PlaynitePaths.ProgramPath,
            new Dictionary<string, object> { ["PlayniteApi"] = host.PluginApi });
    }
    public void Dispose() => host.Dispose();
}
