using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Playnite.Avalonia.Theming;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly DesktopAppViewModel viewModel;
    private readonly DesktopLibrary library;
    private readonly StartupOptions options;
    private readonly AvaloniaRuntimeHost runtimeHost;
    private readonly DesktopSettings settings;
    private readonly DesktopSettingsStore settingsStore;
    private readonly DesktopMainView mainView;

    internal DesktopMainView MainView => mainView;
    internal AvaloniaRuntimeHost RuntimeHost => runtimeHost;

    internal MainWindow(
        DesktopAppViewModel viewModel,
        DesktopLibrary library,
        AvaloniaRuntimeHost runtimeHost,
        DesktopSettings settings,
        DesktopSettingsStore settingsStore,
        StartupOptions options)
    {
        this.viewModel = viewModel;
        this.library = library;
        this.runtimeHost = runtimeHost;
        this.settings = settings;
        this.settingsStore = settingsStore;
        this.options = options;

        Title = "Playnite — Avalonia Desktop Pilot";
        Width = 1440;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 680;
        DataContext = viewModel;

        var themes = new RuntimeThemeManager(Application.Current, typeof(DesktopMainView).Assembly);
        themes.ApplyTheme(
            new[] { ContentPath("Themes", "Desktop", "Default", "Theme.axaml") },
            selectorStyles: new[] { ContentPath("Themes", "Desktop", "Default", "Styles.axaml") });
        themes.ApplyLanguage(ContentPath("Localization", "english.axaml"));

        mainView = new DesktopMainView();
        Content = mainView;
        viewModel.InstalledGameImport.ConfigureFilePickers(PickImportFolderAsync, PickExecutableAsync);
        viewModel.SettingsChanged += (_, _) => SaveSettings();
        Opened += OnOpened;
        Closed += (_, _) => SaveSettings();
    }

    private void SaveSettings()
    {
        if (settingsStore == null)
        {
            return;
        }

        try
        {
            settingsStore.Save(settings);
        }
        catch (Exception exception)
        {
            viewModel.SetStatusMessage($"Desktop settings could not be saved: {exception.Message}");
        }
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        mainView.FocusSelectedGame();
        if (options.SelfTest)
        {
            await DesktopPilotSelfTest.Run(this, viewModel, library);
        }
    }

    private async Task<string> PickImportFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder to scan for games",
            AllowMultiple = false
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string> PickExecutableAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a game executable or shortcut",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executable files")
                {
                    Patterns = new[] { "*.exe", "*.bat", "*.lnk" }
                }
            }
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private static string ContentPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory }.Concat(parts).ToArray());
}
