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

        window.GamepadBridge.ButtonDown(GamepadButton.X);
        window.GamepadBridge.ButtonUp(GamepadButton.X);
        Record(results, "Explicit play action mapping dispatches", () =>
            viewModel.ActivateCount == 1
                ? "X dispatched the pilot play command once"
                : throw new InvalidOperationException($"Dispatch count was {viewModel.ActivateCount}."));

        Record(results, "SDL input source initializes", () =>
            window.SdlInput.IsAvailable
                ? window.SdlInput.Status
                : throw new InvalidOperationException(window.SdlInput.Status));

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
