using Playnite.Metadata;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public MetadataDownloaderSettings MetadataSettings { get; set; } = MetadataDownloaderSettings.GetDefaultSettings();
    public bool UsePerFieldMetadataSettings { get; set; }
    public AgeRatingOrg AgeRatingOrgPriority { get; set; } = AgeRatingOrg.PEGI;
    public string WebImageSearchIconTerm { get; set; } = "{Name} icon";
    public string WebImageSearchCoverTerm { get; set; } = "{Name} cover";
    public string WebImageSearchBackgroundTerm { get; set; } = "{Name} background";
    public global::Playnite.WebImageSearchSource DefaultWebImageSource { get; set; } = global::Playnite.WebImageSearchSource.Google;

    public bool GameSortingNameAutofill { get; set; } = true;
    public List<string> GameSortingNameRemovedArticles { get; set; } = new() { "The", "A", "An" };
}
