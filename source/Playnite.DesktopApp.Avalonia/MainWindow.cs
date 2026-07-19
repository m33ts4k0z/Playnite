using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Input;
using System.Diagnostics;
using Playnite.Avalonia.Theming;
using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.Input;
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
    private readonly ISystemHotKeyService systemHotKeyService;
    private readonly global::Playnite.DiscordManager discord;
    private readonly GamepadInputBridge gamepadBridge;
    private readonly SdlGamepadInputSource sdlInput;
    private WindowState restoreWindowState = WindowState.Normal;
    private bool hasClosed;
    private bool automatedRunStarted;
    private AvaloniaThemePackage activeThemePackage;
    private readonly HashSet<Guid> runningGames = new();

    internal DesktopMainView MainView => mainView;
    internal AvaloniaRuntimeHost RuntimeHost => runtimeHost;
    internal DesktopWindowChrome Chrome => chrome;
    internal DesktopTrayService TrayService => trayService;
    internal bool HasClosed => hasClosed;
    internal AvaloniaThemePackage ActiveThemePackage => activeThemePackage;
    internal HotKey RegisteredSystemHotKey => systemHotKeyService.RegisteredHotKey;
    internal GamepadInputBridge GamepadBridge => gamepadBridge;
    internal SdlGamepadInputSource SdlInput => sdlInput;

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

        // Launch minimized when requested; the tray/minimize handlers then hide it
        // to the tray if that is also configured (matching the WPF behavior).
        if (!options.SelfTest && settings.StartMinimized)
        {
            WindowState = WindowState.Minimized;
        }

        themeManager = new RuntimeThemeManager(Application.Current, typeof(DesktopMainView).Assembly);
        ApplyRuntimeTheme();
        ApplyTypographyResources();
        themeManager.ApplyLanguage(
            Playnite.Avalonia.App.Services.LanguageCatalog.ResolveLanguagePaths(
                ContentPath("Localization"), settings.Language));

        mainView = new DesktopMainView();
        chrome = new DesktopWindowChrome(this)
        {
            Content = mainView
        };
        Content = chrome;
        gamepadBridge = new GamepadInputBridge(this);
        gamepadBridge.MapCommand(
            GamepadButton.DPadLeft,
            CreateLibraryNavigationCommand(() => MoveControllerSelection(-1)));
        gamepadBridge.MapCommand(
            GamepadButton.DPadRight,
            CreateLibraryNavigationCommand(() => MoveControllerSelection(1)));
        gamepadBridge.MapCommand(
            GamepadButton.DPadUp,
            CreateLibraryNavigationCommand(() => MoveControllerSelection(-ControllerRowStep())));
        gamepadBridge.MapCommand(
            GamepadButton.DPadDown,
            CreateLibraryNavigationCommand(() => MoveControllerSelection(ControllerRowStep())));
        sdlInput = new SdlGamepadInputSource(
            gamepadBridge,
            settings.EnableGameControllerSupport,
            settings.DisabledGameControllers);
        viewModel.Settings.Input.ConfigureControllerSource(
            () => sdlInput.Devices,
            (enabled, disabled) => sdlInput.ApplySettings(enabled, disabled));
        sdlInput.DevicesChanged += (_, _) => viewModel.Settings.Input.RefreshControllers();
        systemHotKeyService = new SystemHotKeyService(this);
        discord = new global::Playnite.DiscordManager(settings.DiscordPresenceEnabled);
        viewModel.Updates.ConfigureProgramInstaller(LaunchProgramUpdater);
        trayService = new DesktopTrayService(
            viewModel,
            iconPath,
            RestoreFromTray,
            RequestExit,
            CanOpenFullscreen,
            OpenFullscreen);
        trayService.ApplySettings(settings.EnableTray, ResolveTrayIconPath(settings.TrayIcon));
        viewModel.PluginSettings.ConfigureOwnerHandle(
            () => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        viewModel.InstalledGameImport.ConfigureFilePickers(PickImportFolderAsync, PickExecutableAsync);
        viewModel.SettingsChanged += ViewModel_SettingsChanged;
        if (runtimeHost != null)
        {
            runtimeHost.Actions.GameStateChanged += OnGameRunStateChanged;
        }

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.F || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        if (settings.GlobalSearchOpenWithLegacySearch)
        {
            viewModel.OpenGlobalSearch(string.Empty);
        }
        else
        {
            mainView.SearchBox?.Focus();
            mainView.SearchBox?.SelectAll();
        }

        e.Handled = true;
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

    internal string ResolveTrayIconPath(TrayIconOption option) => option switch
    {
        TrayIconOption.Bright => ContentPath("Assets", "tray-bright.png"),
        TrayIconOption.Dark => ContentPath("Assets", "tray-dark.png"),
        _ => ContentPath("Assets", "tray-default.png")
    };

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
        global::Playnite.Common.NLogLogger.IsTraceEnabled = settings.TraceLogEnabled;
        if (discord.IsPresenceEnabled && !settings.DiscordPresenceEnabled)
        {
            discord.ClearPresence();
        }
        discord.IsPresenceEnabled = settings.DiscordPresenceEnabled;
        ApplyTypographyResources();
        trayService.ApplySettings(settings.EnableTray, ResolveTrayIconPath(settings.TrayIcon));
        ApplySystemHotKey();
        SaveSettings();
    }

    private void ApplySystemHotKey()
    {
        if (!systemHotKeyService.Register(
                settings.SystemSearchHotkey,
                () =>
                {
                    RestoreFromTray();
                    viewModel.OpenGlobalSearch(string.Empty);
                },
                out var error))
        {
            viewModel.SetStatusMessage(error);
        }
    }

    private System.Windows.Input.ICommand CreateLibraryNavigationCommand(Action execute) =>
        new global::Playnite.Avalonia.App.ViewModels.RelayCommand(execute, IsLibraryControllerContext);

    private bool IsLibraryControllerContext()
    {
        var focused = FocusManager?.GetFocusedElement() as Control;
        return focused is not TextBox &&
            !viewModel.Editor.IsVisible &&
            !viewModel.MetadataDownload.IsVisible &&
            !viewModel.LibrarySync.IsVisible &&
            !viewModel.InstalledGameImport.IsVisible &&
            !viewModel.PluginSettings.IsVisible &&
            !viewModel.Settings.IsVisible &&
            !viewModel.PluginSearch.IsVisible &&
            !viewModel.IsNotificationsVisible &&
            !viewModel.IsActionPickerVisible &&
            !viewModel.IsDialogVisible &&
            !viewModel.IsPluginMenuVisible;
    }

    private int ControllerRowStep() => viewModel.IsListView
        ? 1
        : mainView.TilePanel?.NavigationColumns ?? 1;

    private void MoveControllerSelection(int offset)
    {
        var gameList = mainView.GameList;
        if (gameList == null || gameList.ItemCount == 0)
        {
            return;
        }

        var current = Math.Max(0, gameList.SelectedIndex);
        var target = Math.Clamp(current + offset, 0, gameList.ItemCount - 1);
        gameList.SelectedIndex = target;
        gameList.ScrollIntoView(target);
        mainView.FocusSelectedGame();
    }

    private void ApplyTypographyResources()
    {
        var interfaceFont = new FontFamily(settings.FontFamilyName);
        var monospaceFont = new FontFamily(settings.MonospaceFontFamilyName);
        FontFamily = interfaceFont;
        FontSize = settings.FontSize;
        Resources["DesktopFontFamily"] = interfaceFont;
        Resources["DesktopMonospaceFontFamily"] = monospaceFont;
        Resources["DesktopFontSizeSmall"] = settings.FontSizeSmall;
        Resources["DesktopFontSize"] = settings.FontSize;
        Resources["DesktopFontSizeLarge"] = settings.FontSizeLarge;
        Resources["DesktopFontSizeLarger"] = settings.FontSizeLarger;
        Resources["DesktopFontSizeLargest"] = settings.FontSizeLargest;
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
        if (runtimeHost != null)
        {
            runtimeHost.Actions.GameStateChanged -= OnGameRunStateChanged;
        }

        viewModel.PluginSearch.Dispose();
        viewModel.Updates.Dispose();
        sdlInput.Dispose();
        gamepadBridge.Dispose();
        discord.Dispose();
        systemHotKeyService.Dispose();
        trayService.Dispose();
        SaveSettings();
    }

    // The runner raises GameStateChanged for many reasons; act only on running
    // transitions, tracked per game id, to drive the after-launch/after-close
    // window behavior.
    private void OnGameRunStateChanged(object sender, global::Playnite.SDK.Models.Game game)
    {
        if (game == null)
        {
            return;
        }

        bool started;
        if (game.IsRunning && runningGames.Add(game.Id))
        {
            started = true;
        }
        else if (!game.IsRunning && runningGames.Remove(game.Id))
        {
            started = false;
        }
        else
        {
            return;
        }

        var clearDiscord = !started && runningGames.Count == 0;
        Dispatcher.UIThread.Post(() =>
        {
            if (started)
            {
                discord.SetPresence(game.Name);
                ApplyAfterLaunch();
            }
            else
            {
                if (clearDiscord)
                {
                    discord.ClearPresence();
                }
                ApplyAfterGameClose();
            }
        });
    }

    internal void ApplyAfterLaunch()
    {
        if (hasClosed)
        {
            return;
        }

        switch (settings.AfterLaunch)
        {
            case AfterLaunchOption.Minimize:
                WindowState = WindowState.Minimized;
                break;
            case AfterLaunchOption.Close when !options.SelfTest:
                RequestExit();
                break;
        }
    }

    internal void ApplyAfterGameClose()
    {
        if (hasClosed)
        {
            return;
        }

        switch (settings.AfterGameClose)
        {
            case AfterGameCloseOption.Restore:
                RestoreFromTray();
                break;
            case AfterGameCloseOption.Exit when !options.SelfTest:
                RequestExit();
                break;
        }
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
        sdlInput.Start();
        ApplySystemHotKey();
        if (!options.PluginCompatibilityTest && !options.SelfTest)
        {
            try
            {
                await viewModel.Updates.StartAsync();
            }
            catch (OperationCanceledException) when (hasClosed)
            {
            }
            catch (Exception exception)
            {
                viewModel.SetStatusMessage($"Automatic update checks failed: {exception.Message}");
            }
            return;
        }

        if (automatedRunStarted)
        {
            return;
        }

        automatedRunStarted = true;
        if (options.PluginCompatibilityTest)
        {
            await InstalledPluginCompatibilityTest.Run(this, library, options);
        }
        else
        {
            await DesktopPilotSelfTest.Run(this, viewModel, library);
        }
    }

    private void LaunchProgramUpdater(string updaterPath)
    {
        var portable = global::Playnite.PlaynitePaths.IsPortable ? "/PORTABLE" : string.Empty;
        var programPath = global::Playnite.PlaynitePaths.ProgramPath;
        var arguments = $"/SILENT /NOCANCEL /DIR=\"{programPath}\" /UPDATE {portable}";
        var startInfo = new ProcessStartInfo(updaterPath, arguments)
        {
            UseShellExecute = true
        };
        if (!global::Playnite.Common.FileSystem.CanWriteToFolder(programPath))
        {
            startInfo.Verb = "runas";
        }
        Process.Start(startInfo);
        RequestExit();
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
