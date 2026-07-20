using Playnite.Avalonia.App.Services;
using Playnite.SDK.Models;

namespace Playnite.FullscreenApp.Avalonia.Services;

public enum FullscreenButtonPrompts
{
    Xbox,
    PlayStation
}

public sealed class FullscreenSettings : IAvaloniaHostSettings
{
    public int Version { get; set; } = 1;
    public string ActiveFilter { get; set; } = "All";
    public Guid ActiveFilterPreset { get; set; }
    public SortOrder SortingOrder { get; set; } = SortOrder.Name;
    public SortOrderDirection SortingDirection { get; set; } = SortOrderDirection.Ascending;
    public bool ShowHiddenGames { get; set; }
    public bool SwapConfirmCancelButtons { get; set; }
    public bool SwapStartDetailsAction { get; set; }
    public bool GuideButtonFocus { get; set; } = true;
    public bool HideMouseCursor { get; set; }
    public bool EnableGameControllerSupport { get; set; } = true;
    public List<string> DisabledGameControllers { get; set; } = new();
    public bool AudioEnabled { get; set; } = true;
    public int InterfaceVolume { get; set; } = 70;
    public int BackgroundVolume { get; set; } = 20;
    public bool MuteInBackground { get; set; } = true;
    public string ThemePath { get; set; }
    public string Language { get; set; } = "english";
    // Target display index carried over from the WPF fullscreen profile; -1 keeps
    // the platform's primary screen. Applied by MainWindow via MonitorSelection.
    public int Monitor { get; set; } = -1;
    public bool UsePrimaryDisplay { get; set; }
    public bool ShowClock { get; set; } = true;
    public bool ShowBattery { get; set; }
    public bool ShowBatteryPercentage { get; set; }
    public bool MinimizeAfterGameStartup { get; set; } = true;
    public int Rows { get; set; } = 2;
    public int Columns { get; set; } = 4;
    public bool HorizontalLayout { get; set; }
    public int FullscreenItemSpacing { get; set; } = 14;
    public bool SmoothScrolling { get; set; } = true;
    public bool DarkenUninstalledGamesGrid { get; set; }
    public bool EnableMainBackgroundImage { get; set; }
    public int MainBackgroundImageBlurAmount { get; set; }
    public double MainBackgroundImageDarkAmount { get; set; } = 30;
    public bool ShowGameTitles { get; set; }
    public double FontSize { get; set; } = 22;
    public double FontSizeSmall { get; set; } = 18;
    public FullscreenButtonPrompts ButtonPrompts { get; set; } = FullscreenButtonPrompts.Xbox;
    public bool MainMenuShowRestart { get; set; } = true;
    public bool MainMenuShowShutdown { get; set; } = true;
    public bool MainMenuShowSuspend { get; set; } = true;
    public bool MainMenuShowHibernate { get; set; } = true;
    public bool MainMenuShowMinimize { get; set; } = true;
    public bool MainMenuShowLogout { get; set; }
    public bool MainMenuShowLock { get; set; }
    public bool MainMenuShowTools { get; set; } = true;
    public bool MainMenuShowExtensions { get; set; } = true;
    public bool MainMenuShowClients { get; set; } = true;
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
