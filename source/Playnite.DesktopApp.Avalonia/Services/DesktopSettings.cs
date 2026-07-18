using Playnite.Avalonia.App.Services;
using Playnite.Metadata;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopSettings : IAvaloniaHostSettings
{
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "english";
    public string ThemePath { get; set; }
    public List<string> DisabledPlugins { get; set; } = new();
    public bool IsMusicMuted { get; set; }
    public bool SwapConfirmCancelButtons => false;
    public bool SwapStartDetailsAction => false;
    public bool GuideButtonFocus => false;
    public string DesktopTheme => ThemePath ?? string.Empty;
    public string FullscreenTheme => string.Empty;
    public string ViewMode { get; set; } = "Grid";
    public SortOrder SortOrder { get; set; } = SortOrder.Name;
    public SortOrderDirection SortDirection { get; set; } = SortOrderDirection.Ascending;
    public GroupableField Grouping { get; set; } = GroupableField.None;
    public Guid ActiveFilterPreset { get; set; }
    public MetadataGamesSource MetadataGamesSource { get; set; } = MetadataGamesSource.Selected;
    public bool MetadataSkipExistingValues { get; set; } = true;
    public bool DownloadBackgroundsImmediately { get; set; } = true;
    public List<Guid> MetadataSourceIds { get; set; } = new();
    public List<MetadataField> MetadataFields { get; set; } = GetDefaultMetadataFields();

    public static List<MetadataField> GetDefaultMetadataFields() =>
        Enum.GetValues<MetadataField>().Where(field => field != MetadataField.Name).ToList();
}
