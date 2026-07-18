using Playnite.Common;
using Playnite.SDK;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class AvaloniaFullscreenThemeManifest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public ApplicationMode Mode { get; set; }
    public string ThemeApiVersion { get; set; }
    public string Framework { get; set; }
    public string EntryPoint { get; set; } = "Theme.axaml";
    public List<string> Styles { get; set; } = new();
}

public sealed class AvaloniaFullscreenThemePackage
{
    public static readonly Version CurrentApiVersion = new("3.0.0");

    public string RootDirectory { get; }
    public string Name { get; }
    public IReadOnlyList<string> ResourceDictionaries { get; }
    public IReadOnlyList<string> SelectorStyles { get; }
    public bool IsRawDictionary { get; }

    private AvaloniaFullscreenThemePackage(
        string rootDirectory,
        string name,
        IReadOnlyList<string> resourceDictionaries,
        IReadOnlyList<string> selectorStyles,
        bool isRawDictionary)
    {
        RootDirectory = rootDirectory;
        Name = name;
        ResourceDictionaries = resourceDictionaries;
        SelectorStyles = selectorStyles;
        IsRawDictionary = isRawDictionary;
    }

    public static AvaloniaFullscreenThemePackage Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A theme directory, theme.yaml, or AXAML dictionary is required.", nameof(path));
        }

        var resolved = Path.GetFullPath(path);
        if (File.Exists(resolved) && resolved.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
        {
            return new AvaloniaFullscreenThemePackage(
                Path.GetDirectoryName(resolved),
                Path.GetFileNameWithoutExtension(resolved),
                new[] { resolved },
                Array.Empty<string>(),
                true);
        }

        var root = Directory.Exists(resolved) ? resolved : Path.GetDirectoryName(resolved);
        var manifestPath = Directory.Exists(resolved) ? Path.Combine(resolved, "theme.yaml") : resolved;
        if (root == null || !File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "An Avalonia Fullscreen package must contain theme.yaml, or --theme must point to a loose .axaml dictionary.",
                manifestPath);
        }

        var manifest = Serialization.FromYaml<AvaloniaFullscreenThemeManifest>(File.ReadAllText(manifestPath));
        Validate(manifest);
        var dictionary = ResolvePackageFile(root, manifest.EntryPoint, required: true);
        var styles = (manifest.Styles ?? new List<string>())
            .Select(style => ResolvePackageFile(root, style, required: true))
            .ToList();
        return new AvaloniaFullscreenThemePackage(
            root,
            string.IsNullOrWhiteSpace(manifest.Name) ? Path.GetFileName(root) : manifest.Name,
            new[] { dictionary },
            styles,
            false);
    }

    private static void Validate(AvaloniaFullscreenThemeManifest manifest)
    {
        if (manifest == null)
        {
            throw new InvalidDataException("theme.yaml is empty.");
        }

        if (!string.Equals(manifest.Framework, "Avalonia", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "This is not an Avalonia theme package. Migrate WPF XAML and set Framework: Avalonia.");
        }

        if (manifest.Mode != ApplicationMode.Fullscreen)
        {
            throw new InvalidDataException("Only Mode: Fullscreen themes can be loaded by the Fullscreen app.");
        }

        if (!Version.TryParse(manifest.ThemeApiVersion, out var version) ||
            version.Major != CurrentApiVersion.Major ||
            version > CurrentApiVersion)
        {
            throw new InvalidDataException(
                $"Theme API {manifest.ThemeApiVersion ?? "(missing)"} is incompatible; this app supports {CurrentApiVersion}.");
        }
    }

    private static string ResolvePackageFile(string root, string relativePath, bool required)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("The theme entry point is missing.");
        }

        var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!candidate.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Theme path '{relativePath}' escapes the package directory.");
        }

        if (required && !File.Exists(candidate))
        {
            throw new FileNotFoundException($"Theme file '{relativePath}' does not exist.", candidate);
        }

        return candidate;
    }
}
