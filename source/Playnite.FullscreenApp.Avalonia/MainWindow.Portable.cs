using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Playnite.Avalonia.Input;
using Playnite.Controllers;
using Playnite.SDK.Models;

namespace Playnite.FullscreenApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly FullscreenLibrary library;
    private readonly StartupOptions options;
    private readonly ListBox gameList;
    private readonly TextBlock status;
    private readonly GamepadInputBridge gamepadBridge;
    private readonly SdlGamepadInputSource sdlInput;
    private VirtualizingStackPanel gamePanel;
    private int activations;

    internal MainWindow(FullscreenLibrary library, string startupError, StartupOptions options)
    {
        this.library = library;
        this.options = options;
        Title = "Playnite — Avalonia Linux Fullscreen Preview";
        Width = 1280;
        Height = 720;
        MinWidth = 960;
        MinHeight = 540;
        WindowState = options.Windowed ? WindowState.Normal : WindowState.FullScreen;
        Background = new SolidColorBrush(Color.Parse("#080B12"));

        status = new TextBlock
        {
            Text = startupError ??
                "Choose a game with the controller; SDK v6 WPF plugins require Windows.",
            FontSize = 16,
            Foreground = new SolidColorBrush(startupError == null
                ? Color.Parse("#B7C3D3")
                : Color.Parse("#FF8A80"))
        };
        gameList = new ListBox
        {
            ItemsSource = library.Games,
            SelectionMode = SelectionMode.Single,
            ItemsPanel = new FuncTemplate<Panel>(() =>
            {
                gamePanel = new VirtualizingStackPanel
                {
                    Orientation = Orientation.Horizontal
                };
                return gamePanel;
            }),
            ItemTemplate = new FuncDataTemplate<Game>((game, _) => new Border
            {
                Width = 280,
                Height = 360,
                Margin = new Thickness(10),
                Padding = new Thickness(22),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.Parse("#1D2635")),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,Auto,Auto"),
                    Children =
                    {
                        CreateArtworkPlaceholder(game),
                        CreateGameName(game),
                        CreateGameState(game)
                    }
                }
            }, supportsRecycling: true)
        };
        if (library.Games.Count > 0)
        {
            gameList.SelectedIndex = 0;
        }

        var heading = new TextBlock
        {
            Text = "PLAYNITE",
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        };
        var instructions = new TextBlock
        {
            Text = "D-pad: navigate   A: play   B: exit",
            Foreground = new SolidColorBrush(Color.Parse("#7F91A8"))
        };
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { status, instructions }
        };
        Grid.SetColumn(instructions, 1);

        var root = new DockPanel { Margin = new Thickness(34) };
        DockPanel.SetDock(heading, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(heading);
        root.Children.Add(footer);
        root.Children.Add(gameList);
        Content = root;

        gamepadBridge = new GamepadInputBridge(this);
        gamepadBridge.MapCommand(GamepadButton.LeftShoulder, new PortableCommand(() => MoveSelection(-1)));
        gamepadBridge.MapCommand(GamepadButton.RightShoulder, new PortableCommand(() => MoveSelection(1)));
        gamepadBridge.MapCommand(GamepadButton.Confirm, new PortableCommand(ActivateSelected));
        gamepadBridge.MapCommand(GamepadButton.Cancel, new PortableCommand(Close));
        sdlInput = new SdlGamepadInputSource(gamepadBridge);

        KeyDown += OnKeyDown;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    internal void RestoreAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = options.Windowed ? WindowState.Normal : WindowState.FullScreen;
        }

        Activate();
    }

    internal bool ProcessUri(string uri)
    {
        try
        {
            var parsed = PlayniteUriHandler.ParseUri(uri);
            if (!string.Equals(parsed.source, "playnite", StringComparison.OrdinalIgnoreCase) ||
                parsed.arguments.Length < 2)
            {
                status.Text = $"No URI handler is registered for '{uri}'.";
                return false;
            }

            var command = parsed.arguments[0];
            if (!string.Equals(command, UriCommands.StartGame, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(command, UriCommands.ShowGame, StringComparison.OrdinalIgnoreCase))
            {
                status.Text = $"Unsupported Playnite URI '{uri}'.";
                return false;
            }

            if (!Guid.TryParse(parsed.arguments[1], out var gameId))
            {
                status.Text = $"Invalid game identifier '{parsed.arguments[1]}'.";
                return false;
            }

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
                ActivateSelected();
            }
            else
            {
                status.Text = $"Selected {game.Name}.";
            }

            return true;
        }
        catch (Exception exception)
        {
            status.Text = $"Invalid Playnite URI: {exception.Message}";
            return false;
        }
    }

    private static Control CreateArtworkPlaceholder(Game game)
    {
        var artwork = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(game.Favorite ? Color.Parse("#5D3AAE") : Color.Parse("#314158")),
            Child = new TextBlock
            {
                Text = game.Favorite ? "★" : "▶",
                FontSize = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            }
        };
        Grid.SetRow(artwork, 0);
        return artwork;
    }

    private static Control CreateGameName(Game game)
    {
        var name = new TextBlock
        {
            Text = game.Name,
            Margin = new Thickness(0, 16, 0, 4),
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(name, 1);
        return name;
    }

    private static Control CreateGameState(Game game)
    {
        var state = new TextBlock
        {
            Text = game.IsInstalled ? "Ready to play" : "Not installed",
            Foreground = new SolidColorBrush(Color.Parse("#93A4B8"))
        };
        Grid.SetRow(state, 2);
        return state;
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        gameList.Focus();
        sdlInput.Start();
        if (!options.SelfTest)
        {
            status.Text = sdlInput.Status;
            return;
        }

        await RunSelfTest();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        sdlInput.Dispose();
        gamepadBridge.Dispose();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void MoveSelection(int offset)
    {
        if (library.Games.Count == 0)
        {
            return;
        }

        gameList.SelectedIndex = Math.Clamp(gameList.SelectedIndex + offset, 0, library.Games.Count - 1);
        gameList.ScrollIntoView(gameList.SelectedItem);
    }

    private void ActivateSelected()
    {
        if (gameList.SelectedItem is not Game game)
        {
            return;
        }

        activations++;
        if (options.SelfTest)
        {
            status.Text = $"Controller activation: {game.Name}";
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

    private async Task RunSelfTest()
    {
        var results = new List<(string Name, bool Passed, string Detail)>();
        try
        {
            await Task.Delay(350);
            Record(results, "Portable Playnite.Core library opens", () =>
                library.IsOpen && library.Games.Count == 1_000
                    ? $"{library.Games.Count:N0} games loaded"
                    : throw new InvalidOperationException($"Library state: open={library.IsOpen}, games={library.Games.Count}."));
            Record(results, "Fullscreen carousel virtualization is bounded", () =>
                gamePanel != null && gamePanel.Children.Count > 0 && gamePanel.Children.Count < 100
                    ? $"{gamePanel.Children.Count} of {library.Games.Count:N0} containers realized"
                    : throw new InvalidOperationException($"Realized count was {gamePanel?.Children.Count ?? 0}."));
            Record(results, "Gamepad confirmation reaches the active game", () =>
            {
                gamepadBridge.ButtonDown(GamepadButton.Confirm);
                gamepadBridge.ButtonUp(GamepadButton.Confirm);
                return activations == 1
                    ? status.Text
                    : throw new InvalidOperationException($"Activation count was {activations}.");
            });
            Record(results, "Gamepad navigation raises native Avalonia input", () =>
            {
                gamepadBridge.ButtonDown(GamepadButton.DPadRight);
                gamepadBridge.ButtonUp(GamepadButton.DPadRight);
                return gamepadBridge.KeysSynthesized == 1
                    ? "one routed key event synthesized"
                    : throw new InvalidOperationException($"Synthesized key count was {gamepadBridge.KeysSynthesized}.");
            });
            Record(results, "SDL controller source is active", () =>
                sdlInput.IsAvailable
                    ? sdlInput.Status
                    : throw new InvalidOperationException(sdlInput.Status));
        }
        catch (Exception exception)
        {
            results.Add(("Self-test execution", false, exception.ToString()));
        }

        var report = BuildReport(results);
        Console.WriteLine(report);
        File.WriteAllText(Path.Combine(Program.RuntimeOutputDirectory, "linux-fullscreen-self-test.txt"), report);
        var exitCode = results.All(result => result.Passed) ? 0 : 1;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
        }
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
        report.AppendLine("=== Playnite Phase 8 native Linux Fullscreen checks ===");
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
