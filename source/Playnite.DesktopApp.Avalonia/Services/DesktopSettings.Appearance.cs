using Avalonia.Media;
using Avalonia.Controls;
using Playnite.Avalonia.App.Services;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public string ThemePath { get; set; }
    public string ViewMode { get; set; } = "Grid";
    public SortOrder SortOrder { get; set; } = SortOrder.Name;
    public SortOrderDirection SortDirection { get; set; } = SortOrderDirection.Ascending;
    public GroupableField Grouping { get; set; } = GroupableField.None;
    public Guid ActiveFilterPreset { get; set; }
    public List<string> CollapsedGameGroups { get; set; } = new();

    public bool ShowGroupCount { get; set; } = true;
    public bool PlaytimeUseDaysFormat { get; set; }

    public double GridItemWidth { get; set; } = 180;
    public int GridItemWidthRatio { get; set; } = 3;
    public int GridItemHeightRatio { get; set; } = 4;
    public Stretch CoverArtStretch { get; set; } = Stretch.UniformToFill;
    public double GridItemSpacing { get; set; } = 12;
    public double GridItemMargin { get; set; } = 6;
    public bool ShowGridItemBackground { get; set; } = true;
    public bool ShowNamesUnderCovers { get; set; } = true;
    public bool ShowNameEmptyCover { get; set; } = true;
    public bool DarkenUninstalledGamesGrid { get; set; }
    public double GridViewScrollSensitivity { get; set; } = 1.5;
    public int GridViewScrollDurationMilliseconds { get; set; } = 250;
    public bool GridViewSmoothScrollEnabled { get; set; }

    public bool ShowIconsOnList { get; set; } = true;
    public double ListViewScrollSensitivity { get; set; } = 1.5;
    public int ListViewScrollDurationMilliseconds { get; set; } = 250;
    public bool ListViewSmoothScrollEnabled { get; set; }

    public DetailsVisibilitySettings DetailsVisibility { get; set; } = new();
    public double DetailsViewScrollSensitivity { get; set; } = 1.5;
    public int DetailsViewScrollDurationMilliseconds { get; set; } = 250;
    public bool DetailsViewSmoothScrollEnabled { get; set; }
    public bool IndentGameDetails { get; set; }
    public double GameDetailsIndentation { get; set; } = 26;
    public double GameDetailsCoverHeight { get; set; } = 310;
    public double DetailsViewListIconSize { get; set; } = 56;

    public Dock GridViewDetailsPosition { get; set; } = Dock.Right;
    public double GridDetailsWidth { get; set; } = 390;
    // Keep the library focused on the games and their details. The optional
    // sidebar remains available from Appearance settings when its controls
    // are useful, but it should not compete with the library by default.
    public bool ShowPanelSeparators { get; set; }
    public bool SidebarVisible { get; set; }
    public Dock SidebarPosition { get; set; } = Dock.Left;

    public bool ShowBackgroundImageOnWindow { get; set; } = true;
    public bool BlurWindowBackgroundImage { get; set; } = true;
    public double BackgroundImageBlurAmount { get; set; } = 60;
    public bool DarkenWindowBackgroundImage { get; set; } = true;
    public double BackgroundImageDarkAmount { get; set; } = 0.7;
    public bool ShowBackImageOnGridView { get; set; }
    public bool BackgroundImageAnimation { get; set; } = true;

    // Fontconfig resolves the generic aliases to the distribution's configured
    // faces, so Linux profiles show a name that actually renders.
    internal static string DefaultFontFamilyName { get; } =
        OperatingSystem.IsWindows() ? "Trebuchet MS" : "Sans";
    internal static string DefaultMonospaceFontFamilyName { get; } =
        OperatingSystem.IsWindows() ? "Consolas" : "Monospace";

    public string FontFamilyName { get; set; } = DefaultFontFamilyName;
    public string MonospaceFontFamilyName { get; set; } = DefaultMonospaceFontFamilyName;
    public double FontSize { get; set; } = 14;
    public double FontSizeSmall { get; set; } = 12;
    public double FontSizeLarge { get; set; } = 15;
    public double FontSizeLarger { get; set; } = 20;
    public double FontSizeLargest { get; set; } = 29;

    public DefaultIconSourceOptions DefaultIconSource { get; set; } = DefaultIconSourceOptions.General;
    public DefaultCoverSourceOptions DefaultCoverSource { get; set; } = DefaultCoverSourceOptions.General;
    public DefaultBackgroundSourceOptions DefaultBackgroundSource { get; set; } = DefaultBackgroundSourceOptions.None;

    public DateFormattingOptions DateTimeFormatAdded { get; set; } = new();
    public DateFormattingOptions DateTimeFormatModified { get; set; } = new();
    public DateFormattingOptions DateTimeFormatRecentActivity { get; set; } = new()
    {
        PastWeekRelativeFormat = true
    };
    public ReleaseDateFormattingOptions DateTimeFormatReleaseDate { get; set; } = new();
    public DateFormattingOptions DateTimeFormatLastPlayed { get; set; } = new()
    {
        PastWeekRelativeFormat = true
    };

    public Dock PluginTopPanelAlignment { get; set; } = Dock.Right;
    public bool TopPanelShowLibrarySummary { get; set; } = true;
    public bool TopPanelShowNotifications { get; set; } = true;
    public bool TopPanelShowFilterStatus { get; set; } = true;
    public bool TopPanelShowUpdateStatus { get; set; } = true;
}
