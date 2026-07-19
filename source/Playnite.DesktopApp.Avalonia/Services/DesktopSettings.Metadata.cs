using Playnite.Metadata;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public MetadataGamesSource MetadataGamesSource { get; set; } = MetadataGamesSource.Selected;
    public bool MetadataSkipExistingValues { get; set; } = true;
    public bool DownloadBackgroundsImmediately { get; set; } = true;
    public List<Guid> MetadataSourceIds { get; set; } = new();
    public List<MetadataField> MetadataFields { get; set; } = GetDefaultMetadataFields();
    public List<Guid> LibraryPluginIds { get; set; } = new();
    public bool LibraryPluginSelectionConfigured { get; set; }
    public List<Guid> GameScannerIds { get; set; } = new();
    public bool GameScannerSelectionConfigured { get; set; }
}
