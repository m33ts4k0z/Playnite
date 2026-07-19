using Avalonia.Controls;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public interface ISettingsSection
{
    string Key { get; }
    string Title { get; }
    Control Content { get; }
    void Open();
    SettingsSectionValidationResult Validate();
    SettingsSectionSaveResult Save();
    SettingsSectionSelfCheckResult SelfCheck();
}

public readonly record struct SettingsSectionSaveResult(bool RestartRequired)
{
    public static SettingsSectionSaveResult Saved { get; } = new(false);
    public static SettingsSectionSaveResult SavedWithRestart { get; } = new(true);
}

public readonly record struct SettingsSectionValidationResult(bool IsValid, string Message)
{
    public static SettingsSectionValidationResult Valid { get; } = new(true, string.Empty);
}

public sealed record SettingsSectionSelfCheckResult(
    string SectionKey,
    bool Passed,
    string Detail);
