using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopRestartRequest : EventArgs
{
    public bool SafeMode { get; }
    public IReadOnlyList<string> ExtraArguments { get; }

    public DesktopRestartRequest(bool safeMode, IReadOnlyList<string> extraArguments = null)
    {
        SafeMode = safeMode;
        ExtraArguments = extraArguments ?? Array.Empty<string>();
    }
}

public sealed partial class DesktopAppViewModel
{
    private bool isAboutVisible;
    private bool isExplorerVisible;
    private bool isFirstTimeWizardVisible;
    private string explorerSearchText = string.Empty;
    private LanguageOption selectedWizardLanguage;

    public event EventHandler ExitRequested;
    public event EventHandler<DesktopRestartRequest> RestartRequested;
    public event EventHandler InteractivePowerShellRequested;

    public DesktopToolsConfigViewModel ToolsConfig { get; private set; }
    public bool IsAboutVisible { get => isAboutVisible; private set => SetField(ref isAboutVisible, value); }
    public bool IsExplorerVisible { get => isExplorerVisible; private set => SetField(ref isExplorerVisible, value); }
    public bool IsFirstTimeWizardVisible
    {
        get => isFirstTimeWizardVisible;
        private set => SetField(ref isFirstTimeWizardVisible, value);
    }

    public string AboutVersion => CoreRuntime.ApplicationVersion().ToString();
    public string AboutRuntime => $".NET {Environment.Version} · Avalonia {typeof(global::Avalonia.Application).Assembly.GetName().Version}";
    public string AboutSdkVersions =>
        $"SDK v6 {typeof(IPlayniteAPI).Assembly.GetName().Version} · SDK v7 {runtimeHost?.V7SdkVersion ?? new Version(7, 0, 0)}";
    public IReadOnlyList<LanguageOption> WizardLanguages { get; private set; } = Array.Empty<LanguageOption>();
    public LanguageOption SelectedWizardLanguage
    {
        get => selectedWizardLanguage;
        set => SetField(ref selectedWizardLanguage, value);
    }

    public ObservableCollection<DesktopExplorerItem> ExplorerItems { get; } = new();
    public string ExplorerSearchText
    {
        get => explorerSearchText;
        set
        {
            if (SetField(ref explorerSearchText, value ?? string.Empty))
            {
                RefreshExplorerItems();
            }
        }
    }

    public bool IsGlobalProgressVisible =>
        MetadataDownload.IsRunning || LibrarySync.IsRunning || InstalledGameImport.IsRunning ||
        PluginSettings.IsRunning || Backups.IsRunning || Updates.IsChecking || Updates.IsInstalling;
    public string GlobalProgressText => MetadataDownload.IsRunning
        ? MetadataDownload.ProgressText
        : LibrarySync.IsRunning
            ? LibrarySync.ProgressText
            : InstalledGameImport.IsRunning
                ? InstalledGameImport.ProgressText
                : PluginSettings.IsRunning
                    ? PluginSettings.StatusText
                    : Backups.IsRunning
                        ? Backups.StatusText
                        : Updates.StatusText;
    public int GlobalProgressValue => InstalledGameImport.IsRunning ? InstalledGameImport.ProgressValue : 0;
    public int GlobalProgressMaximum => InstalledGameImport.IsRunning ? InstalledGameImport.ProgressTotal : 0;
    public bool IsGlobalProgressIndeterminate => GlobalProgressMaximum <= 0;
    public bool CanCancelGlobalProgress =>
        MetadataDownload.IsRunning || LibrarySync.IsRunning || InstalledGameImport.IsRunning;
    public bool HasProgramUpdate => Updates.HasProgramUpdate;
    public IReadOnlyList<DesktopDetailsFilterLink> DetailsFilterLinks => BuildDetailsFilterLinks();
    internal IReadOnlyList<LibraryPlugin> LibraryClients =>
        runtimeHost?.LibraryPlugins ?? Array.Empty<LibraryPlugin>();
    internal IReadOnlyList<AppSoftware> SoftwareTools => database?.SoftwareApps
        .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList() ?? new List<AppSoftware>();
    internal string ResolveDatabaseFile(string path) => string.IsNullOrWhiteSpace(path)
        ? null
        : database?.GetFullFilePath(path);
    internal void StartSoftwareTool(AppSoftware app) => runtimeHost?.StartSoftwareTool(app);

    public ICommand OpenToolsConfigCommand { get; private set; }
    public ICommand RunSoftwareToolCommand { get; private set; }
    public ICommand OpenAboutCommand { get; private set; }
    public ICommand OpenExplorerCommand { get; private set; }
    public ICommand SelectRandomGameCommand { get; private set; }
    public ICommand SelectRandomFilteredGameCommand { get; private set; }
    public ICommand ClearNotificationsCommand { get; private set; }
    public ICommand CancelGlobalProgressCommand { get; private set; }
    public ICommand CheckUpdatesCommand { get; private set; }
    public ICommand InstallProgramUpdateCommand { get; private set; }
    public ICommand BackupDataCommand { get; private set; }
    public ICommand RestoreDataBackupCommand { get; private set; }
    public ICommand OpenHelpCommand { get; private set; }
    public ICommand OpenHomepageCommand { get; private set; }
    public ICommand OpenSourceCommand { get; private set; }
    public ICommand OpenIssuesCommand { get; private set; }
    public ICommand OpenLicenseCommand { get; private set; }
    public ICommand CreateDiagnosticPackageCommand { get; private set; }
    public ICommand RestartCommand { get; private set; }
    public ICommand RestartSafeModeCommand { get; private set; }
    public ICommand ExitApplicationCommand { get; private set; }
    public ICommand OpenInteractivePowerShellCommand { get; private set; }
    public ICommand ReloadScriptsCommand { get; private set; }
    public ICommand FinishFirstTimeWizardCommand { get; private set; }
    public ICommand WizardOpenAddonStoreCommand { get; private set; }
    public ICommand WizardImportInstalledCommand { get; private set; }
    public ICommand ApplyDetailsFilterCommand { get; private set; }
    public ICommand SetCompletionStatusCommand { get; private set; }

    private void InitializeChromeParity()
    {
        WizardLanguages = LanguageCatalog.Discover(Path.Combine(AppContext.BaseDirectory, "Localization"));
        selectedWizardLanguage = WizardLanguages.FirstOrDefault(language =>
            string.Equals(language.Id, settings.Language, StringComparison.OrdinalIgnoreCase)) ??
            WizardLanguages.FirstOrDefault();
        ToolsConfig = new DesktopToolsConfigViewModel(database, () => dialogService, () =>
        {
            OnPropertyChanged(nameof(ToolsConfig));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        });
        ToolsConfig.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DesktopToolsConfigViewModel.IsVisible))
            {
                RaiseGameCommandStates();
                (OpenToolsConfigCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
            }
        };

        OpenToolsConfigCommand = new AppRelayCommand(() =>
        {
            CloseOverlays();
            ToolsConfig.Open();
        }, () => database != null && !ToolsConfig.IsVisible);
        RunSoftwareToolCommand = new AppRelayCommand(parameter =>
        {
            if (parameter is AppSoftware app)
            {
                runtimeHost?.StartSoftwareTool(app);
            }
        });
        OpenAboutCommand = new AppRelayCommand(() =>
        {
            CloseOverlays();
            IsAboutVisible = true;
        });
        OpenExplorerCommand = new AppRelayCommand(() =>
        {
            CloseOverlays();
            RefreshExplorerItems();
            IsExplorerVisible = true;
        }, () => database != null);
        SelectRandomGameCommand = new AppRelayCommand(() => SelectRandom(allGames));
        SelectRandomFilteredGameCommand = new AppRelayCommand(() => SelectRandom(Games));
        ClearNotificationsCommand = new AppRelayCommand(
            () => runtimeHost?.Notifications.RemoveAll(),
            () => NotificationCount > 0);
        CancelGlobalProgressCommand = new AppRelayCommand(CancelGlobalProgress, () => CanCancelGlobalProgress);
        CheckUpdatesCommand = new AppRelayCommand(async () => await CheckUpdatesAsync());
        InstallProgramUpdateCommand = new AppRelayCommand(
            async () => await Updates.InstallProgramUpdateAsync(),
            () => HasProgramUpdate && !Updates.IsInstalling);
        BackupDataCommand = new AppRelayCommand(CreateManualBackup, () => dialogService != null);
        RestoreDataBackupCommand = new AppRelayCommand(RestoreManualBackup, () => dialogService != null);
        OpenHelpCommand = new AppRelayCommand(() => OpenUrl(global::Playnite.UrlConstants.Wiki));
        OpenHomepageCommand = new AppRelayCommand(() => OpenUrl("https://playnite.link/"));
        OpenSourceCommand = new AppRelayCommand(() => OpenUrl("https://github.com/JosefNemec/Playnite"));
        OpenIssuesCommand = new AppRelayCommand(() => OpenUrl(global::Playnite.UrlConstants.Issues));
        OpenLicenseCommand = new AppRelayCommand(OpenLicense);
        CreateDiagnosticPackageCommand = new AppRelayCommand(async () => await CreateDiagnosticPackageAsync());
        RestartCommand = new AppRelayCommand(() => RestartRequested?.Invoke(this, new DesktopRestartRequest(false)));
        RestartSafeModeCommand = new AppRelayCommand(() => RestartRequested?.Invoke(this, new DesktopRestartRequest(true)));
        ExitApplicationCommand = new AppRelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        OpenInteractivePowerShellCommand = new AppRelayCommand(() =>
            InteractivePowerShellRequested?.Invoke(this, EventArgs.Empty));
        ReloadScriptsCommand = new AppRelayCommand(() =>
        {
            runtimeHost?.ReloadScripts();
            RefreshPluginSurfaces();
            StatusText = Localize("LOCScriptsReloaded", "Extension scripts reloaded.");
        }, () => runtimeHost != null);
        FinishFirstTimeWizardCommand = new AppRelayCommand(FinishFirstTimeWizard);
        WizardOpenAddonStoreCommand = new AppRelayCommand(() =>
        {
            IsFirstTimeWizardVisible = false;
            OpenAddonStore();
        });
        WizardImportInstalledCommand = new AppRelayCommand(() =>
        {
            IsFirstTimeWizardVisible = false;
            OpenInstalledGameImport();
        });
        ApplyDetailsFilterCommand = new AppRelayCommand(parameter =>
        {
            if (parameter is DesktopDetailsFilterLink link)
            {
                ApplyDetailsFilter(link);
            }
        });
        SetCompletionStatusCommand = new AppRelayCommand(SetCompletionStatus, () => SelectedGame != null);

        Updates.PropertyChanged += ChromeTask_PropertyChanged;
        Backups.PropertyChanged += ChromeTask_PropertyChanged;
    }

    public void ShowFirstTimeWizard()
    {
        if (!settings.FirstTimeWizardComplete)
        {
            CloseOverlays();
            IsFirstTimeWizardVisible = true;
        }
    }

    internal void CloseChromeParityOverlays()
    {
        IsAboutVisible = false;
        IsExplorerVisible = false;
        IsFirstTimeWizardVisible = false;
        ToolsConfig?.Close();
    }

    internal bool ImportDroppedGame(string path)
    {
        if (database == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (extension is not null && !new[]
            {
                ".exe", ".lnk", ".url", ".bat", ".AppImage", ".appimage", ".sh", ".desktop"
            }
            .Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var game = global::Playnite.GameExtensions.GetGameFromExecutable(path);
        var icon = game.Icon;
        game.Icon = null;
        icon ??= game.GetRawExecutablePath();
        if (!string.IsNullOrWhiteSpace(icon) && File.Exists(icon))
        {
            game.Icon = database.AddFile(icon, game.Id, true, CancellationToken.None);
        }

        database.Games.Add(game);
        SynchronizeLibrary();
        OpenGameEditor(game.Id, saved =>
        {
            if (saved != true)
            {
                database.Games.Remove(game);
                SynchronizeLibrary();
            }
            else
            {
                SelectGame(game.Id);
            }
        });
        return true;
    }

    internal bool ImportDroppedFolder(string path)
    {
        if (database == null || !Directory.Exists(path) || !InstalledGameImport.Open())
        {
            return false;
        }

        InstalledGameImport.FolderPath = path;
        if (InstalledGameImport.ScanFolderCommand.CanExecute(null))
        {
            InstalledGameImport.ScanFolderCommand.Execute(null);
        }
        return true;
    }

    private void SelectRandom(IReadOnlyCollection<DesktopGameItemViewModel> source)
    {
        if (source.Count == 0)
        {
            StatusText = Localize("LOCNoGamesFound", "No games match the current view.");
            return;
        }

        SelectedGame = source.ElementAt(Random.Shared.Next(source.Count));
        StatusText = string.Format(
            Localize("LOCRandomGameSelected", "Random game selected: {0}"),
            SelectedGame.Name);
    }

    private void RefreshExplorerItems()
    {
        ExplorerItems.Clear();
        var query = ExplorerSearchText.Trim();
        foreach (var group in FilterGroups)
        {
            foreach (var option in group.Options)
            {
                if (query.Length > 0 &&
                    !group.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                    !option.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                ExplorerItems.Add(new DesktopExplorerItem(group.Title, option));
            }
        }
    }

    private void FinishFirstTimeWizard()
    {
        if (SelectedWizardLanguage != null)
        {
            settings.Language = SelectedWizardLanguage.Id;
        }
        settings.FirstTimeWizardComplete = true;
        IsFirstTimeWizardVisible = false;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        StatusText = Localize("LOCSetupComplete", "Initial setup is complete.");
    }

    private void CancelGlobalProgress()
    {
        if (MetadataDownload.IsRunning)
        {
            MetadataDownload.Cancel();
        }
        if (LibrarySync.IsRunning)
        {
            LibrarySync.Cancel();
        }
        if (InstalledGameImport.IsRunning)
        {
            InstalledGameImport.Cancel();
        }
    }

    private async Task CheckUpdatesAsync()
    {
        await Updates.CheckProgramAsync();
        await Updates.CheckAddonsAsync();
    }

    private void CreateManualBackup()
    {
        var output = dialogService.SaveFile("Playnite backup|*.zip", true);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        var optionalItems = new[]
        {
            global::Playnite.BackupDataItem.LibraryFiles,
            global::Playnite.BackupDataItem.Extensions,
            global::Playnite.BackupDataItem.ExtensionsData,
            global::Playnite.BackupDataItem.Themes
        };
        var selection = dialogService.SelectMultiple(
            Localize("LOCMenuBackupData", "Back up Playnite data"),
            Localize("LOCBackupDataBackupMessage", "Choose the optional data to include."),
            optionalItems.Select(item => new AvaloniaSelectionItem<global::Playnite.BackupDataItem>(
                BackupItemName(item), item, selected: true)).ToList());
        if (!selection.Confirmed)
        {
            return;
        }

        var options = new global::Playnite.BackupOptions
        {
            OutputFile = output,
            DataDir = global::Playnite.PlaynitePaths.ConfigRootPath,
            LibraryDir = database.DatabasePath,
            BackupItems = new[]
            {
                global::Playnite.BackupDataItem.Settings,
                global::Playnite.BackupDataItem.Library
            }.Concat(selection.SelectedItems).Distinct().ToList()
        };
        FileSystem.WriteStringToFile(
            global::Playnite.PlaynitePaths.BackupActionFile,
            Serialization.ToJson(options));
        RestartRequested?.Invoke(this, new DesktopRestartRequest(
            false,
            new[] { "--backup", global::Playnite.PlaynitePaths.BackupActionFile }));
    }

    private void RestoreManualBackup()
    {
        var backupFile = dialogService.SelectFiles("Playnite backup|*.zip", false).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(backupFile))
        {
            return;
        }

        var available = global::Playnite.Backup.GetRestoreSelections(backupFile);
        var selection = dialogService.SelectMultiple(
            Localize("LOCMenuRestoreBackup", "Restore backup"),
            Localize("LOCBackupRestoreMessage", "Choose the data to restore."),
            available.Select(item => new AvaloniaSelectionItem<global::Playnite.BackupDataItem>(
                BackupItemName(item), item, selected: true)).ToList());
        if (!selection.Confirmed)
        {
            return;
        }

        var options = new global::Playnite.BackupRestoreOptions
        {
            BackupFile = backupFile,
            DataDir = global::Playnite.PlaynitePaths.ConfigRootPath,
            LibraryDir = database.DatabasePath,
            RestoreItems = selection.SelectedItems.ToList(),
            RestoreLibrarySettingsPath = settings.DatabasePath
        };
        FileSystem.WriteStringToFile(
            global::Playnite.PlaynitePaths.RestoreBackupActionFile,
            Serialization.ToJson(options));
        RestartRequested?.Invoke(this, new DesktopRestartRequest(
            false,
            new[] { "--restore-backup", global::Playnite.PlaynitePaths.RestoreBackupActionFile }));
    }

    private static string BackupItemName(global::Playnite.BackupDataItem item) => item switch
    {
        global::Playnite.BackupDataItem.Settings => Localize("LOCBackupOptionSettings", "Settings"),
        global::Playnite.BackupDataItem.Library => Localize("LOCBackupOptionLibrary", "Library database"),
        global::Playnite.BackupDataItem.LibraryFiles => Localize("LOCBackupOptionGameMedia", "Game media"),
        global::Playnite.BackupDataItem.Extensions => Localize("LOCBackupOptionExtensions", "Extensions"),
        global::Playnite.BackupDataItem.ExtensionsData => Localize("LOCBackupOptionExtensionsData", "Extension data"),
        global::Playnite.BackupDataItem.Themes => Localize("LOCBackupOptionThemes", "Themes"),
        _ => item.ToString()
    };

    private async Task CreateDiagnosticPackageAsync()
    {
        var output = dialogService?.SaveFile("Diagnostic package|*.zip", true);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        StatusText = Localize("LOCCreatingDiagnosticPackage", "Creating diagnostic package…");
        await Task.Run(() => global::Playnite.Diagnostic.CreateDiagPackage(
            output,
            "Created from the Avalonia Desktop About screen.",
            new global::Playnite.DiagnosticPackageInfo
            {
                PlayniteVersion = AboutVersion,
                IsCrashPackage = false
            }));
        StatusText = string.Format(
            Localize("LOCDiagnosticPackageCreated", "Diagnostic package created at {0}"),
            output);
    }

    private void OpenLicense()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "license.txt"),
            Path.Combine(AppContext.BaseDirectory, "LICENSE.md")
        };
        var path = candidates.FirstOrDefault(File.Exists);
        dialogService?.ShowSelectableString(
            Localize("LOCLicense", "License"),
            "Playnite",
            path == null ? "Playnite is distributed under the MIT license." : File.ReadAllText(path));
    }

    private static void OpenUrl(string url) => ProcessStarter.StartUrl(url);

    private IReadOnlyList<DesktopDetailsFilterLink> BuildDetailsFilterLinks()
    {
        var game = SelectedGame?.Game;
        if (game == null || database == null)
        {
            return Array.Empty<DesktopDetailsFilterLink>();
        }

        var links = new List<DesktopDetailsFilterLink>();
        AddDetailsLinks(links, nameof(FilterPresetSettings.Platform), game.PlatformIds, id => database.Platforms[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.Genre), game.GenreIds, id => database.Genres[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.Category), game.CategoryIds, id => database.Categories[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.Tag), game.TagIds, id => database.Tags[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.Feature), game.FeatureIds, id => database.Features[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.Series), game.SeriesIds, id => database.Series[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.Region), game.RegionIds, id => database.Regions[id]?.Name);
        AddDetailsLinks(links, nameof(FilterPresetSettings.AgeRating), game.AgeRatingIds, id => database.AgeRatings[id]?.Name);
        return links;
    }

    private void AddDetailsLinks(
        ICollection<DesktopDetailsFilterLink> target,
        string field,
        IEnumerable<Guid> ids,
        Func<Guid, string> resolve)
    {
        foreach (var id in ids ?? Array.Empty<Guid>())
        {
            var name = resolve(id);
            if (!string.IsNullOrWhiteSpace(name))
            {
                target.Add(new DesktopDetailsFilterLink(field, id, name, ApplyDetailsFilter));
            }
        }
    }

    private void ApplyDetailsFilter(DesktopDetailsFilterLink link)
    {
        var group = FilterGroups.FirstOrDefault(group => group.Field == link.Field);
        var option = group?.Options.FirstOrDefault(option => Equals(option.Value, link.Value));
        if (option == null)
        {
            return;
        }

        option.IsSelected = true;
        IsFilterPanelVisible = true;
    }

    private void SetCompletionStatus()
    {
        var game = SelectedGame?.Game;
        if (game == null || database == null || dialogService == null)
        {
            return;
        }

        var options = database.CompletionStatuses
            .OrderBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(status => new AvaloniaSelectionItem<Guid>(
                status.Name,
                status.Id,
                selected: status.Id == game.CompletionStatusId))
            .ToList();
        var result = dialogService.SelectSingle(
            Localize("LOCCompletionStatus", "Completion status"),
            game.Name,
            options);
        if (!result.Confirmed)
        {
            return;
        }

        game.CompletionStatusId = result.SelectedItem;
        database.Games.Update(game);
        RefreshGame(game.Id);
    }

    private void ChromeTask_PropertyChanged(object sender, PropertyChangedEventArgs e) =>
        RaiseGlobalProgressProperties();

    private void RaiseGlobalProgressProperties()
    {
        OnPropertyChanged(nameof(IsGlobalProgressVisible));
        OnPropertyChanged(nameof(GlobalProgressText));
        OnPropertyChanged(nameof(GlobalProgressValue));
        OnPropertyChanged(nameof(GlobalProgressMaximum));
        OnPropertyChanged(nameof(IsGlobalProgressIndeterminate));
        OnPropertyChanged(nameof(CanCancelGlobalProgress));
        OnPropertyChanged(nameof(HasProgramUpdate));
        (CancelGlobalProgressCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (InstallProgramUpdateCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
    }
}

public sealed class DesktopExplorerItem
{
    private readonly DesktopFilterOptionViewModel option;
    public string Field { get; }
    public string Name => option.Name;
    public int Count => option.Count;
    public string CountText => $"{Count:N0} games";
    public bool IsSelected { get => option.IsSelected; set => option.IsSelected = value; }

    public DesktopExplorerItem(string field, DesktopFilterOptionViewModel option)
    {
        Field = field;
        this.option = option;
    }
}

public sealed class DesktopDetailsFilterLink
{
    public string Field { get; }
    public object Value { get; }
    public string Name { get; }
    public ICommand Command { get; }

    public DesktopDetailsFilterLink(
        string field,
        object value,
        string name,
        Action<DesktopDetailsFilterLink> activate)
    {
        Field = field;
        Value = value;
        Name = name;
        Command = new AppRelayCommand(() => activate(this));
    }
}
