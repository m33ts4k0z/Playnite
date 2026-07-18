using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

internal static class DesktopPilotSelfTest
{
    public static async Task Run(
        MainWindow window,
        DesktopAppViewModel viewModel,
        DesktopLibrary library)
    {
        var results = new List<(string Name, bool Pass, string Detail)>();
        await Task.Delay(500);

        Record(results, "Playnite.Core library opens", () =>
            library.IsOpen && library.Database.GetType().Assembly.GetName().Name == "Playnite.Core"
                ? $"{library.Games.Count:N0} games loaded from Playnite.Core"
                : throw new InvalidOperationException("The concrete Core database is not open."));

        Record(results, "Loose Desktop theme applies", () =>
            window.MainView.TemplateAppliedCount > 0 && window.MainView.GameList != null
                ? "DesktopMainView resolved its runtime template contract"
                : throw new InvalidOperationException("The Desktop theme template was not applied."));

        Record(results, "Grid and list views share the Desktop model", () =>
            window.MainView.GridGameList != null && window.MainView.ListGameList != null
                ? "both virtualized view surfaces resolved from the loose theme"
                : throw new InvalidOperationException("One of the Desktop library views was not resolved."));

        Record(results, "Desktop tile grid stays virtualized", () =>
        {
            var realized = window.MainView.TilePanel?.RealizedCount ?? 0;
            return realized > 0 && realized <= 200
                ? $"{realized} of {library.Games.Count:N0} containers realized"
                : throw new InvalidOperationException($"Realized container count was {realized}.");
        });

        viewModel.SelectedViewMode = "List";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Desktop view switching is live", () =>
            viewModel.IsListView && window.MainView.GameList == window.MainView.ListGameList
                ? "the persisted view selector switched to the list surface"
                : throw new InvalidOperationException("The list surface did not become active."));
        viewModel.SelectedViewMode = "Grid";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        var before = viewModel.Games.Count;
        viewModel.SearchText = "Desktop Pilot 99";
        Record(results, "Desktop search filters live data", () =>
            viewModel.Games.Count > 0 && viewModel.Games.Count < before
                ? $"search reduced {before:N0} games to {viewModel.Games.Count:N0}"
                : throw new InvalidOperationException($"Search returned {viewModel.Games.Count:N0} games."));
        viewModel.SearchText = string.Empty;

        viewModel.InstalledOnly = true;
        Record(results, "Desktop installed filter uses Core state", () =>
            viewModel.Games.Count > 0 && viewModel.Games.All(game => game.IsInstalled)
                ? $"{viewModel.Games.Count:N0} installed games remain"
                : throw new InvalidOperationException("An uninstalled game passed the filter."));
        viewModel.InstalledOnly = false;

        var favoritesPreset = viewModel.FilterPresets.FirstOrDefault(preset => preset.Name == "Favorites");
        viewModel.SelectedFilterPreset = favoritesPreset;
        Record(results, "Core filter presets drive the Desktop library", () =>
            favoritesPreset != null && viewModel.Games.Count > 0 && viewModel.Games.All(game => game.Favorite)
                ? $"the Favorites preset selected {viewModel.Games.Count:N0} games"
                : throw new InvalidOperationException("The Core Favorites preset was not applied."));
        viewModel.SelectedFilterPreset = viewModel.FilterPresets.FirstOrDefault(preset => preset.Name == "All");

        viewModel.SelectedGrouping = Playnite.SDK.Models.GroupableField.None;
        viewModel.SelectedSortOrder = Playnite.SDK.Models.SortOrder.Playtime;
        viewModel.SelectedSortDirection = Playnite.SDK.Models.SortOrderDirection.Descending;
        Record(results, "Desktop sorting uses library metadata", () =>
            viewModel.Games.Count > 1 &&
            viewModel.Games.First().Game.Playtime >= viewModel.Games.Last().Game.Playtime
                ? $"playtime descending spans {viewModel.Games.First().PlaytimeText} to {viewModel.Games.Last().PlaytimeText}"
                : throw new InvalidOperationException("Playtime descending order was not applied."));

        viewModel.SelectedGrouping = Playnite.SDK.Models.GroupableField.InstallationStatus;
        Record(results, "Desktop grouping emits virtualized section headers", () =>
        {
            var headers = viewModel.Games
                .Where(game => game.ShowGroupHeader)
                .Select(game => game.GroupHeader)
                .ToList();
            return headers.Count >= 2 && headers.All(header => !string.IsNullOrWhiteSpace(header))
                ? $"{string.Join(", ", headers)} groups share one flat virtualized collection"
                : throw new InvalidOperationException($"Only {headers.Count} group headers were produced.");
        });
        viewModel.SelectedGrouping = Playnite.SDK.Models.GroupableField.None;
        viewModel.SelectedSortOrder = Playnite.SDK.Models.SortOrder.Name;
        viewModel.SelectedSortDirection = Playnite.SDK.Models.SortOrderDirection.Ascending;

        window.MainView.FocusSelectedGame();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Selection drives Desktop details", () =>
            viewModel.SelectedGame != null && window.MainView.GameList.SelectedItem == viewModel.SelectedGame
                ? $"details bound to {viewModel.SelectedGame.Name}"
                : throw new InvalidOperationException("The selected tile and details model diverged."));

        Record(results, "Shared plugin and game-operation host initializes", () =>
            window.RuntimeHost?.Actions != null &&
            window.RuntimeHost.Extensions != null &&
            window.RuntimeHost.LoadedPluginCount == 0 &&
            viewModel.PluginSummary.Contains("self-test", StringComparison.OrdinalIgnoreCase)
                ? $"{viewModel.PluginSummary}; Core action orchestration is attached"
                : throw new InvalidOperationException("The shared Avalonia runtime host is unavailable."));

        viewModel.SelectedGame = viewModel.Games.First(game => game.IsInstalled);
        viewModel.ActivateCommand.Execute(null);
        Record(results, "Desktop actions fail honestly without a provider", () =>
            viewModel.StatusText.Contains("No play action is available", StringComparison.OrdinalIgnoreCase)
                ? viewModel.StatusText
                : throw new InvalidOperationException($"Unexpected operation status: {viewModel.StatusText}"));

        window.RuntimeHost.Notifications.RemoveAll();
        window.RuntimeHost.ShowMessage("Desktop pilot notification", false);
        var notification = viewModel.Notifications.SingleOrDefault();
        Record(results, "Notifications reach the Desktop surface", () =>
            notification != null && viewModel.NotificationCount == 1 &&
            viewModel.StatusText == "Desktop pilot notification"
                ? "the shared host updated both the observable overlay and status bar"
                : throw new InvalidOperationException("The notification did not reach the Desktop view model."));
        notification?.CloseCommand.Execute(null);
        Record(results, "Desktop notifications close through the host", () =>
            viewModel.NotificationCount == 0
                ? "the SDK notification close command removed the host message"
                : throw new InvalidOperationException("The notification remained in the Desktop collection."));

        Dispatcher.UIThread.Post(
            () => viewModel.ConfirmDialogCommand.Execute(null),
            DispatcherPriority.Background);
        var dialogResult = window.RuntimeHost.Dialogs.ShowMessage(
            "Synchronous Desktop dialog check",
            "Pilot dialog",
            new[] { "Yes", "No" });
        Record(results, "Native Avalonia dialogs complete synchronously", () =>
            dialogResult == "Yes" && !viewModel.IsDialogVisible
                ? "the nested Avalonia dispatcher returned the selected option"
                : throw new InvalidOperationException($"Dialog returned '{dialogResult}'."));

        Record(results, "Desktop settings persist atomically", () =>
        {
            var store = new DesktopSettingsStore(library.ActiveUserDataDirectory);
            store.Save(new DesktopSettings
            {
                ViewMode = "List",
                SortOrder = Playnite.SDK.Models.SortOrder.Playtime,
                SortDirection = Playnite.SDK.Models.SortOrderDirection.Descending,
                Grouping = Playnite.SDK.Models.GroupableField.Platform,
                DisabledPlugins = new List<string> { "pilot-plugin" }
            });
            var loaded = store.Load();
            if (loaded.ViewMode != "List" ||
                loaded.SortOrder != Playnite.SDK.Models.SortOrder.Playtime ||
                loaded.SortDirection != Playnite.SDK.Models.SortOrderDirection.Descending ||
                loaded.Grouping != Playnite.SDK.Models.GroupableField.Platform ||
                loaded.DisabledPlugins.SingleOrDefault() != "pilot-plugin")
            {
                throw new InvalidOperationException("The persisted Desktop settings did not round-trip.");
            }

            return $"settings round-tripped at {store.SettingsPath}";
        });

        var report = BuildReport(results);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "desktop-pilot-results.txt"), report);
        Console.WriteLine(report);
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(
            results.Count(result => !result.Pass));
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
        report.AppendLine("=== Playnite Phase 5 Avalonia Desktop pilot checks ===");
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
