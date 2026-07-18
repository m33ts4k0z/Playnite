using CommandLine;
using CommandLine.Text;
using Playnite.API;
using Playnite.Common;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.Avalonia.Theming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Playnite.Toolbox
{
    class Program
    {
        public static int AppResult { get; set; } = 0;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        static int Main(string[] args)
        {
            FileSystem.CreateDirectory(PlaynitePaths.JitProfilesPath);
            ProfileOptimization.SetProfileRoot(PlaynitePaths.JitProfilesPath);
            ProfileOptimization.StartProfile("toolbox");

            logger.Debug("Toolbox started.");
            logger.Debug(Environment.CommandLine);

            var cmdlineParser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = null;
            });

            var result = cmdlineParser.ParseArguments<
                NewCmdLineOptions,
                PackCmdLineOptions,
                UpdateCmdLineOptions,
                VerifyManifestOptions,
                MigrationCheckOptions>(args);
            result.WithParsed<NewCmdLineOptions>(ProcessNewOptions)
                .WithParsed<PackCmdLineOptions>(ProcessPackOptions)
                .WithParsed<UpdateCmdLineOptions>(ProcessUpdateOptions)
                .WithParsed<VerifyManifestOptions>(ProcessVerifyOptions)
                .WithParsed<MigrationCheckOptions>(ProcessMigrationCheckOptions)
                .WithNotParsed(errs => DisplayHelp(result, errs));
            if (result.Tag == ParserResultType.NotParsed)
            {
                AppResult = 2;
            }

            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }

            return AppResult;
        }

        static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.AddEnumValuesToHelpText = true;
                h.AutoHelp = false;
                h.AutoVersion = false;
                return h;
            });
            Console.WriteLine(helpText);
        }

        public static ItemType GetExtensionType(string directory)
        {
            var themeMan = Path.Combine(directory, PlaynitePaths.ThemeManifestFileName);
            var extMan = Path.Combine(directory, PlaynitePaths.ExtensionManifestFileName);
            if (File.Exists(themeMan))
            {
                if (IsAvaloniaTheme(directory))
                {
                    var package = AvaloniaThemePackage.Load(directory);
                    return package.Mode == AvaloniaThemeMode.Desktop
                        ? ItemType.DesktopTheme
                        : ItemType.FullscreenTheme;
                }

                var desc = ExtensionInstaller.GetThemeManifest(themeMan);
                switch (desc.Mode)
                {
                    case ApplicationMode.Desktop:
                        return ItemType.DesktopTheme;
                    case ApplicationMode.Fullscreen:
                        return ItemType.FullscreenTheme;
                }
            }
            else if (File.Exists(extMan))
            {
                var desc = ExtensionInstaller.GetExtensionManifest(extMan);
                switch (desc.Type)
                {
                    case ExtensionType.GenericPlugin:
                        return ItemType.GenericPlugin;
                    case ExtensionType.GameLibrary:
                        return ItemType.LibraryPlugin;
                    case ExtensionType.Script:
                        return ItemType.PowerShellScript;
                    case ExtensionType.MetadataProvider:
                        return ItemType.MetadataPlugin;
                }
            }

            return ItemType.Uknown;
        }

        public static void ProcessNewOptions(NewCmdLineOptions options)
        {
            if (!options.OutDirectory.IsNullOrEmpty())
                options.OutDirectory = options.OutDirectory.Trim('"');

            try
            {
                var outPath = string.Empty;
                switch (options.Type)
                {
                    case ItemType.DesktopTheme:
                        outPath = options.Framework == ThemeFramework.Avalonia
                            ? GenerateAvaloniaTheme(AvaloniaThemeMode.Desktop, options.Name, options.OutDirectory)
                            : Themes.GenerateNewTheme(ApplicationMode.Desktop, options.Name);
                        break;
                    case ItemType.FullscreenTheme:
                        outPath = options.Framework == ThemeFramework.Avalonia
                            ? GenerateAvaloniaTheme(AvaloniaThemeMode.Fullscreen, options.Name, options.OutDirectory)
                            : Themes.GenerateNewTheme(ApplicationMode.Fullscreen, options.Name);
                        break;
                    case ItemType.PowerShellScript:
                        outPath = Extensions.GenerateScriptExtension(options.Name, options.OutDirectory);
                        break;
                    case ItemType.GenericPlugin:
                        outPath = Extensions.GeneratePluginExtension(
                            ExtensionType.GenericPlugin, options.Name, options.OutDirectory, options.Sdk);
                        break;
                    case ItemType.MetadataPlugin:
                        outPath = Extensions.GeneratePluginExtension(
                            ExtensionType.MetadataProvider, options.Name, options.OutDirectory, options.Sdk);
                        break;
                    case ItemType.LibraryPlugin:
                        outPath = Extensions.GeneratePluginExtension(
                            ExtensionType.GameLibrary, options.Name, options.OutDirectory, options.Sdk);
                        break;
                    default:
                        throw new NotSupportedException($"Uknown extension type {options.Type}.");
                }

                logger.Info($"Created new {options.Type} in \"{outPath}\"");
                logger.Warn($"Don't forget to update manifest file with relevant information.");
                if (options.Type == ItemType.GenericPlugin || options.Type == ItemType.LibraryPlugin || options.Type == ItemType.MetadataPlugin)
                {
                    logger.Warn($"Use generated .sln solution file to open plugin source.");
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                AppResult = 1;
                logger.Error(e, $"Failed to create new {options.Type}." + Environment.NewLine + e.Message);
            }
        }

        public static void ProcessPackOptions(PackCmdLineOptions options)
        {
            options.Destination = options.Destination.Trim('"');
            options.Directory = options.Directory.Trim('"');

            try
            {
                var outPath = string.Empty;
                var type = GetExtensionType(options.Directory);
                switch (type)
                {
                    case ItemType.DesktopTheme:
                        outPath = IsAvaloniaTheme(options.Directory)
                            ? AvaloniaThemeTool.Pack(options.Directory, options.Destination, AvaloniaThemeMode.Desktop)
                            : Themes.PackageTheme(options.Directory, options.Destination, ApplicationMode.Desktop);
                        break;
                    case ItemType.FullscreenTheme:
                        outPath = IsAvaloniaTheme(options.Directory)
                            ? AvaloniaThemeTool.Pack(options.Directory, options.Destination, AvaloniaThemeMode.Fullscreen)
                            : Themes.PackageTheme(options.Directory, options.Destination, ApplicationMode.Fullscreen);
                        break;
                    case ItemType.PowerShellScript:
                    case ItemType.GenericPlugin:
                    case ItemType.MetadataPlugin:
                    case ItemType.LibraryPlugin:
                        outPath = Extensions.PackageExtension(options.Directory, options.Destination);
                        break;
                    case ItemType.Uknown:
                        throw new NotSupportedException();
                }

                logger.Info($"{type} successfully packed as \"{outPath}\"");
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                AppResult = 1;
                logger.Error(e, $"Failed to pack extension: {options.Directory}." + Environment.NewLine + e.Message);
            }
        }

        public static void ProcessUpdateOptions(UpdateCmdLineOptions options)
        {
            options.Directory = options.Directory.Trim('"');

            try
            {
                var type = GetExtensionType(options.Directory);
                switch (type)
                {
                    case ItemType.DesktopTheme:
                        if (IsAvaloniaTheme(options.Directory))
                        {
                            AvaloniaThemeTool.Validate(options.Directory, AvaloniaThemeMode.Desktop);
                            logger.Info("Avalonia Desktop theme is valid for the current API.");
                        }
                        else
                        {
                            Themes.UpdateTheme(options.Directory, ApplicationMode.Desktop);
                        }
                        break;
                    case ItemType.FullscreenTheme:
                        if (IsAvaloniaTheme(options.Directory))
                        {
                            AvaloniaThemeTool.Validate(options.Directory, AvaloniaThemeMode.Fullscreen);
                            logger.Info("Avalonia Fullscreen theme is valid for the current API.");
                        }
                        else
                        {
                            Themes.UpdateTheme(options.Directory, ApplicationMode.Fullscreen);
                        }
                        break;
                    case ItemType.Uknown:
                    case ItemType.PowerShellScript:
                    case ItemType.GenericPlugin:
                    case ItemType.MetadataPlugin:
                    case ItemType.LibraryPlugin:
                        throw new NotSupportedException();
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                AppResult = 1;
                logger.Error(e, "Failed to update extension." + Environment.NewLine + e.Message);
            }
        }

        private static string GenerateAvaloniaTheme(
            AvaloniaThemeMode mode,
            string name,
            string outputRoot)
        {
            var directoryName = Common.Paths.GetSafePathName(name).Replace(" ", string.Empty);
            var root = outputRoot.IsNullOrWhiteSpace()
                ? Path.Combine(PlaynitePaths.ThemesProgramPath, mode.ToString(), directoryName)
                : Path.Combine(outputRoot, directoryName);
            return AvaloniaThemeTool.Create(mode, name, root);
        }

        private static bool IsAvaloniaTheme(string directory)
        {
            var manifestPath = Path.Combine(directory, PlaynitePaths.ThemeManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            return Regex.IsMatch(
                File.ReadAllText(manifestPath),
                "^\\s*Framework\\s*:\\s*[\\\"']?Avalonia[\\\"']?\\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public static void ProcessVerifyOptions(VerifyManifestOptions options)
        {
            try
            {
                switch (options.Type)
                {
                    case ManifestType.Installer:
                        AppResult = Verify.VerifyInstallerManifest(options.ManifestPath, out var _) ? 0 : 1;
                        break;
                    case ManifestType.Addon:
                        AppResult = Verify.VerifyAddonManifest(options.ManifestPath) ? 0 : 1;
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (Exception e) when(!Debugger.IsAttached)
            {
                AppResult = 1;
                logger.Error(e, "Failed to verify manifest." + Environment.NewLine + e.Message);
            }
        }

        public static void ProcessMigrationCheckOptions(MigrationCheckOptions options)
        {
            try
            {
                var report = SdkV7MigrationAnalyzer.Analyze(options.Directory.Trim('"'));
                var output = options.Format == MigrationReportFormat.Json
                    ? report.ToJson()
                    : report.ToText();
                Console.WriteLine(output);
                if (!options.Output.IsNullOrWhiteSpace())
                {
                    var outputPath = Path.GetFullPath(options.Output.Trim('"'));
                    var outputDirectory = Path.GetDirectoryName(outputPath);
                    if (!outputDirectory.IsNullOrWhiteSpace())
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }
                    File.WriteAllText(outputPath, output);
                }

                AppResult = report.IsReady ? 0 : 1;
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                AppResult = 1;
                logger.Error(e, "Failed to analyze SDK 7 migration readiness." + Environment.NewLine + e.Message);
            }
        }
    }
}
