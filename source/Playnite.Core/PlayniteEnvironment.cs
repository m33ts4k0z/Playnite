using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Playnite
{
    public enum ReleaseChannel
    {
        Stable,
        Beta,
        Patreon
    }

    public static class PlayniteEnvironment
    {
        [DllImport("libc")]
        private static extern uint geteuid();

        public static ReleaseChannel ReleaseChannel
        {
            get
            {
                switch (AppConfig.GetAppConfigValue("UpdateBranch"))
                {
                    case "stable":
                        return ReleaseChannel.Stable;
                    case "patreon":
                        return ReleaseChannel.Patreon;
                    case "beta":
                        return ReleaseChannel.Beta;
                    default:
                        return ReleaseChannel.Stable;
                }
            }
        }

        public static string DocsRootUrl => AppConfig.GetAppConfigValue("DocsRootUrl");

        public static string AppBranch => AppConfig.GetAppConfigValue("AppBranch");

        public static bool ThrowAllErrors => AppConfig.GetAppConfigBoolValue("ThrowAllErrors") && Debugger.IsAttached;

        public static bool InOfflineMode => AppConfig.GetAppConfigBoolValue("OfflineMode");

        public static bool IsDebuggerAttached => Debugger.IsAttached;

        public static bool IsDebugBuild
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsElevated
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return geteuid() == 0;
                }

                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }
    }
}
