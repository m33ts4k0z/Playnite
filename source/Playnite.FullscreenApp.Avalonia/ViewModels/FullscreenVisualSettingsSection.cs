using Playnite.Avalonia.App.Services;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenVisualSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private ThemeOption selectedTheme;
    private string originalThemePath;
    private bool darkenUninstalledGamesGrid;
    private bool enableMainBackgroundImage;
    private int mainBackgroundImageBlurAmount;
    private double mainBackgroundImageDarkAmount;
    private bool showGameTitles;
    private double fontSize;
    private double fontSizeSmall;
    private FullscreenButtonPrompts buttonPrompts;

    public override string Key => "Visuals";
    public override string Title => "Visuals";
    public override global::Avalonia.Controls.Control Content { get; }
    public IReadOnlyList<ThemeOption> AvailableThemes { get; } =
        ThemeCatalog.DiscoverFullscreenThemes(App.FullscreenThemeRoots());
    public IReadOnlyList<FullscreenButtonPrompts> ButtonPromptOptions { get; } =
        Enum.GetValues<FullscreenButtonPrompts>();
    public ThemeOption SelectedTheme { get => selectedTheme; set => SetField(ref selectedTheme, value); }
    public bool DarkenUninstalledGamesGrid { get => darkenUninstalledGamesGrid; set => SetField(ref darkenUninstalledGamesGrid, value); }
    public bool EnableMainBackgroundImage { get => enableMainBackgroundImage; set => SetField(ref enableMainBackgroundImage, value); }
    public int MainBackgroundImageBlurAmount { get => mainBackgroundImageBlurAmount; set => SetField(ref mainBackgroundImageBlurAmount, Math.Clamp(value, 0, 100)); }
    public double MainBackgroundImageDarkAmount { get => mainBackgroundImageDarkAmount; set => SetField(ref mainBackgroundImageDarkAmount, Math.Clamp(value, 0, 100)); }
    public bool ShowGameTitles { get => showGameTitles; set => SetField(ref showGameTitles, value); }
    public double FontSize { get => fontSize; set => SetField(ref fontSize, Math.Clamp(value, 8, 72)); }
    public double FontSizeSmall { get => fontSizeSmall; set => SetField(ref fontSizeSmall, Math.Clamp(value, 8, 72)); }
    public FullscreenButtonPrompts ButtonPrompts { get => buttonPrompts; set => SetField(ref buttonPrompts, value); }

    public FullscreenVisualSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenVisualSettingsView { DataContext = this };
    }

    public override void Open()
    {
        originalThemePath = settings.ThemePath ?? string.Empty;
        selectedTheme = AvailableThemes.FirstOrDefault(option =>
            string.Equals(option.Path, originalThemePath, StringComparison.OrdinalIgnoreCase)) ?? AvailableThemes[0];
        darkenUninstalledGamesGrid = settings.DarkenUninstalledGamesGrid;
        enableMainBackgroundImage = settings.EnableMainBackgroundImage;
        mainBackgroundImageBlurAmount = settings.MainBackgroundImageBlurAmount;
        mainBackgroundImageDarkAmount = settings.MainBackgroundImageDarkAmount;
        showGameTitles = settings.ShowGameTitles;
        fontSize = settings.FontSize;
        fontSizeSmall = settings.FontSizeSmall;
        buttonPrompts = settings.ButtonPrompts;
        OnPropertyChanged(string.Empty);
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        var nextTheme = SelectedTheme?.Path ?? string.Empty;
        var restart = !string.Equals(nextTheme, originalThemePath, StringComparison.OrdinalIgnoreCase);
        settings.ThemePath = nextTheme;
        originalThemePath = nextTheme;
        settings.DarkenUninstalledGamesGrid = DarkenUninstalledGamesGrid;
        settings.EnableMainBackgroundImage = EnableMainBackgroundImage;
        settings.MainBackgroundImageBlurAmount = MainBackgroundImageBlurAmount;
        settings.MainBackgroundImageDarkAmount = MainBackgroundImageDarkAmount;
        settings.ShowGameTitles = ShowGameTitles;
        settings.FontSize = FontSize;
        settings.FontSizeSmall = FontSizeSmall;
        settings.ButtonPrompts = ButtonPrompts;
        return new FullscreenSettingsSectionSaveResult(restart);
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = AvailableThemes.Any(theme => string.IsNullOrEmpty(theme.Path)) &&
            FontSize is >= 8 and <= 72 && FontSizeSmall is >= 8 and <= 72;
        return new(Key, valid, $"Default and {AvailableThemes.Count - 1} installed Fullscreen theme(s) are discoverable");
    }
}
