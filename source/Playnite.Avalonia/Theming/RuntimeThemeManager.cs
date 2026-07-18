using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Playnite.Avalonia.Theming;

/// <summary>
/// Transactional manager for Playnite's loose default/custom theme dictionaries,
/// selector styles, and localization dictionary. Files are fully parsed before
/// the active resources are changed, so a malformed community theme leaves the
/// current UI intact.
/// </summary>
public sealed class RuntimeThemeManager
{
    private readonly Application application;
    private readonly LooseXamlLoader loader;
    private readonly List<ResourceDictionary> activeThemeDictionaries = new();
    private readonly List<Styles> activeThemeStyles = new();
    private ResourceDictionary activeLanguage;

    public IReadOnlyList<ResourceDictionary> ActiveThemeDictionaries => activeThemeDictionaries;
    public ResourceDictionary ActiveLanguage => activeLanguage;

    public RuntimeThemeManager(Application application, Assembly controlsAssembly = null)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
        loader = new LooseXamlLoader(controlsAssembly);
    }

    public void ApplyTheme(
        IEnumerable<string> defaultDictionaries,
        IEnumerable<string> customDictionaries = null,
        IEnumerable<string> selectorStyles = null)
    {
        var dictionaryPaths = (defaultDictionaries ?? Enumerable.Empty<string>())
            .Concat(customDictionaries ?? Enumerable.Empty<string>())
            .ToList();
        var stylePaths = (selectorStyles ?? Enumerable.Empty<string>()).ToList();

        var dictionaries = dictionaryPaths.Select(loader.LoadFile<ResourceDictionary>).ToList();
        var styles = stylePaths.Select(loader.LoadFile<Styles>).ToList();

        foreach (var dictionary in activeThemeDictionaries)
        {
            application.Resources.MergedDictionaries.Remove(dictionary);
        }

        foreach (var style in activeThemeStyles)
        {
            application.Styles.Remove(style);
        }

        activeThemeDictionaries.Clear();
        activeThemeStyles.Clear();

        foreach (var dictionary in dictionaries)
        {
            application.Resources.MergedDictionaries.Add(dictionary);
            activeThemeDictionaries.Add(dictionary);
        }

        foreach (var style in styles)
        {
            application.Styles.Add(style);
            activeThemeStyles.Add(style);
        }
    }

    public void ApplyLanguage(string languageDictionaryPath)
    {
        var language = loader.LoadFile<ResourceDictionary>(languageDictionaryPath);

        if (activeLanguage != null)
        {
            application.Resources.MergedDictionaries.Remove(activeLanguage);
        }

        application.Resources.MergedDictionaries.Add(language);
        activeLanguage = language;
    }
}
