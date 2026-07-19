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
    public bool ShowPanelSeparators { get; set; } = true;
}
