namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenSettings
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
    public List<string> DisabledPlugins { get; set; } = new();
}
