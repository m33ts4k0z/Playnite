using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;
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
    private readonly RuntimeThemeManager themeManager;
    private readonly DesktopMainView mainView;
    private readonly DesktopWindowChrome chrome;
    private readonly DesktopTrayService trayService;
    private WindowState restoreWindowState = WindowState.Normal;
    private bool hasClosed;
    private AvaloniaThemePackage activeThemePackage;

    internal DesktopMainView MainView => mainView;
    internal AvaloniaRuntimeHost RuntimeHost => runtimeHost;
    internal DesktopWindowChrome Chrome => chrome;
    internal DesktopTrayService TrayService => trayService;
    internal bool HasClosed => hasClosed;
    internal AvaloniaThemePackage ActiveThemePackage => activeThemePackage;

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
        MinWidth = 1100;
        MinHeight = 680;
        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = 42;
        DataContext = viewModel;

        var iconPath = ContentPath("Assets", "applogo.ico");
        Icon = new WindowIcon(iconPath);
        RestoreWindowPosition();
        restoreWindowState = WindowState;

        themeManager = new RuntimeThemeManager(Application.Current, typeof(DesktopMainView).Assembly);
        ApplyRuntimeTheme();
        themeManager.ApplyLanguage(
            Playnite.Avalonia.App.Services.LanguageCatalog.ResolveLanguagePaths(
                ContentPath("Localization"), settings.Language));

        mainView = new DesktopMainView();
        chrome = new DesktopWindowChrome(this)
        {
            Content = mainView
        };
        Content = chrome;
        trayService = new DesktopTrayService(
            viewModel,
            iconPath,
            RestoreFromTray,
            RequestExit,
            CanOpenFullscreen,
            OpenFullscreen);
        trayService.ApplySettings(settings.EnableTray);
        viewModel.PluginSettings.ConfigureOwnerHandle(
            () => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        viewModel.InstalledGameImport.ConfigureFilePickers(PickImportFolderAsync, PickExecutableAsync);
        viewModel.SettingsChanged += ViewModel_SettingsChanged;
        Opened += OnOpened;
        Closing += OnClosing;
        PositionChanged += OnPositionChanged;
        Resized += OnResized;
        PropertyChanged += OnWindowPropertyChanged;
        Closed += OnClosed;
    }

    private void ApplyRuntimeTheme()
    {
        var defaultTheme = AvaloniaThemePackage.Load(
            ContentPath("Themes", "Desktop", "Default"),
            AvaloniaThemeMode.Desktop);
        themeManager.ApplyTheme(
            defaultTheme.ResourceDictionaries,
            selectorStyles: defaultTheme.SelectorStyles);
        activeThemePackage = defaultTheme;

        var customThemePath = options.CustomThemePath ?? settings.ThemePath;
        if (string.IsNullOrWhiteSpace(customThemePath))
        {
            return;
        }

        try
        {
            var customTheme = AvaloniaThemePackage.Load(
                customThemePath,
                AvaloniaThemeMode.Desktop);
            themeManager.ApplyTheme(
                defaultTheme.ResourceDictionaries,
                customTheme.ResourceDictionaries,
                defaultTheme.SelectorStyles.Concat(customTheme.SelectorStyles));
            activeThemePackage = customTheme;
            viewModel.SetStatusMessage($"Theme '{customTheme.Name}' loaded.");
        }
        catch (Exception exception) when (
            exception is LooseXamlLoadException or InvalidDataException or FileNotFoundException or ArgumentException)
        {
            viewModel.SetStatusMessage(
                $"Custom theme failed to load; the default theme is active. " +
                (exception.InnerException?.Message ?? exception.Message));
            Trace.WriteLine(exception);
        }
    }

    internal void RestoreFromTray()
    {
        if (hasClosed)
        {
            return;
        }

        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = restoreWindowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        mainView.FocusSelectedGame();
    }

    internal void RequestExit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Close();
        }
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

    private void RestoreWindowPosition()
    {
        if (settings.WindowX.HasValue && settings.WindowY.HasValue)
        {
            var savedPosition = new PixelPoint(settings.WindowX.Value, settings.WindowY.Value);
            if (Screens.ScreenFromPoint(savedPosition) != null)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = savedPosition;
            }
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void ViewModel_SettingsChanged(object sender, EventArgs e)
    {
        trayService.ApplySettings(settings.EnableTray);
        SaveSettings();
    }

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        CaptureWindowPlacement();
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            return;
        }

        if (settings.EnableTray && settings.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        hasClosed = true;
        viewModel.SettingsChanged -= ViewModel_SettingsChanged;
        viewModel.PluginSearch.Dispose();
        trayService.Dispose();
        SaveSettings();
    }

    private void OnPositionChanged(object sender, PixelPointEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        settings.WindowX = e.Point.X;
        settings.WindowY = e.Point.Y;
    }

    private void OnResized(object sender, WindowResizedEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        settings.WindowWidth = Math.Max(MinWidth, e.ClientSize.Width);
        settings.WindowHeight = Math.Max(MinHeight, e.ClientSize.Height);
    }

    private void OnWindowPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != WindowStateProperty)
        {
            return;
        }

        if (WindowState != WindowState.Minimized)
        {
            restoreWindowState = WindowState;
            settings.WindowMaximized = WindowState == WindowState.Maximized;
        }
        else if (settings.EnableTray && settings.MinimizeToTray)
        {
            Dispatcher.UIThread.Post(HideToTray, DispatcherPriority.Background);
        }
    }

    private void HideToTray()
    {
        if (!settings.EnableTray || hasClosed)
        {
            return;
        }

        CaptureWindowPlacement();
        ShowInTaskbar = false;
        Hide();
        SaveSettings();
    }

    private void CaptureWindowPlacement()
    {
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = Math.Max(MinWidth, Bounds.Width);
            settings.WindowHeight = Math.Max(MinHeight, Bounds.Height);
            settings.WindowX = Position.X;
            settings.WindowY = Position.Y;
        }

        settings.WindowMaximized = WindowState == WindowState.Maximized ||
            WindowState == WindowState.Minimized && restoreWindowState == WindowState.Maximized;
    }

    private bool CanOpenFullscreen() => ResolveFullscreenExecutable() != null;

    private void OpenFullscreen()
    {
        var executable = ResolveFullscreenExecutable();
        if (executable == null)
        {
            viewModel.SetStatusMessage("The Fullscreen application is not installed beside this Desktop build.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = true
            };
            if (Path.GetFileNameWithoutExtension(executable).EndsWith(".Avalonia", StringComparison.Ordinal))
            {
                startInfo.ArgumentList.Add("--userdatadir");
                startInfo.ArgumentList.Add(options.UserDataDirectory);
                startInfo.ArgumentList.Add("--library-path");
                startInfo.ArgumentList.Add(options.LibraryPath);
            }

            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            viewModel.SetStatusMessage($"Fullscreen could not be opened: {exception.Message}");
        }
    }

    private static string ResolveFullscreenExecutable()
    {
        var candidates = new[]
        {
            ContentPath("Playnite.FullscreenApp.Avalonia.exe"),
            ContentPath("Playnite.FullscreenApp.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        mainView.FocusSelectedGame();
        if (options.PluginCompatibilityTest)
        {
            await InstalledPluginCompatibilityTest.Run(this, library, options);
        }
        else if (options.SelfTest)
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
