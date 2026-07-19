using Playnite.Avalonia.Theming;

namespace Playnite.Avalonia.App.Services;

// A selectable desktop theme. An empty Path denotes the built-in Default theme
// (which is always applied first); a non-empty Path is a custom theme package
// directory assigned to the shell's ThemePath setting.
public sealed class ThemeOption
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Path { get; init; }

    public override string ToString() => Name;
}

// Enumerates the desktop themes available to the shell: the built-in Default
// plus any valid theme packages found under the given theme root directories.
// Only packages that load as Desktop, theme-API-3 themes are listed, so an
// incompatible or malformed theme never appears as a choice.
public static class ThemeCatalog
{
    private const string DefaultThemeName = "Default";

    public static IReadOnlyList<ThemeOption> DiscoverDesktopThemes(IEnumerable<string> themeRootDirectories)
        => DiscoverThemes(themeRootDirectories, AvaloniaThemeMode.Desktop);

    public static IReadOnlyList<ThemeOption> DiscoverFullscreenThemes(IEnumerable<string> themeRootDirectories)
        => DiscoverThemes(themeRootDirectories, AvaloniaThemeMode.Fullscreen);

    public static string ResolveFullscreenThemeReference(
        string reference,
        IEnumerable<string> themeRootDirectories)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return string.Empty;
        }

        if (Directory.Exists(reference))
        {
            try
            {
                var package = AvaloniaThemePackage.Load(reference, AvaloniaThemeMode.Fullscreen);
                return package.RootDirectory;
            }
            catch (Exception exception) when (
                exception is InvalidDataException or FileNotFoundException or ArgumentException)
            {
                return string.Empty;
            }
        }

        var themes = DiscoverFullscreenThemes(themeRootDirectories);
        var match = themes.FirstOrDefault(option =>
            string.Equals(option.Path, reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.Id, reference, StringComparison.OrdinalIgnoreCase));
        return match?.Path ?? string.Empty;
    }

    private static IReadOnlyList<ThemeOption> DiscoverThemes(
        IEnumerable<string> themeRootDirectories,
        AvaloniaThemeMode mode)
    {
        var options = new List<ThemeOption>
        {
            new() { Id = string.Empty, Name = DefaultThemeName, Path = string.Empty }
        };

        foreach (var root in themeRootDirectories ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.GetDirectories(root))
            {
                var folderName = System.IO.Path.GetFileName(directory);
                if (folderName.Equals(DefaultThemeName, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(System.IO.Path.Combine(directory, "theme.yaml")))
                {
                    continue;
                }

                try
                {
                    var package = AvaloniaThemePackage.Load(directory, mode);
                    options.Add(new ThemeOption
                    {
                        Id = package.Manifest?.Id ?? string.Empty,
                        Name = string.IsNullOrWhiteSpace(package.Name) ? folderName : package.Name,
                        Path = System.IO.Path.GetFullPath(directory)
                    });
                }
                catch (Exception exception) when (
                    exception is InvalidDataException or FileNotFoundException or ArgumentException)
                {
                    // Skip malformed packages and themes for the other shell mode.
                }
            }
        }

        return options
            .GroupBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}
