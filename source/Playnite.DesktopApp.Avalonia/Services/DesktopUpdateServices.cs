using System.ComponentModel;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Newtonsoft.Json;
using Playnite.Avalonia.Theming;
using Playnite.Common;
using Playnite.Plugins;
using Playnite.Services;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.Services;

internal interface IDesktopAddonUpdateService
{
    Task<DesktopAddonUpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
    Task QueueAsync(
        DesktopAddonUpdate update,
        Func<string, string, Task<bool>> acceptLicense,
        CancellationToken cancellationToken);
}

internal sealed record DesktopAddonUpdateCheckResult(
    IReadOnlyList<DesktopAddonUpdate> Updates,
    IReadOnlyList<string> Failures);

public sealed class DesktopAddonUpdate : INotifyPropertyChanged
{
    internal AddonManifest Manifest { get; }
    internal AddonInstallerPackage Package { get; }

    public string Id => Manifest.AddonId;
    public string Name => Manifest.Name;
    public Version CurrentVersion { get; }
    public Version AvailableVersion => Package.Version;
    public string VersionChange => $"{CurrentVersion} → {AvailableVersion}";
    public string Changelog { get; }
    private bool isSelected = true;
    private string status = "Available";

    public event PropertyChangedEventHandler PropertyChanged;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected != value)
            {
                isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
    public string Status
    {
        get => status;
        internal set
        {
            if (!string.Equals(status, value, StringComparison.Ordinal))
            {
                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
    }

    internal DesktopAddonUpdate(
        AddonManifest manifest,
        AddonInstallerPackage package,
        AddonInstallerManifest installer,
        Version currentVersion)
    {
        Manifest = manifest;
        Package = package;
        CurrentVersion = currentVersion;
        Changelog = string.Join(
            Environment.NewLine,
            (installer.Packages ?? [])
            .Where(candidate => candidate.Version > currentVersion && candidate.Version <= package.Version)
            .OrderBy(candidate => candidate.Version)
            .SelectMany(candidate => new[] { candidate.Version.ToString() }
                .Concat((candidate.Changelog ?? []).Select(change => $"  • {change}"))));
    }
}

internal sealed class DesktopAddonUpdateService : IDesktopAddonUpdateService
{
    private const string ServicesEndpoint = "https://api.playnite.link/api";
    private readonly ServicesClient client;
    private readonly HttpClient httpClient;

    public DesktopAddonUpdateService()
        : this(new ServicesClient(ServicesEndpoint), new HttpClient())
    {
    }

    internal DesktopAddonUpdateService(ServicesClient client, HttpClient httpClient)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<DesktopAddonUpdateCheckResult> CheckAsync(CancellationToken cancellationToken) =>
        await Task.Run(Check, cancellationToken);

    private DesktopAddonUpdateCheckResult Check()
    {
        var updates = new List<DesktopAddonUpdate>();
        var failures = new List<string>();
        var blacklist = (client.GetAddonBlacklist() ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var installed in DiscoverInstalledAddons())
        {
            try
            {
                if (blacklist.Contains(installed.Id))
                {
                    continue;
                }

                var manifest = client.GetAddon(installed.Id);
                if (manifest == null)
                {
                    continue;
                }

                var installer = client.GetAddonInstaller(installed.Id);
                if (installer == null)
                {
                    throw new InvalidDataException("The service returned no installer manifest.");
                }

                installer.AddonType = manifest.Type;
                var package = installer.GetCompatiblePackages(installed.ApiVersion)?.FirstOrDefault();
                if (package?.Version > installed.Version)
                {
                    updates.Add(new DesktopAddonUpdate(manifest, package, installer, installed.Version));
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{installed.Name}: {exception.Message}");
            }
        }

        return new DesktopAddonUpdateCheckResult(updates, failures);
    }

    public async Task QueueAsync(
        DesktopAddonUpdate update,
        Func<string, string, Task<bool>> acceptLicense,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(acceptLicense);
        var agreement = update.Manifest.UserAgreement;
        if (agreement != null)
        {
            var accepted = ExtensionInstaller.GetAddonLicenseAgreed(update.Id);
            if (accepted == null || accepted < agreement.Updated)
            {
                var text = await httpClient.GetStringAsync(agreement.AgreementUrl, cancellationToken);
                if (!await acceptLicense(update.Name, text))
                {
                    ExtensionInstaller.RemoveAddonLicenseAgreement(update.Id);
                    update.Status = "License declined";
                    return;
                }

                ExtensionInstaller.AgreeAddonLicense(update.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(update.Package.PackageUrl))
        {
            throw new InvalidDataException($"The update package for {update.Name} has no download URL.");
        }

        var target = update.Manifest.GetTargetDownloadPath();
        FileSystem.PrepareSaveFile(target);
        if (Uri.TryCreate(update.Package.PackageUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            await using var source = await httpClient.GetStreamAsync(uri, cancellationToken);
            await using var destination = File.Create(target);
            await source.CopyToAsync(destination, cancellationToken);
        }
        else
        {
            File.Copy(update.Package.PackageUrl, target, true);
        }

        ExtensionInstaller.QueuePackageInstall(target);
        update.Status = "Queued for restart";
    }

    private static IReadOnlyList<InstalledAddon> DiscoverInstalledAddons()
    {
        var installed = ExtensionFactory.GetInstalledManifests()
            .Where(manifest => !manifest.IsExternalDev && Version.TryParse(manifest.Version, out _))
            .Select(manifest => new InstalledAddon(
                manifest.Id,
                manifest.Name,
                Version.Parse(manifest.Version),
                ResolveExtensionApiVersion(manifest)))
            .ToList();
        installed.AddRange(DiscoverThemes(PlaynitePaths.ThemesUserDataPath));
        return installed
            .GroupBy(addon => addon.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(addon => addon.Version).First())
            .ToList();
    }

    private static IEnumerable<InstalledAddon> DiscoverThemes(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
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
                exception is InvalidDataException or IOException or ArgumentException)
            {
                continue;
            }

            if (package.IsRawDictionary || package.Manifest == null ||
                !Version.TryParse(package.Manifest.Version, out var version))
            {
                continue;
            }

            yield return new InstalledAddon(
                package.Manifest.Id,
                package.Name,
                version,
                AvaloniaThemePackage.CurrentApiVersion);
        }
    }

    private static Version ResolveExtensionApiVersion(ExtensionManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Module))
        {
            return SdkVersions.SDKVersion;
        }

        var assemblyPath = Path.Combine(manifest.DirectoryPath, manifest.Module);
        if (!File.Exists(assemblyPath))
        {
            return SdkVersions.SDKVersion;
        }

        // A corrupt, locked, or non-.NET module must not fail the whole update
        // check; such an addon simply keeps the default v6 API version.
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return SdkVersions.SDKVersion;
            }

            var metadata = peReader.GetMetadataReader();
            foreach (var referenceHandle in metadata.AssemblyReferences)
            {
                var reference = metadata.GetAssemblyReference(referenceHandle);
                if (string.Equals(
                        metadata.GetString(reference.Name),
                        "Playnite.SDK",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return reference.Version.Major == 7 ? new Version(7, 0) : SdkVersions.SDKVersion;
                }
            }
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
        }

        return SdkVersions.SDKVersion;
    }

    private sealed record InstalledAddon(
        string Id,
        string Name,
        Version Version,
        Version ApiVersion);
}

internal interface IDesktopProgramUpdateService
{
    bool IsSupported { get; }
    Task<DesktopProgramUpdate> CheckAsync(CancellationToken cancellationToken);
    Task<string> DownloadAsync(
        DesktopProgramUpdate update,
        IProgress<int> progress,
        CancellationToken cancellationToken);
}

public sealed record DesktopProgramUpdate(
    Version CurrentVersion,
    Version AvailableVersion,
    string Checksum,
    IReadOnlyList<string> PackageUrls);

internal sealed class DesktopProgramUpdateService : IDesktopProgramUpdateService
{
    private static readonly string[] updateRoots =
    [
        "https://www.playnite.link/update/",
        "https://api.playnite.link/update/"
    ];
    private readonly HttpClient httpClient;
    private readonly Func<Version> currentVersion;

    public bool IsSupported => OperatingSystem.IsWindows();

    public DesktopProgramUpdateService()
        : this(new HttpClient(), CoreRuntime.ApplicationVersion)
    {
    }

    internal DesktopProgramUpdateService(HttpClient httpClient, Func<Version> currentVersion)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
    }

    public async Task<DesktopProgramUpdate> CheckAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return null;
        }

        var current = currentVersion();
        var errors = new List<string>();
        foreach (var root in updateRoots)
        {
            try
            {
                var url = $"{root.TrimEnd('/')}/stable/{current.Major}.{current.Minor}/update.json";
                var json = await httpClient.GetStringAsync(url, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ProgramUpdateManifest>(json)
                    ?? throw new InvalidDataException("The update manifest is empty.");
                return manifest.Version > current
                    ? new DesktopProgramUpdate(current, manifest.Version, manifest.Checksum, manifest.PackageUrls ?? [])
                    : null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                errors.Add(exception.Message);
            }
        }

        throw new HttpRequestException(
            $"Program update manifests could not be downloaded: {string.Join("; ", errors)}");
    }

    public async Task<string> DownloadAsync(
        DesktopProgramUpdate update,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var target = Path.Combine(PlaynitePaths.TempPath, "update.exe");
        var failures = new List<string>();
        foreach (var url in update.PackageUrls.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try
            {
                FileSystem.PrepareSaveFile(target);
                using var response = await httpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(target);
                var buffer = new byte[81920];
                long written = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    if (total > 0)
                    {
                        progress?.Report((int)Math.Min(100, written * 100 / total.Value));
                    }
                }

                if (!string.Equals(
                        FileSystem.GetMD5(target),
                        update.Checksum,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The downloaded updater checksum does not match the published manifest.");
                }

                progress?.Report(100);
                return target;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(exception.Message);
            }
        }

        throw new IOException($"The program update could not be downloaded: {string.Join("; ", failures)}");
    }

    private sealed class ProgramUpdateManifest
    {
        public Version Version { get; set; }
        public string Checksum { get; set; }
        public List<string> PackageUrls { get; set; }
    }
}
