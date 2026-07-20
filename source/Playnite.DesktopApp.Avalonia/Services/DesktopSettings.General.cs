using Playnite.Avalonia.App.Services;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public string Language { get; set; } = "english";
    public bool FirstTimeWizardComplete { get; set; }
    public PlaytimeImportMode LibraryPlaytimeImportMode { get; set; } = PlaytimeImportMode.NewImportsOnly;
    public bool DownloadMetadataOnImport { get; set; } = true;
    public bool EnableTray { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; } = true;
    public TrayIconOption TrayIcon { get; set; } = TrayIconOption.Default;
    public int QuickLaunchItems { get; set; } = 5;
    public bool ShowHiddenInQuickLaunch { get; set; }
    public bool StartOnBoot { get; set; }
    public bool StartOnBootClosedToTray { get; set; }
    public bool StartMinimized { get; set; }
    public bool StartInFullscreen { get; set; }
    public AfterLaunchOption AfterLaunch { get; set; } = AfterLaunchOption.Minimize;
    public AfterGameCloseOption AfterGameClose { get; set; } = AfterGameCloseOption.Restore;
    public bool FuzzyMatchingInNameFilter { get; set; } = true;
    public bool ScanLibInstallSizeOnLibUpdate { get; set; }
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public bool WindowMaximized { get; set; }
}
