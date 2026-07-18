using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

public sealed class App : Application
{
    private DesktopLibrary library;
    private AvaloniaRuntimeHost runtimeHost;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = Program.Options;
            library = new DesktopLibrary(options.UserDataDirectory, options.LibraryPath);
            string startupError = null;
            try
            {
                if (options.SelfTest)
                {
                    library.OpenTemporaryLibrary(1_000);
                }
                else
                {
                    library.OpenExistingLibrary();
                }
            }
            catch (Exception exception)
            {
                startupError = exception.Message;
            }

            var settingsStore = new DesktopSettingsStore(library.ActiveUserDataDirectory);
            var settings = options.SelfTest ? new DesktopSettings() : settingsStore.Load();
            var viewModel = new DesktopAppViewModel(library.Games, library.Database, settings, startupError);
            if (library.IsOpen)
            {
                var dialogs = new DesktopDialogService(viewModel);
                runtimeHost = new AvaloniaRuntimeHost(library.Database, new AvaloniaHostCallbacks
                {
                    Mode = Playnite.SDK.ApplicationMode.Desktop,
                    Settings = settings,
                    Dialogs = dialogs,
                    FilteredGames = () => viewModel.Games.Select(game => game.Game).ToList(),
                    SelectedGame = () => viewModel.SelectedGame?.Game,
                    SelectGame = viewModel.SelectGame,
                    OpenSearch = term => viewModel.SearchText = term,
                    ActiveDesktopView = () => viewModel.IsGridView
                        ? Playnite.SDK.DesktopView.Grid
                        : Playnite.SDK.DesktopView.List,
                    SortOrder = () => viewModel.SelectedSortOrder,
                    SortDirection = () => viewModel.SelectedSortDirection,
                    Grouping = () => viewModel.SelectedGrouping,
                    SetSortDirection = value => viewModel.SelectedSortDirection = value,
                    SetGrouping = value => viewModel.SelectedGrouping = value,
                    ApplyFilterPreset = viewModel.ApplyFilterPreset,
                    ActiveFilterPreset = () => viewModel.SelectedFilterPreset?.Id ?? Guid.Empty,
                    CurrentFilterSettings = viewModel.GetCurrentFilterSettings,
                    FilterPresets = () => viewModel.FilterPresets.ToList(),
                    SetStatus = viewModel.SetStatusMessage,
                    SetPluginSummary = viewModel.SetPluginSummary,
                    RefreshGame = viewModel.RefreshGame
                });
                viewModel.AttachRuntime(runtimeHost);
                runtimeHost.InitializePlugins(!options.SelfTest);
            }

            desktop.MainWindow = new MainWindow(
                viewModel,
                library,
                runtimeHost,
                settings,
                options.SelfTest ? null : settingsStore,
                options);
            desktop.Exit += (_, _) =>
            {
                runtimeHost?.Dispose();
                library.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
