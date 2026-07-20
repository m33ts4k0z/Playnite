using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    private readonly SystemPowerService powerService = new();
    private readonly Dictionary<string, SdlGameControllerDevice> connectedControllers =
        new(StringComparer.OrdinalIgnoreCase);

    internal GamepadInputBridge GamepadBridge => gamepadBridge;
    internal SdlGamepadInputSource SdlInput => sdlInput;
    internal FullscreenMainView MainView => mainView;
    internal FullscreenRuntimeHost RuntimeHost => runtimeHost;
    internal FullscreenAudioService AudioService => audioService;
    internal bool IsMouseCursorHidden => ReferenceEquals(Cursor, hiddenCursor);
    internal int GuideFocusRequestCount => guideFocusRequestCount;
    internal SystemPowerService PowerService => powerService;

    internal void RestoreAndActivate() => RefocusWindow();

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
        ApplyVisualResources();
        themeManager.ApplyLanguage(
            LanguageCatalog.ResolveLanguagePaths(
                ContentPath("Localization"),
                settings.Language,
                "english.fullscreen.axaml"));

        mainView = new FullscreenMainView();
        Content = mainView;
        DragDrop.SetAllowDrop(mainView, true);
        DragDrop.AddDragOverHandler(mainView, OnDragOver);
        DragDrop.AddDropHandler(mainView, OnDrop);
        mainView.AddHandler(InputElement.GotFocusEvent, OnControlGotFocus, RoutingStrategies.Bubble);

        gamepadBridge = new GamepadInputBridge(this);
        focusedActivationCommand = new RelayCommand(ActivateFocusedControl);
        focusedGameActivationCommand = new RelayCommand(ActivateFocusedControlOrGame);
        UpdateGamepadBindings();
        gamepadBridge.MapCommand(GamepadButton.Start, new RelayCommand(HandleStartButton));
        gamepadBridge.MapCommand(GamepadButton.Back, viewModel.ToggleMenuCommand);
        gamepadBridge.MapCommand(GamepadButton.Y, new RelayCommand(HandleSpaceOrSearch));
        gamepadBridge.MapCommand(GamepadButton.LeftShoulder, new RelayCommand(() => HandleShoulder(-1)));
        gamepadBridge.MapCommand(GamepadButton.RightShoulder, new RelayCommand(() => HandleShoulder(1)));
        gamepadBridge.MapCommand(GamepadButton.RightStick, new RelayCommand(HandleCapsOrFilters));
        gamepadBridge.MapCommand(GamepadButton.LeftStick, viewModel.ToggleNotificationsCommand);
        sdlInput = new SdlGamepadInputSource(
            gamepadBridge,
            settings.EnableGameControllerSupport,
            settings.DisabledGameControllers);
        viewModel.Settings.Input.ConfigureControllerSource(
            () => sdlInput.Devices,
            (enabled, disabled) => sdlInput.ApplySettings(enabled, disabled));
        sdlInput.DevicesChanged += SdlInput_DevicesChanged;
        sdlInput.ButtonStateChanged += SdlInput_ButtonStateChanged;
        ApplyCursorSettings();

        viewModel.LibraryFocusRequested += (_, _) =>
            Dispatcher.UIThread.Post(mainView.FocusSelectedGame, DispatcherPriority.Input);
        viewModel.SettingsChanged += (_, _) => SaveSettings();
        viewModel.SettingsChanged += (_, _) => UpdateInputSettings();
        viewModel.SettingsChanged += (_, _) => audioService?.ApplySettings();
        viewModel.SettingsChanged += (_, _) => ApplyGeneralSettings();
        viewModel.SettingsChanged += (_, _) => ApplyVisualResources();
        viewModel.GameLaunchSucceeded += (_, _) => MinimizeAfterGameLaunch();
        viewModel.RestoreRequested += (_, _) => RefocusWindow();
        viewModel.MinimizeRequested += (_, _) => WindowState = WindowState.Minimized;
        viewModel.PowerActionRequested += ExecutePowerAction;
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
        if (options.Windowed || options.SelfTest)
        {
            return;
        }

        var screens = Screens?.All;
        if (screens == null || screens.Count == 0)
        {
            return;
        }

        // Move to the requested display in the normal state, then let FullScreen
        // fill that screen — a fullscreen window ignores an in-place move.
        var target = Screens.Primary;
        if (!settings.UsePrimaryDisplay)
        {
            var index = MonitorSelection.Resolve(settings.Monitor, screens.Count);
            if (index == null)
            {
                return;
            }

            target = screens[index.Value];
        }

        if (target == null)
        {
            return;
        }

        WindowState = WindowState.Normal;
        Position = target.Bounds.Position;
        WindowState = WindowState.FullScreen;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        statusTimer.Stop();
        SaveSettings();
        viewModel.PluginSearch.Dispose();
        sdlInput.DevicesChanged -= SdlInput_DevicesChanged;
        sdlInput.ButtonStateChanged -= SdlInput_ButtonStateChanged;
        sdlInput.Dispose();
        gamepadBridge.Dispose();
        audioService?.Dispose();
    }

    private void OnControlGotFocus(object sender, RoutedEventArgs e)
    {
        if (!viewModel.IsSettingsVisible)
        {
            viewModel.SetFocusedSettingsDescription(string.Empty);
            return;
        }

        var description = e.Source is Control control
            ? ToolTip.GetTip(control) as string
            : null;
        viewModel.SetFocusedSettingsDescription(description ?? string.Empty);
    }

    private void ConfigureMonitorOptions()
    {
        var screens = Screens?.All;
        var names = screens == null
            ? Array.Empty<string>()
            : screens.Select((screen, index) =>
                $"Display {index + 1}{(screen.IsPrimary ? " (Primary)" : string.Empty)} — " +
                $"{screen.Bounds.Width}×{screen.Bounds.Height}").ToArray();
        viewModel.Settings.General.SetMonitors(names);
    }

    private void ApplyGeneralSettings()
    {
        UpdateStatusWidgets();
        ApplyMonitorPlacement();
    }

    private void ApplyVisualResources()
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.Resources["FullscreenFontSize"] = settings.FontSize;
        Application.Current.Resources["FullscreenFontSizeSmall"] = settings.FontSizeSmall;
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

    private void ExecutePowerAction(SystemPowerAction action)
    {
        var result = powerService.Execute(action);
        viewModel.SetStatusMessage(result.Message);
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
            new RelayCommand(HandleBackspaceOrAction));
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
            !viewModel.IsCommandMenuVisible &&
            !viewModel.IsTextInputVisible &&
            !viewModel.IsGameStatusVisible &&
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

    private void HandleStartButton()
    {
        if (viewModel.IsTextInputVisible)
        {
            viewModel.ConfirmTextInputCommand.Execute(null);
        }
        else if (viewModel.IsSearchVisible)
        {
            viewModel.CloseSearchCommand.Execute(null);
        }
        else
        {
            viewModel.OpenGameMenuCommand.Execute(null);
        }
    }

    private void HandleSpaceOrSearch()
    {
        var keyboard = mainView.ActiveKeyboard;
        if (keyboard != null)
        {
            keyboard.AddSpace();
        }
        else
        {
            viewModel.OpenSearchCommand.Execute(null);
        }
    }

    private void HandleBackspaceOrAction()
    {
        var keyboard = mainView.ActiveKeyboard;
        if (keyboard != null)
        {
            keyboard.Backspace();
        }
        else if (settings.SwapStartDetailsAction)
        {
            viewModel.ShowDetailsCommand.Execute(null);
        }
        else
        {
            viewModel.ActivateCommand.Execute(null);
        }
    }

    private void HandleShoulder(int offset)
    {
        var keyboard = mainView.ActiveKeyboard;
        if (keyboard != null)
        {
            keyboard.Clear();
        }
        else if (viewModel.IsFiltersVisible)
        {
            (offset < 0 ? viewModel.CyclePreviousPresetCommand : viewModel.CycleNextPresetCommand).Execute(null);
        }
        else
        {
            (offset < 0 ? viewModel.SelectPreviousCommand : viewModel.SelectNextCommand).Execute(null);
        }
    }

    private void HandleCapsOrFilters()
    {
        var keyboard = mainView.ActiveKeyboard;
        if (keyboard != null)
        {
            keyboard.ToggleCaps();
        }
        else
        {
            viewModel.ToggleFiltersCommand.Execute(null);
        }
    }

    private void SdlInput_ButtonStateChanged(object sender, GamepadButtonStateChangedEventArgs e) =>
        runtimeHost?.NotifyControllerButtonStateChanged(e.Button, e.IsPressed);

    private void SdlInput_DevicesChanged(object sender, EventArgs e)
    {
        viewModel.Settings.Input.RefreshControllers();
        var current = sdlInput.Devices.ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var device in current.Values.Where(device => !connectedControllers.ContainsKey(device.Id)))
        {
            runtimeHost?.NotifyControllerConnected(device);
        }

        foreach (var device in connectedControllers.Values.Where(device => !current.ContainsKey(device.Id)))
        {
            runtimeHost?.NotifyControllerDisconnected(device);
        }

        connectedControllers.Clear();
        foreach (var device in current)
        {
            connectedControllers.Add(device.Key, device.Value);
        }
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

    private void OnDragOver(object sender, DragEventArgs args)
    {
        args.DragEffects = GetDroppedAddonPaths(args).Count > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        args.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs args)
    {
        foreach (var path in GetDroppedAddonPaths(args))
        {
            try
            {
                InstallDroppedAddon(path);
            }
            catch (Exception exception)
            {
                viewModel.SetStatusMessage($"The dropped add-on could not be installed: {exception.Message}");
            }
        }

        args.DragEffects = DragDropEffects.Copy;
        args.Handled = true;
    }

    private void InstallDroppedAddon(string path)
    {
        var extension = Path.GetExtension(path);
        string name;
        if (extension.Equals(global::Playnite.PlaynitePaths.PackedThemeFileExtention, StringComparison.OrdinalIgnoreCase))
        {
            global::Playnite.Plugins.ExtensionInstaller.VerifyThemePackage(path);
            var manifest = global::Playnite.Plugins.ExtensionInstaller.GetPackedThemeManifest(path);
            manifest.VerifyManifest();
            name = manifest.Name;
        }
        else
        {
            global::Playnite.Plugins.ExtensionInstaller.VerifyExtensionPackage(path);
            var manifest = global::Playnite.Plugins.ExtensionInstaller.GetPackedExtensionManifest(path);
            manifest.VerifyManifest();
            name = manifest.Name;
        }

        viewModel.OpenDialog(
            Localize("LOCInstallAddon", "Install add-on"),
            string.Format(Localize("LOCInstallAddonPrompt", "Queue '{0}' for installation?"), name),
            new[] { Localize("LOCInstall", "Install"), Localize("LOCCancelLabel", "Cancel") },
            0,
            1,
            result =>
            {
                if (result != Localize("LOCInstall", "Install"))
                {
                    return;
                }

                global::Playnite.Plugins.ExtensionInstaller.QueuePackageInstall(path);
                viewModel.OpenDialog(
                    Localize("LOCRestartRequired", "Restart required"),
                    Localize("LOCAddonInstallRestartMessage", "The add-on will be installed after Playnite restarts."),
                    new[] { Localize("LOCRestartNow", "Restart now"), Localize("LOCLater", "Later") },
                    0,
                    1,
                    restartResult =>
                    {
                        if (restartResult == Localize("LOCRestartNow", "Restart now"))
                        {
                            viewModel.RestartApplicationCommand.Execute(null);
                        }
                    });
            });
    }

    internal static bool IsSupportedDroppedAddon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(global::Playnite.PlaynitePaths.PackedExtensionFileExtention, StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(global::Playnite.PlaynitePaths.PackedThemeFileExtention, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetDroppedAddonPaths(DragEventArgs args) =>
        args.DataTransfer.TryGetFiles()
            ?.Select(file => file.TryGetLocalPath())
            .Where(IsSupportedDroppedAddon)
            .ToList() ?? new List<string>();

    private static string Localize(string key, string fallback)
    {
        var value = Playnite.SDK.ResourceProvider.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key || value == $"<!{key}!>"
            ? fallback
            : value;
    }

    private static string ContentPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory }.Concat(parts).ToArray());
}
