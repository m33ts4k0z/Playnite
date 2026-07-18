using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Playnite.Avalonia.Theming;
using Playnite.DesktopApp.Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly DesktopAppViewModel viewModel;
    private readonly DesktopLibrary library;
    private readonly StartupOptions options;
    private readonly DesktopMainView mainView;

    internal DesktopMainView MainView => mainView;

    internal MainWindow(
        DesktopAppViewModel viewModel,
        DesktopLibrary library,
        StartupOptions options)
    {
        this.viewModel = viewModel;
        this.library = library;
        this.options = options;

        Title = "Playnite — Avalonia Desktop Pilot";
        Width = 1440;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 680;
        DataContext = viewModel;

        var themes = new RuntimeThemeManager(Application.Current, typeof(DesktopMainView).Assembly);
        themes.ApplyTheme(
            new[] { ContentPath("Themes", "Desktop", "Default", "Theme.axaml") },
            selectorStyles: new[] { ContentPath("Themes", "Desktop", "Default", "Styles.axaml") });
        themes.ApplyLanguage(ContentPath("Localization", "english.axaml"));

        mainView = new DesktopMainView();
        Content = mainView;
        Opened += OnOpened;
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        mainView.FocusSelectedGame();
        if (options.SelfTest)
        {
            await DesktopPilotSelfTest.Run(this, viewModel, library);
        }
    }

    private static string ContentPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory }.Concat(parts).ToArray());
}
