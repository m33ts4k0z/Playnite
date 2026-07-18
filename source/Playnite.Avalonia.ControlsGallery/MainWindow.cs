using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Playnite.Avalonia.Controls;
using Playnite.Avalonia.Input;
using Playnite.Avalonia.Theming;

namespace Playnite.Avalonia.ControlsGallery;

public sealed class MainWindow : Window
{
    private const int GalleryItemCount = 10_000;

    private readonly bool autoMode;
    private readonly RuntimeThemeManager themeManager;
    private readonly List<(string Name, bool Pass, string Detail)> results = new();
    private readonly GamePanel gamePanel;
    private readonly TextBlock localizedGreeting;
    private readonly TextBlock statusText;
    private readonly TextBox logBox;
    private readonly ListBox gameList;
    private readonly GamepadInputBridge gamepadBridge;
    private readonly CountingCommand startCommand = new();
    private UniformGridVirtualizingPanel tilePanel;
    private bool customThemeActive;
    private bool swedishActive;

    public MainWindow(bool autoMode)
    {
        this.autoMode = autoMode;
        Title = "Playnite — Avalonia 12 Controls Gallery";
        Width = 1100;
        Height = 800;

        themeManager = new RuntimeThemeManager(Application.Current, typeof(GamePanel).Assembly);
        themeManager.ApplyTheme(
            new[] { ContentPath("Themes", "Default", "Default.axaml") },
            selectorStyles: new[] { ContentPath("Themes", "Default", "Controls.axaml") });
        themeManager.ApplyLanguage(ContentPath("Localization", "english.axaml"));

        gamePanel = new GamePanel { Title = "Grand Theft Auto V" };
        localizedGreeting = new TextBlock { FontSize = 17 };
        localizedGreeting.GetResourceObservable("LOC_Greeting")
            .Subscribe(new ResourceObserver<object>(value =>
                localizedGreeting.Text = value as string ?? "(missing localization)"));

        statusText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 4)
        };

        gameList = new ListBox
        {
            Height = 420,
            ItemsSource = Enumerable.Range(0, GalleryItemCount).Select(index => $"Game {index:N0}").ToList(),
            ItemsPanel = new FuncTemplate<Panel>(() =>
            {
                tilePanel = new UniformGridVirtualizingPanel
                {
                    ItemWidth = 145,
                    ItemHeight = 64
                };
                return tilePanel;
            }),
            ItemTemplate = new FuncDataTemplate<string>((title, _) => new Border
            {
                Margin = new Thickness(3),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#25252A")),
                Child = new TextBlock
                {
                    Text = title,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }, supportsRecycling: true)
        };

        logBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 150,
            FontFamily = new FontFamily("Consolas")
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(CreateButton("Toggle custom theme", ToggleTheme));
        buttons.Children.Add(CreateButton("Toggle language", ToggleLanguage));
        buttons.Children.Add(CreateButton("Simulate Start", () =>
        {
            gamepadBridge.ButtonDown(GamepadButton.Start);
            statusText.Text = $"Start command executions: {startCommand.ExecuteCount}";
        }));

        var content = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        content.Children.Add(gamePanel);
        content.Children.Add(localizedGreeting);
        content.Children.Add(buttons);
        content.Children.Add(statusText);
        content.Children.Add(gameList);
        content.Children.Add(logBox);
        Content = content;

        gamepadBridge = new GamepadInputBridge(this);
        gamepadBridge.MapCommand(GamepadButton.Start, startCommand);

        Opened += async (_, _) =>
        {
            if (autoMode)
            {
                await RunAutomatedChecks();
            }
            else
            {
                DispatcherTimer.Run(() =>
                {
                    UpdateStatus();
                    return true;
                }, TimeSpan.FromMilliseconds(500));
            }
        };

        Closed += (_, _) => gamepadBridge.Dispose();
    }

    private async Task RunAutomatedChecks()
    {
        try
        {
            await Task.Delay(350);
            Record("Loose default theme and implicit ControlTheme", () =>
            {
                if (gamePanel.TemplateAppliedCount == 0 ||
                    !gamePanel.ResolvedParts.Contains("PART_TitleText") ||
                    !gamePanel.ResolvedParts.Contains("PART_ActionButton"))
                {
                    throw new InvalidOperationException("GamePanel template contract was not resolved.");
                }

                return $"template marker={gamePanel.TemplateMarker}, parts={string.Join(",", gamePanel.ResolvedParts)}";
            });

            Record("Loose localization dictionary", () =>
                localizedGreeting.Text == "Hello from Playnite"
                    ? localizedGreeting.Text
                    : throw new InvalidOperationException($"Greeting was '{localizedGreeting.Text}'."));

            Record("Uniform grid virtualization is bounded", () =>
            {
                if (tilePanel == null || tilePanel.RealizedCount == 0 || tilePanel.RealizedCount > 200)
                {
                    throw new InvalidOperationException($"Realized count was {tilePanel?.RealizedCount ?? 0}.");
                }

                return $"{tilePanel.RealizedCount} of {GalleryItemCount:N0} containers realized";
            });

            gameList.ScrollIntoView(GalleryItemCount - 1);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Record("Far-index ScrollIntoView", () =>
                gameList.ContainerFromIndex(GalleryItemCount - 1) != null
                    ? "container 9,999 realized"
                    : throw new InvalidOperationException("Far-index container was not realized."));

            ToggleTheme();
            var freshPanel = new GamePanel { Title = "Fresh themed instance" };
            ((StackPanel)Content).Children.Insert(1, freshPanel);
            await Task.Delay(150);
            Record("Custom theme overrides default resources", () =>
                freshPanel.TemplateMarker == "custom"
                    ? "fresh control received custom template"
                    : throw new InvalidOperationException($"Template marker was '{freshPanel.TemplateMarker}'."));

            var activeCount = themeManager.ActiveThemeDictionaries.Count;
            Record("Malformed theme fails transactionally", () =>
            {
                try
                {
                    themeManager.ApplyTheme(new[] { ContentPath("Themes", "Broken", "Broken.axaml") });
                    throw new InvalidOperationException("Malformed theme unexpectedly loaded.");
                }
                catch (LooseXamlLoadException)
                {
                    if (themeManager.ActiveThemeDictionaries.Count != activeCount)
                    {
                        throw new InvalidOperationException("Active theme changed after parse failure.");
                    }

                    return "parse error caught; active custom theme preserved";
                }
            });

            ToggleLanguage();
            await Task.Delay(100);
            Record("Runtime language swap", () =>
                localizedGreeting.Text == "Hej från Playnite"
                    ? localizedGreeting.Text
                    : throw new InvalidOperationException($"Greeting was '{localizedGreeting.Text}'."));

            gamepadBridge.ButtonDown(GamepadButton.Start);
            Record("Explicit gamepad action command", () =>
                startCommand.ExecuteCount == 1
                    ? "Start command dispatched once"
                    : throw new InvalidOperationException($"Dispatch count was {startCommand.ExecuteCount}."));
        }
        catch (Exception exception)
        {
            results.Add(("Automated check runner", false, exception.ToString()));
        }

        var failures = results.Count(result => !result.Pass);
        var report = BuildReport();
        Console.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "controls-gallery-results.txt"), report);
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(failures);
    }

    private void ToggleTheme()
    {
        customThemeActive = !customThemeActive;
        themeManager.ApplyTheme(
            new[] { ContentPath("Themes", "Default", "Default.axaml") },
            customThemeActive ? new[] { ContentPath("Themes", "Custom", "Custom.axaml") } : null,
            new[] { ContentPath("Themes", "Default", "Controls.axaml") });
    }

    private void ToggleLanguage()
    {
        swedishActive = !swedishActive;
        themeManager.ApplyLanguage(ContentPath(
            "Localization",
            swedishActive ? "swedish.axaml" : "english.axaml"));
    }

    private void UpdateStatus()
    {
        statusText.Text = tilePanel == null
            ? "Virtualizing panel is initializing…"
            : $"items={GalleryItemCount:N0}  realized={tilePanel.RealizedCount}  pooled={tilePanel.PooledCount}  " +
              $"created={tilePanel.ContainersCreated}  reused={tilePanel.ContainersReused}";
    }

    private void Record(string name, Func<string> check)
    {
        try
        {
            var detail = check();
            results.Add((name, true, detail));
            Log($"[PASS] {name} — {detail}");
        }
        catch (Exception exception)
        {
            results.Add((name, false, exception.Message));
            Log($"[FAIL] {name} — {exception.Message}");
        }
    }

    private void Log(string text) => logBox.Text += text + Environment.NewLine;

    private string BuildReport()
    {
        var report = new StringBuilder();
        report.AppendLine("=== Playnite Phase 3 Avalonia foundation checks ===");
        report.AppendLine($"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version}; .NET {Environment.Version}");
        report.AppendLine();
        foreach (var result in results)
        {
            report.AppendLine($"[{(result.Pass ? "PASS" : "FAIL")}] {result.Name}");
            report.AppendLine($"       {result.Detail}");
        }

        report.AppendLine();
        report.AppendLine($"VERDICT: {results.Count(result => result.Pass)}/{results.Count} checks passed.");
        return report.ToString();
    }

    private static Button CreateButton(string caption, Action action)
    {
        var button = new Button { Content = caption };
        button.Classes.Add("accent");
        button.Click += (_, _) => action();
        return button;
    }

    private static string ContentPath(params string[] pathParts) =>
        Path.Combine(new[] { AppContext.BaseDirectory }.Concat(pathParts).ToArray());

    private sealed class CountingCommand : ICommand
    {
        public int ExecuteCount { get; private set; }
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => ExecuteCount++;
    }
}
