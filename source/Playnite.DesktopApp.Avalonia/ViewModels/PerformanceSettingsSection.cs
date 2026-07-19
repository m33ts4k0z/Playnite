using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class PerformanceSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private bool originalDisableHwAcceleration;
    private bool originalAsyncImageLoading;
    private bool disableHwAcceleration;
    private bool asyncImageLoading;
    private bool showImagePerformanceWarning;

    public override string Key => "Performance";
    public override string Title => "Performance";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool DisableHwAcceleration
    {
        get => disableHwAcceleration;
        set => SetField(ref disableHwAcceleration, value);
    }

    public bool AsyncImageLoading
    {
        get => asyncImageLoading;
        set => SetField(ref asyncImageLoading, value);
    }

    public bool ShowImagePerformanceWarning
    {
        get => showImagePerformanceWarning;
        set => SetField(ref showImagePerformanceWarning, value);
    }

    public PerformanceSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new PerformanceSettingsView { DataContext = this };
    }

    public override void Open()
    {
        DisableHwAcceleration = settings.DisableHwAcceleration;
        AsyncImageLoading = settings.AsyncImageLoading;
        ShowImagePerformanceWarning = settings.ShowImagePerformanceWarning;
        originalDisableHwAcceleration = DisableHwAcceleration;
        originalAsyncImageLoading = AsyncImageLoading;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.DisableHwAcceleration = DisableHwAcceleration;
        settings.AsyncImageLoading = AsyncImageLoading;
        settings.ShowImagePerformanceWarning = ShowImagePerformanceWarning;
        global::Playnite.Avalonia.Controls.GameCoverImage.AsyncLoadingEnabled = AsyncImageLoading;
        return originalDisableHwAcceleration != DisableHwAcceleration ||
               originalAsyncImageLoading != AsyncImageLoading
            ? SettingsSectionSaveResult.SavedWithRestart
            : SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this;
        return new(Key, valid, valid
            ? "software rendering, asynchronous decoding, and oversized-media warnings are configurable"
            : "performance settings view is not connected");
    }
}
