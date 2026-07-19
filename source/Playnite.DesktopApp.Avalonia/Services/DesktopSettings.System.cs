namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public bool DisableHwAcceleration { get; set; }
    public bool AsyncImageLoading { get; set; } = true;
    public bool ShowImagePerformanceWarning { get; set; } = true;
    public bool TraceLogEnabled { get; set; }
    public List<DevelopmentExtensionPath> DevelopmentExtensions { get; set; } = new();
    public bool DiscordPresenceEnabled { get; set; }
    public bool ShowElevatedRightsWarning { get; set; } = true;
    public bool InstallSizeScanUseSizeOnDisk { get; set; }
    public string DirectoryOpenCommand { get; set; }
    public string DatabasePath { get; set; }
    public bool ClearWebCacheOnNextStartup { get; set; }
}

public sealed class DevelopmentExtensionPath
{
    public string Path { get; set; }
    public bool IsEnabled { get; set; } = true;

    public DevelopmentExtensionPath Clone() => new() { Path = Path, IsEnabled = IsEnabled };
}
