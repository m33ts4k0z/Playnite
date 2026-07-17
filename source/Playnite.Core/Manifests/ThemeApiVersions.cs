using Playnite.SDK;

namespace Playnite
{
    // Theme API versions, relocated from ThemeManager (WPF side) so manifest
    // types can live in Playnite.Core. ThemeManager delegates to these.
    public static class ThemeApiVersions
    {
        public static System.Version DesktopApiVersion => new System.Version("2.9.0");
        public static System.Version FullscreenApiVersion => new System.Version("2.9.0");

        public static System.Version GetApiVersion(ApplicationMode mode)
        {
            return mode == ApplicationMode.Desktop ? DesktopApiVersion : FullscreenApiVersion;
        }
    }
}
