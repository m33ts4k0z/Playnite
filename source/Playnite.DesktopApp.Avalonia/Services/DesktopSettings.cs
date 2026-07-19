using Playnite.Avalonia.App.Services;
using Playnite.Metadata;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopSettings : IAvaloniaHostSettings
{
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "english";
    public string ThemePath { get; set; }
    public List<string> DisabledPlugins { get; set; } = new();
    public bool IsMusicMuted { get; set; }
    public bool SwapConfirmCancelButtons => false;
    public bool SwapStartDetailsAction => false;
    public bool GuideButtonFocus => false;
    public string GlobalPreScript { get; set; }
    public string GlobalGameStartedScript { get; set; }
    public string GlobalPostScript { get; set; }
    public bool ShutdownLibraryClients { get; set; }
    public uint ClientShutdownGraceSeconds { get; set; } = 60;
    public uint ClientShutdownMinimumSessionSeconds { get; set; } = 120;
    public List<Guid> ClientShutdownPluginIds { get; set; } = new();
    public string DesktopTheme => ThemePath ?? string.Empty;
    public string FullscreenTheme => string.Empty;
    public string ViewMode { get; set; } = "Grid";
    public SortOrder SortOrder { get; set; } = SortOrder.Name;
    public SortOrderDirection SortDirection { get; set; } = SortOrderDirection.Ascending;
    public GroupableField Grouping { get; set; } = GroupableField.None;
    public Guid ActiveFilterPreset { get; set; }
    public MetadataGamesSource MetadataGamesSource { get; set; } = MetadataGamesSource.Selected;
    public bool MetadataSkipExistingValues { get; set; } = true;
    public bool DownloadBackgroundsImmediately { get; set; } = true;
    public List<Guid> MetadataSourceIds { get; set; } = new();
    public List<MetadataField> MetadataFields { get; set; } = GetDefaultMetadataFields();
    public List<Guid> LibraryPluginIds { get; set; } = new();
    public bool LibraryPluginSelectionConfigured { get; set; }
    public List<Guid> GameScannerIds { get; set; } = new();
    public bool GameScannerSelectionConfigured { get; set; }
    public PlaytimeImportMode LibraryPlaytimeImportMode { get; set; } = PlaytimeImportMode.NewImportsOnly;
    public PlaytimeImportMode PlaytimeImportMode => LibraryPlaytimeImportMode;
    public bool DownloadMetadataOnImport { get; set; } = true;
    public bool EnableTray { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; } = true;
    public TrayIconOption TrayIcon { get; set; } = TrayIconOption.Default;
    public bool StartOnBoot { get; set; }
    public bool StartOnBootClosedToTray { get; set; }
    public bool StartMinimized { get; set; }
    public bool StartInFullscreen { get; set; }
    public AfterLaunchOption AfterLaunch { get; set; } = AfterLaunchOption.Minimize;
    public AfterGameCloseOption AfterGameClose { get; set; } = AfterGameCloseOption.Restore;
    public bool FuzzyMatchingInNameFilter { get; set; } = true;
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public bool WindowMaximized { get; set; }

    public static List<MetadataField> GetDefaultMetadataFields() =>
        Enum.GetValues<MetadataField>().Where(field => field != MetadataField.Name).ToList();
}
