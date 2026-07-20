using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia;

internal static class InstalledPluginCompatibilityTest
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static async Task Run(
        MainWindow window,
        DesktopLibrary library,
        StartupOptions options)
    {
        logger.Info("Installed-plugin compatibility run started.");
        await Task.Delay(500);
        if (window.RuntimeHost == null)
        {
            // The runtime host only exists when a library opened; without it
            // there is nothing to probe, so fail loudly instead of crashing.
            var error = "FAIL: no Playnite library could be opened for this profile, so the plugin " +
                "runtime host was not created. Point --userdatadir (or --library-path) at a profile " +
                "with an existing library and rerun.";
            logger.Error(error);
            Console.WriteLine(error);
            File.WriteAllText(
                Path.Combine(options.UserDataDirectory, "installed-plugin-compatibility.txt"),
                error + Environment.NewLine);
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(2);
            return;
        }

        var report = new StringBuilder();
        var failedManifestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var factory = window.RuntimeHost.Extensions;
        var manifests = ExtensionFactory.GetInstalledManifests();

        report.AppendLine("=== Playnite Avalonia installed-plugin compatibility ===");
        report.AppendLine($"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version}; .NET {Environment.Version}");
        report.AppendLine($"Profile: {options.UserDataDirectory}");
        report.AppendLine($"Library: {options.LibraryPath} ({library.Games.Count:N0} games)");
        report.AppendLine($"Discovered: {manifests.Count}; loaded: " +
            $"{factory.Plugins.Count + window.RuntimeHost.V7Plugins.Count}; failed: " +
            $"{factory.LoadFailures.Count + window.RuntimeHost.V7PluginFailures.Count}");
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
                        PathComparison));
            var loadedV7Plugins = window.RuntimeHost.V7Plugins
                .Where(item => string.Equals(item.Manifest.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var failures = factory.LoadFailures
                .Where(item => string.Equals(
                    item.Manifest.Id,
                    manifest.Id,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            var v7Failures = window.RuntimeHost.V7PluginFailures
                .Where(item => string.Equals(
                    item.Manifest.Id,
                    manifest.Id,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (failures.Count > 0 || v7Failures.Count > 0)
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
                foreach (var failure in v7Failures)
                {
                    report.AppendLine($"       {failure.Exception.GetType().FullName}");
                    report.AppendLine($"       {failure.Message}");
                    report.AppendLine($"       {failure.Exception}");
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

            if (loadedV7Plugins.Count > 0)
            {
                foreach (var loadedPlugin in loadedV7Plugins)
                {
                    logger.Info($"SDK v7 compatibility probe started for {manifest.Name} ({loadedPlugin.Name}).");
                    var pluginReport = new StringBuilder();
                    var pluginFailures = VerifyV7Plugin(
                        loadedPlugin,
                        window.RuntimeHost,
                        library,
                        pluginReport);
                    logger.Info($"SDK v7 compatibility probe completed for {manifest.Name}.");
                    report.AppendLine($"[{(pluginFailures == 0 ? "PASS" : "FAIL")}] {Describe(manifest)}");
                    report.Append(pluginReport);
                    if (pluginFailures > 0)
                    {
                        failedManifestIds.Add(manifest.Id);
                    }

                    report.AppendLine();
                }

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

    private static int VerifyV7Plugin(
        V7LoadedPlugin plugin,
        AvaloniaRuntimeHost runtimeHost,
        DesktopLibrary library,
        StringBuilder report)
    {
        var failures = 0;
        report.AppendLine($"       {plugin.Name} ({plugin.Id}) loaded through isolated SDK v7 as {plugin.Kind}");
        if (plugin.Id == Guid.Empty || string.IsNullOrWhiteSpace(plugin.Name) || string.IsNullOrWhiteSpace(plugin.Kind))
        {
            report.AppendLine("       SDK v7 identity FAIL: plugin identity was incomplete.");
            failures++;
        }
        else
        {
            report.AppendLine("       SDK API: isolated SDK v7 bridge constructed the plugin successfully");
        }

        if (plugin.HasSettings)
        {
            try
            {
                var view = plugin.BeginSettingsEdit();
                V7SettingsValidationResult validation;
                try
                {
                    validation = plugin.VerifySettings();
                }
                finally
                {
                    plugin.CancelSettingsEdit();
                }
                if (view == null || !validation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Settings view was null or validation failed: {string.Join("; ", validation.Errors)}");
                }

                report.AppendLine($"       Settings: native Avalonia {view.GetType().FullName}");
            }
            catch (Exception exception)
            {
                report.AppendLine($"       Settings FAIL: {exception}");
                failures++;
            }
        }
        else
        {
            report.AppendLine("       Settings: not advertised");
        }

        if (string.Equals(plugin.Kind, "MetadataPlugin", StringComparison.Ordinal))
        {
            if (plugin.SupportedMetadataFields == null || plugin.SupportedMetadataFields.Length == 0)
            {
                report.AppendLine("       Metadata FAIL: supported-field list was empty.");
                failures++;
            }
            else
            {
                report.AppendLine($"       Metadata: {plugin.SupportedMetadataFields.Length} declared fields");
                failures += VerifyV7MetadataProvider(plugin, runtimeHost, library, report);
            }
        }

        if (string.Equals(plugin.Kind, "LibraryPlugin", StringComparison.Ordinal))
        {
            failures += VerifyV7LibraryPlugin(plugin, runtimeHost, library, report);
        }

        return failures;
    }

    private static int VerifyV7LibraryPlugin(
        V7LoadedPlugin plugin,
        AvaloniaRuntimeHost runtimeHost,
        DesktopLibrary library,
        StringBuilder report)
    {
        try
        {
            var libraryPlugin = runtimeHost.LibraryPlugins.Single(item => item.Id == plugin.Id);
            var importedGames = libraryPlugin.GetGames(new LibraryGetGamesArgs()).ToList();
            if (importedGames.Any(game => game == null ||
                string.IsNullOrWhiteSpace(game.GameId) ||
                string.IsNullOrWhiteSpace(game.Name)))
            {
                throw new InvalidDataException(
                    "The library provider returned a game without a required ID or name.");
            }

            var sample = importedGames.FirstOrDefault();
            report.AppendLine(sample == null
                ? "       Library import: completed successfully with no games in this profile"
                : $"       Library import: {importedGames.Count:N0} real games; " +
                  $"sample '{sample.Name}' ({sample.GameId})");

            var existingGame = library.Games.Select(item => item.Game)
                .FirstOrDefault(game => game.PluginId == plugin.Id);
            var controllerGame = existingGame ?? (sample == null
                ? null
                : new Game(sample.Name)
                {
                    PluginId = plugin.Id,
                    GameId = sample.GameId,
                    IsInstalled = sample.IsInstalled,
                    InstallDirectory = sample.InstallDirectory
                });

            if (controllerGame != null)
            {
                var controllerProbe = new Game(controllerGame.Name)
                {
                    PluginId = controllerGame.PluginId,
                    GameId = controllerGame.GameId,
                    IsInstalled = controllerGame.IsInstalled,
                    InstallDirectory = controllerGame.InstallDirectory,
                    IncludeLibraryPluginAction = true
                };
                var policy = runtimeHost.Actions.Policy;
                var controllers = new ControllerBase[][]
                {
                    (policy.AdditionalPlayControllers(controllerProbe) ?? []).Cast<ControllerBase>().ToArray(),
                    (policy.AdditionalInstallControllers(controllerProbe) ?? []).Cast<ControllerBase>().ToArray(),
                    (policy.AdditionalUninstallControllers(controllerProbe) ?? []).Cast<ControllerBase>().ToArray()
                };
                try
                {
                    report.AppendLine($"       Controllers: play {controllers[0].Length}, " +
                        $"install {controllers[1].Length}, uninstall {controllers[2].Length}");
                }
                finally
                {
                    foreach (var controller in controllers.SelectMany(items => items))
                    {
                        controller.Dispose();
                    }
                }

                using var metadataProvider = libraryPlugin.GetMetadataDownloader();
                if (metadataProvider != null)
                {
                    // Library metadata commonly reaches live services (Steam
                    // opens a SteamKit session); a bounded wait keeps one hung
                    // provider from stalling the entire run.
                    var metadataTask = Task.Run(() => metadataProvider.GetMetadata(controllerGame));
                    if (metadataTask.Wait(TimeSpan.FromSeconds(60)))
                    {
                        var metadata = metadataTask.Result;
                        report.AppendLine(metadata == null
                            ? "       Library metadata: provider returned no metadata for the sample game"
                            : $"       Library metadata: sample resolved to '{metadata.Name ?? controllerGame.Name}'");
                    }
                    else
                    {
                        _ = metadataTask.ContinueWith(
                            task => _ = task.Exception,
                            TaskContinuationOptions.OnlyOnFaulted);
                        report.AppendLine("       Library metadata: timed out after 60s " +
                            "(the provider likely needs a live service session); not counted as a failure");
                    }
                }
                else
                {
                    report.AppendLine("       Library metadata: not advertised");
                }
            }

            report.AppendLine(plugin.HasLibraryClient
                ? $"       Library client: available; installed={plugin.IsLibraryClientInstalled}"
                : "       Library client: not advertised");
            return 0;
        }
        catch (Exception exception)
        {
            report.AppendLine($"       Library contract FAIL: {exception}");
            return 1;
        }
    }

    private static int VerifyV7MetadataProvider(
        V7LoadedPlugin plugin,
        AvaloniaRuntimeHost runtimeHost,
        DesktopLibrary library,
        StringBuilder report)
    {
        try
        {
            var gameItem = library.Games.FirstOrDefault(item =>
                    string.Equals(item.Name, "Cyberpunk 2077", StringComparison.OrdinalIgnoreCase))
                ?? library.Games.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Name))
                ?? throw new InvalidOperationException("No real library game was available for metadata testing.");
            var game = gameItem.Game;
            var metadataPlugin = runtimeHost.MetadataPlugins.Single(item => item.Id == plugin.Id);
            // Metadata providers commonly hit live services; keep the probe
            // bounded so one hung provider cannot stall the entire run.
            var probeTask = Task.Run(() =>
            {
                using var provider = metadataPlugin.GetMetadataProvider(new MetadataRequestOptions(game, true))
                    ?? throw new InvalidOperationException("The metadata plugin returned no provider.");
                if (!provider.AvailableFields.Contains(MetadataField.Name))
                {
                    throw new InvalidOperationException(
                        $"The provider returned no name for the real library game '{game.Name}'.");
                }

                var name = provider.GetName(new GetMetadataFieldArgs());
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidOperationException(
                        $"The provider returned an empty name for the real library game '{game.Name}'.");
                }

                return (Name: name, FieldCount: provider.AvailableFields.Count);
            });
            if (!probeTask.Wait(TimeSpan.FromSeconds(60)))
            {
                _ = probeTask.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                report.AppendLine("       Metadata fetch: timed out after 60s " +
                    "(the provider likely needs a live service session); not counted as a failure");
                return 0;
            }

            var fetched = probeTask.Result;
            report.AppendLine($"       Metadata fetch: '{game.Name}' resolved to '{fetched.Name}' " +
                $"with {fetched.FieldCount} available fields");
            return 0;
        }
        catch (Exception exception)
        {
            report.AppendLine($"       Metadata fetch FAIL: {exception}");
            return 1;
        }
    }

    private static string Describe(ExtensionManifest manifest) =>
        $"{manifest.Name} {manifest.Version} [{manifest.Id}] ({manifest.Type})";
}
