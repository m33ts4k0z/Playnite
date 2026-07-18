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
}
