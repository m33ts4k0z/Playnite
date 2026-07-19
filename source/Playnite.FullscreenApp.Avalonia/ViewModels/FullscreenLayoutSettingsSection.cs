using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenLayoutSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private int rows;
    private int columns;
    private bool horizontalLayout;
    private int itemSpacing;
    private bool smoothScrolling;

    public override string Key => "Layout";
    public override string Title => "Layout";
    public override global::Avalonia.Controls.Control Content { get; }
    public int Rows { get => rows; set => SetField(ref rows, Math.Clamp(value, 1, 10)); }
    public int Columns { get => columns; set => SetField(ref columns, Math.Clamp(value, 1, 20)); }
    public bool HorizontalLayout { get => horizontalLayout; set => SetField(ref horizontalLayout, value); }
    public int ItemSpacing { get => itemSpacing; set => SetField(ref itemSpacing, Math.Clamp(value, 0, 100)); }
    public bool SmoothScrolling { get => smoothScrolling; set => SetField(ref smoothScrolling, value); }

    public FullscreenLayoutSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenLayoutSettingsView { DataContext = this };
    }

    public override void Open()
    {
        rows = settings.Rows;
        columns = settings.Columns;
        horizontalLayout = settings.HorizontalLayout;
        itemSpacing = settings.FullscreenItemSpacing;
        smoothScrolling = settings.SmoothScrolling;
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(HorizontalLayout));
        OnPropertyChanged(nameof(ItemSpacing));
        OnPropertyChanged(nameof(SmoothScrolling));
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.Rows = Rows;
        settings.Columns = Columns;
        settings.HorizontalLayout = HorizontalLayout;
        settings.FullscreenItemSpacing = ItemSpacing;
        settings.SmoothScrolling = SmoothScrolling;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Rows is >= 1 and <= 10 && Columns is >= 1 and <= 20,
            "Layout geometry and smooth-scrolling working copy are registered");
}
