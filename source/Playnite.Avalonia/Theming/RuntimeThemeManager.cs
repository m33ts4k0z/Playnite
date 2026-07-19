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

    public void ApplyLanguage(string languageDictionaryPath)
    {
        Dispatcher.UIThread.VerifyAccess();
        var language = loader.LoadFile<ResourceDictionary>(languageDictionaryPath);
        var previousLanguage = activeLanguage;
        var languageIndex = previousLanguage == null
            ? application.Resources.MergedDictionaries.Count
            : application.Resources.MergedDictionaries.IndexOf(previousLanguage);
        if (languageIndex < 0)
        {
            languageIndex = application.Resources.MergedDictionaries.Count;
        }

        try
        {
            if (previousLanguage != null)
            {
                application.Resources.MergedDictionaries.Remove(previousLanguage);
            }

            application.Resources.MergedDictionaries.Insert(
                Math.Min(languageIndex, application.Resources.MergedDictionaries.Count),
                language);
        }
        catch (Exception applyException)
        {
            try
            {
                application.Resources.MergedDictionaries.Remove(language);
                if (previousLanguage != null)
                {
                    application.Resources.MergedDictionaries.Insert(
                        Math.Min(languageIndex, application.Resources.MergedDictionaries.Count),
                        previousLanguage);
                }
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

        activeLanguage = language;
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

        var languageIndex = activeLanguage == null
            ? -1
            : application.Resources.MergedDictionaries.IndexOf(activeLanguage);
        return languageIndex >= 0 ? languageIndex : application.Resources.MergedDictionaries.Count;
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
