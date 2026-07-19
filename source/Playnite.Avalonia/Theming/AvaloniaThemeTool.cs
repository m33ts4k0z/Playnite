using System.IO.Compression;
using System.Text.Json;

namespace Playnite.Avalonia.Theming;

public static class AvaloniaThemeTool
{
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly HashSet<string> excludedDirectories = new(
        new[] { ".git", ".vs", "bin", "obj" },
        StringComparer.OrdinalIgnoreCase);

    public static string Create(
        AvaloniaThemeMode mode,
        string name,
        string outputDirectory)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A theme name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        }

        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new IOException($"Theme output directory is not empty: {root}");
        }

        Directory.CreateDirectory(root);
        var id = $"{SafeFileName(name)}_{Guid.NewGuid():N}";
        var manifest = string.Join(Environment.NewLine, new[]
        {
            $"Id: {YamlString(id)}",
            $"Name: {YamlString(name)}",
            "Author: \"Your Name Here\"",
            "Version: 1.0.0",
            $"Mode: {mode}",
            $"ThemeApiVersion: {AvaloniaThemePackage.CurrentApiVersion}",
            "Framework: Avalonia",
            "EntryPoint: Theme.axaml",
            "Styles:",
            "  - Styles.axaml",
            string.Empty
        });
        File.WriteAllText(Path.Combine(root, "theme.yaml"), manifest);
        File.WriteAllText(Path.Combine(root, "Theme.axaml"), CreateDictionary(mode));
        File.WriteAllText(Path.Combine(root, "Styles.axaml"),
            "<Styles xmlns=\"https://github.com/avaloniaui\"\n" +
            "        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "</Styles>\n");
        return root;
    }

    public static AvaloniaThemePackage Validate(
        string path,
        AvaloniaThemeMode? expectedMode = null)
    {
        var package = AvaloniaThemePackage.Load(path, expectedMode);
        package.ValidateMarkupStructure();
        return package;
    }

    public static string Pack(
        string path,
        string destinationDirectory,
        AvaloniaThemeMode? expectedMode = null)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("A destination directory is required.", nameof(destinationDirectory));
        }

        var package = Validate(path, expectedMode);
        if (package.IsRawDictionary)
        {
            throw new InvalidDataException("Raw AXAML overrides cannot be packaged; add a theme.yaml manifest.");
        }

        var destination = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destination);
        var packagePath = Path.Combine(
            destination,
            $"{SafeFileName(package.Manifest.Id)}_{SafeFileName(package.Manifest.Version)}.pthm");
        var packagePathFull = Path.GetFullPath(packagePath);
        if ((File.GetAttributes(package.RootDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("A theme package root cannot be a file-system link.");
        }

        var packageFiles = EnumeratePackageFiles(package.RootDirectory)
            .Where(file => !string.Equals(file, packagePathFull, PathComparison))
            .OrderBy(
                file => Path.GetRelativePath(package.RootDirectory, file),
                StringComparer.Ordinal)
            .ToList();
        var temporaryPath = packagePathFull + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var fullPath in packageFiles)
                {
                    var relativePath = Path.GetRelativePath(package.RootDirectory, fullPath).Replace('\\', '/');
                    archive.CreateEntryFromFile(fullPath, relativePath, CompressionLevel.Optimal);
                }
            }

            File.Move(temporaryPath, packagePathFull, true);
        }
        catch (Exception packException)
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Theme packaging and temporary-file cleanup both failed.",
                    packException,
                    cleanupException);
            }

            throw;
        }

        return packagePathFull;
    }

    private static IEnumerable<string> EnumeratePackageFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var fullPath = Path.GetFullPath(entry);
                var attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Theme packages cannot contain file-system links: {fullPath}");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!ShouldExclude(root, fullPath))
                    {
                        pending.Push(fullPath);
                    }
                }
                else if (!ShouldExclude(root, fullPath))
                {
                    yield return fullPath;
                }
            }
        }
    }

    private static bool ShouldExclude(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(segment => excludedDirectories.Contains(segment) ||
            segment.StartsWith("backup_", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".user", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pthm", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .Distinct()
            .ToArray();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character)
            .ToArray())
            .Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Theme" : sanitized;
    }

    private static string YamlString(string value) => JsonSerializer.Serialize(value);

    private static string CreateDictionary(AvaloniaThemeMode mode)
    {
        var resources = mode == AvaloniaThemeMode.Desktop
            ? new[]
            {
                ("DesktopBackgroundBrush", "#10141B"),
                ("DesktopPanelBrush", "#171D26"),
                ("DesktopPanelStrongBrush", "#202834"),
                ("DesktopAccentBrush", "#00A8E8"),
                ("DesktopTextBrush", "#F3F6FA"),
                ("DesktopMutedBrush", "#9EABBA")
            }
            : new[]
            {
                ("FullscreenBackgroundBrush", "#0B0E13"),
                ("FullscreenSurfaceBrush", "#D91A202A"),
                ("FullscreenSurfaceStrongBrush", "#F2232B37"),
                ("FullscreenAccentBrush", "#00A8E8"),
                ("FullscreenTextBrush", "#F7FAFC"),
                ("FullscreenMutedTextBrush", "#AAB6C4")
            };
        var lines = new List<string>
        {
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"",
            "                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
        };
        lines.AddRange(resources.Select(resource =>
            $"    <SolidColorBrush x:Key=\"{resource.Item1}\" Color=\"{resource.Item2}\" />"));
        lines.Add("</ResourceDictionary>");
        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }
}
