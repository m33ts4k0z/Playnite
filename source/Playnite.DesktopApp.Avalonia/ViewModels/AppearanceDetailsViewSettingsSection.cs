using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceDetailsViewSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private DetailsVisibilitySettings visibility = new();
    private double scrollSensitivity;
    private int scrollDurationMilliseconds;
    private bool smoothScrollEnabled;
    private bool indentGameDetails;
    private double gameDetailsIndentation;
    private double gameDetailsCoverHeight;
    private double detailsViewListIconSize;

    public override string Key => "AppearanceDetailsView";
    public override string Title => "Appearance — Details";
    public override global::Avalonia.Controls.Control Content { get; }

    public DetailsVisibilitySettings Visibility { get => visibility; private set => SetField(ref visibility, value); }
    public double ScrollSensitivity { get => scrollSensitivity; set => SetField(ref scrollSensitivity, value); }
    public int ScrollDurationMilliseconds { get => scrollDurationMilliseconds; set => SetField(ref scrollDurationMilliseconds, value); }
    public bool SmoothScrollEnabled { get => smoothScrollEnabled; set => SetField(ref smoothScrollEnabled, value); }
    public bool IndentGameDetails { get => indentGameDetails; set => SetField(ref indentGameDetails, value); }
    public double GameDetailsIndentation { get => gameDetailsIndentation; set => SetField(ref gameDetailsIndentation, value); }
    public double GameDetailsCoverHeight { get => gameDetailsCoverHeight; set => SetField(ref gameDetailsCoverHeight, value); }
    public double DetailsViewListIconSize { get => detailsViewListIconSize; set => SetField(ref detailsViewListIconSize, value); }

    public AppearanceDetailsViewSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceDetailsViewSettingsView { DataContext = this };
    }

    public override void Open()
    {
        Visibility = (settings.DetailsVisibility ?? new DetailsVisibilitySettings()).Clone();
        scrollSensitivity = settings.DetailsViewScrollSensitivity;
        scrollDurationMilliseconds = settings.DetailsViewScrollDurationMilliseconds;
        smoothScrollEnabled = settings.DetailsViewSmoothScrollEnabled;
        indentGameDetails = settings.IndentGameDetails;
        gameDetailsIndentation = settings.GameDetailsIndentation;
        gameDetailsCoverHeight = settings.GameDetailsCoverHeight;
        detailsViewListIconSize = settings.DetailsViewListIconSize;
        OnPropertyChanged(nameof(ScrollSensitivity));
        OnPropertyChanged(nameof(ScrollDurationMilliseconds));
        OnPropertyChanged(nameof(SmoothScrollEnabled));
        OnPropertyChanged(nameof(IndentGameDetails));
        OnPropertyChanged(nameof(GameDetailsIndentation));
        OnPropertyChanged(nameof(GameDetailsCoverHeight));
        OnPropertyChanged(nameof(DetailsViewListIconSize));
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.DetailsVisibility ??= new DetailsVisibilitySettings();
        settings.DetailsVisibility.CopyFrom(Visibility);
        settings.DetailsViewScrollSensitivity = Math.Clamp(ScrollSensitivity, 0.1, 10);
        settings.DetailsViewScrollDurationMilliseconds = Math.Clamp(ScrollDurationMilliseconds, 16, 5000);
        settings.DetailsViewSmoothScrollEnabled = SmoothScrollEnabled;
        settings.IndentGameDetails = IndentGameDetails;
        settings.GameDetailsIndentation = Math.Clamp(GameDetailsIndentation, 0, 200);
        settings.GameDetailsCoverHeight = Math.Clamp(GameDetailsCoverHeight, 100, 800);
        settings.DetailsViewListIconSize = Math.Clamp(DetailsViewListIconSize, 20, 160);
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Visibility != null && ScrollSensitivity is >= 0.1 and <= 10 &&
            ScrollDurationMilliseconds is >= 16 and <= 5000 &&
            GameDetailsIndentation is >= 0 and <= 200 &&
            GameDetailsCoverHeight is >= 100 and <= 800 &&
            DetailsViewListIconSize is >= 20 and <= 160;
        return new SettingsSectionSelfCheckResult(
            Key,
            valid,
            valid ? "31 visibility switches, details geometry, and scrolling ranges are valid" : "a details appearance value is outside its supported range");
    }
}
