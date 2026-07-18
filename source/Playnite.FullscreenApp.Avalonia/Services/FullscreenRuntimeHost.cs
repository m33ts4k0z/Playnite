using Playnite.Avalonia.App.Services;
using Avalonia.Controls;
using Playnite.Controllers;
using Playnite.Plugins;
using Playnite.SDK.Models;
using Playnite.FullscreenApp.Avalonia.ViewModels;

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
    public void Dispose() => host.Dispose();
}
