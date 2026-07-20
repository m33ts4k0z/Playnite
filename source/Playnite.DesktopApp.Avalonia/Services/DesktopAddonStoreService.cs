using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http;
using Playnite.Avalonia.Theming;
using Playnite.Common;
using Playnite.Plugins;
using Playnite.Services;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.Services;

public enum DesktopAddonStoreCategory
{
    Extensions,
    GameLibraries,
    MetadataProviders,
    Scripts,
    Generators,
    DesktopThemes,
    FullscreenThemes
}

public sealed record DesktopAddonStoreCategoryOption(
    DesktopAddonStoreCategory Category,
    string ResourceKey,
    string FallbackName)
{
    public string Name => DesktopAddonStoreLocalization.Resolve(ResourceKey, FallbackName);
    public override string ToString() => Name;
}

public sealed record DesktopAddonPackageChoice(
    AddonInstallerPackage Package,
    bool IsCompatible,
    string CompatibilityReason)
{
    public string DisplayName =>
        $"{Package.Version} — API {Package.RequiredApiVersion}" +
        (IsCompatible ? string.Empty : $" — {CompatibilityReason}");

    public override string ToString() => DisplayName;
}

internal sealed record DesktopAddonCatalogItem(
    AddonManifest Manifest,
    AddonInstallerManifest Installer,
    IReadOnlyList<DesktopAddonPackageChoice> Packages,
    string CompatibilityReason)
{
    public string Id => Manifest.AddonId;
    public string Name => Manifest.Name;
    public string Author => Manifest.Author;
    public string Description => string.IsNullOrWhiteSpace(Manifest.Description)
        ? Manifest.ShortDescription
        : Manifest.Description;
    public string ShortDescription => Manifest.ShortDescription;
    public string CommunityNote => Manifest.CommunityNote;
    public string IconUrl => Manifest.IconUrl;
    public AddonType Type => Manifest.Type;
    public bool IsCompatible => Packages.Any(package => package.IsCompatible);
    public string CompatibilityText => IsCompatible ? "Compatible" : CompatibilityReason;
    public string LatestVersion => Packages.FirstOrDefault()?.Package.Version?.ToString() ?? "Unavailable";
}

internal sealed record DesktopAddonBrowseResult(
    IReadOnlyList<DesktopAddonCatalogItem> Items,
    IReadOnlyList<string> Failures);

internal sealed record DesktopInstalledAddon(
    string Id,
    string Name,
    string Version,
    string Type,
    string DirectoryPath,
    bool IsPlugin,
    bool IsDevelopment,
    bool CanUninstall,
    string UninstallReason);

internal static class DesktopAddonStoreLocalization
{
    public static string Resolve(string resourceKey, string fallback)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return fallback;
        }

        var application = global::Avalonia.Application.Current;
        return application?.TryGetResource(resourceKey, null, out var resource) == true &&
            resource is string localized && !string.IsNullOrWhiteSpace(localized)
                ? localized
                : fallback;
    }
}

internal interface IDesktopAddonCatalogClient
{
    IReadOnlyList<AddonManifest> GetAllAddons(AddonType type, string searchTerm);
    AddonInstallerManifest GetAddonInstaller(string addonId);
}

internal sealed class DesktopAddonCatalogClient : IDesktopAddonCatalogClient
{
    private const string ServicesEndpoint = "https://api.playnite.link/api";
    private readonly ServicesClient client = new(ServicesEndpoint);

    public IReadOnlyList<AddonManifest> GetAllAddons(AddonType type, string searchTerm) =>
        client.GetAllAddons(type, searchTerm) ?? [];

    public AddonInstallerManifest GetAddonInstaller(string addonId) =>
        client.GetAddonInstaller(addonId);
}

internal interface IDesktopAddonStoreService
{
    Task<DesktopAddonBrowseResult> BrowseAsync(
        DesktopAddonStoreCategory category,
        string searchTerm,
        bool supportsLegacyV6,
        CancellationToken cancellationToken);

    IReadOnlyList<DesktopInstalledAddon> DiscoverInstalled(IReadOnlyList<string> developmentPaths);

    Task QueueInstallAsync(
        DesktopAddonCatalogItem addon,
        DesktopAddonPackageChoice package,
        Func<string, string, Task<bool>> acceptLicense,
        CancellationToken cancellationToken);

    void QueueUninstall(DesktopInstalledAddon addon);
}

internal sealed class DesktopAddonStoreService : IDesktopAddonStoreService
{
    private static readonly Version NativeSdkVersion = new(7, 0);
    private readonly IDesktopAddonCatalogClient client;
    private readonly HttpClient httpClient;
    private readonly ConcurrentDictionary<string, InstallerLookup> installerCache =
        new(StringComparer.OrdinalIgnoreCase);

    public DesktopAddonStoreService()
        : this(new DesktopAddonCatalogClient(), new HttpClient())
    {
    }

    internal DesktopAddonStoreService(IDesktopAddonCatalogClient client, HttpClient httpClient)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<DesktopAddonBrowseResult> BrowseAsync(
        DesktopAddonStoreCategory category,
        string searchTerm,
        bool supportsLegacyV6,
        CancellationToken cancellationToken) =>
        Task.Run(
            () => Browse(category, searchTerm?.Trim() ?? string.Empty, supportsLegacyV6, cancellationToken),
            cancellationToken);

    private DesktopAddonBrowseResult Browse(
        DesktopAddonStoreCategory category,
        string searchTerm,
        bool supportsLegacyV6,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var manifests = new List<AddonManifest>();
        foreach (var type in GetCatalogTypes(category))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                manifests.AddRange(client.GetAllAddons(type, searchTerm));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add($"{GetTypeName(type)} catalog: {exception.Message}");
            }
        }

        var malformed = manifests.Count(manifest =>
            manifest == null || string.IsNullOrWhiteSpace(manifest.AddonId));
        if (malformed > 0)
        {
            failures.Add($"Skipped {malformed:N0} catalog entries without an add-on ID.");
        }

        var candidates = manifests
            .Where(manifest => manifest != null &&
                !string.IsNullOrWhiteSpace(manifest.AddonId) &&
                MatchesCategory(manifest, category))
            .GroupBy(manifest => manifest.AddonId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var results = new ConcurrentBag<DesktopAddonCatalogItem>();
        var entryFailures = new ConcurrentBag<string>();
        Parallel.ForEach(
            candidates,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 8
            },
            manifest =>
            {
                var lookup = GetInstaller(manifest);
                if (lookup.Error != null || lookup.Installer == null)
                {
                    entryFailures.Add($"{manifest.Name ?? manifest.AddonId}: " +
                        (lookup.Error?.Message ?? "The service returned no installer manifest."));
                    return;
                }

                var packages = (lookup.Installer.Packages ?? [])
                    .Where(package => package?.Version != null)
                    .OrderByDescending(package => package.Version)
                    .Select(package => ClassifyPackage(manifest.Type, package, supportsLegacyV6))
                    .ToList();
                results.Add(new DesktopAddonCatalogItem(
                    manifest,
                    lookup.Installer,
                    packages,
                    GetAddonCompatibilityReason(manifest.Type, packages, supportsLegacyV6)));
            });
        failures.AddRange(entryFailures.OrderBy(failure => failure, StringComparer.CurrentCultureIgnoreCase));

        return new DesktopAddonBrowseResult(
            results.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            failures);
    }

    private InstallerLookup FetchInstaller(AddonManifest manifest)
    {
        try
        {
            return new InstallerLookup(client.GetAddonInstaller(manifest.AddonId), null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new InstallerLookup(null, exception);
        }
    }

    private InstallerLookup GetInstaller(AddonManifest manifest)
    {
        if (installerCache.TryGetValue(manifest.AddonId, out var cached))
        {
            return cached;
        }

        var lookup = FetchInstaller(manifest);
        if (lookup.Error == null && lookup.Installer != null)
        {
            installerCache.TryAdd(manifest.AddonId, lookup);
        }

        return lookup;
    }

    public IReadOnlyList<DesktopInstalledAddon> DiscoverInstalled(IReadOnlyList<string> developmentPaths)
    {
        var installed = ExtensionFactory.GetInstalledManifests(
                (developmentPaths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).ToList())
            .Select(manifest =>
            {
                var isUser = IsUnder(manifest.DirectoryPath, PlaynitePaths.ExtensionsUserDataPath);
                var canUninstall = isUser && !manifest.IsExternalDev;
                return new DesktopInstalledAddon(
                    manifest.Id,
                    manifest.Name,
                    manifest.Version,
                    GetExtensionTypeName(manifest.Type),
                    manifest.DirectoryPath,
                    true,
                    manifest.IsExternalDev,
                    canUninstall,
                    canUninstall
                        ? string.Empty
                        : manifest.IsExternalDev
                            ? "Development extensions are managed from their source directory."
                            : "Built-in extensions cannot be uninstalled.");
            })
            .ToList();

        installed.AddRange(DiscoverThemes(Path.Combine(PlaynitePaths.ThemesUserDataPath, "Desktop"), true));
        installed.AddRange(DiscoverThemes(Path.Combine(PlaynitePaths.ThemesUserDataPath, "Fullscreen"), true));
        installed.AddRange(DiscoverThemes(Path.Combine(AppContext.BaseDirectory, "Themes", "Desktop"), false));
        installed.AddRange(DiscoverThemes(Path.Combine(AppContext.BaseDirectory, "Themes", "Fullscreen"), false));
        return installed
            .Where(addon => !string.IsNullOrWhiteSpace(addon.Id))
            .GroupBy(addon => addon.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(addon => Version.TryParse(addon.Version, out var value)
                ? value
                : new Version()).First())
            .OrderBy(addon => addon.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task QueueInstallAsync(
        DesktopAddonCatalogItem addon,
        DesktopAddonPackageChoice package,
        Func<string, string, Task<bool>> acceptLicense,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addon);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(acceptLicense);
        if (!package.IsCompatible)
        {
            throw new InvalidOperationException(package.CompatibilityReason);
        }

        var agreement = addon.Manifest.UserAgreement;
        if (agreement != null)
        {
            var accepted = ExtensionInstaller.GetAddonLicenseAgreed(addon.Id);
            if (accepted == null || accepted < agreement.Updated)
            {
                var license = await httpClient.GetStringAsync(agreement.AgreementUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (!await acceptLicense(addon.Name, license).ConfigureAwait(false))
                {
                    ExtensionInstaller.RemoveAddonLicenseAgreement(addon.Id);
                    throw new OperationCanceledException("The add-on license was declined.", cancellationToken);
                }

                ExtensionInstaller.AgreeAddonLicense(addon.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(package.Package.PackageUrl))
        {
            throw new InvalidDataException($"The package for {addon.Name} has no download URL.");
        }

        var target = addon.Manifest.GetTargetDownloadPath();
        FileSystem.PrepareSaveFile(target);
        try
        {
            if (Uri.TryCreate(package.Package.PackageUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
            {
                await using var source = await httpClient.GetStreamAsync(uri, cancellationToken)
                    .ConfigureAwait(false);
                await using var destination = File.Create(target);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                File.Copy(package.Package.PackageUrl, target, true);
            }

            ValidatePackage(addon, target);
            ExtensionInstaller.QueuePackageInstall(target);
        }
        catch (Exception exception)
        {
            try
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "The add-on operation failed and its incomplete package could not be removed.",
                    exception,
                    cleanupException);
            }

            throw;
        }
    }

    public void QueueUninstall(DesktopInstalledAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        if (!addon.CanUninstall || string.IsNullOrWhiteSpace(addon.DirectoryPath))
        {
            throw new InvalidOperationException(addon.UninstallReason);
        }

        ExtensionInstaller.QueueExtensionUninstall(addon.DirectoryPath);
    }

    private static void ValidatePackage(DesktopAddonCatalogItem addon, string packagePath)
    {
        if (addon.Manifest.IsExtension)
        {
            ExtensionInstaller.VerifyExtensionPackage(packagePath);
            var manifest = ExtensionInstaller.GetPackedExtensionManifest(packagePath);
            if (!string.Equals(manifest?.Id, addon.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Package identity '{manifest?.Id ?? "(missing)"}' does not match add-on '{addon.Id}'.");
            }

            return;
        }

        ExtensionInstaller.VerifyThemePackage(packagePath);
        ValidateAvaloniaThemePackage(addon, packagePath);
    }

    private static void ValidateAvaloniaThemePackage(DesktopAddonCatalogItem addon, string packagePath)
    {
        var validationRoot = Path.Combine(
            Path.GetTempPath(),
            $"playnite-theme-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(validationRoot);
        try
        {
            ZipFile.ExtractToDirectory(packagePath, validationRoot);
            var expectedMode = addon.Type == AddonType.ThemeDesktop
                ? AvaloniaThemeMode.Desktop
                : AvaloniaThemeMode.Fullscreen;
            var package = AvaloniaThemePackage.Load(validationRoot, expectedMode);
            package.ValidateMarkupStructure();
            if (!string.Equals(package.Manifest.Id, addon.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Theme identity '{package.Manifest.Id}' does not match add-on '{addon.Id}'.");
            }
        }
        finally
        {
            if (Directory.Exists(validationRoot))
            {
                Directory.Delete(validationRoot, true);
            }
        }
    }

    private static DesktopAddonPackageChoice ClassifyPackage(
        AddonType type,
        AddonInstallerPackage package,
        bool supportsLegacyV6)
    {
        var required = package.RequiredApiVersion;
        if (required == null)
        {
            return new DesktopAddonPackageChoice(package, false, "The package does not declare an API version.");
        }

        if (type is AddonType.ThemeDesktop or AddonType.ThemeFullscreen)
        {
            var compatible = required.Major == AvaloniaThemePackage.CurrentApiVersion.Major &&
                required <= AvaloniaThemePackage.CurrentApiVersion;
            return new DesktopAddonPackageChoice(
                package,
                compatible,
                compatible
                    ? string.Empty
                    : required.Major < AvaloniaThemePackage.CurrentApiVersion.Major
                        ? "Legacy WPF theme; an Avalonia theme package is required."
                        : $"Requires theme API {required}; this build supports {AvaloniaThemePackage.CurrentApiVersion}.");
        }

        if (required.Major == NativeSdkVersion.Major)
        {
            var compatible = required <= NativeSdkVersion;
            return new DesktopAddonPackageChoice(
                package,
                compatible,
                compatible ? string.Empty : $"Requires SDK {required}; this build supports {NativeSdkVersion}.");
        }

        if (required.Major == SdkVersions.SDKVersion.Major)
        {
            var compatible = supportsLegacyV6 && required <= SdkVersions.SDKVersion;
            return new DesktopAddonPackageChoice(
                package,
                compatible,
                compatible
                    ? string.Empty
                    : supportsLegacyV6
                        ? $"Requires SDK {required}; this build supports {SdkVersions.SDKVersion}."
                        : "SDK v6/WPF add-ons are supported on Windows only; Linux requires SDK v7.");
        }

        return new DesktopAddonPackageChoice(
            package,
            false,
            $"API {required} is not supported by this build.");
    }

    private static string GetAddonCompatibilityReason(
        AddonType type,
        IReadOnlyList<DesktopAddonPackageChoice> packages,
        bool supportsLegacyV6)
    {
        if (packages.Count == 0)
        {
            return "No installable packages are published.";
        }

        if (!supportsLegacyV6 &&
            type is not AddonType.ThemeDesktop and not AddonType.ThemeFullscreen &&
            packages.Any(package => package.Package.RequiredApiVersion?.Major == SdkVersions.SDKVersion.Major))
        {
            return "SDK v6/WPF add-ons are supported on Windows only; Linux requires SDK v7.";
        }

        return packages.First().CompatibilityReason;
    }

    private static IReadOnlyList<AddonType> GetCatalogTypes(DesktopAddonStoreCategory category) =>
        category switch
        {
            DesktopAddonStoreCategory.Extensions or
            DesktopAddonStoreCategory.Scripts or
            DesktopAddonStoreCategory.Generators =>
                [AddonType.GameLibrary, AddonType.MetadataProvider, AddonType.Generic],
            DesktopAddonStoreCategory.GameLibraries => [AddonType.GameLibrary],
            DesktopAddonStoreCategory.MetadataProviders => [AddonType.MetadataProvider],
            DesktopAddonStoreCategory.DesktopThemes => [AddonType.ThemeDesktop],
            DesktopAddonStoreCategory.FullscreenThemes => [AddonType.ThemeFullscreen],
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

    private static bool MatchesCategory(AddonManifest manifest, DesktopAddonStoreCategory category)
    {
        if (category == DesktopAddonStoreCategory.Scripts)
        {
            return HasTag(manifest, "script");
        }

        if (category == DesktopAddonStoreCategory.Generators)
        {
            return HasTag(manifest, "generator");
        }

        return true;
    }

    private static bool HasTag(AddonManifest manifest, string term) =>
        (manifest.Tags ?? []).Any(tag =>
            tag?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
        manifest.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
        manifest.ShortDescription?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static IEnumerable<DesktopInstalledAddon> DiscoverThemes(string root, bool canUninstall)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var manifestPath in Directory.EnumerateFiles(root, "theme.yaml", SearchOption.AllDirectories))
        {
            AvaloniaThemePackage package;
            try
            {
                package = AvaloniaThemePackage.Load(manifestPath);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or FileNotFoundException or IOException or ArgumentException)
            {
                continue;
            }

            if (package.IsRawDictionary || package.Manifest == null)
            {
                continue;
            }

            yield return new DesktopInstalledAddon(
                package.Manifest.Id,
                package.Name,
                package.Manifest.Version,
                $"{package.Mode} theme",
                package.RootDirectory,
                false,
                false,
                canUninstall,
                canUninstall ? string.Empty : "Built-in themes cannot be uninstalled.");
        }
    }

    private static bool IsUnder(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidatePath = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(rootPath, comparison);
    }

    private static string GetTypeName(AddonType type) =>
        type switch
        {
            AddonType.GameLibrary => DesktopAddonStoreLocalization.Resolve("LOCLibraries", "Game library"),
            AddonType.MetadataProvider => DesktopAddonStoreLocalization.Resolve(
                "LOCMetadataProviders", "Metadata provider"),
            AddonType.Generic => DesktopAddonStoreLocalization.Resolve("LOCExtensionGeneric", "Extension"),
            AddonType.ThemeDesktop => DesktopAddonStoreLocalization.Resolve(
                "LOCAddonsThemesDesktop", "Desktop theme"),
            AddonType.ThemeFullscreen => DesktopAddonStoreLocalization.Resolve(
                "LOCAddonsThemesFullscren", "Fullscreen theme"),
            _ => type.ToString()
        };

    private static string GetExtensionTypeName(ExtensionType type) =>
        type switch
        {
            ExtensionType.GameLibrary => DesktopAddonStoreLocalization.Resolve("LOCLibraries", "Game library"),
            ExtensionType.MetadataProvider => DesktopAddonStoreLocalization.Resolve(
                "LOCMetadataProviders", "Metadata provider"),
            ExtensionType.Script => DesktopAddonStoreLocalization.Resolve("LOCScripts", "Script"),
            ExtensionType.GenericPlugin => DesktopAddonStoreLocalization.Resolve(
                "LOCExtensionGeneric", "Extension"),
            _ => type.ToString()
        };

    private sealed record InstallerLookup(AddonInstallerManifest Installer, Exception Error);
}
