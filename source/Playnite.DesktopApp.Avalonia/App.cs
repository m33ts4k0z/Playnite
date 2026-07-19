using Avalonia;
using Avalonia.Controls;
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
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
            var settings = LoadOrImportSettings(settingsStore, library.ActiveUserDataDirectory, options);
            var viewModel = new DesktopAppViewModel(library.Games, library.Database, settings, startupError);
            MainWindow window = null;
            if (library.IsOpen)
            {
                var dialogs = new DesktopDialogService(viewModel, () => window);
                var gameEditor = new DesktopGameEditorService(viewModel);
                runtimeHost = new AvaloniaRuntimeHost(library.Database, new AvaloniaHostCallbacks
                {
                    Mode = Playnite.SDK.ApplicationMode.Desktop,
                    Settings = settings,
                    Dialogs = dialogs,
                    CurrentWindow = () => window,
                    FilteredGames = () => viewModel.Games.Select(game => game.Game).ToList(),
                    SelectedGame = () => viewModel.SelectedGame?.Game,
                    SelectGame = viewModel.SelectGame,
                    OpenSearch = term => viewModel.SearchText = term,
                    OpenSearchContext = viewModel.PluginSearch.Open,
                    OpenPluginSettings = viewModel.OpenPluginSettings,
                    OpenEditDialog = gameEditor.Show,
                    ActiveDesktopView = () => viewModel.IsGridView
                        ? Playnite.SDK.DesktopView.Grid
                        : Playnite.SDK.DesktopView.List,
                    SetActiveDesktopView = value => viewModel.SelectedViewMode = value switch
                    {
                        Playnite.SDK.DesktopView.Grid => "Grid",
                        Playnite.SDK.DesktopView.List => "List",
                        _ => throw new NotSupportedException(
                            "The Avalonia desktop pilot does not expose a separate details view.")
                    },
                    SortOrder = () => viewModel.SelectedSortOrder,
                    SortDirection = () => viewModel.SelectedSortDirection,
                    Grouping = () => viewModel.SelectedGrouping,
                    SetSortDirection = value => viewModel.SelectedSortDirection = value,
                    SetGrouping = value => viewModel.SelectedGrouping = value,
                    ApplyFilterPreset = viewModel.ApplyFilterPreset,
                    ActiveFilterPreset = () => viewModel.SelectedFilterPreset?.Id ?? Guid.Empty,
                    CurrentFilterSettings = viewModel.GetCurrentFilterSettings,
                    FilterPresets = () => viewModel.FilterPresets.ToList(),
                    SwitchToLibraryView = viewModel.SwitchToLibraryView,
                    SetStatus = viewModel.SetStatusMessage,
                    SetPluginSummary = viewModel.SetPluginSummary,
                    RefreshGame = viewModel.RefreshGame
                });
                viewModel.AttachRuntime(runtimeHost);
            }

            window = new MainWindow(
                viewModel,
                library,
                runtimeHost,
                settings,
                options.SelfTest || options.PluginCompatibilityTest ? null : settingsStore,
                options);
            desktop.MainWindow = window;
            // Parse the loose theme before third-party assemblies enter the process. A plugin
            // with an incompatible dependency must not interfere with Avalonia's XAML discovery.
            runtimeHost?.InitializePlugins(!options.SelfTest);
            if (!string.IsNullOrWhiteSpace(options.UriData) && runtimeHost?.ProcessUri(options.UriData) == false)
            {
                viewModel.SetStatusMessage($"No URI handler is registered for '{options.UriData}'.");
            }
            viewModel.RefreshPluginSurfaces();
            desktop.Exit += (_, _) =>
            {
                runtimeHost?.Dispose();
                library.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static DesktopSettings LoadOrImportSettings(
        DesktopSettingsStore settingsStore, string userDataDirectory, StartupOptions options)
    {
        if (options.SelfTest)
        {
            return new DesktopSettings();
        }

        if (options.PluginCompatibilityTest)
        {
            return new DesktopSettings { EnableTray = false, CloseToTray = false };
        }

        if (settingsStore.Exists)
        {
            return settingsStore.Load();
        }

        // First launch against this profile: carry the WPF language and main
        // window placement across so the shell opens where the user left off.
        var settings = new DesktopSettings();
        ImportWpfDefaults(settings, userDataDirectory);
        settingsStore.Save(settings);
        return settings;
    }

    private static void ImportWpfDefaults(DesktopSettings settings, string userDataDirectory)
    {
        var defaults = WpfProfileImport.Read(userDataDirectory);
        if (!string.IsNullOrWhiteSpace(defaults.Language))
        {
            settings.Language = defaults.Language;
        }

        var placement = defaults.MainWindow;
        if (placement == null)
        {
            return;
        }

        if (placement.Width is > 0)
        {
            settings.WindowWidth = placement.Width.Value;
        }

        if (placement.Height is > 0)
        {
            settings.WindowHeight = placement.Height.Value;
        }

        settings.WindowX = placement.X;
        settings.WindowY = placement.Y;
        settings.WindowMaximized = placement.Maximized;
    }
}
