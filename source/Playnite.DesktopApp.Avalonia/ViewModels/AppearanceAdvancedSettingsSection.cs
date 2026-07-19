using System.Globalization;
using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceAdvancedSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private bool showBackgroundImageOnWindow;
    private bool blurWindowBackgroundImage;
    private double backgroundImageBlurAmount;
    private bool darkenWindowBackgroundImage;
    private double backgroundImageDarkAmount;
    private bool showBackImageOnGridView;
    private bool backgroundImageAnimation;
    private string fontFamilyName;
    private string monospaceFontFamilyName;
    private double fontSize;
    private double fontSizeSmall;
    private double fontSizeLarge;
    private double fontSizeLarger;
    private double fontSizeLargest;
    private DefaultIconSourceOptions defaultIconSource;
    private DefaultCoverSourceOptions defaultCoverSource;
    private DefaultBackgroundSourceOptions defaultBackgroundSource;
    private DateFormattingOptions dateTimeFormatAdded = new();
    private DateFormattingOptions dateTimeFormatModified = new();
    private DateFormattingOptions dateTimeFormatRecentActivity = new();
    private ReleaseDateFormattingOptions dateTimeFormatReleaseDate = new();
    private DateFormattingOptions dateTimeFormatLastPlayed = new();

    public override string Key => "AppearanceAdvanced";
    public override string Title => "Appearance — Advanced";
    public override global::Avalonia.Controls.Control Content { get; }
    public IReadOnlyList<DefaultIconSourceOptions> IconSources { get; } = Enum.GetValues<DefaultIconSourceOptions>();
    public IReadOnlyList<DefaultCoverSourceOptions> CoverSources { get; } = Enum.GetValues<DefaultCoverSourceOptions>();
    public IReadOnlyList<DefaultBackgroundSourceOptions> BackgroundSources { get; } = Enum.GetValues<DefaultBackgroundSourceOptions>();
    public ICommand ResetFontSizesCommand { get; }
    public ICommand ResetDateFormatsCommand { get; }

    public bool ShowBackgroundImageOnWindow { get => showBackgroundImageOnWindow; set => SetField(ref showBackgroundImageOnWindow, value); }
    public bool BlurWindowBackgroundImage { get => blurWindowBackgroundImage; set => SetField(ref blurWindowBackgroundImage, value); }
    public double BackgroundImageBlurAmount { get => backgroundImageBlurAmount; set => SetField(ref backgroundImageBlurAmount, value); }
    public bool DarkenWindowBackgroundImage { get => darkenWindowBackgroundImage; set => SetField(ref darkenWindowBackgroundImage, value); }
    public double BackgroundImageDarkAmount { get => backgroundImageDarkAmount; set => SetField(ref backgroundImageDarkAmount, value); }
    public bool ShowBackImageOnGridView { get => showBackImageOnGridView; set => SetField(ref showBackImageOnGridView, value); }
    public bool BackgroundImageAnimation { get => backgroundImageAnimation; set => SetField(ref backgroundImageAnimation, value); }
    public string FontFamilyName { get => fontFamilyName; set => SetField(ref fontFamilyName, value); }
    public string MonospaceFontFamilyName { get => monospaceFontFamilyName; set => SetField(ref monospaceFontFamilyName, value); }
    public double FontSize { get => fontSize; set => SetField(ref fontSize, value); }
    public double FontSizeSmall { get => fontSizeSmall; set => SetField(ref fontSizeSmall, value); }
    public double FontSizeLarge { get => fontSizeLarge; set => SetField(ref fontSizeLarge, value); }
    public double FontSizeLarger { get => fontSizeLarger; set => SetField(ref fontSizeLarger, value); }
    public double FontSizeLargest { get => fontSizeLargest; set => SetField(ref fontSizeLargest, value); }
    public DefaultIconSourceOptions DefaultIconSource { get => defaultIconSource; set => SetField(ref defaultIconSource, value); }
    public DefaultCoverSourceOptions DefaultCoverSource { get => defaultCoverSource; set => SetField(ref defaultCoverSource, value); }
    public DefaultBackgroundSourceOptions DefaultBackgroundSource { get => defaultBackgroundSource; set => SetField(ref defaultBackgroundSource, value); }
    public DateFormattingOptions DateTimeFormatAdded { get => dateTimeFormatAdded; private set => SetField(ref dateTimeFormatAdded, value); }
    public DateFormattingOptions DateTimeFormatModified { get => dateTimeFormatModified; private set => SetField(ref dateTimeFormatModified, value); }
    public DateFormattingOptions DateTimeFormatRecentActivity { get => dateTimeFormatRecentActivity; private set => SetField(ref dateTimeFormatRecentActivity, value); }
    public ReleaseDateFormattingOptions DateTimeFormatReleaseDate { get => dateTimeFormatReleaseDate; private set => SetField(ref dateTimeFormatReleaseDate, value); }
    public DateFormattingOptions DateTimeFormatLastPlayed { get => dateTimeFormatLastPlayed; private set => SetField(ref dateTimeFormatLastPlayed, value); }
    public string AddedDateExample => BuildExample(DateTimeFormatAdded);
    public string ModifiedDateExample => BuildExample(DateTimeFormatModified);
    public string RecentActivityDateExample => BuildExample(DateTimeFormatRecentActivity);
    public string ReleaseDateExample => BuildReleaseExample();
    public string LastPlayedDateExample => BuildExample(DateTimeFormatLastPlayed);

    public AppearanceAdvancedSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ResetFontSizesCommand = new AppRelayCommand(ResetFontSizes);
        ResetDateFormatsCommand = new AppRelayCommand(ResetDateFormats);
        Content = new AppearanceAdvancedSettingsView { DataContext = this };
    }

    public override void Open()
    {
        ShowBackgroundImageOnWindow = settings.ShowBackgroundImageOnWindow;
        BlurWindowBackgroundImage = settings.BlurWindowBackgroundImage;
        BackgroundImageBlurAmount = settings.BackgroundImageBlurAmount;
        DarkenWindowBackgroundImage = settings.DarkenWindowBackgroundImage;
        BackgroundImageDarkAmount = settings.BackgroundImageDarkAmount;
        ShowBackImageOnGridView = settings.ShowBackImageOnGridView;
        BackgroundImageAnimation = settings.BackgroundImageAnimation;
        FontFamilyName = settings.FontFamilyName;
        MonospaceFontFamilyName = settings.MonospaceFontFamilyName;
        FontSize = settings.FontSize;
        FontSizeSmall = settings.FontSizeSmall;
        FontSizeLarge = settings.FontSizeLarge;
        FontSizeLarger = settings.FontSizeLarger;
        FontSizeLargest = settings.FontSizeLargest;
        DefaultIconSource = settings.DefaultIconSource;
        DefaultCoverSource = settings.DefaultCoverSource;
        DefaultBackgroundSource = settings.DefaultBackgroundSource;
        UnsubscribeDateOptions();
        DateTimeFormatAdded = settings.DateTimeFormatAdded.Clone();
        DateTimeFormatModified = settings.DateTimeFormatModified.Clone();
        DateTimeFormatRecentActivity = settings.DateTimeFormatRecentActivity.Clone();
        DateTimeFormatReleaseDate = settings.DateTimeFormatReleaseDate.Clone();
        DateTimeFormatLastPlayed = settings.DateTimeFormatLastPlayed.Clone();
        SubscribeDateOptions();
        OnPropertyChanged(nameof(AddedDateExample));
        OnPropertyChanged(nameof(ModifiedDateExample));
        OnPropertyChanged(nameof(RecentActivityDateExample));
        OnPropertyChanged(nameof(ReleaseDateExample));
        OnPropertyChanged(nameof(LastPlayedDateExample));
    }

    public override SettingsSectionValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(FontFamilyName) || string.IsNullOrWhiteSpace(MonospaceFontFamilyName))
        {
            return new(false, "Interface and monospace font family names cannot be empty.");
        }

        var fontSizes = new[] { FontSizeSmall, FontSize, FontSizeLarge, FontSizeLarger, FontSizeLargest };
        if (fontSizes.Any(size => size is < 9 or > 100))
        {
            return new(false, "Font sizes must be between 9 and 100.");
        }

        var formats = new[]
        {
            DateTimeFormatAdded.Format,
            DateTimeFormatModified.Format,
            DateTimeFormatRecentActivity.Format,
            DateTimeFormatReleaseDate.Format,
            DateTimeFormatReleaseDate.PartialFormat,
            DateTimeFormatLastPlayed.Format
        };
        return formats.All(DateFormattingService.IsValidFormat)
            ? SettingsSectionValidationResult.Valid
            : new(false, "One or more date formats are invalid. Use standard or custom .NET date format strings.");
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.ShowBackgroundImageOnWindow = ShowBackgroundImageOnWindow;
        settings.BlurWindowBackgroundImage = BlurWindowBackgroundImage;
        settings.BackgroundImageBlurAmount = Math.Clamp(BackgroundImageBlurAmount, 0, 100);
        settings.DarkenWindowBackgroundImage = DarkenWindowBackgroundImage;
        settings.BackgroundImageDarkAmount = Math.Clamp(BackgroundImageDarkAmount, 0, 1);
        settings.ShowBackImageOnGridView = ShowBackImageOnGridView;
        settings.BackgroundImageAnimation = BackgroundImageAnimation;
        settings.FontFamilyName = FontFamilyName.Trim();
        settings.MonospaceFontFamilyName = MonospaceFontFamilyName.Trim();
        settings.FontSize = FontSize;
        settings.FontSizeSmall = FontSizeSmall;
        settings.FontSizeLarge = FontSizeLarge;
        settings.FontSizeLarger = FontSizeLarger;
        settings.FontSizeLargest = FontSizeLargest;
        settings.DefaultIconSource = DefaultIconSource;
        settings.DefaultCoverSource = DefaultCoverSource;
        settings.DefaultBackgroundSource = DefaultBackgroundSource;
        settings.DateTimeFormatAdded = DateTimeFormatAdded.Clone();
        settings.DateTimeFormatModified = DateTimeFormatModified.Clone();
        settings.DateTimeFormatRecentActivity = DateTimeFormatRecentActivity.Clone();
        settings.DateTimeFormatReleaseDate = DateTimeFormatReleaseDate.Clone();
        settings.DateTimeFormatLastPlayed = DateTimeFormatLastPlayed.Clone();
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Validate().IsValid && Content.DataContext == this;
        return new(Key, valid, valid
            ? "background, typography, fallback-media, and date-format controls are valid and isolated"
            : "advanced appearance working copy is invalid");
    }

    private void ResetFontSizes()
    {
        FontSizeSmall = 12;
        FontSize = 14;
        FontSizeLarge = 15;
        FontSizeLarger = 20;
        FontSizeLargest = 29;
    }

    private void ResetDateFormats()
    {
        DateTimeFormatAdded.Format = DateFormattingService.DefaultFormat;
        DateTimeFormatModified.Format = DateFormattingService.DefaultFormat;
        DateTimeFormatRecentActivity.Format = DateFormattingService.DefaultFormat;
        DateTimeFormatReleaseDate.Format = DateFormattingService.DefaultFormat;
        DateTimeFormatReleaseDate.PartialFormat = DateFormattingService.DefaultPartialFormat;
        DateTimeFormatLastPlayed.Format = DateFormattingService.DefaultFormat;
        OnPropertyChanged(nameof(AddedDateExample));
        OnPropertyChanged(nameof(ModifiedDateExample));
        OnPropertyChanged(nameof(RecentActivityDateExample));
        OnPropertyChanged(nameof(ReleaseDateExample));
        OnPropertyChanged(nameof(LastPlayedDateExample));
    }

    private static string BuildExample(DateFormattingOptions options)
    {
        if (!DateFormattingService.IsValidFormat(options?.Format))
        {
            return "Invalid format";
        }

        return DateFormattingService.Format(new DateTime(2026, 7, 19), options, new DateTime(2026, 7, 19), CultureInfo.CurrentCulture);
    }

    private string BuildReleaseExample()
    {
        if (!DateFormattingService.IsValidFormat(DateTimeFormatReleaseDate?.Format) ||
            !DateFormattingService.IsValidFormat(DateTimeFormatReleaseDate?.PartialFormat))
        {
            return "Invalid format";
        }

        return DateFormattingService.FormatReleaseDate(
            new DateTime(2026, 7, 1), true, false, DateTimeFormatReleaseDate, culture: CultureInfo.CurrentCulture);
    }

    private void SubscribeDateOptions()
    {
        DateTimeFormatAdded.PropertyChanged += DateOption_PropertyChanged;
        DateTimeFormatModified.PropertyChanged += DateOption_PropertyChanged;
        DateTimeFormatRecentActivity.PropertyChanged += DateOption_PropertyChanged;
        DateTimeFormatReleaseDate.PropertyChanged += DateOption_PropertyChanged;
        DateTimeFormatLastPlayed.PropertyChanged += DateOption_PropertyChanged;
    }

    private void UnsubscribeDateOptions()
    {
        DateTimeFormatAdded.PropertyChanged -= DateOption_PropertyChanged;
        DateTimeFormatModified.PropertyChanged -= DateOption_PropertyChanged;
        DateTimeFormatRecentActivity.PropertyChanged -= DateOption_PropertyChanged;
        DateTimeFormatReleaseDate.PropertyChanged -= DateOption_PropertyChanged;
        DateTimeFormatLastPlayed.PropertyChanged -= DateOption_PropertyChanged;
    }

    private void DateOption_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AddedDateExample));
        OnPropertyChanged(nameof(ModifiedDateExample));
        OnPropertyChanged(nameof(RecentActivityDateExample));
        OnPropertyChanged(nameof(ReleaseDateExample));
        OnPropertyChanged(nameof(LastPlayedDateExample));
    }
}
