using Playnite.Avalonia.App.Services;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public UpdateCheckFrequency CheckForProgramUpdates { get; set; } = UpdateCheckFrequency.OnEveryStartup;
    public UpdateCheckFrequency CheckForAddonUpdates { get; set; } = UpdateCheckFrequency.OnEveryStartup;
    public LibraryUpdateCheckFrequency CheckForLibraryUpdates { get; set; } =
        LibraryUpdateCheckFrequency.OnEveryStartup;
    public LibraryUpdateCheckFrequency CheckForEmulatedLibraryUpdates { get; set; } =
        LibraryUpdateCheckFrequency.OnEveryStartup;
    public bool UpdateNotificationOnPatchesOnly { get; set; }
    public DateTime LastProgramUpdateCheck { get; set; }
    public DateTime LastAddonUpdateCheck { get; set; }
    public DateTime LastLibraryUpdateCheck { get; set; }
    public DateTime LastEmulatedLibraryUpdateCheck { get; set; }
}
