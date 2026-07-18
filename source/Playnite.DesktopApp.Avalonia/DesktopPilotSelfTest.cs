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

        Record(results, "Desktop tile grid stays virtualized", () =>
        {
            var realized = window.MainView.TilePanel?.RealizedCount ?? 0;
            return realized > 0 && realized <= 200
                ? $"{realized} of {library.Games.Count:N0} containers realized"
                : throw new InvalidOperationException($"Realized container count was {realized}.");
        });

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

        window.MainView.FocusSelectedGame();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Selection drives Desktop details", () =>
            viewModel.SelectedGame != null && window.MainView.GameList.SelectedItem == viewModel.SelectedGame
                ? $"details bound to {viewModel.SelectedGame.Name}"
                : throw new InvalidOperationException("The selected tile and details model diverged."));

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
