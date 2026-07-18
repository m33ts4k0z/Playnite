using Avalonia.Controls;
using Playnite.SDK.Models;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum PlaytimeImportMode
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Always,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    NewImportsOnly,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Never
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IFullscreenSettingsAPI
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool IsMusicMuted { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool SwapConfirmCancelButtons { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool SwapStartDetailsAction { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool GuideButtonFocus { get; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface ICompletionStatusSettingsAPI
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Guid DefaultStatus { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Guid PlayedStatus { get; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IPlayniteSettingsAPI
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    int Version { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    int GridItemWidthRatio { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    int GridItemHeightRatio { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool FirstTimeWizardComplete { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool DisableHwAcceleration { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool AsyncImageLoading { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool DownloadMetadataOnImport { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool StartInFullscreen { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string DatabasePath { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool MinimizeToTray { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool CloseToTray { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool EnableTray { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string Language { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool UpdateLibStartup { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string DesktopTheme { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string FullscreenTheme { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool StartMinimized { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool StartOnBoot { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    PlaytimeImportMode PlaytimeImportMode { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string FontFamilyName { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool DiscordPresenceEnabled { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    AgeRatingOrg AgeRatingOrgPriority { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool SidebarVisible { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Dock SidebarPosition { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IFullscreenSettingsAPI Fullscreen { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    ICompletionStatusSettingsAPI CompletionStatus { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool GetGameExcludedFromImport(string gameId, Guid libraryId);
}
