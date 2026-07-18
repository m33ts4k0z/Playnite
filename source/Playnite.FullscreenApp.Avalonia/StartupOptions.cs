namespace Playnite.FullscreenApp.Avalonia;

internal sealed class StartupOptions
{
    public bool SelfTest { get; private set; }
    public bool Windowed { get; private set; }
    public string UserDataDirectory { get; private set; }
    public string LibraryPath { get; private set; }
    public string CustomThemePath { get; private set; }

    public static StartupOptions Parse(string[] args)
    {
        var options = new StartupOptions();
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "--self-test":
                case "--auto":
                    options.SelfTest = true;
                    options.Windowed = true;
                    break;
                case "--windowed":
                    options.Windowed = true;
                    break;
                case "--userdatadir" when index + 1 < args.Length:
                    options.UserDataDirectory = Path.GetFullPath(args[++index]);
                    break;
                case "--library-path" when index + 1 < args.Length:
                    options.LibraryPath = Path.GetFullPath(args[++index]);
                    break;
                case "--theme" when index + 1 < args.Length:
                    options.CustomThemePath = Path.GetFullPath(args[++index]);
                    break;
            }
        }

        options.UserDataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Playnite");
        options.LibraryPath ??= Path.Combine(options.UserDataDirectory, "library");
        return options;
    }
}
