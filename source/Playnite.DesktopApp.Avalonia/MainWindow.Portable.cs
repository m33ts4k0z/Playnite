using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Playnite.Avalonia.Controls;
using Playnite.Common;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly DesktopLibrary library;
    private readonly bool selfTest;
    private readonly ListBox gameList;
    private readonly TextBlock summary;
    private readonly TextBlock status;
    private UniformGridVirtualizingPanel tilePanel;

    internal MainWindow(DesktopLibrary library, string startupError, bool selfTest)
    {
        this.library = library;
        this.selfTest = selfTest;
        Title = "Playnite — Avalonia Linux Preview";
        Width = 1180;
        Height = 760;
        MinWidth = 760;
        MinHeight = 520;
        Background = new SolidColorBrush(Color.Parse("#10141B"));

        var search = new TextBox
        {
            PlaceholderText = "Search games",
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        search.TextChanged += (_, _) => ApplySearch(search.Text);
        summary = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#9EABBA")),
            VerticalAlignment = VerticalAlignment.Center
        };
        status = new TextBlock
        {
            Text = startupError ?? "Native .NET 10 / Avalonia 12 Linux shell",
            Foreground = new SolidColorBrush(startupError == null
                ? Color.Parse("#9EABBA")
                : Color.Parse("#FF8A80"))
        };

        gameList = new ListBox
        {
            ItemsSource = library.Games,
            ItemsPanel = new FuncTemplate<Panel>(() =>
            {
                tilePanel = new UniformGridVirtualizingPanel
                {
                    ItemWidth = 210,
                    ItemHeight = 104
                };
                return tilePanel;
            }),
            ItemTemplate = new FuncDataTemplate<Game>((game, _) => new Border
            {
                Margin = new Thickness(5),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.Parse("#202834")),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = game.Name,
                            FontSize = 16,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brushes.White,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        },
                        new TextBlock
                        {
                            Text = game.IsInstalled ? "Installed" : "Not installed",
                            Foreground = new SolidColorBrush(Color.Parse("#9EABBA"))
                        }
                    }
                }
            }, supportsRecycling: true)
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,20,*"),
            Children = { search, summary }
        };
        Grid.SetColumn(summary, 2);
        var content = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        content.Children.Add(header);
        content.Children.Add(status);
        content.Children.Add(gameList);
        Content = content;
        ApplySearch(string.Empty);
        Opened += OnOpened;
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        if (!selfTest)
        {
            return;
        }

        var results = new List<(string Name, bool Passed, string Detail)>();
        try
        {
            await Task.Delay(350);
            Record(results, "Portable Playnite.Core library opens", () =>
                library.IsOpen && library.Games.Count == 1_000
                    ? $"{library.Games.Count:N0} games loaded"
                    : throw new InvalidOperationException($"Library state: open={library.IsOpen}, games={library.Games.Count}."));
            Record(results, "Linux shell grid virtualization is bounded", () =>
                tilePanel != null && tilePanel.RealizedCount > 0 && tilePanel.RealizedCount < 200
                    ? $"{tilePanel.RealizedCount} of {library.Games.Count:N0} containers realized"
                    : throw new InvalidOperationException($"Realized count was {tilePanel?.RealizedCount ?? 0}."));
            ApplySearch("Linux Game 999");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Record(results, "Native shell search filters the Core library", () =>
                gameList.ItemCount > 0 && gameList.ItemCount < library.Games.Count
                    ? $"search returned {gameList.ItemCount:N0} game(s)"
                    : throw new InvalidOperationException($"Search returned {gameList.ItemCount:N0} games."));
            Record(results, "Linux application sources are callable", () =>
            {
                var programs = Programs.GetInstalledPrograms(CancellationToken.None).GetAwaiter().GetResult();
                return programs != null ? $"{programs.Count:N0} desktop/package applications discovered" :
                    throw new InvalidOperationException("Application discovery was cancelled unexpectedly.");
            });
        }
        catch (Exception exception)
        {
            results.Add(("Self-test execution", false, exception.ToString()));
        }

        var report = BuildReport(results);
        Console.WriteLine(report);
        var outputPath = Path.Combine(AppContext.BaseDirectory, "linux-desktop-self-test.txt");
        File.WriteAllText(outputPath, report);
        var exitCode = results.All(result => result.Passed) ? 0 : 1;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
        }
    }

    private void ApplySearch(string term)
    {
        var games = string.IsNullOrWhiteSpace(term)
            ? library.Games
            : library.Games.Where(game => game.Name?.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
        gameList.ItemsSource = games;
        summary.Text = $"{games.Count:N0} games";
    }

    private static void Record(
        ICollection<(string Name, bool Passed, string Detail)> results,
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

    private static string BuildReport(IReadOnlyCollection<(string Name, bool Passed, string Detail)> results)
    {
        var report = new StringBuilder();
        report.AppendLine("=== Playnite Phase 8 native Linux Desktop checks ===");
        report.AppendLine($"Avalonia {typeof(Application).Assembly.GetName().Version}; .NET {Environment.Version}");
        report.AppendLine();
        foreach (var result in results)
        {
            report.AppendLine($"[{(result.Passed ? "PASS" : "FAIL")}] {result.Name}");
            report.AppendLine($"       {result.Detail}");
        }

        report.AppendLine();
        report.AppendLine($"VERDICT: {results.Count(result => result.Passed)}/{results.Count} checks passed.");
        return report.ToString();
    }
}
