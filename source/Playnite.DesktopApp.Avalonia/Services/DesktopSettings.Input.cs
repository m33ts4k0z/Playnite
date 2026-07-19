namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public bool EnableGameControllerSupport { get; set; } = true;
    public List<string> DisabledGameControllers { get; set; } = new();
}
