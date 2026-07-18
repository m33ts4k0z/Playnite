using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;

namespace Playnite.Avalonia.App.Services;

public sealed class AvaloniaHostCallbacks
{
    public ApplicationMode Mode { get; init; }
    public IAvaloniaHostSettings Settings { get; init; }
    public IAvaloniaDialogService Dialogs { get; init; }
    public Func<IReadOnlyList<Game>> FilteredGames { get; init; } = () => Array.Empty<Game>();
    public Func<Game> SelectedGame { get; init; } = () => null;
    public Action<Guid> SelectGame { get; init; } = _ => { };
    public Action<string> OpenSearch { get; init; } = _ => { };
    public Func<Guid, bool> OpenPluginSettings { get; init; } = _ => false;
    public Func<IReadOnlyList<Guid>, bool?> OpenEditDialog { get; init; } = _ => null;
    public Func<DesktopView> ActiveDesktopView { get; init; } = () => DesktopView.Grid;
    public Func<FullscreenView> ActiveFullscreenView { get; init; } = () => FullscreenView.List;
    public Func<SortOrder> SortOrder { get; init; } = () => Playnite.SDK.Models.SortOrder.Name;
    public Func<SortOrderDirection> SortDirection { get; init; } = () => SortOrderDirection.Ascending;
    public Func<GroupableField> Grouping { get; init; } = () => GroupableField.None;
    public Action<SortOrderDirection> SetSortDirection { get; init; } = _ => { };
    public Action<GroupableField> SetGrouping { get; init; } = _ => { };
    public Action<Guid> ApplyFilterPreset { get; init; } = _ => { };
    public Func<Guid> ActiveFilterPreset { get; init; } = () => Guid.Empty;
    public Func<FilterPresetSettings> CurrentFilterSettings { get; init; } = () => new FilterPresetSettings();
    public Func<List<FilterPreset>> FilterPresets { get; init; } = () => new List<FilterPreset>();
    public Action<string> SetStatus { get; init; } = _ => { };
    public Action<string> SetPluginSummary { get; init; } = _ => { };
    public Action<Guid> RefreshGame { get; init; } = _ => { };
    public Action<Plugin, AddSettingsSupportArgs> AddSettingsSupport { get; init; } = (_, _) => { };
    public Action<Plugin, AddCustomElementSupportArgs> AddCustomElementSupport { get; init; } = (_, _) => { };
    public Action<Plugin, AddConvertersSupportArgs> AddConvertersSupport { get; init; } = (_, _) => { };
    public Func<List<GamepadController>> ConnectedControllers { get; init; } = () => new List<GamepadController>();
}
