using System.Configuration;

namespace Playnite
{
    // App.config appSettings access, extracted from PlayniteSettings so that
    // UI-free core code (PlayniteEnvironment, services) can read configuration
    // without referencing the WPF-coupled settings class.
    public static class AppConfig
    {
        public static string GetAppConfigValue(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static bool GetAppConfigBoolValue(string key)
        {
            if (bool.TryParse(ConfigurationManager.AppSettings[key], out var result))
            {
                return result;
            }
            else
            {
                return false;
            }
        }
    }
}
