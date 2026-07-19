using Playnite.Avalonia.App.Services;
using Playnite.Metadata;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings : IAvaloniaHostSettings
{
    public int Version { get; set; } = 1;
    public bool IsMusicMuted { get; set; }
    public bool SwapConfirmCancelButtons => false;
    public bool SwapStartDetailsAction => false;
    public bool GuideButtonFocus => false;
    public string DesktopTheme => ThemePath ?? string.Empty;
    public string FullscreenTheme => string.Empty;
    public PlaytimeImportMode PlaytimeImportMode => LibraryPlaytimeImportMode;

    public static List<MetadataField> GetDefaultMetadataFields() =>
        Enum.GetValues<MetadataField>().Where(field => field != MetadataField.Name).ToList();
}
