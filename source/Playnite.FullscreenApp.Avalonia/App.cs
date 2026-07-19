using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Playnite.Avalonia.App.Services;
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
            var settings = LoadOrImportSettings(settingsStore, library.ActiveUserDataDirectory, options);
            var viewModel = new FullscreenAppViewModel(library.Games, settings, startupError);
            MainWindow window = null;
            if (library.IsOpen)
            {
                runtimeHost = new FullscreenRuntimeHost(library, viewModel, settings, () => window);
                viewModel.AttachRuntime(runtimeHost);
            }

            window = new MainWindow(
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
            // Parse the loose theme before third-party assemblies enter the process. A plugin
            // with an incompatible dependency must not interfere with Avalonia's XAML discovery.
            runtimeHost?.InitializePlugins(!options.SelfTest);
            Program.InstanceCoordinator?.SetCommandHandler(command =>
                Dispatcher.UIThread.Post(() =>
                    ProcessCommand(command, window, desktop, runtimeHost, viewModel)));
            if (!string.IsNullOrWhiteSpace(options.UriData) &&
                !ProcessUri(options.UriData, runtimeHost, viewModel))
            {
                viewModel.SetStatusMessage($"No URI handler is registered for '{options.UriData}'.");
            }
            desktop.Exit += (_, _) =>
            {
                runtimeHost?.Dispose();
                library.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ProcessCommand(
        CommandExecutedEventArgs command,
        MainWindow window,
        IClassicDesktopStyleApplicationLifetime desktop,
        FullscreenRuntimeHost host,
        FullscreenAppViewModel viewModel)
    {
        switch (command.Command)
        {
            case CmdlineCommand.Focus:
                window.Show();
                window.Activate();
                break;
            case CmdlineCommand.UriRequest:
                if (!ProcessUri(command.Args, host, viewModel))
                {
                    viewModel.SetStatusMessage($"No URI handler is registered for '{command.Args}'.");
                }
                break;
            case CmdlineCommand.Shutdown:
                desktop.Shutdown();
                break;
        }
    }

    private static bool ProcessUri(
        string uri,
        FullscreenRuntimeHost host,
        FullscreenAppViewModel viewModel)
    {
        try
        {
            return host?.ProcessUri(uri) == true;
        }
        catch (Exception exception)
        {
            viewModel.SetStatusMessage($"Invalid Playnite URI: {exception.Message}");
            return false;
        }
    }

    private static FullscreenSettings LoadOrImportSettings(
        FullscreenSettingsStore settingsStore, string userDataDirectory, StartupOptions options)
    {
        if (options.SelfTest)
        {
            return new FullscreenSettings();
        }

        if (settingsStore.Exists)
        {
            return settingsStore.Load();
        }

        // First launch against this profile: carry the WPF language and target
        // display across so fullscreen opens on the monitor the user chose.
        var settings = new FullscreenSettings();
        var defaults = WpfProfileImport.Read(userDataDirectory);
        if (!string.IsNullOrWhiteSpace(defaults.Language))
        {
            settings.Language = defaults.Language;
        }

        if (defaults.FullscreenMonitor is int monitor)
        {
            settings.Monitor = monitor;
        }

        settings.InterfaceVolume = defaults.FullscreenInterfaceVolume ?? settings.InterfaceVolume;
        settings.BackgroundVolume = defaults.FullscreenBackgroundVolume ?? settings.BackgroundVolume;
        settings.MuteInBackground = defaults.FullscreenMuteInBackground ?? settings.MuteInBackground;
        settings.UsePrimaryDisplay = defaults.FullscreenUsePrimaryDisplay ?? settings.UsePrimaryDisplay;
        settings.ShowClock = defaults.FullscreenShowClock ?? settings.ShowClock;
        settings.ShowBattery = defaults.FullscreenShowBattery ?? settings.ShowBattery;
        settings.ShowBatteryPercentage = defaults.FullscreenShowBatteryPercentage ?? settings.ShowBatteryPercentage;
        settings.MinimizeAfterGameStartup =
            defaults.FullscreenMinimizeAfterGameStartup ?? settings.MinimizeAfterGameStartup;

        settingsStore.Save(settings);
        return settings;
    }
}
