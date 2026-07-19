using Avalonia.Controls;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public interface ISettingsSection
{
    string Key { get; }
    string Title { get; }
    Control Content { get; }
    void Open();
    SettingsSectionSaveResult Save();
    SettingsSectionSelfCheckResult SelfCheck();
}

public readonly record struct SettingsSectionSaveResult(bool RestartRequired)
{
    public static SettingsSectionSaveResult Saved { get; } = new(false);
    public static SettingsSectionSaveResult SavedWithRestart { get; } = new(true);
}

public sealed record SettingsSectionSelfCheckResult(
    string SectionKey,
    bool Passed,
    string Detail);
