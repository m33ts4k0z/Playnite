using CommandLine;
using NUnit.Framework;
using Playnite.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Playnite.Toolbox.Tests
{
    [TestFixture]
    public class ToolboxV3Tests
    {
        private string testRoot;
        private string repositoryRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), $"PlayniteToolboxV3Tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);
            repositoryRoot = FindRepositoryRoot();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void ToolboxReportsMajorVersionThree()
        {
            Assert.That(typeof(Extensions).Assembly.GetName().Version.Major, Is.EqualTo(3));
        }

        [Test]
        public void NewCommandDefaultsToAvaloniaAndSdkSeven()
        {
            NewCmdLineOptions parsed = null;
            new Parser(settings => settings.HelpWriter = null)
                .ParseArguments<
                    NewCmdLineOptions,
                    PackCmdLineOptions,
                    UpdateCmdLineOptions,
                    VerifyManifestOptions,
                    MigrationCheckOptions>(
                    new[] { "new", "GenericPlugin", "Pilot" })
                .WithParsed<NewCmdLineOptions>(options => parsed = options);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Sdk, Is.EqualTo(SdkGeneration.V7));
            Assert.That(parsed.Framework, Is.EqualTo(ThemeFramework.Avalonia));
        }

        [Test]
        public void MigrationCheckCommandParsesMachineReadableOutputOptions()
        {
            MigrationCheckOptions parsed = null;
            new Parser(settings => settings.HelpWriter = null)
                .ParseArguments<
                    NewCmdLineOptions,
                    PackCmdLineOptions,
                    UpdateCmdLineOptions,
                    VerifyManifestOptions,
                    MigrationCheckOptions>(new[]
                {
                    "migration-check",
                    testRoot,
                    "--format", "Json",
                    "--output", Path.Combine(testRoot, "report.json")
                })
                .WithParsed<MigrationCheckOptions>(options => parsed = options);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Directory, Is.EqualTo(testRoot));
            Assert.That(parsed.Format, Is.EqualTo(MigrationReportFormat.Json));
            Assert.That(parsed.Output, Is.EqualTo(Path.Combine(testRoot, "report.json")));
        }

        [Test]
        public void MigrationCheckReportsSdkSixAndWpfBlockersWithoutMutatingSources()
        {
            var pluginRoot = Path.Combine(testRoot, "LegacyPlugin");
            Directory.CreateDirectory(pluginRoot);
            File.WriteAllText(Path.Combine(pluginRoot, "extension.yaml"),
                "Id: legacy_00000000-0000-0000-0000-000000000001\n" +
                "Name: Legacy plugin\n" +
                "Author: Test\n" +
                "Version: 1.0.0\n" +
                "Module: LegacyPlugin.dll\n" +
                "Type: GenericPlugin\n");
            File.WriteAllText(Path.Combine(pluginRoot, "LegacyPlugin.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk.WindowsDesktop\">" +
                "<PropertyGroup><TargetFramework>net462</TargetFramework><UseWPF>true</UseWPF></PropertyGroup>" +
                "<ItemGroup><PackageReference Include=\"PlayniteSDK\" Version=\"6.15.0\" /></ItemGroup>" +
                "</Project>");
            File.WriteAllText(Path.Combine(pluginRoot, "LegacyPlugin.cs"),
                "using System.Windows.Controls;\n" +
                "PlayniteApi.Dialogs.ShowMessage(\"legacy\");\n" +
                "webView.NavigateAndWait(\"https://example.test\");\n");
            File.WriteAllText(Path.Combine(pluginRoot, "SettingsView.xaml"),
                "<UserControl xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" />");
            var before = Directory.EnumerateFiles(pluginRoot)
                .ToDictionary(Path.GetFileName, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

            var report = SdkV7MigrationAnalyzer.Analyze(pluginRoot);

            var codes = report.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            Assert.That(report.IsReady, Is.False);
            Assert.That(codes, Does.Contain("SDK7P003"));
            Assert.That(codes, Does.Contain("SDK7P004"));
            Assert.That(codes, Does.Contain("SDK7P005"));
            Assert.That(codes, Does.Contain("SDK7P006"));
            Assert.That(codes, Does.Contain("SDK7P010"));
            Assert.That(codes, Does.Contain("SDK7P010A"));
            Assert.That(codes, Does.Contain("SDK7P011"));
            Assert.That(codes, Does.Contain("SDK7S001"));
            Assert.That(codes, Does.Contain("SDK7S010"));
            Assert.That(codes, Does.Contain("SDK7S020"));
            Assert.That(codes, Does.Contain("SDK7X001"));
            Assert.That(report.ToText(), Does.Contain("Ready: NO"));
            Assert.That(report.ToJson(), Does.Contain("\"Severity\": \"Error\""));

            var after = Directory.EnumerateFiles(pluginRoot)
                .ToDictionary(Path.GetFileName, File.ReadAllText, StringComparer.OrdinalIgnoreCase);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void MigrationCheckAcceptsSdkSevenTemplateContractAndWritesJsonReport()
        {
            var pluginRoot = Path.Combine(testRoot, "ReadyPlugin");
            Directory.CreateDirectory(pluginRoot);
            File.WriteAllText(Path.Combine(pluginRoot, "extension.yaml"),
                "Id: ready_00000000-0000-0000-0000-000000000001\n" +
                "Name: Ready plugin\n" +
                "Author: Test\n" +
                "Version: 1.0.0\n" +
                "Module: ReadyPlugin.dll\n" +
                "Type: GenericPlugin\n");
            File.WriteAllText(Path.Combine(pluginRoot, "ReadyPlugin.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                "<TargetFramework>net10.0</TargetFramework>" +
                "<Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>" +
                "<NuGetAudit>true</NuGetAudit></PropertyGroup><ItemGroup>" +
                "<PackageReference Include=\"PlayniteSDK\" Version=\"7.0.0\">" +
                "<IncludeAssets>compile;build;buildTransitive;analyzers</IncludeAssets><PrivateAssets>all</PrivateAssets>" +
                "</PackageReference><PackageReference Include=\"Avalonia\" Version=\"12.1.0\">" +
                "<IncludeAssets>compile;build;buildTransitive;analyzers</IncludeAssets><PrivateAssets>all</PrivateAssets>" +
                "</PackageReference></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(pluginRoot, "ReadyPlugin.cs"),
                "using Avalonia.Controls;\nnamespace ReadyPlugin;\npublic sealed class SettingsView : UserControl { }\n");
            File.WriteAllText(Path.Combine(pluginRoot, "SettingsView.axaml"),
                "<UserControl xmlns=\"https://github.com/avaloniaui\" />");
            var testsRoot = Path.Combine(pluginRoot, "Tests");
            Directory.CreateDirectory(testsRoot);
            File.WriteAllText(Path.Combine(testsRoot, "ReadyPlugin.Tests.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk.WindowsDesktop\"><PropertyGroup>" +
                "<TargetFramework>net10.0-windows</TargetFramework><UseWPF>true</UseWPF>" +
                "</PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(testsRoot, "LegacyTestView.cs"),
                "using System.Windows.Controls;\nnamespace ReadyPlugin.Tests;\n");
            var reportPath = Path.Combine(testRoot, "migration-report.json");

            var report = SdkV7MigrationAnalyzer.Analyze(pluginRoot);
            Program.AppResult = -1;
            Program.ProcessMigrationCheckOptions(new MigrationCheckOptions
            {
                Directory = pluginRoot,
                Format = MigrationReportFormat.Json,
                Output = reportPath
            });

            Assert.That(report.IsReady, Is.True, report.ToText());
            Assert.That(report.ErrorCount, Is.Zero);
            Assert.That(report.WarningCount, Is.Zero);
            Assert.That(report.Diagnostics.Select(item => item.Code), Is.EqualTo(new[] { "SDK7I001" }));
            Assert.That(Program.AppResult, Is.Zero);
            Assert.That(File.ReadAllText(reportPath), Does.Contain("\"IsReady\": true"));
        }

        [Test]
        public void ToolboxRoutesAvaloniaThemeCreatePackAndUpdate()
        {
            Program.AppResult = 0;
            Program.ProcessNewOptions(new NewCmdLineOptions
            {
                Type = ItemType.DesktopTheme,
                Name = "Toolbox API3 Theme",
                OutDirectory = testRoot,
                Framework = ThemeFramework.Avalonia,
                Sdk = SdkGeneration.V7
            });

            var themeDirectory = Path.Combine(testRoot, "ToolboxAPI3Theme");
            Assert.That(Program.AppResult, Is.EqualTo(0));
            Assert.That(File.ReadAllText(Path.Combine(themeDirectory, "theme.yaml")),
                Does.Contain("Framework: Avalonia"));

            var packageDirectory = Path.Combine(testRoot, "packages");
            Program.ProcessPackOptions(new PackCmdLineOptions
            {
                Directory = themeDirectory,
                Destination = packageDirectory
            });
            Program.ProcessUpdateOptions(new UpdateCmdLineOptions
            {
                Directory = themeDirectory
            });

            Assert.That(Program.AppResult, Is.EqualTo(0));
            Assert.That(Directory.EnumerateFiles(packageDirectory, "*.pthm").Count(), Is.EqualTo(1));
        }

        [Test]
        public void LegacySdkSixPluginGenerationRemainsAvailable()
        {
            var templateDirectory = Path.Combine(
                repositoryRoot,
                "source",
                "Tools",
                "Playnite.Toolbox",
                "Templates",
                "Extensions",
                "GenericPlugin");
            var archiveRoot = Path.Combine(testRoot, "LegacyArchive");
            Directory.CreateDirectory(archiveRoot);
            foreach (var relativePath in File.ReadAllLines(Path.Combine(templateDirectory, "BuildInclude.txt"))
                .Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var sourcePath = Path.Combine(templateDirectory, relativePath);
                var destinationPath = Path.Combine(archiveRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                File.Copy(sourcePath, destinationPath);
            }

            var archivePath = Path.Combine(testRoot, "GenericPlugin.zip");
            ZipFile.CreateFromDirectory(archiveRoot, archivePath);
            const string name = "PilotGenericPlugin";
            var generatedDirectory = Extensions.GeneratePluginExtension(
                ExtensionType.GenericPlugin,
                name,
                testRoot,
                SdkGeneration.V6,
                archivePath);

            var project = File.ReadAllText(Path.Combine(generatedDirectory, name + ".csproj"));
            var solution = File.ReadAllText(Path.Combine(generatedDirectory, name + ".sln"));
            Assert.That(project, Does.Contain($"<AssemblyName>{name}</AssemblyName>"));
            Assert.That(project, Does.Contain("PlayniteSDK.6.15.0"));
            Assert.That(solution, Does.Contain($"\"{name}\", \"{name}.csproj\""));
            Assert.That(solution, Does.Not.Contain("PilotPilotGenericPlugin"));
        }

        [Test]
        public async Task GenerateAndBuildAllSdkSevenPluginTemplates()
        {
            var packageDirectory = Path.Combine(testRoot, "packages");
            await RunDotnet(
                "pack",
                Path.Combine(repositoryRoot, "source", "PlayniteSDK.V7", "Playnite.SDK.V7.csproj"),
                "-c", "Release",
                "-o", packageDirectory,
                "-p:TreatWarningsAsErrors=true",
                "-p:NuGetAudit=true");

            var templates = new Dictionary<ExtensionType, string>
            {
                [ExtensionType.GenericPlugin] = "GenericPluginV7",
                [ExtensionType.GameLibrary] = "CustomLibraryPluginV7",
                [ExtensionType.MetadataProvider] = "CustomMetadataPluginV7"
            };

            foreach (var template in templates)
            {
                var templateDirectory = Path.Combine(
                    repositoryRoot,
                    "source",
                    "Tools",
                    "Playnite.Toolbox",
                    "Templates",
                    "Extensions",
                    template.Value);
                var archivePath = Path.Combine(testRoot, template.Value + ".zip");
                var archiveRoot = Path.Combine(testRoot, template.Value + "Archive");
                Directory.CreateDirectory(archiveRoot);
                foreach (var relativePath in File.ReadAllLines(Path.Combine(templateDirectory, "BuildInclude.txt"))
                    .Where(path => !string.IsNullOrWhiteSpace(path)))
                {
                    var sourcePath = Path.Combine(templateDirectory, relativePath);
                    var destinationPath = Path.Combine(archiveRoot, relativePath);
                    Assert.That(File.Exists(sourcePath), Is.True, $"Missing {template.Value}/{relativePath}");
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    File.Copy(sourcePath, destinationPath);
                }
                ZipFile.CreateFromDirectory(archiveRoot, archivePath);

                var name = "Pilot" + template.Value;
                var generatedDirectory = Extensions.GeneratePluginExtension(
                    template.Key,
                    name,
                    testRoot,
                    SdkGeneration.V7,
                    archivePath);
                var projectPath = Path.Combine(generatedDirectory, name + ".csproj");

                await RunDotnet(
                    "restore",
                    projectPath,
                    "--source", packageDirectory,
                    "--source", "https://api.nuget.org/v3/index.json",
                    "-p:TreatWarningsAsErrors=true",
                    "-p:NuGetAudit=true");
                await RunDotnet(
                    "build",
                    projectPath,
                    "-c", "Release",
                    "--no-restore",
                    "-p:TreatWarningsAsErrors=true",
                    "-p:NuGetAudit=true");

                var outputDirectory = Path.Combine(generatedDirectory, "bin", "Release", "net10.0");
                Assert.That(File.Exists(Path.Combine(outputDirectory, name + ".dll")), Is.True, template.Value);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "extension.yaml")), Is.True, template.Value);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "Playnite.SDK.dll")), Is.False, template.Value);
                Assert.That(
                    Directory.EnumerateFiles(outputDirectory, "Avalonia*.dll").Any(),
                    Is.False,
                    template.Value);

                var generatedText = string.Join(
                    Environment.NewLine,
                    Directory.EnumerateFiles(generatedDirectory, "*", SearchOption.AllDirectories)
                        .Where(path => !path.Contains(
                            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        .Where(path => !path.Contains(
                            Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        .Where(path => new[] { ".cs", ".csproj", ".sln", ".axaml", ".yaml", ".md" }
                            .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        .Select(File.ReadAllText));
                Assert.That(generatedText, Does.Not.Contain("_name_"));
                Assert.That(generatedText, Does.Not.Contain("_namespace_"));
                Assert.That(generatedText, Does.Not.Contain("00000000-0000-0000-0000-000000000001"));
                Assert.That(generatedText, Does.Contain("PlayniteSDK\" Version=\"7.0.0"));
                Assert.That(generatedText, Does.Not.Contain("System.Windows"));
            }
        }

        private async Task RunDotnet(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;
            Assert.That(process.ExitCode, Is.EqualTo(0),
                $"dotnet {string.Join(" ", arguments)}{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(directory.FullName, "source", "Playnite.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the Playnite repository root.");
        }
    }
}
