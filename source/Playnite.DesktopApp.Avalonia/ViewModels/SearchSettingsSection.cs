using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using Playnite.Plugins;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class SearchSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Func<IReadOnlyList<LoadedPlugin>> plugins;
    private readonly Func<IReadOnlyList<V7LoadedPlugin>> v7Plugins;
    private GameSearchItemAction primaryGameSearchItemAction;
    private GameSearchItemAction secondaryGameSearchItemAction;
    private bool globalSearchOpenWithLegacySearch;
    private bool saveGlobalSearchFilterSettings;
    private bool includeCommandsInDefaultSearch;
    private HotKey systemSearchHotkey;
    private SearchWindowVisibilitySettings visibility = new();

    public override string Key => "Search";
    public override string Title => "Library — Search";
    public override global::Avalonia.Controls.Control Content { get; }
    public IReadOnlyList<GameSearchItemAction> GameActions { get; } = Enum.GetValues<GameSearchItemAction>();
    public ObservableCollection<SearchProviderSettingsRow> SearchProviders { get; } = new();
    public string SystemHotkeySupportText => OperatingSystem.IsWindows()
        ? "The system-wide shortcut is registered through the Windows platform adapter."
        : "System-wide shortcuts are unavailable on this platform; desktop environments may reserve them. Ctrl+F still works inside Playnite.";

    public GameSearchItemAction PrimaryGameSearchItemAction
    {
        get => primaryGameSearchItemAction;
        set => SetField(ref primaryGameSearchItemAction, value);
    }
    public GameSearchItemAction SecondaryGameSearchItemAction
    {
        get => secondaryGameSearchItemAction;
        set => SetField(ref secondaryGameSearchItemAction, value);
    }
    public bool GlobalSearchOpenWithLegacySearch
    {
        get => globalSearchOpenWithLegacySearch;
        set => SetField(ref globalSearchOpenWithLegacySearch, value);
    }
    public bool SaveGlobalSearchFilterSettings
    {
        get => saveGlobalSearchFilterSettings;
        set => SetField(ref saveGlobalSearchFilterSettings, value);
    }
    public bool IncludeCommandsInDefaultSearch
    {
        get => includeCommandsInDefaultSearch;
        set => SetField(ref includeCommandsInDefaultSearch, value);
    }
    public HotKey SystemSearchHotkey
    {
        get => systemSearchHotkey;
        set => SetField(ref systemSearchHotkey, value);
    }
    public SearchWindowVisibilitySettings Visibility
    {
        get => visibility;
        private set => SetField(ref visibility, value);
    }

    public SearchSettingsSection(
        DesktopSettings settings,
        Func<IReadOnlyList<LoadedPlugin>> plugins,
        Func<IReadOnlyList<V7LoadedPlugin>> v7Plugins)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.plugins = plugins ?? (() => Array.Empty<LoadedPlugin>());
        this.v7Plugins = v7Plugins ?? (() => Array.Empty<V7LoadedPlugin>());
        Content = new SearchSettingsView { DataContext = this };
    }

    public override void Open()
    {
        PrimaryGameSearchItemAction = settings.PrimaryGameSearchItemAction;
        SecondaryGameSearchItemAction = settings.SecondaryGameSearchItemAction;
        GlobalSearchOpenWithLegacySearch = settings.GlobalSearchOpenWithLegacySearch;
        SaveGlobalSearchFilterSettings = settings.SaveGlobalSearchFilterSettings;
        IncludeCommandsInDefaultSearch = settings.IncludeCommandsInDefaultSearch;
        SystemSearchHotkey = settings.SystemSearchHotkey;
        Visibility = (settings.SearchWindowVisibility ?? new SearchWindowVisibilitySettings()).Clone();

        SearchProviders.Clear();
        foreach (var plugin in plugins().Where(plugin => plugin?.Plugin != null))
        {
            foreach (var support in plugin.Plugin.Searches ?? [])
            {
                if (support == null || string.IsNullOrWhiteSpace(support.DefaultKeyword) || support.Context == null)
                {
                    continue;
                }

                var id = GetProviderId(plugin, support.DefaultKeyword);
                settings.CustomSearchKeywords.TryGetValue(id, out var customKeyword);
                SearchProviders.Add(new SearchProviderSettingsRow(
                    id,
                    support.Name,
                    support.DefaultKeyword,
                    customKeyword));
            }
        }


        foreach (var plugin in v7Plugins())
        {
            foreach (var support in plugin.GetSearches())
            {
                var id = $"{plugin.Manifest.Id}{support.DefaultKeyword}";
                settings.CustomSearchKeywords.TryGetValue(id, out var customKeyword);
                SearchProviders.Add(new SearchProviderSettingsRow(
                    id,
                    support.Name,
                    support.DefaultKeyword,
                    customKeyword));
            }
        }
    }

    public override SettingsSectionValidationResult Validate()
    {
        var duplicate = SearchProviders
            .Select(provider => provider.EffectiveKeyword)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .GroupBy(keyword => keyword, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            return new(false, $"Search-provider keyword '{duplicate.Key}' is assigned more than once.");
        }

        if (SearchProviders.Any(provider => provider.EffectiveKeyword.Any(char.IsWhiteSpace)))
        {
            return new(false, "Search-provider keywords cannot contain spaces.");
        }

        return SettingsSectionValidationResult.Valid;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.PrimaryGameSearchItemAction = PrimaryGameSearchItemAction;
        settings.SecondaryGameSearchItemAction = SecondaryGameSearchItemAction;
        settings.GlobalSearchOpenWithLegacySearch = GlobalSearchOpenWithLegacySearch;
        settings.SaveGlobalSearchFilterSettings = SaveGlobalSearchFilterSettings;
        settings.IncludeCommandsInDefaultSearch = IncludeCommandsInDefaultSearch;
        settings.SystemSearchHotkey = SystemSearchHotkey;
        settings.SearchWindowVisibility = Visibility.Clone();
        settings.CustomSearchKeywords = SearchProviders
            .Where(provider => !string.IsNullOrWhiteSpace(provider.CustomKeyword))
            .ToDictionary(
                provider => provider.Id,
                provider => provider.CustomKeyword.Trim(),
                StringComparer.OrdinalIgnoreCase);
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this && Validate().IsValid;
        return new(Key, valid, valid
            ? $"global actions, seven visibility fields, hotkey capture, and {SearchProviders.Count} provider keyword(s) are available"
            : "global-search settings are invalid");
    }

    internal static string GetProviderId(LoadedPlugin plugin, string defaultKeyword) =>
        $"{plugin.Description.Id}{defaultKeyword}";
}

public sealed class SearchProviderSettingsRow : INotifyPropertyChanged
{
    private string customKeyword;

    public string Id { get; }
    public string Name { get; }
    public string DefaultKeyword { get; }
    public string CustomKeyword
    {
        get => customKeyword;
        set
        {
            if (!string.Equals(customKeyword, value, StringComparison.Ordinal))
            {
                customKeyword = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomKeyword)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveKeyword)));
            }
        }
    }
    public string EffectiveKeyword => string.IsNullOrWhiteSpace(CustomKeyword)
        ? DefaultKeyword
        : CustomKeyword.Trim();
    public event PropertyChangedEventHandler PropertyChanged;

    public SearchProviderSettingsRow(
        string id,
        string name,
        string defaultKeyword,
        string customKeyword)
    {
        Id = id;
        Name = name;
        DefaultKeyword = defaultKeyword;
        this.customKeyword = customKeyword ?? string.Empty;
    }
}
