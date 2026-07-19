namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public List<string> DisabledPlugins { get; set; } = new();
    public string GlobalPreScript { get; set; }
    public string GlobalGameStartedScript { get; set; }
    public string GlobalPostScript { get; set; }
    public bool ShutdownLibraryClients { get; set; }
    public uint ClientShutdownGraceSeconds { get; set; } = 60;
    public uint ClientShutdownMinimumSessionSeconds { get; set; } = 120;
    public List<Guid> ClientShutdownPluginIds { get; set; } = new();
}
