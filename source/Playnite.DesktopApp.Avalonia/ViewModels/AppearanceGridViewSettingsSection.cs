using Avalonia.Media;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceGridViewSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private double gridItemWidth;
    private int gridItemWidthRatio;
    private int gridItemHeightRatio;
    private Stretch coverArtStretch;
    private double gridItemSpacing;
    private double gridItemMargin;
    private bool showGridItemBackground;
    private bool showNamesUnderCovers;
    private bool showNameEmptyCover;
    private bool darkenUninstalledGamesGrid;
    private double scrollSensitivity;
    private int scrollDurationMilliseconds;
    private bool smoothScrollEnabled;

    public override string Key => "AppearanceGridView";
    public override string Title => "Appearance — Grid";
    public override global::Avalonia.Controls.Control Content { get; }
    public IReadOnlyList<Stretch> CoverStretchOptions { get; } = Enum.GetValues<Stretch>();

    public double GridItemWidth { get => gridItemWidth; set => SetField(ref gridItemWidth, value); }
    public int GridItemWidthRatio { get => gridItemWidthRatio; set => SetField(ref gridItemWidthRatio, value); }
    public int GridItemHeightRatio { get => gridItemHeightRatio; set => SetField(ref gridItemHeightRatio, value); }
    public Stretch CoverArtStretch { get => coverArtStretch; set => SetField(ref coverArtStretch, value); }
    public double GridItemSpacing { get => gridItemSpacing; set => SetField(ref gridItemSpacing, value); }
    public double GridItemMargin { get => gridItemMargin; set => SetField(ref gridItemMargin, value); }
    public bool ShowGridItemBackground { get => showGridItemBackground; set => SetField(ref showGridItemBackground, value); }
    public bool ShowNamesUnderCovers { get => showNamesUnderCovers; set => SetField(ref showNamesUnderCovers, value); }
    public bool ShowNameEmptyCover { get => showNameEmptyCover; set => SetField(ref showNameEmptyCover, value); }
    public bool DarkenUninstalledGamesGrid { get => darkenUninstalledGamesGrid; set => SetField(ref darkenUninstalledGamesGrid, value); }
    public double ScrollSensitivity { get => scrollSensitivity; set => SetField(ref scrollSensitivity, value); }
    public int ScrollDurationMilliseconds { get => scrollDurationMilliseconds; set => SetField(ref scrollDurationMilliseconds, value); }
    public bool SmoothScrollEnabled { get => smoothScrollEnabled; set => SetField(ref smoothScrollEnabled, value); }

    public AppearanceGridViewSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceGridViewSettingsView { DataContext = this };
    }

    public override void Open()
    {
        gridItemWidth = settings.GridItemWidth;
        gridItemWidthRatio = settings.GridItemWidthRatio;
        gridItemHeightRatio = settings.GridItemHeightRatio;
        coverArtStretch = settings.CoverArtStretch;
        gridItemSpacing = settings.GridItemSpacing;
        gridItemMargin = settings.GridItemMargin;
        showGridItemBackground = settings.ShowGridItemBackground;
        showNamesUnderCovers = settings.ShowNamesUnderCovers;
        showNameEmptyCover = settings.ShowNameEmptyCover;
        darkenUninstalledGamesGrid = settings.DarkenUninstalledGamesGrid;
        scrollSensitivity = settings.GridViewScrollSensitivity;
        scrollDurationMilliseconds = settings.GridViewScrollDurationMilliseconds;
        smoothScrollEnabled = settings.GridViewSmoothScrollEnabled;
        RaiseAll();
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.GridItemWidth = Math.Clamp(GridItemWidth, 80, 600);
        settings.GridItemWidthRatio = Math.Clamp(GridItemWidthRatio, 1, 20);
        settings.GridItemHeightRatio = Math.Clamp(GridItemHeightRatio, 1, 20);
        settings.CoverArtStretch = CoverArtStretch;
        settings.GridItemSpacing = Math.Clamp(GridItemSpacing, 0, 80);
        settings.GridItemMargin = Math.Clamp(GridItemMargin, 0, 40);
        settings.ShowGridItemBackground = ShowGridItemBackground;
        settings.ShowNamesUnderCovers = ShowNamesUnderCovers;
        settings.ShowNameEmptyCover = ShowNameEmptyCover;
        settings.DarkenUninstalledGamesGrid = DarkenUninstalledGamesGrid;
        settings.GridViewScrollSensitivity = Math.Clamp(ScrollSensitivity, 0.1, 10);
        settings.GridViewScrollDurationMilliseconds = Math.Clamp(ScrollDurationMilliseconds, 16, 5000);
        settings.GridViewSmoothScrollEnabled = SmoothScrollEnabled;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = GridItemWidth is >= 80 and <= 600 &&
            GridItemWidthRatio is >= 1 and <= 20 && GridItemHeightRatio is >= 1 and <= 20 &&
            ScrollSensitivity is >= 0.1 and <= 10;
        return new SettingsSectionSelfCheckResult(
            Key,
            valid,
            valid ? "grid geometry, presentation, and scrolling ranges are valid" : "a grid appearance value is outside its supported range");
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(GridItemWidth));
        OnPropertyChanged(nameof(GridItemWidthRatio));
        OnPropertyChanged(nameof(GridItemHeightRatio));
        OnPropertyChanged(nameof(CoverArtStretch));
        OnPropertyChanged(nameof(GridItemSpacing));
        OnPropertyChanged(nameof(GridItemMargin));
        OnPropertyChanged(nameof(ShowGridItemBackground));
        OnPropertyChanged(nameof(ShowNamesUnderCovers));
        OnPropertyChanged(nameof(ShowNameEmptyCover));
        OnPropertyChanged(nameof(DarkenUninstalledGamesGrid));
        OnPropertyChanged(nameof(ScrollSensitivity));
        OnPropertyChanged(nameof(ScrollDurationMilliseconds));
        OnPropertyChanged(nameof(SmoothScrollEnabled));
    }
}
