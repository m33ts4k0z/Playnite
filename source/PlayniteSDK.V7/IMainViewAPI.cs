using Avalonia.Threading;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum DesktopView
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Details,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Grid,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    List
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum FullscreenView
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    List,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Details
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IMainViewAPI
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    DesktopView ActiveDesktopView { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    FullscreenView ActiveFullscreenView { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    SortOrder SortOrder { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    SortOrderDirection SortOrderDirection { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    GroupableField Grouping { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Dispatcher UIDispatcher { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IEnumerable<Game> SelectedGames { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    List<Game> FilteredGames { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<bool> OpenPluginSettingsAsync(Guid pluginId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void SwitchToLibraryView();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void SelectGame(Guid gameId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void SelectGames(IEnumerable<Guid> gameIds);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void ApplyFilterPreset(Guid filterId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void ApplyFilterPreset(FilterPreset preset);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Guid GetActiveFilterPreset();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    FilterPresetSettings GetCurrentFilterSettings();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void OpenSearch(string searchTerm);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void OpenSearch(SearchContext context, string searchTerm);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<bool?> OpenEditDialogAsync(Guid gameId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<bool?> OpenEditDialogAsync(IReadOnlyList<Guid> gameIds);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    List<FilterPreset> GetSortedFilterPresets();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    List<FilterPreset> GetSortedFilterFullscreenPresets();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void ToggleFullscreenView();
}
