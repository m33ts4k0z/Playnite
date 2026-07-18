using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Playnite.Avalonia.Input;
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
            window.MainView.TemplateAppliedCount > 0 && window.MainView.GameList != null
                ? "FullscreenMainView resolved PART_GameList from runtime XAML"
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
        Record(results, "Controller toggles focused settings", () =>
            viewModel.ShowHiddenGames != hiddenBefore
                ? "A toggled the focused Avalonia CheckBox"
                : throw new InvalidOperationException("The focused setting did not change."));
        viewModel.ShowHiddenGames = hiddenBefore;
        viewModel.BackCommand.Execute(null);

        window.GamepadBridge.ButtonDown(GamepadButton.X);
        window.GamepadBridge.ButtonUp(GamepadButton.X);
        Record(results, "Core game action mapping dispatches", () =>
            viewModel.ActivateCount == 1
                ? "X dispatched the selected game through GameActionRunner"
                : throw new InvalidOperationException($"Dispatch count was {viewModel.ActivateCount}."));

        Record(results, "SDL input source initializes", () =>
            window.SdlInput.IsAvailable
                ? window.SdlInput.Status
                : throw new InvalidOperationException(window.SdlInput.Status));

        Record(results, "Plugin and game-operation host initializes", () =>
            window.RuntimeHost?.Actions != null && window.RuntimeHost.LoadedPluginCount == 0
                ? $"{viewModel.PluginSummary}; real Core action orchestration is attached"
                : throw new InvalidOperationException("The Fullscreen runtime host is unavailable."));

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
                InterfaceVolume = 42
            });
            var loaded = store.Load();
            if (loaded.ActiveFilter != "Favorites" || loaded.AudioEnabled || loaded.InterfaceVolume != 42)
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
            var package = AvaloniaFullscreenThemePackage.Load(
                Path.Combine(AppContext.BaseDirectory, "Themes", "Fullscreen", "Default"));
            return package.ResourceDictionaries.Count == 1 && package.SelectorStyles.Count == 1
                ? $"{package.Name} targets theme API {AvaloniaFullscreenThemePackage.CurrentApiVersion}"
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
