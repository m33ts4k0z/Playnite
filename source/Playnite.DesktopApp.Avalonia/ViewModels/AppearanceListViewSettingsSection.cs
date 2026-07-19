using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceListViewSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private bool showIconsOnList;
    private double scrollSensitivity;
    private int scrollDurationMilliseconds;
    private bool smoothScrollEnabled;

    public override string Key => "AppearanceListView";
    public override string Title => "Appearance — List";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool ShowIconsOnList { get => showIconsOnList; set => SetField(ref showIconsOnList, value); }
    public double ScrollSensitivity { get => scrollSensitivity; set => SetField(ref scrollSensitivity, value); }
    public int ScrollDurationMilliseconds { get => scrollDurationMilliseconds; set => SetField(ref scrollDurationMilliseconds, value); }
    public bool SmoothScrollEnabled { get => smoothScrollEnabled; set => SetField(ref smoothScrollEnabled, value); }

    public AppearanceListViewSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceListViewSettingsView { DataContext = this };
    }

    public override void Open()
    {
        showIconsOnList = settings.ShowIconsOnList;
        scrollSensitivity = settings.ListViewScrollSensitivity;
        scrollDurationMilliseconds = settings.ListViewScrollDurationMilliseconds;
        smoothScrollEnabled = settings.ListViewSmoothScrollEnabled;
        OnPropertyChanged(nameof(ShowIconsOnList));
        OnPropertyChanged(nameof(ScrollSensitivity));
        OnPropertyChanged(nameof(ScrollDurationMilliseconds));
        OnPropertyChanged(nameof(SmoothScrollEnabled));
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.ShowIconsOnList = ShowIconsOnList;
        settings.ListViewScrollSensitivity = Math.Clamp(ScrollSensitivity, 0.1, 10);
        settings.ListViewScrollDurationMilliseconds = Math.Clamp(ScrollDurationMilliseconds, 16, 5000);
        settings.ListViewSmoothScrollEnabled = SmoothScrollEnabled;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = ScrollSensitivity is >= 0.1 and <= 10 &&
            ScrollDurationMilliseconds is >= 16 and <= 5000;
        return new SettingsSectionSelfCheckResult(
            Key,
            valid,
            valid ? "list icon and scrolling options are valid" : "a list scrolling value is outside its supported range");
    }
}
