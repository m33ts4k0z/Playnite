using Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceLayoutSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private Dock detailsPosition;
    private double detailsWidth;
    private bool showPanelSeparators;

    public override string Key => "AppearanceLayout";
    public override string Title => "Appearance — Layout";
    public override global::Avalonia.Controls.Control Content { get; }
    public IReadOnlyList<Dock> DetailsPositions { get; } = new[] { Dock.Left, Dock.Right };

    public Dock DetailsPosition { get => detailsPosition; set => SetField(ref detailsPosition, value); }
    public double DetailsWidth { get => detailsWidth; set => SetField(ref detailsWidth, value); }
    public bool ShowPanelSeparators { get => showPanelSeparators; set => SetField(ref showPanelSeparators, value); }

    public AppearanceLayoutSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceLayoutSettingsView { DataContext = this };
    }

    public override void Open()
    {
        detailsPosition = settings.GridViewDetailsPosition;
        detailsWidth = settings.GridDetailsWidth;
        showPanelSeparators = settings.ShowPanelSeparators;
        OnPropertyChanged(nameof(DetailsPosition));
        OnPropertyChanged(nameof(DetailsWidth));
        OnPropertyChanged(nameof(ShowPanelSeparators));
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.GridViewDetailsPosition = DetailsPosition is Dock.Left or Dock.Right
            ? DetailsPosition
            : Dock.Right;
        settings.GridDetailsWidth = Math.Clamp(DetailsWidth, 240, 800);
        settings.ShowPanelSeparators = ShowPanelSeparators;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = DetailsPosition is Dock.Left or Dock.Right && DetailsWidth is >= 240 and <= 800;
        return new SettingsSectionSelfCheckResult(
            Key,
            valid,
            valid ? "details position, width, and separator options are valid" : "the details layout is invalid");
    }
}
