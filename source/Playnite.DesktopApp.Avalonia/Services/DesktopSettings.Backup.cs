using Playnite.Avalonia.App.Services;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public bool AutoBackupEnabled { get; set; }
    public AutoBackupFrequency AutoBackupFrequency { get; set; } = AutoBackupFrequency.OnceAWeek;
    public string AutoBackupDir { get; set; }
    public int RotatingBackups { get; set; }
    public bool AutoBackupIncludeLibFiles { get; set; } = true;
    public bool AutoBackupIncludeExtensionsData { get; set; } = true;
    public bool AutoBackupIncludeExtensions { get; set; }
    public bool AutoBackupIncludeThemes { get; set; }
    public DateTime LastAutoBackup { get; set; }
}
