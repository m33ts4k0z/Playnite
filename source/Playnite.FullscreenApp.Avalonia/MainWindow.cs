using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.Input;
using Playnite.Avalonia.Theming;
using Playnite.FullscreenApp.Avalonia.Controls;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly FullscreenAppViewModel viewModel;
    private readonly PlayniteLibrary library;
    private readonly FullscreenRuntimeHost runtimeHost;
    private readonly FullscreenSettings settings;
    private readonly FullscreenSettingsStore settingsStore;
    private readonly StartupOptions options;
    private readonly RuntimeThemeManager themeManager;
    private readonly FullscreenMainView mainView;
    private readonly GamepadInputBridge gamepadBridge;
    private readonly SdlGamepadInputSource sdlInput;
    private FullscreenAudioService audioService;
    private AvaloniaThemePackage activeThemePackage;
    private readonly System.Windows.Input.ICommand focusedActivationCommand;
    private readonly System.Windows.Input.ICommand focusedGameActivationCommand;
    private static readonly Cursor hiddenCursor = new(StandardCursorType.None);
    private int guideFocusRequestCount;
    private readonly DispatcherTimer statusTimer;

    internal GamepadInputBridge GamepadBridge => gamepadBridge;
    internal SdlGamepadInputSource SdlInput => sdlInput;
    internal FullscreenMainView MainView => mainView;
    internal FullscreenRuntimeHost RuntimeHost => runtimeHost;
    internal FullscreenAudioService AudioService => audioService;
    internal bool IsMouseCursorHidden => ReferenceEquals(Cursor, hiddenCursor);
    internal int GuideFocusRequestCount => guideFocusRequestCount;

    internal MainWindow(
        FullscreenAppViewModel viewModel,
        PlayniteLibrary library,
        FullscreenRuntimeHost runtimeHost,
        FullscreenSettings settings,
        FullscreenSettingsStore settingsStore,
        StartupOptions options)
    {
        this.viewModel = viewModel;
        this.library = library;
        this.runtimeHost = runtimeHost;
        this.settings = settings;
        this.settingsStore = settingsStore;
        this.options = options;

        Title = "Playnite — Avalonia Fullscreen Pilot";
        Width = 1280;
        Height = 720;
        MinWidth = 960;
        MinHeight = 540;
        WindowState = options.Windowed ? WindowState.Normal : WindowState.FullScreen;
        DataContext = viewModel;

        themeManager = new RuntimeThemeManager(Application.Current, typeof(FullscreenMainView).Assembly);
        ApplyRuntimeTheme();
        themeManager.ApplyLanguage(ContentPath("Localization", "english.axaml"));

        mainView = new FullscreenMainView();
        Content = mainView;

        gamepadBridge = new GamepadInputBridge(this);
        focusedActivationCommand = new RelayCommand(ActivateFocusedControl);
        focusedGameActivationCommand = new RelayCommand(ActivateFocusedControlOrGame);
        UpdateGamepadBindings();
        gamepadBridge.MapCommand(GamepadButton.Start, viewModel.ToggleMenuCommand);
        gamepadBridge.MapCommand(GamepadButton.Back, viewModel.ToggleMenuCommand);
        gamepadBridge.MapCommand(GamepadButton.Y, viewModel.OpenSearchCommand);
        gamepadBridge.MapCommand(GamepadButton.LeftShoulder, viewModel.SelectPreviousCommand);
        gamepadBridge.MapCommand(GamepadButton.RightShoulder, viewModel.SelectNextCommand);
        gamepadBridge.MapCommand(GamepadButton.RightStick, viewModel.ToggleFiltersCommand);
        gamepadBridge.MapCommand(GamepadButton.LeftStick, viewModel.ToggleNotificationsCommand);
        sdlInput = new SdlGamepadInputSource(
            gamepadBridge,
            settings.EnableGameControllerSupport,
            settings.DisabledGameControllers);
        viewModel.Settings.Input.ConfigureControllerSource(
            () => sdlInput.Devices,
            (enabled, disabled) => sdlInput.ApplySettings(enabled, disabled));
        sdlInput.DevicesChanged += (_, _) => viewModel.Settings.Input.RefreshControllers();
        ApplyCursorSettings();

        viewModel.LibraryFocusRequested += (_, _) =>
            Dispatcher.UIThread.Post(mainView.FocusSelectedGame, DispatcherPriority.Input);
        viewModel.SettingsChanged += (_, _) => SaveSettings();
        viewModel.SettingsChanged += (_, _) => UpdateInputSettings();
        viewModel.SettingsChanged += (_, _) => audioService?.ApplySettings();
        viewModel.SettingsChanged += (_, _) => ApplyGeneralSettings();
        viewModel.GameLaunchSucceeded += (_, _) => MinimizeAfterGameLaunch();
        viewModel.NavigationRequested += (_, _) => audioService?.PlayNavigation();
        viewModel.ActivationRequested += (_, _) => audioService?.PlayActivation();
        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        statusTimer.Tick += (_, _) => UpdateStatusWidgets();
        KeyDown += OnKeyDown;
        Opened += OnOpened;
        Closed += OnClosed;
        Activated += (_, _) => audioService?.SetWindowActive(true);
        Deactivated += (_, _) => audioService?.SetWindowActive(false);
    }

    private void ApplyRuntimeTheme()
    {
        var defaultTheme = AvaloniaThemePackage.Load(
            ContentPath("Themes", "Fullscreen", "Default"),
            AvaloniaThemeMode.Fullscreen);
        themeManager.ApplyTheme(
            defaultTheme.ResourceDictionaries,
            selectorStyles: defaultTheme.SelectorStyles);
        activeThemePackage = defaultTheme;

        var customThemePath = options.CustomThemePath ?? settings.ThemePath;
        if (!string.IsNullOrWhiteSpace(customThemePath))
        {
            try
            {
                var customTheme = AvaloniaThemePackage.Load(
                    customThemePath,
                    AvaloniaThemeMode.Fullscreen);
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
                System.Diagnostics.Trace.WriteLine(exception);
            }
        }
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        ConfigureMonitorOptions();
        ApplyMonitorPlacement();
        mainView.FocusSelectedGame();
        sdlInput.Start();
        audioService = new FullscreenAudioService(settings, GetThemeRoot());
        UpdateStatusWidgets();
        statusTimer.Start();
        viewModel.SetStatusMessage(audioService.Status);
        if (options.SelfTest)
        {
            await FullscreenPilotSelfTest.Run(this, viewModel, library);
        }
    }

    private void ApplyMonitorPlacement()
    {
        if (options.Windowed || options.SelfTest || settings.UsePrimaryDisplay)
        {
            return;
        }

        var screens = Screens?.All;
        if (screens == null || screens.Count == 0)
        {
            return;
        }

        var index = MonitorSelection.Resolve(settings.Monitor, screens.Count);
        if (index == null)
        {
            return;
        }

        // Move to the requested display in the normal state, then let FullScreen
        // fill that screen — a fullscreen window ignores an in-place move.
        var target = screens[index.Value];
        WindowState = WindowState.Normal;
        Position = target.Bounds.Position;
        WindowState = WindowState.FullScreen;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        statusTimer.Stop();
        SaveSettings();
        viewModel.PluginSearch.Dispose();
        sdlInput.Dispose();
        gamepadBridge.Dispose();
        audioService?.Dispose();
    }

    private void ConfigureMonitorOptions()
    {
        var screens = Screens?.All;
        var names = screens == null
            ? Array.Empty<string>()
            : screens.Select((screen, index) =>
                $"Display {index + 1} — {screen.Bounds.Width}×{screen.Bounds.Height}").ToArray();
        viewModel.Settings.General.SetMonitors(names);
    }

    private void ApplyGeneralSettings()
    {
        UpdateStatusWidgets();
        ApplyMonitorPlacement();
    }

    private void UpdateStatusWidgets() =>
        viewModel.UpdateStatusWidgets(DateTime.Now, BatteryStatusService.Read());

    private void MinimizeAfterGameLaunch()
    {
        if (settings.MinimizeAfterGameStartup && !options.SelfTest)
        {
            WindowState = WindowState.Minimized;
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
            viewModel.SetStatusMessage($"Settings could not be saved: {exception.Message}");
        }
    }

    private void UpdateInputSettings()
    {
        UpdateGamepadBindings();
        sdlInput.ApplySettings(settings.EnableGameControllerSupport, settings.DisabledGameControllers);
        ApplyCursorSettings();
    }

    private void UpdateGamepadBindings()
    {
        var primaryCommand = settings.SwapStartDetailsAction
            ? focusedGameActivationCommand
            : focusedActivationCommand;
        gamepadBridge.MapCommand(
            GamepadButton.Confirm,
            settings.SwapConfirmCancelButtons ? viewModel.BackCommand : primaryCommand);
        gamepadBridge.MapCommand(
            GamepadButton.Cancel,
            settings.SwapConfirmCancelButtons ? primaryCommand : viewModel.BackCommand);
        gamepadBridge.MapCommand(
            GamepadButton.X,
            settings.SwapStartDetailsAction ? viewModel.ShowDetailsCommand : viewModel.ActivateCommand);
        if (settings.GuideButtonFocus)
        {
            gamepadBridge.MapCommand(GamepadButton.Guide, new RelayCommand(RefocusWindow));
        }
        else
        {
            gamepadBridge.UnmapCommand(GamepadButton.Guide);
        }
    }

    private void ActivateFocusedControl()
    {
        var focused = FocusManager?.GetFocusedElement();
        if (focused is Button button && button.Command?.CanExecute(button.CommandParameter) == true)
        {
            button.Command.Execute(button.CommandParameter);
            return;
        }

        if (focused is CheckBox checkBox)
        {
            checkBox.IsChecked = !(checkBox.IsChecked ?? false);
            return;
        }

        if (mainView.NotificationsList?.SelectedItem is Playnite.SDK.NotificationMessage notification &&
            focused is Control notificationControl &&
            notificationControl.GetVisualAncestors().Contains(mainView.NotificationsList))
        {
            if (notification.ActivationAction != null)
            {
                notification.ActivateCommand.Execute(null);
            }
            else
            {
                viewModel.DismissNotificationCommand.Execute(notification);
            }

            return;
        }

        viewModel.ConfirmCommand.Execute(null);
    }

    private void ActivateFocusedControlOrGame()
    {
        var focused = FocusManager?.GetFocusedElement() as Control;
        if (!viewModel.IsDetailsVisible &&
            !viewModel.IsMenuVisible &&
            !viewModel.IsSearchVisible &&
            !viewModel.IsFiltersVisible &&
            !viewModel.IsSettingsVisible &&
            !viewModel.IsNotificationsVisible &&
            !viewModel.IsActionPickerVisible &&
            !viewModel.IsDialogVisible &&
            focused != null &&
            mainView.GameList != null &&
            (ReferenceEquals(focused, mainView.GameList) ||
             focused.GetVisualAncestors().Contains(mainView.GameList)))
        {
            viewModel.ActivateCommand.Execute(null);
            return;
        }

        ActivateFocusedControl();
    }

    private void RefocusWindow()
    {
        guideFocusRequestCount++;
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = options.Windowed ? WindowState.Normal : WindowState.FullScreen;
        }

        Activate();
        mainView.FocusSelectedGame();
    }

    private void ApplyCursorSettings() => Cursor = settings.HideMouseCursor ? hiddenCursor : Cursor.Default;

    private string GetThemeRoot()
    {
        return activeThemePackage?.RootDirectory ?? ContentPath("Themes", "Fullscreen", "Default");
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            viewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            viewModel.ToggleMenuCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static string ContentPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory }.Concat(parts).ToArray());
}
