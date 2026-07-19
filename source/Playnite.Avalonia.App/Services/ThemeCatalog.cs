using Playnite.Avalonia.Theming;

namespace Playnite.Avalonia.App.Services;

// A selectable desktop theme. An empty Path denotes the built-in Default theme
// (which is always applied first); a non-empty Path is a custom theme package
// directory assigned to the shell's ThemePath setting.
public sealed class ThemeOption
{
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
    {
        var options = new List<ThemeOption>
        {
            new() { Name = DefaultThemeName, Path = string.Empty }
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
                    var package = AvaloniaThemePackage.Load(directory, AvaloniaThemeMode.Desktop);
                    options.Add(new ThemeOption
                    {
                        Name = string.IsNullOrWhiteSpace(package.Name) ? folderName : package.Name,
                        Path = System.IO.Path.GetFullPath(directory)
                    });
                }
                catch (Exception exception) when (
                    exception is InvalidDataException or FileNotFoundException or ArgumentException)
                {
                    // Skip malformed or non-Desktop theme packages.
                }
            }
        }

        return options
            .GroupBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}
