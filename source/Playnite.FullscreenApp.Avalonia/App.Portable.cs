using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;

namespace Playnite.FullscreenApp.Avalonia;

public sealed class App : Application
{
    private FullscreenLibrary library;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = Program.Options;
            library = new FullscreenLibrary(options.UserDataDirectory, options.LibraryPath);
            string startupError = null;
            try
            {
                if (options.SelfTest)
                {
                    library.OpenTemporaryLibrary(1_000);
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

            var window = new MainWindow(library, startupError, options);
            desktop.MainWindow = window;
            Program.InstanceCoordinator?.SetCommandHandler(command =>
                Dispatcher.UIThread.Post(() => ProcessCommand(command, window, desktop)));
            if (!string.IsNullOrWhiteSpace(options.UriData))
            {
                window.ProcessUri(options.UriData);
            }

            desktop.Exit += (_, _) => library.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ProcessCommand(
        CommandExecutedEventArgs command,
        MainWindow window,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        switch (command.Command)
        {
            case CmdlineCommand.Focus:
                window.RestoreAndActivate();
                break;
            case CmdlineCommand.UriRequest:
                window.ProcessUri(command.Args);
                break;
            case CmdlineCommand.Shutdown:
                desktop.Shutdown();
                break;
        }
    }
}
