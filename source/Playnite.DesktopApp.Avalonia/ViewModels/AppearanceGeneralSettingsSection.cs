using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceGeneralSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private bool showGroupCount;
    private bool playtimeUseDaysFormat;

    public override string Key => "AppearanceGeneral";
    public override string Title => "Appearance — General";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool ShowGroupCount { get => showGroupCount; set => SetField(ref showGroupCount, value); }
    public bool PlaytimeUseDaysFormat { get => playtimeUseDaysFormat; set => SetField(ref playtimeUseDaysFormat, value); }

    public AppearanceGeneralSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceGeneralSettingsView { DataContext = this };
    }

    public override void Open()
    {
        showGroupCount = settings.ShowGroupCount;
        playtimeUseDaysFormat = settings.PlaytimeUseDaysFormat;
        OnPropertyChanged(nameof(ShowGroupCount));
        OnPropertyChanged(nameof(PlaytimeUseDaysFormat));
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.ShowGroupCount = ShowGroupCount;
        settings.PlaytimeUseDaysFormat = PlaytimeUseDaysFormat;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck() => new(
        Key,
        Content.DataContext == this,
        "group-count and playtime formatting controls own an isolated working copy");
}
