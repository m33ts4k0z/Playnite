using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Playnite.Toolbox
{
    public enum MigrationDiagnosticSeverity
    {
        Error,
        Warning,
        Info
    }

    public sealed class MigrationDiagnostic
    {
        public string Code { get; set; }
        public MigrationDiagnosticSeverity Severity { get; set; }
        public string Message { get; set; }
        public string File { get; set; }
        public int? Line { get; set; }
    }

    public sealed class SdkV7MigrationReport
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public string Directory { get; set; }
        public List<MigrationDiagnostic> Diagnostics { get; set; } = new();
        public int ErrorCount => Diagnostics.Count(item => item.Severity == MigrationDiagnosticSeverity.Error);
        public int WarningCount => Diagnostics.Count(item => item.Severity == MigrationDiagnosticSeverity.Warning);
        public int InfoCount => Diagnostics.Count(item => item.Severity == MigrationDiagnosticSeverity.Info);
        public bool IsReady => ErrorCount == 0;

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== Playnite SDK 7 migration check ===");
            builder.AppendLine($"Directory: {Directory}");
            builder.AppendLine($"Ready: {(IsReady ? "YES" : "NO")}");
            builder.AppendLine($"Errors: {ErrorCount}; warnings: {WarningCount}; info: {InfoCount}");
            foreach (var diagnostic in Diagnostics)
            {
                builder.AppendLine();
                var location = diagnostic.File ?? string.Empty;
                if (diagnostic.Line.HasValue)
                {
                    location += $":{diagnostic.Line.Value}";
                }
                builder.AppendLine($"[{diagnostic.Severity.ToString().ToUpperInvariant()} {diagnostic.Code}] {location}");
                builder.AppendLine($"  {diagnostic.Message}");
            }
            return builder.ToString().TrimEnd();
        }
    }

    public static class SdkV7MigrationAnalyzer
    {
        private sealed class SourceRule
        {
            public string Code { get; }
            public MigrationDiagnosticSeverity Severity { get; }
            public Regex Pattern { get; }
            public string Message { get; }

            public SourceRule(
                string code,
                MigrationDiagnosticSeverity severity,
                string pattern,
                string message)
            {
                Code = code;
                Severity = severity;
                Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
                Message = message;
            }
        }

        private static readonly IReadOnlyList<SourceRule> CSharpRules = new[]
        {
            new SourceRule(
                "SDK7S001",
                MigrationDiagnosticSeverity.Error,
                @"\busing\s+(?:static\s+)?System\.Windows(?:\.|\s*;)",
                "WPF System.Windows namespaces are not available in SDK 7. Replace the UI code with Avalonia APIs."),
            new SourceRule(
                "SDK7S002",
                MigrationDiagnosticSeverity.Error,
                @"\bSystem\.Windows\.",
                "A fully-qualified WPF type is used. SDK 7 UI surfaces use Avalonia types."),
            new SourceRule(
                "SDK7S003",
                MigrationDiagnosticSeverity.Error,
                @"\b(?:PresentationFramework|PresentationCore|WindowsBase)\b",
                "A WPF framework assembly is referenced from source. SDK 7 does not reference WPF assemblies."),
            new SourceRule(
                "SDK7S010",
                MigrationDiagnosticSeverity.Error,
                @"\bPlayniteApi\.Dialogs\.(?:ShowErrorMessage|ShowMessage|SelectFile|SelectFiles|SelectFolder)\s*\(",
                "Legacy synchronous dialog calls must be migrated to the corresponding SDK 7 Async method and awaited."),
            new SourceRule(
                "SDK7S011",
                MigrationDiagnosticSeverity.Error,
                @"\bPlayniteApi\.MainView\.OpenPluginSettings\s*\(",
                "OpenPluginSettings was replaced by OpenPluginSettingsAsync in SDK 7."),
            new SourceRule(
                "SDK7S012",
                MigrationDiagnosticSeverity.Error,
                @"\bPlayniteApi\.MainView\.OpenEditDialog\s*\(",
                "OpenEditDialog was replaced by OpenEditDialogAsync in SDK 7."),
            new SourceRule(
                "SDK7S013",
                MigrationDiagnosticSeverity.Error,
                @"\bPlayniteApi\.(?:StartGame|InstallGame|UninstallGame)\s*\(",
                "Game operations are asynchronous in SDK 7; use and await the corresponding Async method."),
            new SourceRule(
                "SDK7S020",
                MigrationDiagnosticSeverity.Warning,
                @"\.(?:NavigateAndWait|GetPageText|GetPageSource|GetCurrentAddress|SetCookies|GetCookies|DeleteCookies|DeleteDomainCookies|DeleteDomainCookiesRegex)\s*\(",
                "This looks like a legacy IWebView call. SDK 7 uses async navigation/page/cookie methods and the Address property; confirm the receiver type."),
            new SourceRule(
                "SDK7S021",
                MigrationDiagnosticSeverity.Warning,
                @"\.CreateView\s*\(\s*\d+\s*,",
                "The SDK 7 web-view factory accepts WebViewSettings instead of width/height overloads; confirm the receiver type.")
        };

        public static SdkV7MigrationReport Analyze(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A plugin source directory is required.", nameof(directory));
            }

            var root = Path.GetFullPath(directory);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"Plugin source directory does not exist: {root}");
            }

            var report = new SdkV7MigrationReport { Directory = root };
            var module = AnalyzeManifest(root, report);
            var allProjects = EnumerateSourceFiles(root, "*.csproj").ToList();
            var projects = SelectPluginProjects(root, allProjects, module, report);
            if (allProjects.Count == 0)
            {
                Add(report, "SDK7P001", MigrationDiagnosticSeverity.Error,
                    "No compiled plugin project (.csproj) was found.", null, null);
            }
            else
            {
                foreach (var project in projects)
                {
                    AnalyzeProject(root, project, report);
                }
            }

            var excludedProjectRoots = allProjects
                .Except(projects, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetDirectoryName)
                .Where(path => !string.IsNullOrWhiteSpace(path) &&
                    !projects.Any(project => string.Equals(
                        Path.GetDirectoryName(project),
                        path,
                        StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var source in EnumerateSourceFiles(root, "*.cs", excludedProjectRoots))
            {
                AnalyzeCSharp(root, source, report);
            }
            foreach (var xaml in EnumerateSourceFiles(root, "*.xaml", excludedProjectRoots))
            {
                Add(report, "SDK7X001", MigrationDiagnosticSeverity.Error,
                    "WPF .xaml files must be migrated to Avalonia .axaml markup and Avalonia code-behind.",
                    Relative(root, xaml), 1);
            }
            foreach (var axaml in EnumerateSourceFiles(root, "*.axaml", excludedProjectRoots))
            {
                AnalyzeAxaml(root, axaml, report);
            }

            Add(report, "SDK7I001", MigrationDiagnosticSeverity.Info,
                "This static check does not prove runtime compatibility. Finish with a NuGet-audited warnings-as-errors Release build and an installed-plugin test in Avalonia Playnite.",
                null, null);
            report.Diagnostics = report.Diagnostics
                .OrderBy(item => item.Severity)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.File, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Line)
                .ToList();
            return report;
        }

        private static string AnalyzeManifest(string root, SdkV7MigrationReport report)
        {
            var manifestPath = Path.Combine(root, "extension.yaml");
            if (!File.Exists(manifestPath))
            {
                Add(report, "SDK7M001", MigrationDiagnosticSeverity.Error,
                    "extension.yaml is missing from the plugin source root.", "extension.yaml", null);
                return string.Empty;
            }

            var text = File.ReadAllText(manifestPath);
            var type = ReadYamlScalar(text, "Type");
            var module = ReadYamlScalar(text, "Module");
            if (string.Equals(type, "Script", StringComparison.OrdinalIgnoreCase))
            {
                Add(report, "SDK7M002", MigrationDiagnosticSeverity.Error,
                    "SDK 7 migration applies to compiled plugins; this manifest declares a script extension.",
                    "extension.yaml", FindYamlLine(text, "Type"));
            }
            if (string.IsNullOrWhiteSpace(module) ||
                !string.Equals(Path.GetExtension(module), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                Add(report, "SDK7M003", MigrationDiagnosticSeverity.Error,
                    "The extension manifest Module must name the compiled SDK 7 plugin DLL.",
                    "extension.yaml", FindYamlLine(text, "Module"));
            }
            return module;
        }

        private static List<string> SelectPluginProjects(
            string root,
            List<string> projects,
            string module,
            SdkV7MigrationReport report)
        {
            if (projects.Count <= 1)
            {
                return projects;
            }

            var assemblyName = Path.GetFileNameWithoutExtension(module ?? string.Empty);
            var matching = projects.Where(project =>
                string.Equals(Path.GetFileNameWithoutExtension(project), assemblyName, StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(
                    File.ReadAllText(project),
                    $@"<AssemblyName>\s*{Regex.Escape(assemblyName)}\s*</AssemblyName>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .ToList();
            if (matching.Count > 0)
            {
                return matching;
            }

            var rootProjects = projects.Where(project => string.Equals(
                Path.GetDirectoryName(project),
                root,
                StringComparison.OrdinalIgnoreCase)).ToList();
            if (rootProjects.Count == 1)
            {
                return rootProjects;
            }

            Add(report, "SDK7P008", MigrationDiagnosticSeverity.Error,
                $"Could not identify which project produces manifest module '{module}'. " +
                "Match the project file or AssemblyName to the module DLL.",
                "extension.yaml", null);
            return projects;
        }

        private static void AnalyzeProject(string root, string projectPath, SdkV7MigrationReport report)
        {
            XDocument document;
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using var stream = File.OpenRead(projectPath);
                using var reader = XmlReader.Create(stream, settings);
                document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            }
            catch (Exception exception) when (exception is XmlException or InvalidDataException)
            {
                Add(report, "SDK7P002", MigrationDiagnosticSeverity.Error,
                    $"The project could not be parsed safely: {exception.Message}",
                    Relative(root, projectPath), null);
                return;
            }

            var relative = Relative(root, projectPath);
            var targets = Values(document, "TargetFramework")
                .Concat(Values(document, "TargetFrameworks").SelectMany(value => value.Split(';')))
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (targets.Count == 0)
            {
                targets.AddRange(Values(document, "TargetFrameworkVersion")
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0));
            }
            if (targets.Count == 0 || targets.Any(target => !string.Equals(target, "net10.0", StringComparison.OrdinalIgnoreCase)))
            {
                Add(report, "SDK7P003", MigrationDiagnosticSeverity.Error,
                    $"SDK 7 plugins must target net10.0 without a Windows TFM. Found: {(targets.Count == 0 ? "none" : string.Join(", ", targets))}.",
                    relative, LineOfFirst(document, "TargetFramework", "TargetFrameworks"));
            }

            var projectSdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
            if (projectSdk.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase) ||
                Values(document, "UseWPF").Any(IsTrue))
            {
                Add(report, "SDK7P004", MigrationDiagnosticSeverity.Error,
                    "The project still enables the WPF/WindowsDesktop SDK. Use Microsoft.NET.Sdk without UseWPF.",
                    relative, LineOfFirst(document, "UseWPF"));
            }

            CheckBooleanProperty(document, report, relative, "TreatWarningsAsErrors", "SDK7P005",
                "TreatWarningsAsErrors must remain enabled for SDK 7 migration builds.");
            CheckBooleanProperty(document, report, relative, "NuGetAudit", "SDK7P006",
                "NuGetAudit must remain enabled for SDK 7 migration builds.");
            if (!Values(document, "Nullable").Any(value => string.Equals(value.Trim(), "enable", StringComparison.OrdinalIgnoreCase)))
            {
                Add(report, "SDK7P007", MigrationDiagnosticSeverity.Warning,
                    "Nullable analysis is not enabled; SDK 7 templates enable it to expose migration mistakes.",
                    relative, LineOfFirst(document, "Nullable"));
            }

            var references = document.Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .ToList();
            CheckPackageReference(report, relative, references, "PlayniteSDK", 7, "SDK7P010");
            CheckPackageReference(report, relative, references, "Avalonia", 12, "SDK7P011");

            var forbiddenReferences = document.Descendants()
                .Where(element => element.Name.LocalName is "Reference" or "FrameworkReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Where(value => Regex.IsMatch(
                    value,
                    "^(PresentationFramework|PresentationCore|WindowsBase|Microsoft.WindowsDesktop.App)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .ToList();
            if (forbiddenReferences.Count > 0)
            {
                Add(report, "SDK7P012", MigrationDiagnosticSeverity.Error,
                    $"Remove WPF framework references: {string.Join(", ", forbiddenReferences)}.",
                    relative, null);
            }
        }

        private static void CheckPackageReference(
            SdkV7MigrationReport report,
            string project,
            IReadOnlyList<XElement> references,
            string packageId,
            int expectedMajor,
            string code)
        {
            var reference = references.FirstOrDefault(element => string.Equals(
                element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value,
                packageId,
                StringComparison.OrdinalIgnoreCase));
            if (reference == null)
            {
                Add(report, code, MigrationDiagnosticSeverity.Error,
                    $"Add a compile-only {packageId} {expectedMajor}.x PackageReference.", project, null);
                return;
            }

            var version = reference.Attribute("Version")?.Value ??
                reference.Elements().FirstOrDefault(element => element.Name.LocalName == "Version")?.Value ??
                string.Empty;
            if (!Regex.IsMatch(version, $@"^\s*{expectedMajor}(?:\.|$)", RegexOptions.CultureInvariant))
            {
                Add(report, code, MigrationDiagnosticSeverity.Error,
                    $"{packageId} must use major version {expectedMajor}; found '{version}'.",
                    project, Line(reference));
            }

            var privateAssets = ChildOrAttribute(reference, "PrivateAssets");
            var includeAssets = ChildOrAttribute(reference, "IncludeAssets");
            var assets = includeAssets.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!string.Equals(privateAssets.Trim(), "all", StringComparison.OrdinalIgnoreCase) ||
                !assets.Contains("compile") ||
                assets.Contains("runtime") ||
                assets.Contains("all"))
            {
                Add(report, code + "A", MigrationDiagnosticSeverity.Error,
                    $"{packageId} must remain host-supplied: set PrivateAssets=all and IncludeAssets to compile/build assets without runtime.",
                    project, Line(reference));
            }
        }

        private static void AnalyzeCSharp(string root, string path, SdkV7MigrationReport report)
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var matches = CSharpRules.Where(rule => rule.Pattern.IsMatch(lines[index])).ToList();
                if (matches.Any(rule => rule.Code == "SDK7S001"))
                {
                    matches.RemoveAll(rule => rule.Code == "SDK7S002");
                }
                foreach (var rule in matches)
                {
                    Add(report, rule.Code, rule.Severity, rule.Message, Relative(root, path), index + 1);
                }
            }
        }

        private static void AnalyzeAxaml(string root, string path, SdkV7MigrationReport report)
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(
                    "http://schemas.microsoft.com/winfx/2006/xaml/presentation",
                    StringComparison.OrdinalIgnoreCase))
                {
                    Add(report, "SDK7X002", MigrationDiagnosticSeverity.Error,
                        "Avalonia markup must use the https://github.com/avaloniaui default XML namespace, not the WPF presentation namespace.",
                        Relative(root, path), index + 1);
                }
            }
        }

        private static IEnumerable<string> EnumerateSourceFiles(
            string root,
            string pattern,
            IReadOnlyList<string> excludedRoots = null) =>
            Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(root, path))
                .Where(path => excludedRoots == null || !excludedRoots.Any(excluded =>
                    path.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));

        private static bool IsBuildOutput(string root, string path)
        {
            var relative = Relative(root, path);
            return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> Values(XDocument document, string localName) =>
            document.Descendants()
                .Where(element => element.Name.LocalName == localName)
                .Select(element => element.Value);

        private static void CheckBooleanProperty(
            XDocument document,
            SdkV7MigrationReport report,
            string project,
            string property,
            string code,
            string message)
        {
            if (!Values(document, property).Any(IsTrue))
            {
                Add(report, code, MigrationDiagnosticSeverity.Error, message, project, LineOfFirst(document, property));
            }
        }

        private static bool IsTrue(string value) =>
            string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        private static string ChildOrAttribute(XElement element, string name) =>
            element.Attribute(name)?.Value ??
            element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value ??
            string.Empty;

        private static int? LineOfFirst(XDocument document, params string[] names)
        {
            var nameSet = names.ToHashSet(StringComparer.Ordinal);
            return Line(document.Descendants().FirstOrDefault(element => nameSet.Contains(element.Name.LocalName)));
        }

        private static int? Line(XObject value) =>
            value is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;

        private static string ReadYamlScalar(string text, string name)
        {
            var match = Regex.Match(
                text,
                $@"^\s*{Regex.Escape(name)}\s*:\s*(?<value>.*?)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["value"].Value.Trim().Trim('\'', '"') : string.Empty;
        }

        private static int? FindYamlLine(string text, string name)
        {
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (Regex.IsMatch(lines[index], $@"^\s*{Regex.Escape(name)}\s*:", RegexOptions.IgnoreCase))
                {
                    return index + 1;
                }
            }
            return null;
        }

        private static string Relative(string root, string path) =>
            Path.GetRelativePath(root, path);

        private static void Add(
            SdkV7MigrationReport report,
            string code,
            MigrationDiagnosticSeverity severity,
            string message,
            string file,
            int? line)
        {
            if (report.Diagnostics.Any(item =>
                item.Code == code &&
                string.Equals(item.File, file, StringComparison.OrdinalIgnoreCase) &&
                item.Line == line))
            {
                return;
            }

            report.Diagnostics.Add(new MigrationDiagnostic
            {
                Code = code,
                Severity = severity,
                Message = message,
                File = file,
                Line = line
            });
        }
    }
}
