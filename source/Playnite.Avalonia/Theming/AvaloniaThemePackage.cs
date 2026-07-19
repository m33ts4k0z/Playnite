using System.Xml;
using System.Xml.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Playnite.Avalonia.Theming;

public enum AvaloniaThemeMode
{
    Desktop,
    Fullscreen
}

public sealed class AvaloniaThemeManifest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public string Mode { get; set; }
    public string ThemeApiVersion { get; set; }
    public string Framework { get; set; }
    public string EntryPoint { get; set; } = "Theme.axaml";
    public List<string> Styles { get; set; } = new();
}

public sealed class AvaloniaThemePackage
{
    public static readonly System.Version CurrentApiVersion = new("3.0.0");
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string RootDirectory { get; }
    public string ManifestPath { get; }
    public string Name { get; }
    public AvaloniaThemeMode Mode { get; }
    public AvaloniaThemeManifest Manifest { get; }
    public IReadOnlyList<string> ResourceDictionaries { get; }
    public IReadOnlyList<string> SelectorStyles { get; }
    public bool IsRawDictionary { get; }

    private AvaloniaThemePackage(
        string rootDirectory,
        string manifestPath,
        string name,
        AvaloniaThemeMode mode,
        AvaloniaThemeManifest manifest,
        IReadOnlyList<string> resourceDictionaries,
        IReadOnlyList<string> selectorStyles,
        bool isRawDictionary)
    {
        RootDirectory = rootDirectory;
        ManifestPath = manifestPath;
        Name = name;
        Mode = mode;
        Manifest = manifest;
        ResourceDictionaries = resourceDictionaries;
        SelectorStyles = selectorStyles;
        IsRawDictionary = isRawDictionary;
    }

    public static AvaloniaThemePackage Load(string path, AvaloniaThemeMode? expectedMode = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "A theme directory, theme.yaml, or AXAML dictionary is required.",
                nameof(path));
        }

        var resolved = Path.GetFullPath(path);
        if (File.Exists(resolved) && resolved.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
        {
            if (expectedMode == null)
            {
                throw new InvalidDataException("A mode is required when loading a raw AXAML dictionary.");
            }

            return new AvaloniaThemePackage(
                Path.GetDirectoryName(resolved),
                null,
                Path.GetFileNameWithoutExtension(resolved),
                expectedMode.Value,
                null,
                new[] { resolved },
                Array.Empty<string>(),
                true);
        }

        var root = Directory.Exists(resolved) ? resolved : Path.GetDirectoryName(resolved);
        var manifestPath = Directory.Exists(resolved) ? Path.Combine(resolved, "theme.yaml") : resolved;
        if (root == null || !File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "An Avalonia theme package must contain theme.yaml, or the path must identify a loose .axaml dictionary.",
                manifestPath);
        }

        AvaloniaThemeManifest manifest;
        try
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            manifest = deserializer.Deserialize<AvaloniaThemeManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception exception) when (exception is YamlException or InvalidOperationException)
        {
            throw new InvalidDataException($"Theme manifest is invalid: {exception.Message}", exception);
        }

        var mode = Validate(manifest, expectedMode);
        var dictionary = ResolvePackageFile(root, manifest.EntryPoint, "entry point");
        var styles = (manifest.Styles ?? new List<string>())
            .Select(style => ResolvePackageFile(root, style, "selector style"))
            .ToList();
        if (styles.Count != styles.Distinct(PathComparer).Count())
        {
            throw new InvalidDataException("The theme manifest contains duplicate selector-style paths.");
        }

        return new AvaloniaThemePackage(
            Path.GetFullPath(root),
            Path.GetFullPath(manifestPath),
            manifest.Name,
            mode,
            manifest,
            new[] { dictionary },
            styles,
            false);
    }

    public void ValidateMarkupStructure()
    {
        foreach (var dictionary in ResourceDictionaries)
        {
            ValidateMarkupRoot(dictionary, "ResourceDictionary");
        }

        foreach (var styles in SelectorStyles)
        {
            ValidateMarkupRoot(styles, "Styles");
        }
    }

    private static AvaloniaThemeMode Validate(
        AvaloniaThemeManifest manifest,
        AvaloniaThemeMode? expectedMode)
    {
        if (manifest == null)
        {
            throw new InvalidDataException("theme.yaml is empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            string.IsNullOrWhiteSpace(manifest.Name) ||
            string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidDataException("Theme Id, Name, and Version are required.");
        }

        if (!System.Version.TryParse(manifest.Version, out _))
        {
            throw new InvalidDataException($"Theme version '{manifest.Version}' is invalid.");
        }

        if (!string.Equals(manifest.Framework, "Avalonia", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "This is not an Avalonia theme package. Migrate WPF XAML and set Framework: Avalonia.");
        }

        if (!Enum.TryParse<AvaloniaThemeMode>(manifest.Mode, true, out var mode))
        {
            throw new InvalidDataException("Theme Mode must be Desktop or Fullscreen.");
        }

        if (expectedMode != null && mode != expectedMode)
        {
            throw new InvalidDataException(
                $"Only Mode: {expectedMode} themes can be loaded by this application.");
        }

        if (!System.Version.TryParse(manifest.ThemeApiVersion, out var version) ||
            version.Major != CurrentApiVersion.Major ||
            version > CurrentApiVersion)
        {
            throw new InvalidDataException(
                $"Theme API {manifest.ThemeApiVersion ?? "(missing)"} is incompatible; " +
                $"this application supports {CurrentApiVersion}.");
        }

        return mode;
    }

    private static string ResolvePackageFile(string root, string relativePath, string role)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException($"The theme {role} is missing.");
        }

        if (!relativePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Theme {role} '{relativePath}' must be an .axaml file.");
        }

        var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!candidate.StartsWith(rootPath, PathComparison))
        {
            throw new InvalidDataException($"Theme path '{relativePath}' escapes the package directory.");
        }

        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Theme file '{relativePath}' does not exist.", candidate);
        }

        return candidate;
    }

    private static void ValidateMarkupRoot(string path, string expectedRoot)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, settings);
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            if (!string.Equals(document.Root?.Name.LocalName, expectedRoot, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Theme file '{Path.GetFileName(path)}' must have a {expectedRoot} root element.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            throw new InvalidDataException(
                $"Theme file '{Path.GetFileName(path)}' is invalid: {exception.Message}",
                exception);
        }
    }
}
