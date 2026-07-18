namespace Playnite
{
    public interface IAutoBackupSettings
    {
        int RotatingBackups { get; }
        bool AutoBackupIncludeExtensions { get; }
        bool AutoBackupIncludeExtensionsData { get; }
        bool AutoBackupIncludeThemes { get; }
        bool AutoBackupIncludeLibFiles { get; }
        string AutoBackupDir { get; }
    }
}
