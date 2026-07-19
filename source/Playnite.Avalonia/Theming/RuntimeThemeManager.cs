using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

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
    private readonly List<ResourceDictionary> activeLanguages = new();

    public IReadOnlyList<ResourceDictionary> ActiveThemeDictionaries => activeThemeDictionaries;

    // The effective (highest-priority) language dictionary, i.e. the selected
    // culture overlay when one is active, otherwise the English base.
    public ResourceDictionary ActiveLanguage => activeLanguages.Count > 0 ? activeLanguages[^1] : null;

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
        Dispatcher.UIThread.VerifyAccess();
        var dictionaryPaths = (defaultDictionaries ?? Enumerable.Empty<string>())
            .Concat(customDictionaries ?? Enumerable.Empty<string>())
            .ToList();
        var stylePaths = (selectorStyles ?? Enumerable.Empty<string>()).ToList();

        var dictionaries = dictionaryPaths.Select(loader.LoadFile<ResourceDictionary>).ToList();
        var styles = stylePaths.Select(loader.LoadFile<Styles>).ToList();
        var previousDictionaries = activeThemeDictionaries.ToList();
        var previousStyles = activeThemeStyles.ToList();
        var dictionaryIndex = FindDictionaryInsertionIndex(previousDictionaries);
        var styleIndex = FindStyleInsertionIndex(previousStyles);

        try
        {
            RemoveDictionaries(previousDictionaries);
            RemoveStyles(previousStyles);
            InsertDictionaries(dictionaries, dictionaryIndex);
            InsertStyles(styles, styleIndex);
        }
        catch (Exception applyException)
        {
            try
            {
                RemoveDictionaries(dictionaries);
                RemoveStyles(styles);
                InsertDictionaries(previousDictionaries, dictionaryIndex);
                InsertStyles(previousStyles, styleIndex);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Theme application failed and the previous theme could not be fully restored.",
                    applyException,
                    rollbackException);
            }

            throw;
        }

        activeThemeDictionaries.Clear();
        activeThemeDictionaries.AddRange(dictionaries);
        activeThemeStyles.Clear();
        activeThemeStyles.AddRange(styles);
    }

    public void ApplyLanguage(string languageDictionaryPath) =>
        ApplyLanguage(new[] { languageDictionaryPath });

    /// <summary>
    /// Applies an ordered set of language dictionaries, lowest priority first
    /// (e.g. shell keys, then the English corpus base, then the selected culture
    /// overlay). Later dictionaries win, so untranslated keys fall back to the
    /// English base. All files are parsed before the active language is swapped.
    /// </summary>
    public void ApplyLanguage(IReadOnlyList<string> languageDictionaryPaths)
    {
        Dispatcher.UIThread.VerifyAccess();
        var languages = (languageDictionaryPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(loader.LoadFile<ResourceDictionary>)
            .ToList();
        var previousLanguages = activeLanguages.ToList();
        var languageIndex = FindLanguageInsertionIndex(previousLanguages);

        try
        {
            RemoveDictionaries(previousLanguages);
            InsertDictionaries(languages, languageIndex);
        }
        catch (Exception applyException)
        {
            try
            {
                RemoveDictionaries(languages);
                InsertDictionaries(previousLanguages, languageIndex);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Language application failed and the previous language could not be restored.",
                    applyException,
                    rollbackException);
            }

            throw;
        }

        activeLanguages.Clear();
        activeLanguages.AddRange(languages);
    }

    private int FindLanguageInsertionIndex(IReadOnlyList<ResourceDictionary> languages)
    {
        var indexes = languages
            .Select(dictionary => application.Resources.MergedDictionaries.IndexOf(dictionary))
            .Where(index => index >= 0)
            .ToList();
        return indexes.Count > 0
            ? indexes.Min()
            : application.Resources.MergedDictionaries.Count;
    }

    private int FindDictionaryInsertionIndex(IReadOnlyList<ResourceDictionary> dictionaries)
    {
        var indexes = dictionaries
            .Select(dictionary => application.Resources.MergedDictionaries.IndexOf(dictionary))
            .Where(index => index >= 0)
            .ToList();
        if (indexes.Count > 0)
        {
            return indexes.Min();
        }

        var languageIndexes = activeLanguages
            .Select(dictionary => application.Resources.MergedDictionaries.IndexOf(dictionary))
            .Where(index => index >= 0)
            .ToList();
        return languageIndexes.Count > 0
            ? languageIndexes.Min()
            : application.Resources.MergedDictionaries.Count;
    }

    private int FindStyleInsertionIndex(IReadOnlyList<Styles> styles)
    {
        var indexes = styles
            .Select(style => application.Styles.IndexOf(style))
            .Where(index => index >= 0)
            .ToList();
        return indexes.Count > 0 ? indexes.Min() : application.Styles.Count;
    }

    private void RemoveDictionaries(IEnumerable<ResourceDictionary> dictionaries)
    {
        foreach (var dictionary in dictionaries)
        {
            application.Resources.MergedDictionaries.Remove(dictionary);
        }
    }

    private void InsertDictionaries(IReadOnlyList<ResourceDictionary> dictionaries, int index)
    {
        for (var offset = 0; offset < dictionaries.Count; offset++)
        {
            application.Resources.MergedDictionaries.Insert(
                Math.Min(index + offset, application.Resources.MergedDictionaries.Count),
                dictionaries[offset]);
        }
    }

    private void RemoveStyles(IEnumerable<Styles> styles)
    {
        foreach (var style in styles)
        {
            application.Styles.Remove(style);
        }
    }

    private void InsertStyles(IReadOnlyList<Styles> styles, int index)
    {
        for (var offset = 0; offset < styles.Count; offset++)
        {
            application.Styles.Insert(
                Math.Min(index + offset, application.Styles.Count),
                styles[offset]);
        }
    }
}
