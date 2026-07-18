using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia;

public sealed class App : Application
{
    private PlayniteLibrary library;
    private FullscreenRuntimeHost runtimeHost;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = Program.Options;
            library = new PlayniteLibrary(options.UserDataDirectory, options.LibraryPath);
            string startupError = null;

            try
            {
                if (options.SelfTest)
                {
                    library.OpenTemporaryPilotLibrary(1_000);
                }
                else
                {
                    library.OpenExistingLibrary();
                }
            }
            catch (Exception exception)
            {
                startupError = exception.Message;
            }

            var settingsStore = new FullscreenSettingsStore(library.ActiveUserDataDirectory);
            var settings = options.SelfTest ? new FullscreenSettings() : settingsStore.Load();
            var viewModel = new FullscreenAppViewModel(library.Games, settings, startupError);
            if (library.IsOpen)
            {
                runtimeHost = new FullscreenRuntimeHost(library, viewModel, settings);
                viewModel.AttachRuntime(runtimeHost);
                runtimeHost.InitializePlugins(!options.SelfTest);
            }

            var window = new MainWindow(
                viewModel,
                library,
                runtimeHost,
                settings,
                options.SelfTest ? null : settingsStore,
                options);
            viewModel.ExitRequested += (_, _) => window.Close();
            viewModel.ToggleFullscreenRequested += (_, _) =>
                window.WindowState = window.WindowState == global::Avalonia.Controls.WindowState.FullScreen
                    ? global::Avalonia.Controls.WindowState.Normal
                    : global::Avalonia.Controls.WindowState.FullScreen;

            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                runtimeHost?.Dispose();
                library.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
