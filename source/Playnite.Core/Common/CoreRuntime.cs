using System;
using System.Reflection;
using Playnite.Common;
using Playnite.SDK;

namespace Playnite
{
    /// <summary>
    /// Host-supplied integration points for core services. The defaults keep the
    /// core usable by tests and command line tools without a UI application.
    /// </summary>
    public static class CoreRuntime
    {
        // The engine assembly carries the product version, so hosts that do not
        // override this report the real Playnite version to plugins instead of
        // whatever their executable happens to be versioned.
        public static Func<Version> ApplicationVersion { get; set; } = () =>
            typeof(CoreRuntime).Assembly.GetName().Version ?? new Version(0, 0);

        public static Func<string> ApplicationExecutablePath { get; set; } = () =>
            Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location;

        /// <summary>
        /// Host-supplied system info collector; screen enumeration needs the UI
        /// framework so the full implementation lives in the host.
        /// </summary>
        public static Func<SystemInfo> CollectSystemInfo { get; set; } = () => new SystemInfo();
    }

    public static class GameControllerDialogs
    {
        public static Action<string, string> ShowError { get; set; } = (_, __) => { };
    }

    public static class AddonRuntime
    {
        public static Func<string, AddonInstallerManifest> GetInstallerManifest { get; set; }

        public static Func<AddonManifest, bool> IsInstalled { get; set; } = _ => false;
    }

    public static class ThemePaths
    {
        public const string DefaultThemeDirectoryName = "Default";

        public static string GetRootDirectory(ApplicationMode mode)
        {
            return mode == ApplicationMode.Desktop ? "Desktop" : "Fullscreen";
        }
    }
}
