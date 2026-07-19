#if !WINDOWS
using Playnite.Common;
using Playnite.SDK;
using System;
using System.Diagnostics;
using System.IO;

namespace Playnite
{
    public class SystemIntegration
    {
        private const string UriDesktopFileName = "playnite-uri.desktop";
        private const string ExtensionDesktopFileName = "playnite-extension.desktop";
        private const string AutostartDesktopFileName = "playnite.desktop";

        public static void RegisterPlayniteUriProtocol()
        {
            var applicationsDirectory = GetApplicationsDirectory();
            var desktopPath = Path.Combine(applicationsDirectory, UriDesktopFileName);
            WriteApplicationEntry(desktopPath, "--uridata %u", "x-scheme-handler/playnite;");
            RunDesktopCommand("update-desktop-database", applicationsDirectory);
            RegisterMimeHandler(UriDesktopFileName, "x-scheme-handler/playnite");
        }

        public static void SetBootupStateRegistration(bool runOnBootup, bool startClosed)
        {
            var startupDirectory = Path.Combine(GetConfigHome(), "autostart");
            var shortcutPath = Path.Combine(startupDirectory, AutostartDesktopFileName);
            if (!runOnBootup)
            {
                FileSystem.DeleteFile(shortcutPath);
                return;
            }

            var args = new CmdLineOptions
            {
                HideSplashScreen = true,
                StartClosedToTray = startClosed
            }.ToString();
            WriteApplicationEntry(shortcutPath, args, null);
        }

        public static void RegisterFileExtensions()
        {
            InstallExtensionMimeDefinitions();
            var desktopPath = Path.Combine(GetApplicationsDirectory(), ExtensionDesktopFileName);
            WriteApplicationEntry(desktopPath, "--installext %f", "application/x-playnite-extension;application/x-playnite-theme;");
            RunDesktopCommand("update-desktop-database", GetApplicationsDirectory());
            RegisterMimeHandler(ExtensionDesktopFileName, "application/x-playnite-extension");
            RegisterMimeHandler(ExtensionDesktopFileName, "application/x-playnite-theme");
        }

        private static string GetApplicationsDirectory()
        {
            return Path.Combine(GetDataHome(), "applications");
        }

        private static string GetDataHome()
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return string.IsNullOrWhiteSpace(dataHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : dataHome;
        }

        private static string GetConfigHome()
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return string.IsNullOrWhiteSpace(configHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : configHome;
        }

        private static void WriteApplicationEntry(string path, string arguments, string mimeTypes)
        {
            FileSystem.PrepareSaveFile(path);
            var executable = CoreRuntime.ApplicationExecutablePath();
            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new InvalidOperationException("The application executable path has not been configured.");
            }

            var content = "[Desktop Entry]" + Environment.NewLine +
                "Type=Application" + Environment.NewLine +
                "Name=Playnite" + Environment.NewLine +
                "Exec=\"" + EscapeDesktopValue(executable) + "\" " + arguments + Environment.NewLine +
                "Terminal=false" + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(mimeTypes))
            {
                content += "MimeType=" + mimeTypes + Environment.NewLine;
            }

            File.WriteAllText(path, content);
        }

        private static string EscapeDesktopValue(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$");
        }

        private static void InstallExtensionMimeDefinitions()
        {
            var mimeRoot = Path.Combine(GetDataHome(), "mime");
            var packagePath = Path.Combine(mimeRoot, "packages", "playnite.xml");
            FileSystem.PrepareSaveFile(packagePath);
            File.WriteAllText(packagePath,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine +
                "<mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\">" + Environment.NewLine +
                "  <mime-type type=\"application/x-playnite-extension\">" + Environment.NewLine +
                "    <comment>Playnite extension</comment>" + Environment.NewLine +
                "    <glob pattern=\"*.pext\"/>" + Environment.NewLine +
                "  </mime-type>" + Environment.NewLine +
                "  <mime-type type=\"application/x-playnite-theme\">" + Environment.NewLine +
                "    <comment>Playnite theme</comment>" + Environment.NewLine +
                "    <glob pattern=\"*.pthm\"/>" + Environment.NewLine +
                "  </mime-type>" + Environment.NewLine +
                "</mime-info>" + Environment.NewLine);
            RunDesktopCommand("update-mime-database", mimeRoot);
        }

        private static void RegisterMimeHandler(string desktopFileName, string mimeType)
        {
            RunDesktopCommand("xdg-mime", "default", desktopFileName, mimeType);
        }

        private static void RunDesktopCommand(string command, params string[] arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using (var process = Process.Start(startInfo))
                {
                    if (process == null || !process.WaitForExit(10000))
                    {
                        process?.Kill(true);
                        throw new InvalidOperationException($"{command} did not complete within 10 seconds.");
                    }

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"{command} exited with code {process.ExitCode}.");
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                LogManager.GetLogger().Warn(exception, $"{command} is not available; desktop registration files were created but the desktop database was not refreshed.");
            }
            catch (InvalidOperationException exception)
            {
                LogManager.GetLogger().Warn(exception, $"{command} could not refresh the desktop database.");
            }
        }
    }
}
#endif
