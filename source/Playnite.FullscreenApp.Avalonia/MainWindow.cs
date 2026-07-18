using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Playnite.Avalonia.Input;
using Playnite.Avalonia.Theming;
using Playnite.FullscreenApp.Avalonia.Controls;
using Playnite.FullscreenApp.Avalonia.Input;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia;

public sealed class MainWindow : Window
{
    private readonly FullscreenAppViewModel viewModel;
    private readonly PlayniteLibrary library;
    private readonly StartupOptions options;
    private readonly RuntimeThemeManager themeManager;
    private readonly FullscreenMainView mainView;
    private readonly GamepadInputBridge gamepadBridge;
    private readonly SdlGamepadInputSource sdlInput;

    internal GamepadInputBridge GamepadBridge => gamepadBridge;
    internal SdlGamepadInputSource SdlInput => sdlInput;
    internal FullscreenMainView MainView => mainView;

    internal MainWindow(
        FullscreenAppViewModel viewModel,
        PlayniteLibrary library,
        StartupOptions options)
    {
        this.viewModel = viewModel;
        this.library = library;
        this.options = options;

        Title = "Playnite — Avalonia Fullscreen Pilot";
        Width = 1280;
        Height = 720;
        MinWidth = 960;
        MinHeight = 540;
        WindowState = options.Windowed ? WindowState.Normal : WindowState.FullScreen;
        DataContext = viewModel;

        themeManager = new RuntimeThemeManager(Application.Current, typeof(FullscreenMainView).Assembly);
        ApplyRuntimeTheme();
        themeManager.ApplyLanguage(ContentPath("Localization", "english.axaml"));

        mainView = new FullscreenMainView();
        Content = mainView;

        gamepadBridge = new GamepadInputBridge(this);
        gamepadBridge.MapCommand(GamepadButton.Confirm, viewModel.ShowDetailsCommand);
        gamepadBridge.MapCommand(GamepadButton.Cancel, viewModel.BackCommand);
        gamepadBridge.MapCommand(GamepadButton.Start, viewModel.ToggleMenuCommand);
        gamepadBridge.MapCommand(GamepadButton.Back, viewModel.ToggleMenuCommand);
        gamepadBridge.MapCommand(GamepadButton.X, viewModel.ActivateCommand);
        gamepadBridge.MapCommand(GamepadButton.Y, viewModel.ShowDetailsCommand);
        gamepadBridge.MapCommand(GamepadButton.LeftShoulder, viewModel.SelectPreviousCommand);
        gamepadBridge.MapCommand(GamepadButton.RightShoulder, viewModel.SelectNextCommand);
        sdlInput = new SdlGamepadInputSource(gamepadBridge);

        viewModel.LibraryFocusRequested += (_, _) =>
            Dispatcher.UIThread.Post(mainView.FocusSelectedGame, DispatcherPriority.Input);
        KeyDown += OnKeyDown;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void ApplyRuntimeTheme()
    {
        var defaultTheme = ContentPath("Themes", "Fullscreen", "Default", "Theme.axaml");
        var styles = ContentPath("Themes", "Fullscreen", "Default", "Styles.axaml");
        themeManager.ApplyTheme(new[] { defaultTheme }, selectorStyles: new[] { styles });

        if (!string.IsNullOrWhiteSpace(options.CustomThemePath))
        {
            try
            {
                themeManager.ApplyTheme(
                    new[] { defaultTheme },
                    new[] { options.CustomThemePath },
                    new[] { styles });
            }
            catch (LooseXamlLoadException exception)
            {
                viewModel.SetStatusMessage(
                    $"Custom theme failed to load; the default theme is active. {exception.InnerException?.Message}");
                System.Diagnostics.Trace.WriteLine(exception);
            }
        }
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        mainView.FocusSelectedGame();
        sdlInput.Start();
        if (options.SelfTest)
        {
            await FullscreenPilotSelfTest.Run(this, viewModel, library);
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        sdlInput.Dispose();
        gamepadBridge.Dispose();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            viewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            viewModel.ToggleMenuCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static string ContentPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory }.Concat(parts).ToArray());
}
