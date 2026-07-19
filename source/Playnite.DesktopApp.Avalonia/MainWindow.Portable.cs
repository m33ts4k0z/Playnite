using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Playnite.Avalonia.Controls;
using Playnite.Avalonia.Input;
using Playnite.Common;
using Playnite.Controllers;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly DesktopLibrary library;
    private readonly bool selfTest;
    private readonly bool startClosedToTray;
    private readonly ListBox gameList;
    private readonly TextBlock summary;
    private readonly TextBlock status;
    private UniformGridVirtualizingPanel tilePanel;
    private readonly GamepadInputBridge gamepadBridge;
    private readonly SdlGamepadInputSource sdlInput;

    internal PortableTrayService TrayService { get; set; }

    internal MainWindow(DesktopLibrary library, string startupError, StartupOptions options)
    {
        this.library = library;
        selfTest = options.SelfTest;
        startClosedToTray = options.StartClosedToTray;
        Title = "Playnite — Avalonia Linux Preview";
        Width = 1180;
        Height = 760;
        MinWidth = 760;
        MinHeight = 520;
        ShowInTaskbar = !startClosedToTray;
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
            Text = startupError ??
                "Native .NET 10 / Avalonia 12 Linux shell; SDK v6 WPF plugins require Windows.",
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
        gameList.DoubleTapped += (_, _) => LaunchSelectedGame();

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
        gamepadBridge = new GamepadInputBridge(this);
        gamepadBridge.MapCommand(GamepadButton.DPadLeft, new PortableCommand(() => MoveControllerSelection(-1)));
        gamepadBridge.MapCommand(GamepadButton.DPadRight, new PortableCommand(() => MoveControllerSelection(1)));
        gamepadBridge.MapCommand(
            GamepadButton.DPadUp,
            new PortableCommand(() => MoveControllerSelection(-(tilePanel?.NavigationColumns ?? 1))));
        gamepadBridge.MapCommand(
            GamepadButton.DPadDown,
            new PortableCommand(() => MoveControllerSelection(tilePanel?.NavigationColumns ?? 1)));
        sdlInput = new SdlGamepadInputSource(gamepadBridge);
        ApplySearch(string.Empty);
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    internal void RestoreAndActivate()
    {
        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    internal bool ProcessUri(string uri)
    {
        try
        {
            var parsed = PlayniteUriHandler.ParseUri(uri);
            if (!string.Equals(parsed.source, "playnite", StringComparison.OrdinalIgnoreCase) ||
                parsed.arguments.Length == 0)
            {
                status.Text = $"No URI handler is registered for '{uri}'.";
                return false;
            }

            var command = parsed.arguments[0];
            if ((string.Equals(command, UriCommands.StartGame, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, UriCommands.ShowGame, StringComparison.OrdinalIgnoreCase)) &&
                parsed.arguments.Length >= 2 &&
                Guid.TryParse(parsed.arguments[1], out var gameId))
            {
                var game = library.Games.FirstOrDefault(candidate => candidate.Id == gameId);
                if (game == null)
                {
                    status.Text = $"Game {gameId} was not found.";
                    return false;
                }

                gameList.SelectedItem = game;
                gameList.ScrollIntoView(game);
                RestoreAndActivate();
                if (string.Equals(command, UriCommands.StartGame, StringComparison.OrdinalIgnoreCase))
                {
                    LaunchSelectedGame();
                }
                else
                {
                    status.Text = $"Selected {game.Name}.";
                }

                return true;
            }

            if (string.Equals(command, UriCommands.Search, StringComparison.OrdinalIgnoreCase) &&
                parsed.arguments.Length >= 2)
            {
                ApplySearch(parsed.arguments[1]);
                RestoreAndActivate();
                status.Text = $"Searching for {parsed.arguments[1]}.";
                return true;
            }

            status.Text = $"Unsupported Playnite URI '{uri}'.";
            return false;
        }
        catch (Exception exception)
        {
            status.Text = $"Invalid Playnite URI: {exception.Message}";
            return false;
        }
    }

    private void LaunchSelectedGame()
    {
        if (gameList.SelectedItem is not Game game)
        {
            status.Text = "Select a game first.";
            return;
        }

        try
        {
            status.Text = PortableGameActionLauncher.Launch(game);
        }
        catch (Exception exception)
        {
            status.Text = $"Could not start {game.Name}: {exception.Message}";
        }
    }

    private void MoveControllerSelection(int offset)
    {
        if (gameList.ItemCount == 0)
        {
            return;
        }

        var target = Math.Clamp(Math.Max(0, gameList.SelectedIndex) + offset, 0, gameList.ItemCount - 1);
        gameList.SelectedIndex = target;
        gameList.ScrollIntoView(target);
        (gameList.ContainerFromIndex(target) as Control)?.Focus();
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        sdlInput.Start();
        if (startClosedToTray && !selfTest)
        {
            Hide();
            return;
        }

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
            gameList.SelectedIndex = 0;
            gameList.Focus();
            gamepadBridge.ButtonDown(GamepadButton.DPadRight);
            gamepadBridge.ButtonUp(GamepadButton.DPadRight);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Record(results, "SDL controller navigation reaches the Desktop game grid", () =>
                gameList.SelectedIndex > 0
                    ? "routed Avalonia controller input moved the selected game"
                    : throw new InvalidOperationException(
                        $"Controller navigation selected index {gameList.SelectedIndex}."));
            Record(results, "Desktop SDL controller source is active", () =>
                sdlInput.IsAvailable
                    ? sdlInput.Status
                    : throw new InvalidOperationException(sdlInput.Status));
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
            Record(results, "StatusNotifier tray contract is available", () =>
                TrayService != null && TrayService.MenuItemCount == 4
                    ? "native tray icon and Open/Fullscreen/Exit menu created"
                    : throw new InvalidOperationException(
                        $"Tray menu contained {TrayService?.MenuItemCount ?? 0} items."));
            foreach (var webViewResult in await PortableWebViewSelfTest.RunAsync())
            {
                results.Add((webViewResult.Name, true, webViewResult.Detail));
            }
        }
        catch (Exception exception)
        {
            results.Add(("Self-test execution", false, exception.ToString()));
        }

        var report = BuildReport(results);
        Console.WriteLine(report);
        var outputPath = Path.Combine(Program.RuntimeOutputDirectory, "linux-desktop-self-test.txt");
        File.WriteAllText(outputPath, report);
        var exitCode = results.All(result => result.Passed) ? 0 : 1;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
        }
    }

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (selfTest || e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            return;
        }

        e.Cancel = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        sdlInput.Dispose();
        gamepadBridge.Dispose();
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

    private sealed class PortableCommand : ICommand
    {
        private readonly Action execute;

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public PortableCommand(Action execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute();
    }
}
