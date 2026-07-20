using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceSettingsSection : SettingsSectionBase
{
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private readonly DesktopSettings settings;
    private ThemeOption selectedTheme;
    private string originalThemePath;

    public override string Key => "Appearance";
    public override string Title => "Appearance";
    public override global::Avalonia.Controls.Control Content { get; }
    public IReadOnlyList<ThemeOption> AvailableThemes { get; } =
        ThemeCatalog.DiscoverDesktopThemes(new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Themes", "Desktop"),
            Path.Combine(global::Playnite.PlaynitePaths.ThemesUserDataPath, "Desktop")
        });

    public ThemeOption SelectedTheme
    {
        get => selectedTheme;
        set => SetField(ref selectedTheme, value);
    }

    public AppearanceSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceSettingsView { DataContext = this };
    }

    public override void Open()
    {
        originalThemePath = settings.ThemePath ?? string.Empty;
        selectedTheme = AvailableThemes.FirstOrDefault(option =>
            string.Equals(option.Path, originalThemePath, PathComparison))
            ?? AvailableThemes.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedTheme));
    }

    public override SettingsSectionSaveResult Save()
    {
        var newThemePath = SelectedTheme?.Path ?? string.Empty;
        if (string.Equals(newThemePath, originalThemePath, PathComparison))
        {
            return SettingsSectionSaveResult.Saved;
        }

        settings.ThemePath = newThemePath;
        originalThemePath = newThemePath;
        return SettingsSectionSaveResult.SavedWithRestart;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var hasDefault = AvailableThemes.Any(theme => string.IsNullOrEmpty(theme.Path));
        return new SettingsSectionSelfCheckResult(
            Key,
            hasDefault,
            hasDefault
                ? $"Default theme and {AvailableThemes.Count - 1} installed theme(s) are discoverable"
                : "The Default desktop theme is missing from the catalog");
    }
}
