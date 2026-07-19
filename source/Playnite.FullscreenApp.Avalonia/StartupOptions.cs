namespace Playnite.FullscreenApp.Avalonia;

internal sealed class StartupOptions
{
    public bool SelfTest { get; private set; }
    public bool Windowed { get; private set; }
    public string UserDataDirectory { get; private set; }
    public string LibraryPath { get; private set; }
    public string CustomThemePath { get; private set; }
    public string UriData { get; private set; }
    public bool Shutdown { get; private set; }

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
                case "--uridata" when index + 1 < args.Length:
                    options.UriData = args[++index];
                    break;
                case "--shutdown":
                    options.Shutdown = true;
                    break;
            }
        }

#if WINDOWS
        options.UserDataDirectory ??= Playnite.Avalonia.App.Services.CanonicalProfile.ResolveDefaultUserDataDirectory();
        options.LibraryPath ??= Playnite.Avalonia.App.Services.CanonicalProfile.ResolveDefaultLibraryPath(options.UserDataDirectory);
#else
        options.UserDataDirectory ??= global::Playnite.PlaynitePaths.ConfigRootPath;
        options.LibraryPath ??= Path.Combine(options.UserDataDirectory, "library");
#endif
        return options;
    }
}
