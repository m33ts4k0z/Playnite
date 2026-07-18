using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

public sealed class App : Application
{
    private DesktopLibrary library;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = Program.Options;
            library = new DesktopLibrary(options.UserDataDirectory, options.LibraryPath);
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

            var viewModel = new DesktopAppViewModel(library.Games, startupError);
            desktop.MainWindow = new MainWindow(viewModel, library, options);
            desktop.Exit += (_, _) => library.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
