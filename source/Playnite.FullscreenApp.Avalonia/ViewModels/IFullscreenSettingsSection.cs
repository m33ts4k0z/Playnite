namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public interface IFullscreenSettingsSection
{
    string Key { get; }
    string Title { get; }
    global::Avalonia.Controls.Control Content { get; }
    void Open();
    FullscreenSettingsSectionSaveResult Save();
    FullscreenSettingsSectionSelfCheckResult SelfCheck();
}

public readonly record struct FullscreenSettingsSectionSaveResult(bool RestartRequired)
{
    public static FullscreenSettingsSectionSaveResult Saved { get; } = new(false);
}

public sealed record FullscreenSettingsSectionSelfCheckResult(
    string SectionKey,
    bool Passed,
    string Detail);
