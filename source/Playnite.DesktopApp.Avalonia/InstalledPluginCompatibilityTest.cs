using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia;

internal static class InstalledPluginCompatibilityTest
{
    private static readonly ILogger logger = LogManager.GetLogger();

    public static async Task Run(
        MainWindow window,
        DesktopLibrary library,
        StartupOptions options)
    {
        logger.Info("Installed-plugin compatibility run started.");
        await Task.Delay(500);
        var report = new StringBuilder();
        var failedManifestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var factory = window.RuntimeHost.Extensions;
        var manifests = ExtensionFactory.GetInstalledManifests();

        report.AppendLine("=== Playnite Avalonia installed-plugin compatibility ===");
        report.AppendLine($"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version}; .NET {Environment.Version}");
        report.AppendLine($"Profile: {options.UserDataDirectory}");
        report.AppendLine($"Library: {options.LibraryPath} ({library.Games.Count:N0} games)");
        report.AppendLine($"Discovered: {manifests.Count}; loaded: {factory.Plugins.Count}; failed: {factory.LoadFailures.Count}");
        report.AppendLine();

        foreach (var manifest in manifests.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var loadedPlugins = factory.Plugins.Values
                .Where(item => string.Equals(item.Description.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var loadedScript = string.IsNullOrWhiteSpace(manifest.Module)
                ? null
                : factory.Scripts.FirstOrDefault(script =>
                    string.Equals(
                        Path.GetFullPath(script.Path),
                        Path.GetFullPath(Path.Combine(manifest.DirectoryPath, manifest.Module)),
                        StringComparison.OrdinalIgnoreCase));
            var failures = factory.LoadFailures
                .Where(item => string.Equals(
                    item.Manifest.Id,
                    manifest.Id,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (failures.Count > 0)
            {
                failedManifestIds.Add(manifest.Id);
                report.AppendLine($"[FAIL] {Describe(manifest)}");
                foreach (var failure in failures)
                {
                    report.AppendLine($"       {failure.Error}: {failure.ExceptionType ?? "No exception type"}");
                    report.AppendLine($"       {failure.Message}");
                    foreach (var line in (failure.Details ?? string.Empty).Split('\n'))
                    {
                        report.AppendLine($"       {line.TrimEnd('\r')}");
                    }
                }

                report.AppendLine();
                continue;
            }

            if (loadedScript != null)
            {
                report.AppendLine($"[PASS] {Describe(manifest)}");
                report.AppendLine($"       PowerShell runtime loaded {loadedScript.Name}");
                report.AppendLine();
                continue;
            }

            if (loadedPlugins.Count == 0)
            {
                failedManifestIds.Add(manifest.Id);
                report.AppendLine($"[FAIL] {Describe(manifest)}");
                report.AppendLine("       The manifest was neither loaded nor assigned a failure diagnostic.");
                report.AppendLine();
                continue;
            }

            foreach (var loadedPlugin in loadedPlugins)
            {
                var plugin = loadedPlugin.Plugin;
                var pluginAssembly = plugin.GetType().Assembly.GetName();
                var pluginReport = new StringBuilder();
                logger.Info($"Compatibility probe started for {manifest.Name} ({plugin.GetType().FullName}).");
                pluginReport.AppendLine($"       {plugin.GetType().FullName} ({plugin.Id}) loaded in " +
                    $"{pluginAssembly.Name} {pluginAssembly.Version}");
                logger.Info($"Compatibility API probe started for {manifest.Name}.");
                var pluginFailures = VerifyPluginApi(plugin, library, pluginReport);
                logger.Info($"Compatibility settings probe started for {manifest.Name}.");
                pluginFailures += VerifySettings(plugin, pluginReport);
                logger.Info($"Compatibility metadata probe started for {manifest.Name}.");
                pluginFailures += VerifyMetadataProvider(plugin, pluginReport);
                logger.Info($"Compatibility probe completed for {manifest.Name}.");
                report.AppendLine($"[{(pluginFailures == 0 ? "PASS" : "FAIL")}] {Describe(manifest)}");
                report.Append(pluginReport);
                if (pluginFailures > 0)
                {
                    failedManifestIds.Add(manifest.Id);
                }
                report.AppendLine();
            }
        }

        report.AppendLine($"VERDICT: {manifests.Count - failedManifestIds.Count}/{manifests.Count} " +
            $"installed manifests compatible; {failedManifestIds.Count} compatibility failures.");
        var reportPath = Path.Combine(options.UserDataDirectory, "installed-plugin-compatibility.txt");
        File.WriteAllText(reportPath, report.ToString());
        Console.WriteLine(report);
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(
            failedManifestIds.Count);
    }

    private static int VerifyPluginApi(Plugin plugin, DesktopLibrary library, StringBuilder report)
    {
        try
        {
            if (plugin.PlayniteApi?.ApplicationInfo?.Mode != ApplicationMode.Desktop ||
                plugin.PlayniteApi.Database?.Games?.Count != library.Games.Count ||
                plugin.PlayniteApi.WebViews == null ||
                plugin.PlayniteApi.Notifications == null)
            {
                throw new InvalidOperationException("The injected Playnite API surface is incomplete.");
            }

            report.AppendLine("       SDK API: Desktop mode, database, notifications, and web views available");
            return 0;
        }
        catch (Exception exception)
        {
            report.AppendLine($"       SDK API FAIL: {exception}");
            return 1;
        }
    }

    private static int VerifySettings(Plugin plugin, StringBuilder report)
    {
        if (!HasSettings(plugin))
        {
            report.AppendLine("       Settings: not advertised");
            return 0;
        }

        try
        {
            var settings = plugin.GetSettings(false);
            var settingsView = plugin.GetSettingsView(false);
            if (settings == null || settingsView == null)
            {
                throw new InvalidOperationException("Settings were advertised but the model or view was null.");
            }

            report.AppendLine($"       Settings: {settings.GetType().FullName} + {settingsView.GetType().FullName}");
            return 0;
        }
        catch (Exception exception)
        {
            report.AppendLine($"       Settings FAIL: {exception}");
            return 1;
        }
    }

    private static int VerifyMetadataProvider(Plugin plugin, StringBuilder report)
    {
        if (plugin is not MetadataPlugin metadataPlugin)
        {
            return 0;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(metadataPlugin.Name) ||
                metadataPlugin.SupportedFields == null ||
                metadataPlugin.SupportedFields.Count == 0)
            {
                throw new InvalidOperationException("The metadata plugin name or supported-field list was empty.");
            }

            report.AppendLine($"       Metadata: {metadataPlugin.SupportedFields.Count} declared fields from " +
                metadataPlugin.Name);
            return 0;
        }
        catch (Exception exception)
        {
            report.AppendLine($"       Metadata FAIL: {exception}");
            return 1;
        }
    }

    private static bool HasSettings(Plugin plugin) => plugin switch
    {
        GenericPlugin genericPlugin => genericPlugin.Properties?.HasSettings == true,
        LibraryPlugin libraryPlugin => libraryPlugin.Properties?.HasSettings == true,
        MetadataPlugin metadataPlugin => metadataPlugin.Properties?.HasSettings == true,
        _ => false
    };

    private static string Describe(ExtensionManifest manifest) =>
        $"{manifest.Name} {manifest.Version} [{manifest.Id}] ({manifest.Type})";
}
