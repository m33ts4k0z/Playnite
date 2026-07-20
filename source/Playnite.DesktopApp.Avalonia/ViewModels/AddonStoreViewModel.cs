using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Common;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AddonStoreViewModel : INotifyPropertyChanged
{
    private readonly DesktopSettings settings;
    private readonly IDesktopAddonStoreService service;
    private readonly Func<DesktopDialogService> getDialogs;
    private readonly Action settingsChanged;
    private readonly Action<string, bool> showMessage;
    private bool isVisible;
    private bool isBrowseSection = true;
    private bool isBusy;
    private bool restartRequired;
    private string searchText = string.Empty;
    private string statusText = "Browse Playnite add-ons.";
    private string failureText = string.Empty;
    private DesktopAddonStoreCategoryOption selectedCategory;
    private OnlineAddonItemViewModel selectedOnlineAddon;
    private InstalledAddonItemViewModel selectedInstalledAddon;

    public event PropertyChangedEventHandler PropertyChanged;

    public IReadOnlyList<DesktopAddonStoreCategoryOption> Categories { get; } =
    [
        new(DesktopAddonStoreCategory.Extensions, "LOCExtensions", "Extensions"),
        new(DesktopAddonStoreCategory.GameLibraries, "LOCLibraries", "Game libraries"),
        new(DesktopAddonStoreCategory.MetadataProviders, "LOCMetadataProviders", "Metadata providers"),
        new(DesktopAddonStoreCategory.Scripts, "LOCScripts", "Scripts"),
        new(DesktopAddonStoreCategory.Generators, string.Empty, "Generators"),
        new(DesktopAddonStoreCategory.DesktopThemes, "LOCAddonsThemesDesktop", "Desktop themes"),
        new(DesktopAddonStoreCategory.FullscreenThemes, "LOCAddonsThemesFullscren", "Fullscreen themes")
    ];

    public ObservableCollection<OnlineAddonItemViewModel> OnlineAddons { get; } = new();
    public ObservableCollection<InstalledAddonItemViewModel> InstalledAddons { get; } = new();

    public bool IsVisible
    {
        get => isVisible;
        private set => SetField(ref isVisible, value);
    }

    public bool IsBrowseSection
    {
        get => isBrowseSection;
        private set
        {
            if (SetField(ref isBrowseSection, value))
            {
                OnPropertyChanged(nameof(IsInstalledSection));
            }
        }
    }

    public bool IsInstalledSection => !IsBrowseSection;
    public bool IsBusy { get => isBusy; private set => SetField(ref isBusy, value); }
    public bool RestartRequired { get => restartRequired; private set => SetField(ref restartRequired, value); }
    public bool HasFailures => !string.IsNullOrWhiteSpace(FailureText);
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }
    public string FailureText
    {
        get => failureText;
        private set
        {
            if (SetField(ref failureText, value))
            {
                OnPropertyChanged(nameof(HasFailures));
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set => SetField(ref searchText, value ?? string.Empty);
    }

    public DesktopAddonStoreCategoryOption SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (SetField(ref selectedCategory, value) && IsVisible && IsBrowseSection)
            {
                RefreshCatalog();
            }
        }
    }

    public OnlineAddonItemViewModel SelectedOnlineAddon
    {
        get => selectedOnlineAddon;
        set
        {
            if (SetField(ref selectedOnlineAddon, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public InstalledAddonItemViewModel SelectedInstalledAddon
    {
        get => selectedInstalledAddon;
        set
        {
            if (SetField(ref selectedInstalledAddon, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand ShowBrowseCommand { get; }
    public ICommand ShowInstalledCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand OpenDataDirectoryCommand { get; }

    internal AddonStoreViewModel(
        DesktopSettings settings,
        IDesktopAddonStoreService service,
        Func<DesktopDialogService> getDialogs,
        Action settingsChanged,
        Action<string, bool> showMessage)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.getDialogs = getDialogs ?? throw new ArgumentNullException(nameof(getDialogs));
        this.settingsChanged = settingsChanged ?? throw new ArgumentNullException(nameof(settingsChanged));
        this.showMessage = showMessage ?? throw new ArgumentNullException(nameof(showMessage));
        selectedCategory = Categories[0];
        CloseCommand = new AppRelayCommand(Close);
        ShowBrowseCommand = new AppRelayCommand(() =>
        {
            IsBrowseSection = true;
            RefreshCatalog();
        });
        ShowInstalledCommand = new AppRelayCommand(() =>
        {
            IsBrowseSection = false;
            RefreshInstalled();
        });
        SearchCommand = new AppRelayCommand(RefreshCatalog, () => !IsBusy);
        InstallCommand = new AppRelayCommand(
            InstallSelected,
            () => !IsBusy && SelectedOnlineAddon?.SelectedPackage?.IsCompatible == true);
        UninstallCommand = new AppRelayCommand(
            UninstallSelected,
            () => !IsBusy && SelectedInstalledAddon?.CanUninstall == true);
        OpenDataDirectoryCommand = new AppRelayCommand(
            OpenDataDirectory,
            () => SelectedInstalledAddon?.IsPlugin == true);
    }

    public void Open()
    {
        IsVisible = true;
        IsBrowseSection = true;
        RefreshInstalled();
        RefreshCatalog();
    }

    public void Close()
    {
        IsVisible = false;
        IsBusy = false;
    }

    internal void RefreshInstalled()
    {
        var paths = (settings.DevelopmentExtensions ?? [])
            .Where(extension => extension?.IsEnabled == true && !string.IsNullOrWhiteSpace(extension.Path))
            .Select(extension => extension.Path)
            .ToList();
        var installed = service.DiscoverInstalled(paths);
        InstalledAddons.Clear();
        foreach (var addon in installed)
        {
            InstalledAddons.Add(new InstalledAddonItemViewModel(
                addon,
                settings,
                MarkSettingsChanged,
                item =>
                {
                    SelectedInstalledAddon = item;
                    UninstallSelected();
                },
                item =>
                {
                    SelectedInstalledAddon = item;
                    OpenDataDirectory();
                }));
        }

        SelectedInstalledAddon = InstalledAddons.FirstOrDefault();
        if (IsInstalledSection)
        {
            StatusText = $"{InstalledAddons.Count:N0} installed add-ons.";
        }
    }

    private void RefreshCatalog()
    {
        if (!IsVisible || IsBusy || SelectedCategory == null)
        {
            return;
        }

        var dialogs = getDialogs();
        if (dialogs == null)
        {
            StatusText = "The add-on catalog is unavailable until the main window is ready.";
            return;
        }

        IsBusy = true;
        RaiseCommandStates();
        DesktopAddonBrowseResult browseResult = null;
        var progressResult = dialogs.ActivateGlobalProgress(
            async args =>
            {
                args.Text = $"Loading {SelectedCategory.Name.ToLowerInvariant()}…";
                browseResult = await service.BrowseAsync(
                    SelectedCategory.Category,
                    SearchText,
                    OperatingSystem.IsWindows(),
                    args.CancelToken).ConfigureAwait(false);
            },
            new GlobalProgressOptions("Loading add-on catalog…", true));
        IsBusy = false;
        if (progressResult.Error != null)
        {
            ReportFailure("The add-on catalog could not be loaded", progressResult.Error);
            RaiseCommandStates();
            return;
        }

        if (progressResult.Canceled || browseResult == null)
        {
            StatusText = "Catalog loading canceled.";
            RaiseCommandStates();
            return;
        }

        OnlineAddons.Clear();
        foreach (var addon in browseResult.Items)
        {
            var item = new OnlineAddonItemViewModel(
                addon,
                OpenUrl,
                RaiseCommandStates,
                selected =>
                {
                    SelectedOnlineAddon = selected;
                    InstallSelected();
                });
            OnlineAddons.Add(item);
        }

        SelectedOnlineAddon = OnlineAddons.FirstOrDefault();
        FailureText = browseResult.Failures.Count == 0
            ? string.Empty
            : $"Skipped {browseResult.Failures.Count:N0} catalog entries:" + Environment.NewLine +
                string.Join(Environment.NewLine, browseResult.Failures.Select(failure => $"• {failure}"));
        StatusText = $"{OnlineAddons.Count:N0} add-ons loaded" +
            (browseResult.Failures.Count == 0 ? "." : $"; {browseResult.Failures.Count:N0} skipped.");
        RaiseCommandStates();
    }

    private void InstallSelected()
    {
        var item = SelectedOnlineAddon;
        var package = item?.SelectedPackage;
        var dialogs = getDialogs();
        if (item == null || package?.IsCompatible != true || dialogs == null)
        {
            return;
        }

        IsBusy = true;
        RaiseCommandStates();
        var result = dialogs.ActivateGlobalProgress(
            async args =>
            {
                args.Text = $"Downloading {item.Name} {package.Package.Version}…";
                await service.QueueInstallAsync(
                    item.Item,
                    package,
                    async (name, license) =>
                    {
                        dialogs.ShowSelectableString(
                            $"Review the license agreement for {name}.",
                            $"{name} license",
                            license);
                        return await Task.FromResult(dialogs.ShowMessage(
                            "Do you accept this add-on license agreement?",
                            $"Install {name}",
                            ["Accept", "Decline"],
                            1,
                            1) == "Accept");
                    },
                    args.CancelToken).ConfigureAwait(false);
            },
            new GlobalProgressOptions($"Installing {item.Name}…", true));
        IsBusy = false;
        if (result.Error is OperationCanceledException || result.Canceled)
        {
            StatusText = $"Installation of {item.Name} was canceled.";
        }
        else if (result.Error != null)
        {
            ReportFailure($"{item.Name} could not be installed", result.Error);
        }
        else
        {
            RestartRequired = true;
            item.Status = "Queued for restart";
            StatusText = $"{item.Name} is queued for installation. Restart Playnite to finish.";
        }

        RaiseCommandStates();
    }

    private void UninstallSelected()
    {
        var item = SelectedInstalledAddon;
        var dialogs = getDialogs();
        if (item?.CanUninstall != true || dialogs == null ||
            dialogs.ShowMessage(
                $"Uninstall {item.Name}? The add-on will be removed after Playnite restarts.",
                "Uninstall add-on",
                ["Uninstall", "Cancel"],
                1,
                1) != "Uninstall")
        {
            return;
        }

        try
        {
            service.QueueUninstall(item.Addon);
            item.Status = "Removal queued";
            RestartRequired = true;
            StatusText = $"{item.Name} is queued for removal. Restart Playnite to finish.";
        }
        catch (Exception exception)
        {
            ReportFailure($"{item.Name} could not be uninstalled", exception);
        }

        RaiseCommandStates();
    }

    private void OpenDataDirectory()
    {
        var item = SelectedInstalledAddon;
        if (item?.IsPlugin != true)
        {
            return;
        }

        try
        {
            var path = Path.Combine(PlaynitePaths.ExtensionsDataPath, Paths.GetSafePathName(item.Id));
            Directory.CreateDirectory(path);
            Explorer.OpenDirectory(path);
        }
        catch (Exception exception)
        {
            ReportFailure("The extension data directory could not be opened", exception);
        }
    }

    private void MarkSettingsChanged()
    {
        RestartRequired = true;
        settingsChanged();
        StatusText = "The add-on state will change after Playnite restarts.";
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            ProcessStarter.StartUrl(url);
        }
        catch (Exception exception)
        {
            ReportFailure("The link could not be opened", exception);
        }
    }

    private void ReportFailure(string message, Exception exception)
    {
        var detail = $"{message}: {exception.Message}";
        StatusText = detail;
        showMessage(detail, true);
    }

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)SearchCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)InstallCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)UninstallCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenDataDirectoryCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}

public sealed class OnlineAddonItemViewModel : INotifyPropertyChanged
{
    private readonly Action raiseCommandStates;
    private DesktopAddonPackageChoice selectedPackage;
    private string status = string.Empty;

    public event PropertyChangedEventHandler PropertyChanged;
    internal DesktopAddonCatalogItem Item { get; }
    public string Id => Item.Id;
    public string Name => Item.Name;
    public string Author => Item.Author;
    public string Description => Item.Description;
    public string ShortDescription => Item.ShortDescription;
    public string CommunityNote => Item.CommunityNote;
    public string IconUrl => Item.IconUrl;
    public string Type => Item.Type switch
    {
        AddonType.GameLibrary => DesktopAddonStoreLocalization.Resolve("LOCLibraries", "Game library"),
        AddonType.MetadataProvider => DesktopAddonStoreLocalization.Resolve(
            "LOCMetadataProviders", "Metadata provider"),
        AddonType.Generic => DesktopAddonStoreLocalization.Resolve("LOCExtensionGeneric", "Extension"),
        AddonType.ThemeDesktop => DesktopAddonStoreLocalization.Resolve(
            "LOCAddonsThemesDesktop", "Desktop theme"),
        AddonType.ThemeFullscreen => DesktopAddonStoreLocalization.Resolve(
            "LOCAddonsThemesFullscren", "Fullscreen theme"),
        _ => Item.Type.ToString()
    };
    public string LatestVersion => Item.LatestVersion;
    public bool IsCompatible => Item.IsCompatible;
    public bool CanInstall => SelectedPackage?.IsCompatible == true;
    public string CompatibilityText => Item.CompatibilityText;
    public double Opacity => IsCompatible ? 1 : 0.55;
    public IReadOnlyList<DesktopAddonPackageChoice> Packages => Item.Packages;
    public ObservableCollection<AddonStoreLinkItem> Links { get; } = new();
    public ObservableCollection<AddonStoreScreenshotItem> Screenshots { get; } = new();
    public ICommand InstallCommand { get; }
    public bool HasLinks => Links.Count > 0;
    public bool HasScreenshots => Screenshots.Count > 0;
    public bool HasCommunityNote => !string.IsNullOrWhiteSpace(CommunityNote);

    public DesktopAddonPackageChoice SelectedPackage
    {
        get => selectedPackage;
        set
        {
            if (!ReferenceEquals(selectedPackage, value))
            {
                selectedPackage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPackage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanInstall)));
                raiseCommandStates();
            }
        }
    }

    public string Status
    {
        get => status;
        set
        {
            if (!string.Equals(status, value, StringComparison.Ordinal))
            {
                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
    }

    internal OnlineAddonItemViewModel(
        DesktopAddonCatalogItem item,
        Action<string> openUrl,
        Action raiseCommandStates,
        Action<OnlineAddonItemViewModel> install)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        this.raiseCommandStates = raiseCommandStates ?? throw new ArgumentNullException(nameof(raiseCommandStates));
        InstallCommand = new AppRelayCommand(() => install(this));
        selectedPackage = Packages.FirstOrDefault(package => package.IsCompatible) ?? Packages.FirstOrDefault();
        foreach (var link in item.Manifest.Links ?? new Dictionary<string, string>())
        {
            Links.Add(new AddonStoreLinkItem(link.Key, link.Value, openUrl));
        }

        var index = 1;
        foreach (var screenshot in item.Manifest.Screenshots ?? [])
        {
            if (!string.IsNullOrWhiteSpace(screenshot.Image) || !string.IsNullOrWhiteSpace(screenshot.Thumbnail))
            {
                Screenshots.Add(new AddonStoreScreenshotItem(
                    $"Screenshot {index++}",
                    screenshot.Thumbnail ?? screenshot.Image,
                    screenshot.Image ?? screenshot.Thumbnail,
                    openUrl));
            }
        }
    }
}

public sealed class InstalledAddonItemViewModel : INotifyPropertyChanged
{
    private readonly DesktopSettings settings;
    private readonly Action changed;
    private bool isEnabled;
    private string status = string.Empty;

    public event PropertyChangedEventHandler PropertyChanged;
    internal DesktopInstalledAddon Addon { get; }
    public string Id => Addon.Id;
    public string Name => Addon.Name;
    public string Version => Addon.Version;
    public string Type => Addon.Type;
    public bool IsPlugin => Addon.IsPlugin;
    public bool IsDevelopment => Addon.IsDevelopment;
    public bool CanUninstall => Addon.CanUninstall;
    public string UninstallReason => Addon.UninstallReason;
    public bool CanToggle => IsPlugin;
    public ICommand UninstallCommand { get; }
    public ICommand OpenDataDirectoryCommand { get; }

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled == value || !CanToggle)
            {
                return;
            }

            isEnabled = value;
            if (value)
            {
                settings.DisabledPlugins.RemoveAll(id =>
                    string.Equals(id, Id, StringComparison.OrdinalIgnoreCase));
            }
            else if (!settings.DisabledPlugins.Contains(Id, StringComparer.OrdinalIgnoreCase))
            {
                settings.DisabledPlugins.Add(Id);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            changed();
        }
    }

    public string Status
    {
        get => status;
        set
        {
            if (!string.Equals(status, value, StringComparison.Ordinal))
            {
                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
    }

    internal InstalledAddonItemViewModel(
        DesktopInstalledAddon addon,
        DesktopSettings settings,
        Action changed,
        Action<InstalledAddonItemViewModel> uninstall,
        Action<InstalledAddonItemViewModel> openDataDirectory)
    {
        Addon = addon ?? throw new ArgumentNullException(nameof(addon));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
        UninstallCommand = new AppRelayCommand(() => uninstall(this));
        OpenDataDirectoryCommand = new AppRelayCommand(() => openDataDirectory(this));
        isEnabled = !settings.DisabledPlugins.Contains(Id, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class AddonStoreLinkItem
{
    public string Name { get; }
    public string Url { get; }
    public ICommand OpenCommand { get; }

    public AddonStoreLinkItem(string name, string url, Action<string> openUrl)
    {
        Name = string.IsNullOrWhiteSpace(name) ? url : name;
        Url = url;
        OpenCommand = new AppRelayCommand(() => openUrl(url));
    }
}

public sealed class AddonStoreScreenshotItem
{
    public string Name { get; }
    public string Thumbnail { get; }
    public string Image { get; }
    public ICommand OpenCommand { get; }

    public AddonStoreScreenshotItem(
        string name,
        string thumbnail,
        string image,
        Action<string> openUrl)
    {
        Name = name;
        Thumbnail = thumbnail;
        Image = image;
        OpenCommand = new AppRelayCommand(() => openUrl(image));
    }
}
