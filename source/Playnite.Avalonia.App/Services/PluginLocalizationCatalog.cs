using System.Xml.Linq;

namespace Playnite.Avalonia.App.Services;

internal static class PluginLocalizationCatalog
{
    private static readonly XNamespace XamlNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    public static IReadOnlyDictionary<string, string> Load(
        string extensionDirectory,
        string language)
    {
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(extensionDirectory))
        {
            return resources;
        }

        var localizationDirectory = Path.Combine(extensionDirectory, "Localization");
        if (!Directory.Exists(localizationDirectory))
        {
            return resources;
        }

        var englishPath = FindDictionary(localizationDirectory, "en_US");
        if (englishPath == null)
        {
            return resources;
        }

        MergeDictionary(englishPath, resources);

        var selectedLanguage = string.IsNullOrWhiteSpace(language) ? "en_US" : language;
        if (string.Equals(selectedLanguage, "english", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(selectedLanguage, "en_US", StringComparison.OrdinalIgnoreCase))
        {
            return resources;
        }

        var localizedPath = FindDictionary(
            localizationDirectory,
            Path.GetFileName(selectedLanguage));
        if (localizedPath != null)
        {
            MergeDictionary(localizedPath, resources);
        }

        return resources;
    }

    private static string FindDictionary(string localizationDirectory, string language)
    {
        foreach (var extension in new[] { ".xaml", ".axaml" })
        {
            var path = Path.Combine(localizationDirectory, language + extension);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static void MergeDictionary(
        string path,
        Dictionary<string, string> resources)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        foreach (var element in document.Descendants()
            .Where(element => element.Name.LocalName == "String"))
        {
            var key = element.Attribute(XamlNamespace + "Key")?.Value;
            var value = element.Value;
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrEmpty(value))
            {
                resources[key] = value;
            }
        }
    }
}
