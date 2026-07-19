using Microsoft.Win32;
using Playnite.SDK;
using System;

namespace Playnite.Common
{
    public enum WindowsVersion
    {
        Unknown,
        Win7,
        Win8,
        Win10,
        Win11
    }

    // Windows version detection, split out of Computer (WPF side) so core
    // subsystems can gate features on OS version.
    public static class WindowsOs
    {
        public static WindowsVersion WindowsVersion
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return WindowsVersion.Unknown;
                }

                var version = Environment.OSVersion.Version;
                if (version.Major == 6 && version.Minor == 1)
                {
                    return WindowsVersion.Win7;
                }
                else if (version.Major == 6 && (version.Minor == 2 || version.Minor == 3))
                {
                    return WindowsVersion.Win8;
                }
                else if (version.Major == 10)
                {
                    // Apparently some people are spoofing Windows 10 build versions but whatherer they are using
                    // is not updating instaled product name, so we need to check that as well.
                    var windowsProd = GetWindowsProductName();
                    if (windowsProd?.Contains("Windows 7") == true)
                        return WindowsVersion.Win7;

                    if (windowsProd?.Contains("Windows 8") == true)
                        return WindowsVersion.Win8;

                    return version.Build >= 22000 ? WindowsVersion.Win11 : WindowsVersion.Win10;
                }
                else
                {
                    return WindowsVersion.Unknown;
                }
            }
        }

        public static int GetWindowsReleaseId()
        {
            if (!OperatingSystem.IsWindows())
            {
                return 0;
            }

            var relVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "");
            if (relVal?.ToString().IsNullOrEmpty() == true)
            {
                return 0;
            }
            else
            {
                return Convert.ToInt32(relVal);
            }
        }

        public static string GetWindowsProductName()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            return Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "")?.ToString();
        }
    }
}
