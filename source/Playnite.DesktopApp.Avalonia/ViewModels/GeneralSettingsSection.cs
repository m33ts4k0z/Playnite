using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class GeneralSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Action<string, bool> showMessage;
    private LanguageOption selectedLanguage;
    private string originalLanguageId;
    private bool enableTray;
    private bool minimizeToTray;
    private bool closeToTray;
    private TrayIconOption trayIcon;
    private bool startOnBoot;
    private bool startOnBootClosedToTray;
    private bool startMinimized;
    private bool startInFullscreen;
    private bool originalStartOnBoot;
    private bool originalStartOnBootClosedToTray;
    private AfterLaunchOption afterLaunch;
    private AfterGameCloseOption afterGameClose;
    private bool fuzzyMatchingInNameFilter;
    private bool scanLibInstallSizeOnLibUpdate;
    private bool downloadMetadataOnImport;
    private PlaytimeImportMode playtimeImportMode;
    private bool useAvaloniaShell;

    public override string Key => "General";
    public override string Title => "General";
    public override global::Avalonia.Controls.Control Content { get; }

    public IReadOnlyList<PlaytimeImportMode> PlaytimeImportModes { get; } =
        Enum.GetValues<PlaytimeImportMode>();
    public IReadOnlyList<AfterLaunchOption> AfterLaunchOptions { get; } =
        Enum.GetValues<AfterLaunchOption>();
    public IReadOnlyList<AfterGameCloseOption> AfterGameCloseOptions { get; } =
        Enum.GetValues<AfterGameCloseOption>();
    public IReadOnlyList<TrayIconOption> TrayIconOptions { get; } =
        Enum.GetValues<TrayIconOption>();
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
        LanguageCatalog.Discover(Path.Combine(AppContext.BaseDirectory, "Localization"));
    public bool CanSwitchShells => global::Playnite.PlaynitePaths.CanSwitchShells;

    public GeneralSettingsSection(DesktopSettings settings, Action<string, bool> showMessage)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.showMessage = showMessage ?? ((_, _) => { });
        Content = new GeneralSettingsView { DataContext = this };
    }

    public LanguageOption SelectedLanguage { get => selectedLanguage; set => SetField(ref selectedLanguage, value); }
    public bool EnableTray
    {
        get => enableTray;
        set
        {
            if (SetField(ref enableTray, value))
            {
                OnPropertyChanged(nameof(TrayOptionsEnabled));
            }
        }
    }
    public bool TrayOptionsEnabled => EnableTray;
    public bool MinimizeToTray { get => minimizeToTray; set => SetField(ref minimizeToTray, value); }
    public bool CloseToTray { get => closeToTray; set => SetField(ref closeToTray, value); }
    public TrayIconOption TrayIcon { get => trayIcon; set => SetField(ref trayIcon, value); }
    public bool StartOnBoot
    {
        get => startOnBoot;
        set
        {
            if (SetField(ref startOnBoot, value))
            {
                OnPropertyChanged(nameof(StartOnBootOptionsEnabled));
            }
        }
    }
    public bool StartOnBootOptionsEnabled => StartOnBoot;
    public bool StartOnBootClosedToTray { get => startOnBootClosedToTray; set => SetField(ref startOnBootClosedToTray, value); }
    public bool StartMinimized { get => startMinimized; set => SetField(ref startMinimized, value); }
    public bool StartInFullscreen { get => startInFullscreen; set => SetField(ref startInFullscreen, value); }
    public AfterLaunchOption AfterLaunch { get => afterLaunch; set => SetField(ref afterLaunch, value); }
    public AfterGameCloseOption AfterGameClose { get => afterGameClose; set => SetField(ref afterGameClose, value); }
    public bool FuzzyMatchingInNameFilter { get => fuzzyMatchingInNameFilter; set => SetField(ref fuzzyMatchingInNameFilter, value); }
    public bool ScanLibInstallSizeOnLibUpdate { get => scanLibInstallSizeOnLibUpdate; set => SetField(ref scanLibInstallSizeOnLibUpdate, value); }
    public bool DownloadMetadataOnImport { get => downloadMetadataOnImport; set => SetField(ref downloadMetadataOnImport, value); }
    public PlaytimeImportMode PlaytimeImportMode { get => playtimeImportMode; set => SetField(ref playtimeImportMode, value); }
    public bool UseAvaloniaShell { get => useAvaloniaShell; set => SetField(ref useAvaloniaShell, value); }

    public override void Open()
    {
        originalLanguageId = string.IsNullOrWhiteSpace(settings.Language)
            ? LanguageCatalog.SourceLanguageId
            : settings.Language;
        selectedLanguage = AvailableLanguages.FirstOrDefault(option =>
            string.Equals(option.Id, originalLanguageId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLanguages.FirstOrDefault();
        enableTray = settings.EnableTray;
        minimizeToTray = settings.MinimizeToTray;
        closeToTray = settings.CloseToTray;
        trayIcon = settings.TrayIcon;
        startOnBoot = settings.StartOnBoot;
        startOnBootClosedToTray = settings.StartOnBootClosedToTray;
        startMinimized = settings.StartMinimized;
        startInFullscreen = settings.StartInFullscreen;
        originalStartOnBoot = settings.StartOnBoot;
        originalStartOnBootClosedToTray = settings.StartOnBootClosedToTray;
        afterLaunch = settings.AfterLaunch;
        afterGameClose = settings.AfterGameClose;
        fuzzyMatchingInNameFilter = settings.FuzzyMatchingInNameFilter;
        scanLibInstallSizeOnLibUpdate = settings.ScanLibInstallSizeOnLibUpdate;
        downloadMetadataOnImport = settings.DownloadMetadataOnImport;
        playtimeImportMode = settings.LibraryPlaytimeImportMode;
        useAvaloniaShell = global::Playnite.PlaynitePaths.IsAvaloniaShellPreferred;
        RaiseAllFields();
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.EnableTray = EnableTray;
        settings.MinimizeToTray = MinimizeToTray;
        settings.CloseToTray = CloseToTray;
        settings.TrayIcon = TrayIcon;
        settings.StartOnBoot = StartOnBoot;
        settings.StartOnBootClosedToTray = StartOnBootClosedToTray;
        settings.StartMinimized = StartMinimized;
        settings.StartInFullscreen = StartInFullscreen;
        settings.AfterLaunch = AfterLaunch;
        settings.AfterGameClose = AfterGameClose;
        settings.FuzzyMatchingInNameFilter = FuzzyMatchingInNameFilter;
        settings.ScanLibInstallSizeOnLibUpdate = ScanLibInstallSizeOnLibUpdate;
        settings.DownloadMetadataOnImport = DownloadMetadataOnImport;
        settings.LibraryPlaytimeImportMode = PlaytimeImportMode;

        if (StartOnBoot != originalStartOnBoot ||
            StartOnBootClosedToTray != originalStartOnBootClosedToTray)
        {
            try
            {
                global::Playnite.SystemIntegration.SetBootupStateRegistration(
                    StartOnBoot,
                    StartOnBootClosedToTray);
                originalStartOnBoot = StartOnBoot;
                originalStartOnBootClosedToTray = StartOnBootClosedToTray;
            }
            catch (Exception exception)
            {
                showMessage($"The run-on-startup setting could not be applied: {exception.Message}", true);
            }
        }

        var restartRequired = false;
        var newLanguageId = SelectedLanguage?.Id ?? LanguageCatalog.SourceLanguageId;
        if (!string.Equals(newLanguageId, originalLanguageId, StringComparison.OrdinalIgnoreCase))
        {
            settings.Language = newLanguageId;
            originalLanguageId = newLanguageId;
            restartRequired = true;
        }

        if (CanSwitchShells && UseAvaloniaShell != global::Playnite.PlaynitePaths.IsAvaloniaShellPreferred)
        {
            if (global::Playnite.PlaynitePaths.SetAvaloniaShellPreferred(UseAvaloniaShell))
            {
                restartRequired = true;
            }
            else
            {
                showMessage(
                    "The interface preference could not be saved. Run Playnite from a writable location and try again.",
                    true);
            }
        }

        return new SettingsSectionSaveResult(restartRequired);
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var passed = AvailableLanguages.Any(option => option.Id == LanguageCatalog.SourceLanguageId) &&
            PlaytimeImportModes.Count > 0 && AfterLaunchOptions.Count > 0 &&
            AfterGameCloseOptions.Count > 0 && TrayIconOptions.Count == 3;
        return new SettingsSectionSelfCheckResult(
            Key,
            passed,
            passed
                ? $"{AvailableLanguages.Count} languages and all General option catalogs are available"
                : "General settings catalogs are incomplete");
    }

    private void RaiseAllFields()
    {
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(EnableTray));
        OnPropertyChanged(nameof(TrayOptionsEnabled));
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(CloseToTray));
        OnPropertyChanged(nameof(TrayIcon));
        OnPropertyChanged(nameof(StartOnBoot));
        OnPropertyChanged(nameof(StartOnBootOptionsEnabled));
        OnPropertyChanged(nameof(StartOnBootClosedToTray));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(StartInFullscreen));
        OnPropertyChanged(nameof(AfterLaunch));
        OnPropertyChanged(nameof(AfterGameClose));
        OnPropertyChanged(nameof(FuzzyMatchingInNameFilter));
        OnPropertyChanged(nameof(ScanLibInstallSizeOnLibUpdate));
        OnPropertyChanged(nameof(DownloadMetadataOnImport));
        OnPropertyChanged(nameof(PlaytimeImportMode));
        OnPropertyChanged(nameof(UseAvaloniaShell));
        OnPropertyChanged(nameof(CanSwitchShells));
    }
}
