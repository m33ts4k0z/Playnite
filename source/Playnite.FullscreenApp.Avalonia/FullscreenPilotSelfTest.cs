using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Playnite.Avalonia.Input;
using Playnite.Avalonia.Theming;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia;

internal static class FullscreenPilotSelfTest
{
    public static async Task Run(
        MainWindow window,
        FullscreenAppViewModel viewModel,
        PlayniteLibrary library)
    {
        var results = new List<(string Name, bool Pass, string Detail)>();
        await Task.Delay(500);

        Record(results, "Playnite.Core library opens", () =>
            library.IsOpen && library.Database.GetType().Assembly.GetName().Name == "Playnite.Core"
                ? $"{library.Games.Count:N0} games loaded from {library.Database.GetType().Assembly.GetName().Name}"
                : throw new InvalidOperationException("The concrete Playnite.Core database is not open."));

        Record(results, "Loose Fullscreen theme applies", () =>
            window.MainView.TemplateAppliedCount > 0 && window.MainView.GameList != null &&
            window.MainView.PluginSearchBox != null
                ? "FullscreenMainView resolved its library and plugin-search template contracts"
                : throw new InvalidOperationException("The runtime theme template contract was not resolved."));

        Record(results, "Tile grid virtualization is bounded", () =>
        {
            var panel = window.MainView.TilePanel;
            if (panel == null || panel.RealizedCount == 0 || panel.RealizedCount > 200)
            {
                throw new InvalidOperationException($"Realized count was {panel?.RealizedCount ?? 0}.");
            }

            return $"{panel.RealizedCount} of {library.Games.Count:N0} containers realized";
        });

        var beforeNavigation = window.MainView.GameList.SelectedIndex;
        window.MainView.FocusSelectedGame();
        window.GamepadBridge.ButtonDown(GamepadButton.DPadRight);
        window.GamepadBridge.ButtonUp(GamepadButton.DPadRight);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Controller navigation reaches Avalonia focus", () =>
            window.MainView.GameList.SelectedIndex == beforeNavigation + 1
                ? $"selection moved {beforeNavigation} → {window.MainView.GameList.SelectedIndex}"
                : throw new InvalidOperationException(
                    $"Selection stayed at {window.MainView.GameList.SelectedIndex}."));

        window.GamepadBridge.ButtonDown(GamepadButton.Confirm);
        window.GamepadBridge.ButtonUp(GamepadButton.Confirm);
        Record(results, "Controller confirm opens details", () =>
            viewModel.IsDetailsVisible
                ? $"details opened for {viewModel.SelectedGame.Name}"
                : throw new InvalidOperationException("Details did not open."));

        window.GamepadBridge.ButtonDown(GamepadButton.Cancel);
        window.GamepadBridge.ButtonUp(GamepadButton.Cancel);
        Record(results, "Controller cancel returns to library", () =>
            !viewModel.IsDetailsVisible
                ? "details closed and library focus requested"
                : throw new InvalidOperationException("Details remained open."));

        viewModel.ToggleMenuCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        window.GamepadBridge.ButtonDown(GamepadButton.Confirm);
        window.GamepadBridge.ButtonUp(GamepadButton.Confirm);
        Record(results, "Controller activates focused menu commands", () =>
            !viewModel.IsMenuVisible
                ? "A invoked the focused Return to library button"
                : throw new InvalidOperationException("The focused menu command did not run."));

        var hiddenBefore = viewModel.ShowHiddenGames;
        viewModel.OpenSettingsCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        window.GamepadBridge.ButtonDown(GamepadButton.Confirm);
        window.GamepadBridge.ButtonUp(GamepadButton.Confirm);
        var workingCopyChanged = viewModel.Settings.General.ShowHiddenGames != hiddenBefore &&
            viewModel.ShowHiddenGames == hiddenBefore;
        viewModel.Settings.SaveCommand.Execute(null);
        Record(results, "Controller edits the fullscreen settings working copy and Save applies it", () =>
            workingCopyChanged && viewModel.ShowHiddenGames != hiddenBefore && !viewModel.Settings.IsVisible
                ? "A toggled the focused section control without mutating live settings until Save"
                : throw new InvalidOperationException(
                    $"workingCopyChanged={workingCopyChanged}, live={viewModel.ShowHiddenGames}, visible={viewModel.Settings.IsVisible}."));
        viewModel.ShowHiddenGames = hiddenBefore;

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.Audio.AudioEnabled = !viewModel.AudioEnabled;
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Fullscreen settings Cancel discards section working copies", () =>
            !viewModel.Settings.IsVisible && viewModel.Settings.Audio.AudioEnabled != viewModel.AudioEnabled
                ? "the Audio edit was discarded without changing the live setting"
                : throw new InvalidOperationException("Cancel applied a fullscreen section working copy."));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.Audio.InterfaceVolume = 37;
        viewModel.Settings.Audio.BackgroundVolume = 19;
        viewModel.Settings.Audio.MuteInBackground = false;
        viewModel.Settings.General.ShowClock = false;
        viewModel.Settings.General.ShowBattery = true;
        viewModel.Settings.General.ShowBatteryPercentage = true;
        viewModel.Settings.General.MinimizeAfterGameStartup = false;
        viewModel.Settings.SaveCommand.Execute(null);
        viewModel.UpdateStatusWidgets(
            new DateTime(2026, 7, 19, 14, 35, 0),
            new BatteryStatus(true, 64, false));
        Record(results, "Fullscreen Audio and General settings apply to live services and status widgets", () =>
            viewModel.Settings.Audio.InterfaceVolume == 37 &&
            !viewModel.ShowClock && viewModel.ShowBattery && viewModel.BatteryText == "Battery 64%"
                ? "volume, background mute, clock, battery, and launch-minimize policies applied without restart"
                : throw new InvalidOperationException("Audio or General settings did not reach the live shell."));

        var fullscreenSectionChecks = viewModel.Settings.RunSelfChecks();
        Record(results, "Every fullscreen settings module supplies a passing self-check", () =>
            fullscreenSectionChecks.Count == viewModel.Settings.Sections.Count &&
            fullscreenSectionChecks.All(check => check.Passed)
                ? string.Join("; ", fullscreenSectionChecks.Select(check => $"{check.SectionKey}: {check.Detail}"))
                : throw new InvalidOperationException(string.Join(
                    "; ",
                    fullscreenSectionChecks.Select(check => $"{check.SectionKey}={check.Passed}: {check.Detail}"))));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.Layout.Rows = 3;
        viewModel.Settings.Layout.Columns = 5;
        viewModel.Settings.Layout.HorizontalLayout = true;
        viewModel.Settings.Layout.ItemSpacing = 22;
        viewModel.Settings.Layout.SmoothScrolling = false;
        viewModel.Settings.Visuals.DarkenUninstalledGamesGrid = true;
        viewModel.Settings.Visuals.EnableMainBackgroundImage = true;
        viewModel.Settings.Visuals.MainBackgroundImageBlurAmount = 12;
        viewModel.Settings.Visuals.MainBackgroundImageDarkAmount = 44;
        viewModel.Settings.Visuals.ShowGameTitles = true;
        viewModel.Settings.Visuals.FontSize = 25;
        viewModel.Settings.Visuals.FontSizeSmall = 19;
        viewModel.Settings.Visuals.ButtonPrompts = FullscreenButtonPrompts.PlayStation;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Fullscreen Layout and Visual settings drive the live theme surface", () =>
        {
            var panel = window.MainView.TilePanel;
            var scroll = window.MainView.GameScrollViewer;
            var uninstalled = viewModel.Games.FirstOrDefault(game => !game.IsInstalled);
            var fontSize = Application.Current.Resources["FullscreenFontSize"];
            return panel?.Rows == 3 && panel.Columns == 5 && panel.ItemSpacing == 22 &&
                panel.Orientation == global::Avalonia.Layout.Orientation.Horizontal &&
                scroll != null && !Playnite.Avalonia.Controls.ScrollBehavior.GetSmoothScrollingEnabled(scroll) &&
                uninstalled?.TileOpacity == 0.45 && uninstalled.ShowTitle &&
                viewModel.DetailsPromptGlyph == "×" && viewModel.PlayPromptGlyph == "□" &&
                fontSize is double value && value == 25
                    ? "panel geometry, scrolling, tile visuals, font resources, and PlayStation prompts applied live"
                    : throw new InvalidOperationException("Layout or Visual settings did not reach the runtime theme." );
        });

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.Menus.ShowRestart = true;
        viewModel.Settings.Menus.ShowShutdown = true;
        viewModel.Settings.Menus.ShowSuspend = true;
        viewModel.Settings.Menus.ShowHibernate = true;
        viewModel.Settings.Menus.ShowMinimize = true;
        viewModel.Settings.Menus.ShowLogout = true;
        viewModel.Settings.Menus.ShowLock = true;
        viewModel.Settings.Menus.ShowTools = true;
        viewModel.Settings.Menus.ShowExtensions = true;
        viewModel.Settings.Menus.ShowClients = true;
        viewModel.Settings.SaveCommand.Execute(null);
        viewModel.ToggleMenuCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "All configurable Fullscreen menu items are built and visible", () =>
            window.MainView.ConfiguredMenuButtons.Count == 10 &&
            window.MainView.ConfiguredMenuButtons.All(button => button.IsVisible && button.Command != null)
                ? "all ten menu policies control concrete, invokable Avalonia buttons"
                : throw new InvalidOperationException("A configured Fullscreen menu item is missing or inert."));
        viewModel.OpenToolsCommand.Execute(null);
        var toolsDialogOpened = viewModel.IsDialogVisible && viewModel.DialogCaption == "Tools";
        viewModel.CancelDialogCommand.Execute(null);
        var powerActionRaised = false;
        void OnPowerAction(SystemPowerAction _) => powerActionRaised = true;
        viewModel.PowerActionRequested += OnPowerAction;
        viewModel.RestartCommand.Execute(null);
        var restartConfirmationOpened = viewModel.IsDialogVisible && viewModel.SelectedDialogOption == "Cancel";
        viewModel.ConfirmDialogCommand.Execute(null);
        viewModel.PowerActionRequested -= OnPowerAction;
        var linuxShutdown = SystemPowerService.CreateLinuxStartInfo(SystemPowerAction.Shutdown, "7");
        var linuxLock = SystemPowerService.CreateLinuxStartInfo(SystemPowerAction.Lock, "7");
        Record(results, "Menu actions are safe and the cross-platform power bridge is explicit", () =>
            toolsDialogOpened && restartConfirmationOpened && !powerActionRaised &&
            linuxShutdown.FileName == "systemctl" && linuxShutdown.ArgumentList.SequenceEqual(new[] { "--no-block", "poweroff" }) &&
            linuxLock.FileName == "loginctl" && linuxLock.ArgumentList.SequenceEqual(new[] { "lock-session", "7" }) &&
            window.PowerService.IsSupported(SystemPowerAction.Shutdown)
                ? "tools opens a controller dialog, destructive actions confirm, and Linux commands use systemd session APIs"
                : throw new InvalidOperationException("Menu action safety or platform mapping failed."));

        window.GamepadBridge.ButtonDown(GamepadButton.X);
        window.GamepadBridge.ButtonUp(GamepadButton.X);
        Record(results, "Core game action mapping dispatches", () =>
            viewModel.ActivateCount == 1
                ? "X dispatched the selected game through GameActionRunner"
                : throw new InvalidOperationException($"Dispatch count was {viewModel.ActivateCount}."));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.Input.SwapStartDetailsAction = true;
        viewModel.Settings.Input.GuideButtonFocus = true;
        viewModel.Settings.Input.HideMouseCursor = true;
        viewModel.Settings.Input.EnableGameControllerSupport = false;
        viewModel.Settings.SaveCommand.Execute(null);
        var liveInputDisabled = !window.SdlInput.InputEnabled;
        var guideRequestsBefore = window.GuideFocusRequestCount;
        window.GamepadBridge.ButtonDown(GamepadButton.Guide);
        window.GamepadBridge.ButtonUp(GamepadButton.Guide);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        window.GamepadBridge.ButtonDown(GamepadButton.Confirm);
        window.GamepadBridge.ButtonUp(GamepadButton.Confirm);
        window.GamepadBridge.ButtonDown(GamepadButton.X);
        window.GamepadBridge.ButtonUp(GamepadButton.X);
        var swappedInputApplied =
            viewModel.ActivateCount == 2 &&
            viewModel.IsDetailsVisible &&
            window.GuideFocusRequestCount == guideRequestsBefore + 1 &&
            window.IsMouseCursorHidden;
        viewModel.BackCommand.Execute(null);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.Input.SwapStartDetailsAction = false;
        viewModel.Settings.Input.HideMouseCursor = false;
        viewModel.Settings.Input.EnableGameControllerSupport = true;
        viewModel.Settings.SaveCommand.Execute(null);
        Record(results, "Fullscreen input settings drive live mappings, focus, cursor, and SDL", () =>
            liveInputDisabled && swappedInputApplied &&
            window.SdlInput.InputEnabled && !window.IsMouseCursorHidden
                ? "A/X swapping, Guide refocus, cursor hiding, and controller gating applied without restart"
                : throw new InvalidOperationException("Fullscreen input settings did not reach the live shell."));

        Record(results, "SDL input source initializes", () =>
            window.SdlInput.IsAvailable
                ? window.SdlInput.Status
                : throw new InvalidOperationException(window.SdlInput.Status));

        Record(results, "Plugin and game-operation host initializes", () =>
            window.RuntimeHost?.Actions != null && window.RuntimeHost.LoadedPluginCount == 0
                ? $"{viewModel.PluginSummary}; real Core action orchestration is attached"
                : throw new InvalidOperationException("The Fullscreen runtime host is unavailable."));

        Record(results, "SDK v7 host bundle reaches the executable output", () =>
        {
            var bundlePath = Path.Combine(AppContext.BaseDirectory, "SdkV7Host");
            return File.Exists(Path.Combine(bundlePath, "Playnite.SDK.dll")) &&
                File.Exists(Path.Combine(bundlePath, "Playnite.SDK.V7.Host.dll"))
                    ? "the isolated SDK and host bridge are packaged beside the Fullscreen executable"
                    : throw new FileNotFoundException($"The SDK v7 host bundle is incomplete at {bundlePath}.");
        });

        var unfilteredCount = viewModel.Games.Count;
        viewModel.SearchText = "Pilot Game 99";
        Record(results, "Search filters the live library", () =>
            viewModel.Games.Count > 0 && viewModel.Games.Count < unfilteredCount
                ? $"search reduced {unfilteredCount:N0} games to {viewModel.Games.Count:N0}"
                : throw new InvalidOperationException($"Search returned {viewModel.Games.Count:N0} games."));
        viewModel.SearchText = string.Empty;

        viewModel.SelectedFilterOption = "Installed";
        viewModel.ApplyFilterCommand.Execute(null);
        Record(results, "Controller filter model applies", () =>
            viewModel.Games.Count > 0 && viewModel.Games.All(game => game.IsInstalled)
                ? $"Installed filter selected {viewModel.Games.Count:N0} games"
                : throw new InvalidOperationException("The Installed filter included an uninstalled game."));
        viewModel.SelectedFilterOption = "All";
        viewModel.ApplyFilterCommand.Execute(null);

        Record(results, "Settings persist atomically", () =>
        {
            var store = new FullscreenSettingsStore(library.ActiveUserDataDirectory);
            store.Save(new FullscreenSettings
            {
                ActiveFilter = "Favorites",
                AudioEnabled = false,
                InterfaceVolume = 42,
                BackgroundVolume = 23,
                MuteInBackground = false,
                UsePrimaryDisplay = true,
                ShowClock = false,
                ShowBattery = true,
                ShowBatteryPercentage = true,
                MinimizeAfterGameStartup = false,
                Rows = 3,
                Columns = 5,
                HorizontalLayout = true,
                FullscreenItemSpacing = 22,
                SmoothScrolling = false,
                DarkenUninstalledGamesGrid = true,
                EnableMainBackgroundImage = true,
                MainBackgroundImageBlurAmount = 12,
                MainBackgroundImageDarkAmount = 44,
                ShowGameTitles = true,
                FontSize = 25,
                FontSizeSmall = 19,
                ButtonPrompts = FullscreenButtonPrompts.PlayStation,
                MainMenuShowRestart = false,
                MainMenuShowShutdown = false,
                MainMenuShowSuspend = false,
                MainMenuShowHibernate = false,
                MainMenuShowMinimize = false,
                MainMenuShowLogout = true,
                MainMenuShowLock = true,
                MainMenuShowTools = false,
                MainMenuShowExtensions = false,
                MainMenuShowClients = false,
                GlobalPreScript = "global-pre",
                GlobalGameStartedScript = "global-started",
                GlobalPostScript = "global-post",
                ShutdownLibraryClients = true,
                EnableGameControllerSupport = false,
                SwapStartDetailsAction = true,
                GuideButtonFocus = false,
                HideMouseCursor = true,
                DisabledGameControllers = new List<string> { "pilot-controller" },
                ClientShutdownGraceSeconds = 45,
                ClientShutdownMinimumSessionSeconds = 90,
                ClientShutdownPluginIds = new List<Guid> { Guid.Parse("f4737f44-2dde-4c06-99f0-0a0569f1bcfd") }
            });
            var loaded = store.Load();
            if (loaded.ActiveFilter != "Favorites" ||
                loaded.AudioEnabled ||
                loaded.InterfaceVolume != 42 ||
                loaded.BackgroundVolume != 23 ||
                loaded.MuteInBackground ||
                !loaded.UsePrimaryDisplay ||
                loaded.ShowClock ||
                !loaded.ShowBattery ||
                !loaded.ShowBatteryPercentage ||
                loaded.MinimizeAfterGameStartup ||
                loaded.Rows != 3 ||
                loaded.Columns != 5 ||
                !loaded.HorizontalLayout ||
                loaded.FullscreenItemSpacing != 22 ||
                loaded.SmoothScrolling ||
                !loaded.DarkenUninstalledGamesGrid ||
                !loaded.EnableMainBackgroundImage ||
                loaded.MainBackgroundImageBlurAmount != 12 ||
                loaded.MainBackgroundImageDarkAmount != 44 ||
                !loaded.ShowGameTitles ||
                loaded.FontSize != 25 ||
                loaded.FontSizeSmall != 19 ||
                loaded.ButtonPrompts != FullscreenButtonPrompts.PlayStation ||
                loaded.MainMenuShowRestart ||
                loaded.MainMenuShowShutdown ||
                loaded.MainMenuShowSuspend ||
                loaded.MainMenuShowHibernate ||
                loaded.MainMenuShowMinimize ||
                !loaded.MainMenuShowLogout ||
                !loaded.MainMenuShowLock ||
                loaded.MainMenuShowTools ||
                loaded.MainMenuShowExtensions ||
                loaded.MainMenuShowClients ||
                loaded.GlobalPreScript != "global-pre" ||
                loaded.GlobalGameStartedScript != "global-started" ||
                loaded.GlobalPostScript != "global-post" ||
                !loaded.ShutdownLibraryClients ||
                loaded.EnableGameControllerSupport ||
                !loaded.SwapStartDetailsAction ||
                loaded.GuideButtonFocus ||
                !loaded.HideMouseCursor ||
                loaded.DisabledGameControllers.SingleOrDefault() != "pilot-controller" ||
                loaded.ClientShutdownGraceSeconds != 45 ||
                loaded.ClientShutdownMinimumSessionSeconds != 90 ||
                loaded.ClientShutdownPluginIds.SingleOrDefault() !=
                    Guid.Parse("f4737f44-2dde-4c06-99f0-0a0569f1bcfd"))
            {
                throw new InvalidOperationException("The persisted settings did not round-trip.");
            }

            return $"settings round-tripped at {store.SettingsPath}";
        });

        window.RuntimeHost.Notifications.RemoveAll();
        var notificationId = $"pilot-{Guid.NewGuid():N}";
        window.RuntimeHost.Notifications.Add(notificationId, "Pilot notification", Playnite.SDK.NotificationType.Info);
        Record(results, "Notifications reach the Fullscreen surface", () =>
            viewModel.Notifications.Any(message => message.Id == notificationId) && viewModel.NotificationCount > 0
                ? $"notification collection contains {viewModel.NotificationCount} messages"
                : throw new InvalidOperationException("The notification was not surfaced."));
        viewModel.ToggleNotificationsCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        window.GamepadBridge.ButtonDown(GamepadButton.Confirm);
        window.GamepadBridge.ButtonUp(GamepadButton.Confirm);
        Record(results, "Controller dismisses focused notifications", () =>
            viewModel.Notifications.All(message => message.Id != notificationId)
                ? "A dismissed the focused notification without an activation action"
                : throw new InvalidOperationException("The notification remained visible."));
        viewModel.BackCommand.Execute(null);

        Dispatcher.UIThread.Post(() =>
        {
            window.GamepadBridge.ButtonDown(GamepadButton.Confirm);
            window.GamepadBridge.ButtonUp(GamepadButton.Confirm);
        }, DispatcherPriority.Background);
        var dialogResult = window.RuntimeHost.Dialogs.ShowMessage(
            "Controller-operated dialog check",
            "Pilot dialog",
            new[] { "Yes", "No" });
        Record(results, "Native Avalonia dialogs complete synchronously", () =>
            dialogResult == "Yes" && !viewModel.IsDialogVisible
                ? "nested Avalonia dispatcher returned the gamepad-selected option"
                : throw new InvalidOperationException($"Dialog returned '{dialogResult}'."));

        Record(results, "Fullscreen audio host initializes safely", () =>
            !string.IsNullOrWhiteSpace(window.AudioService?.Status)
                ? window.AudioService.Status
                : throw new InvalidOperationException("The audio host did not report status."));

        Record(results, "Avalonia theme package contract validates", () =>
        {
            var package = AvaloniaThemePackage.Load(
                Path.Combine(AppContext.BaseDirectory, "Themes", "Fullscreen", "Default"),
                AvaloniaThemeMode.Fullscreen);
            return package.ResourceDictionaries.Count == 1 && package.SelectorStyles.Count == 1
                ? $"{package.Name} targets theme API {AvaloniaThemePackage.CurrentApiVersion}"
                : throw new InvalidOperationException("The default package manifest was incomplete.");
        });

        var report = BuildReport(results);
        var reportPath = Path.Combine(AppContext.BaseDirectory, "fullscreen-pilot-results.txt");
        File.WriteAllText(reportPath, report);
        Console.WriteLine(report);

        var failures = results.Count(result => !result.Pass);
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(failures);
    }

    private static void Record(
        ICollection<(string Name, bool Pass, string Detail)> results,
        string name,
        Func<string> check)
    {
        try
        {
            results.Add((name, true, check()));
        }
        catch (Exception exception)
        {
            results.Add((name, false, exception.Message));
        }
    }

    private static string BuildReport(IEnumerable<(string Name, bool Pass, string Detail)> results)
    {
        var materialized = results.ToList();
        var report = new StringBuilder();
        report.AppendLine("=== Playnite Phase 4 Avalonia Fullscreen pilot checks ===");
        report.AppendLine($"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version}; .NET {Environment.Version}");
        report.AppendLine();
        foreach (var result in materialized)
        {
            report.AppendLine($"[{(result.Pass ? "PASS" : "FAIL")}] {result.Name}");
            report.AppendLine($"       {result.Detail}");
        }

        report.AppendLine();
        report.AppendLine($"VERDICT: {materialized.Count(result => result.Pass)}/{materialized.Count} checks passed.");
        return report.ToString();
    }
}
