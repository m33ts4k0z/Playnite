using Playnite.Avalonia.App.Services;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenSettings : IAvaloniaHostSettings
{
    public int Version { get; set; } = 1;
    public string ActiveFilter { get; set; } = "All";
    public bool ShowHiddenGames { get; set; }
    public bool SwapConfirmCancelButtons { get; set; }
    public bool SwapStartDetailsAction { get; set; }
    public bool GuideButtonFocus { get; set; } = true;
    public bool AudioEnabled { get; set; } = true;
    public int InterfaceVolume { get; set; } = 70;
    public int BackgroundVolume { get; set; } = 20;
    public string ThemePath { get; set; }
    public string Language { get; set; } = "english";
    // Target display index carried over from the WPF fullscreen profile; -1 keeps
    // the platform's primary screen. Applied by MainWindow via MonitorSelection.
    public int Monitor { get; set; } = -1;
    public List<string> DisabledPlugins { get; set; } = new();
    public string GlobalPreScript { get; set; }
    public string GlobalGameStartedScript { get; set; }
    public string GlobalPostScript { get; set; }
    public bool ShutdownLibraryClients { get; set; }
    public uint ClientShutdownGraceSeconds { get; set; } = 60;
    public uint ClientShutdownMinimumSessionSeconds { get; set; } = 120;
    public List<Guid> ClientShutdownPluginIds { get; set; } = new();
    public string DesktopTheme => string.Empty;
    public string FullscreenTheme => ThemePath ?? string.Empty;
    public bool IsMusicMuted
    {
        get => !AudioEnabled;
        set => AudioEnabled = !value;
    }
}
