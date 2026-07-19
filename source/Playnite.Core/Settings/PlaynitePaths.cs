using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playnite
{
    public class PlaynitePaths
    {
        public const string ExtensionManifestFileName = "extension.yaml";
        public const string ThemeManifestFileName = "theme.yaml";
        public const string PackedThemeFileExtention = ".pthm";
        public const string PackedExtensionFileExtention = ".pext";
        public const string EngLocSourceFileName = "LocSource.xaml";

        public const string ThemeSlnFileName = "Theme.sln";
        public const string ThemeProjFileName = "Theme.csproj";
        public const string AppXamlFileName = "App.xaml";

        public const string ExtensionsDirName = "Extensions";
        public const string ExtensionsDataDirName = "ExtensionsData";
        public const string ThemesDirName = "Themes";
        public const string ConfigFileName = "config.json";
        public const string FullscreenConfigFileName = "fullscreenConfig.json";
        public const string WindowPositionsFileName = "windowPositions.json";
        public const string LocalizationsDirName = "Localization";
        public const string AvaloniaShellFlagFileName = "avalonia.flag";
        private const string AvaloniaShellEnvVariable = "PLAYNITE_AVALONIA";

        public static string ProgramPath { get; }
        public static string ConfigRootPath { get; private set; }
        public static string LocalizationsPath { get; }
        public static string DataCachePath { get; private set; }

        public static string DesktopExecutablePath { get; private set; }
        public static string FullscreenExecutablePath { get; private set; }
        public static string PlayniteAssemblyPath { get; }
        public static string PlayniteSDKAssemblyPath { get; }
        public static string ExtensionsUserDataPath { get; private set; }
        public static string ExtensionsProgramPath { get; }
        public static string ExtensionsDataPath { get; private set; }
        public static string ExtensionQueueFilePath { get; private set; }
        public static string AddonLicenseAgreementsFilePath { get; private set; }
        public static string LocalizationsStatusPath { get; }
        public static string ThemesProgramPath { get; }
        public static string ThemesUserDataPath { get; private set; }
        public static string UninstallerPath { get; }
        public static string BrowserCachePath { get; private set; }
        public static string TempPath { get; }
        public static string LogPath { get; private set; }
        public static string ConfigFilePath { get; private set; }
        public static string FullscreenConfigFilePath { get; private set; }
        public static string WindowPositionsPath { get; private set; }
        public static string BackupConfigFilePath { get; private set; }
        public static string BackupFullscreenConfigFilePath { get; private set; }
        public static string BackupWindowPositionsPath { get; private set; }
        public static string ImagesCachePath { get; private set; }
        public static string IconsCachePath { get; private set; }
        public static string JitProfilesPath { get; private set; }
        public static string EmulationDatabasePath { get; }
        public static string SafeStartupFlagFile { get; private set; }
        public static string BackupActionFile { get; private set; }
        public static string RestoreBackupActionFile { get; private set; }

        /// <summary>
        /// Marker file next to the executables that opts a Windows install into
        /// the Avalonia shells. Managed through <see cref="SetAvaloniaShellPreferred"/>.
        /// </summary>
        public static string AvaloniaShellFlagFile { get; private set; }

        public static bool IsPortable { get; }

        static PlaynitePaths()
        {
            // BaseDirectory is the directory the app runs from and IS the program
            // path; just strip any trailing separator. (The old GetDirectoryName
            // call relied on BaseDirectory always ending in a separator to act as a
            // slash-trimmer; some .NET hosts omit it, which wrongly returned the
            // parent directory.)
            ProgramPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            UninstallerPath = Path.Combine(ProgramPath, "unins000.exe");
            IsPortable = OperatingSystem.IsWindows() && !File.Exists(UninstallerPath);

            LocalizationsPath = Path.Combine(ProgramPath, LocalizationsDirName);
            AvaloniaShellFlagFile = Path.Combine(ProgramPath, AvaloniaShellFlagFileName);
            RefreshShellExecutables();
            PlayniteAssemblyPath = Path.Combine(ProgramPath, "Playnite.dll");
            PlayniteSDKAssemblyPath = Path.Combine(ProgramPath, "Playnite.SDK.dll");
            ExtensionsProgramPath = Path.Combine(ProgramPath, ExtensionsDirName);
            LocalizationsStatusPath = Path.Combine(LocalizationsPath, "locstatus.json");
            ThemesProgramPath = Path.Combine(ProgramPath, ThemesDirName);
            EmulationDatabasePath = Path.Combine(ProgramPath, "Emulation", "Database");
            TempPath = Path.Combine(Path.GetTempPath(), "Playnite");

            // We need to always initialize some default set for environments like Blend or Rider
            UpdateUserDataDir(IsPortable ? ProgramPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite"));
        }

        /// <summary>
        /// Selects which shell executables the launch, restart, mode-switch, and
        /// system-integration paths point at. Windows defaults to the WPF
        /// applications until cutover; the Avalonia shells are used when opted in
        /// with the avalonia.flag marker next to the executables (or
        /// PLAYNITE_AVALONIA=1), and always when the WPF executables are absent.
        /// </summary>
        public static void RefreshShellExecutables()
        {
            DesktopExecutablePath = SelectShellExecutable("Playnite.DesktopApp");
            FullscreenExecutablePath = SelectShellExecutable("Playnite.FullscreenApp");
        }

        /// <summary>
        /// True when the Avalonia shells have been opted into, either by the
        /// avalonia.flag marker next to the executables or PLAYNITE_AVALONIA=1.
        /// </summary>
        public static bool IsAvaloniaShellPreferred =>
            File.Exists(AvaloniaShellFlagFile) ||
            Environment.GetEnvironmentVariable(AvaloniaShellEnvVariable) == "1";

        /// <summary>
        /// True when both the WPF and Avalonia desktop executables are present, so
        /// the user can meaningfully switch between shells from settings. On
        /// non-Windows only the Avalonia shells exist, so switching does not apply.
        /// </summary>
        public static bool CanSwitchShells =>
            OperatingSystem.IsWindows() &&
            File.Exists(Path.Combine(ProgramPath, "Playnite.DesktopApp.exe")) &&
            File.Exists(Path.Combine(ProgramPath, "Playnite.DesktopApp.Avalonia.exe"));

        /// <summary>
        /// Opts into or out of the Avalonia shells by creating or deleting the
        /// avalonia.flag marker next to the executables, then refreshes the launch
        /// funnel so mode switch, restart, updater, URI and shortcut paths follow
        /// in-process. The PLAYNITE_AVALONIA override is independent and untouched.
        /// Returns false when the flag cannot be written (e.g. a read-only install
        /// directory); callers should surface that to the user.
        /// </summary>
        public static bool SetAvaloniaShellPreferred(bool preferred)
        {
            try
            {
                if (preferred)
                {
                    File.WriteAllText(AvaloniaShellFlagFile, string.Empty);
                }
                else if (File.Exists(AvaloniaShellFlagFile))
                {
                    File.Delete(AvaloniaShellFlagFile);
                }
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }

            RefreshShellExecutables();
            return true;
        }

        private static string SelectShellExecutable(string baseName)
        {
            if (!OperatingSystem.IsWindows())
            {
                return SelectPortableShellExecutable(ProgramPath, baseName);
            }

            var wpfPath = Path.Combine(ProgramPath, baseName + ".exe");
            var avaloniaPath = Path.Combine(ProgramPath, baseName + ".Avalonia.exe");
            if (File.Exists(avaloniaPath) && (IsAvaloniaShellPreferred || !File.Exists(wpfPath)))
            {
                return avaloniaPath;
            }

            return wpfPath;
        }

        internal static string SelectPortableShellExecutable(string programPath, string baseName)
        {
            var executableName = baseName + ".Avalonia";
            var directPath = Path.Combine(programPath, executableName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            var shellDirectory = baseName.EndsWith("FullscreenApp", StringComparison.Ordinal)
                ? "fullscreen"
                : "desktop";
            var siblingPath = Path.GetFullPath(Path.Combine(
                programPath,
                "..",
                shellDirectory,
                executableName));
            return File.Exists(siblingPath) ? siblingPath : directPath;
        }

        public static void UpdateUserDataDir(string dir)
        {
            ConfigRootPath = dir;
            DataCachePath = Path.Combine(ConfigRootPath, "cache");
            ThemesUserDataPath = Path.Combine(ConfigRootPath, ThemesDirName);
            ExtensionsDataPath = Path.Combine(ConfigRootPath, ExtensionsDataDirName);
            ExtensionQueueFilePath = Path.Combine(ConfigRootPath, "extinstalls.json");
            AddonLicenseAgreementsFilePath = Path.Combine(ConfigRootPath, "licenseagreements.json");
            ExtensionsUserDataPath = Path.Combine(ConfigRootPath, ExtensionsDirName);
            BrowserCachePath = Path.Combine(ConfigRootPath, "browsercache");
            LogPath = Path.Combine(ConfigRootPath, "playnite.log");
            ConfigFilePath = Path.Combine(ConfigRootPath, ConfigFileName);
            FullscreenConfigFilePath = Path.Combine(ConfigRootPath, FullscreenConfigFileName);
            WindowPositionsPath = Path.Combine(ConfigRootPath, WindowPositionsFileName);
            BackupConfigFilePath = Path.Combine(ConfigRootPath, "Backup", ConfigFileName);
            BackupFullscreenConfigFilePath = Path.Combine(ConfigRootPath, "Backup", FullscreenConfigFileName);
            BackupWindowPositionsPath = Path.Combine(ConfigRootPath, "Backup", WindowPositionsFileName);
            ImagesCachePath = Path.Combine(DataCachePath, "images");
            IconsCachePath = Path.Combine(DataCachePath, "icons");
            JitProfilesPath = Path.Combine(ConfigRootPath, "JITProfiles");
            SafeStartupFlagFile = Path.Combine(ConfigRootPath, "safestart.flag");
            BackupActionFile = Path.Combine(ConfigRootPath, "backup.json");
            RestoreBackupActionFile = Path.Combine(ConfigRootPath, "restoreBackup.json");
        }

        public static string ExpandVariables(string inputString, string emulatorDir = null, bool fixSeparators = false)
        {
            if (string.IsNullOrEmpty(inputString) || !inputString.Contains('{'))
            {
                return inputString;
            }

            var result = inputString;
            if (!emulatorDir.IsNullOrEmpty())
            {
                emulatorDir = emulatorDir.Replace(ExpandableVariables.PlayniteDirectory, ProgramPath, StringComparison.Ordinal);
            }

            result = result.Replace(ExpandableVariables.PlayniteDirectory, ProgramPath, StringComparison.Ordinal);
            result = result.Replace(ExpandableVariables.EmulatorDirectory, emulatorDir, StringComparison.Ordinal);
            return fixSeparators ? Paths.FixSeparators(result) : result;
        }
    }
}
