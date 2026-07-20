using System.Text.RegularExpressions;

namespace Playnite.Avalonia.App.Services;

// A selectable interface language. Id matches the Playnite language setting
// value ("english" for the source language, otherwise a culture code such as
// "de_DE"); DisplayName is the file's LanguageName string.
public sealed class LanguageOption
{
    public string Id { get; init; }
    public string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

// Enumerates the bundled interface languages and resolves the ordered set of
// resource dictionaries to merge for a selected language. English is the source
// language (LocSource) and is always the fallback layer; a selected culture is
// overlaid on top so untranslated keys fall through to English.
public static class LanguageCatalog
{
    public const string SourceLanguageId = "english";

    // LocSource is the authoritative English corpus (the fallback base), not a
    // separately selectable language.
    private const string EnglishBaseFile = "LocSource";
    private const string LanguagesFolderName = "Languages";
    private const string ShellKeysFile = "english.axaml";

    // Playnite culture files are named like "de_DE"; anything else (LocSource,
    // the shell keys) is not an interface-language choice.
    private static readonly Regex CulturePattern =
        new("^[A-Za-z]+_[A-Za-z]+$", RegexOptions.Compiled);

    public static IReadOnlyList<LanguageOption> Discover(string localizationDir)
    {
        var options = new List<LanguageOption>
        {
            new() { Id = SourceLanguageId, DisplayName = "English" }
        };

        var languagesDir = Path.Combine(localizationDir, LanguagesFolderName);
        if (Directory.Exists(languagesDir))
        {
            foreach (var file in Directory.GetFiles(languagesDir, "*.axaml"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (id.Equals(EnglishBaseFile, StringComparison.OrdinalIgnoreCase) ||
                    !CulturePattern.IsMatch(id))
                {
                    continue;
                }

                options.Add(new LanguageOption
                {
                    Id = id,
                    DisplayName = ReadLanguageName(file) ?? id
                });
            }
        }

        return options
            .GroupBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // The dictionaries to merge, lowest priority first: shell-specific keys, the
    // English corpus base, then the selected culture overlay (if not English).
    // Shells that ship co-located in one directory must use distinct shell-key
    // file names or the last-staged shell silently overwrites the others.
    public static IReadOnlyList<string> ResolveLanguagePaths(
        string localizationDir,
        string selectedId,
        string shellKeysFileName = ShellKeysFile)
    {
        var paths = new List<string>();
        var shellKeys = Path.Combine(localizationDir, shellKeysFileName);
        if (File.Exists(shellKeys))
        {
            paths.Add(shellKeys);
        }

        var languagesDir = Path.Combine(localizationDir, LanguagesFolderName);
        var englishBase = Path.Combine(languagesDir, EnglishBaseFile + ".axaml");
        if (File.Exists(englishBase))
        {
            paths.Add(englishBase);
        }

        if (!string.IsNullOrWhiteSpace(selectedId) &&
            !selectedId.Equals(SourceLanguageId, StringComparison.OrdinalIgnoreCase))
        {
            var overlay = Path.Combine(languagesDir, selectedId + ".axaml");
            if (File.Exists(overlay))
            {
                paths.Add(overlay);
            }
        }

        return paths;
    }

    private static string ReadLanguageName(string filePath)
    {
        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                var match = Regex.Match(line, "LanguageName\">(.+?)<");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch (IOException)
        {
        }

        return null;
    }
}
