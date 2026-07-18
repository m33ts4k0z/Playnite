using Avalonia.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Playnite.Avalonia.App.Services;

public interface IAvaloniaHostSettings
{
    int Version { get; }
    string Language { get; }
    string DesktopTheme { get; }
    string FullscreenTheme { get; }
    List<string> DisabledPlugins { get; }
    bool IsMusicMuted { get; set; }
    bool SwapConfirmCancelButtons { get; }
    bool SwapStartDetailsAction { get; }
    bool GuideButtonFocus { get; }
    string GlobalPreScript { get; }
    string GlobalGameStartedScript { get; }
    string GlobalPostScript { get; }
    bool ShutdownLibraryClients { get; }
    uint ClientShutdownGraceSeconds { get; }
    uint ClientShutdownMinimumSessionSeconds { get; }
    List<Guid> ClientShutdownPluginIds { get; }
    int GridItemWidthRatio => 1;
    int GridItemHeightRatio => 1;
    bool FirstTimeWizardComplete => true;
    bool DisableHwAcceleration => false;
    bool AsyncImageLoading => true;
    bool DownloadMetadataOnImport => true;
    bool StartInFullscreen => false;
    bool MinimizeToTray => false;
    bool CloseToTray => false;
    bool EnableTray => false;
    bool UpdateLibStartup => false;
    bool StartMinimized => false;
    bool StartOnBoot => false;
    PlaytimeImportMode PlaytimeImportMode => PlaytimeImportMode.NewImportsOnly;
    string FontFamilyName => string.Empty;
    bool DiscordPresenceEnabled => false;
    AgeRatingOrg AgeRatingOrgPriority => AgeRatingOrg.PEGI;
    bool SidebarVisible => true;
    Dock SidebarPosition => Dock.Left;
}
