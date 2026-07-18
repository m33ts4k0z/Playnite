using Playnite.API;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playnite.Toolbox
{
    public class Paths
    {
        public static string GetThemesPath(ApplicationMode mode)
        {
            return Path.Combine(PlaynitePaths.ThemesProgramPath, ThemeManager.GetThemeRootDir(mode));
        }

        public static string GetThemeTemplateDir(ApplicationMode mode)
        {
            return Path.Combine(PlaynitePaths.ProgramPath, "Templates", "Themes", ThemeManager.GetThemeRootDir(mode));
        }

        public static string GetThemeTemplateFilePath(ApplicationMode mode, string fileName)
        {
            return Path.Combine(PlaynitePaths.ProgramPath, "Templates", "Themes", ThemeManager.GetThemeRootDir(mode), fileName);
        }

        public static string GetThemeTemplatePath(string fileName)
        {
            return Path.Combine(PlaynitePaths.ProgramPath, "Templates", "Themes", fileName);
        }

        public static string GetPluginTemplateArchivePath(
            ExtensionType type,
            SdkGeneration sdkGeneration = SdkGeneration.V6)
        {
            var root = Path.Combine(PlaynitePaths.ProgramPath, "Templates", "Extensions");
            var suffix = sdkGeneration == SdkGeneration.V7 ? "V7" : string.Empty;
            switch (type)
            {
                case ExtensionType.GenericPlugin:
                    return Path.Combine(root, $"GenericPlugin{suffix}.zip");
                case ExtensionType.GameLibrary:
                    return Path.Combine(root, $"CustomLibraryPlugin{suffix}.zip");
                case ExtensionType.MetadataProvider:
                    return Path.Combine(root, $"CustomMetadataPlugin{suffix}.zip");
                case ExtensionType.Script:
                default:
                    throw new NotSupportedException();
            }
        }

        public static string GetPluginTemplateProjectName(
            ExtensionType type,
            SdkGeneration sdkGeneration = SdkGeneration.V6)
        {
            var suffix = sdkGeneration == SdkGeneration.V7 ? "V7" : string.Empty;
            switch (type)
            {
                case ExtensionType.GenericPlugin:
                    return $"GenericPlugin{suffix}";
                case ExtensionType.GameLibrary:
                    return $"CustomLibraryPlugin{suffix}";
                case ExtensionType.MetadataProvider:
                    return $"CustomMetadataPlugin{suffix}";
                case ExtensionType.Script:
                default:
                    throw new NotSupportedException();
            }
        }

        public static string ChangeLogsDir => Path.Combine(PlaynitePaths.ProgramPath, "Templates", "Themes", "Changelog");
        public static string ExtFileIgnoreListPath => Path.Combine(PlaynitePaths.ProgramPath, "Templates", "Extensions", "ExtensionsRefIgnoreList.txt");

        public static string GetNextBackupFolder(string rootFolder)
        {
            var latestBack = -1;
            var dirs = Directory.GetDirectories(rootFolder).Where(a => Path.GetFileName(a).StartsWith("backup_")).ToList();
            if (dirs.Any())
            {
                latestBack = dirs.Select(a => int.Parse(Path.GetFileName(a).Replace("backup_", ""))).Max();
            }

            latestBack += 1;
            return Path.Combine(rootFolder, $"backup_{latestBack}");
        }
    }
}
