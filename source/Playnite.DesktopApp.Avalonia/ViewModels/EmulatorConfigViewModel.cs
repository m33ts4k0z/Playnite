using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Common;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class EmulatorConfigViewModel : INotifyPropertyChanged
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly GameDatabase database;
    private readonly Func<DesktopDialogService> dialogs;
    private readonly Action<string, bool> showMessage;
    private ObservableCollection<Emulator> editingEmulators = new();
    private ObservableCollection<GameScannerConfig> editingScanners = new();
    private ObservableCollection<ScannedEmulator> detectedEmulators = new();
    private IReadOnlyList<ScannedEmulator> visibleDetectedEmulators = Array.Empty<ScannedEmulator>();
    private Emulator selectedEmulator;
    private EmulatorProfile selectedProfile;
    private GameScannerConfig selectedScanner;
    private EmulatorDefinition selectedDefinition;
    private string selectedBuiltInProfileName;
    private Emulator selectedScannerEmulator;
    private EmulatorProfile selectedScannerProfile;
    private Platform selectedScannerOverridePlatform;
    private bool hideAlreadyImported = true;
    private bool isVisible;
    private string selectedPage = EmulatorConfigPage.Emulators;
    private string globalCrcExcludeFileTypesText = string.Empty;
    private string statusText = string.Empty;
    private string originalState;

    public event PropertyChangedEventHandler PropertyChanged;

    public static class EmulatorConfigPage
    {
        public const string Emulators = "Emulators";
        public const string Scanners = "Scanners";
        public const string Detect = "Auto-detect";
        public const string Downloads = "Downloads";
    }

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
    public string SelectedPage { get => selectedPage; private set => SetField(ref selectedPage, value); }
    public bool IsEmulatorsPage => SelectedPage == EmulatorConfigPage.Emulators;
    public bool IsScannersPage => SelectedPage == EmulatorConfigPage.Scanners;
    public bool IsDetectPage => SelectedPage == EmulatorConfigPage.Detect;
    public bool IsDownloadsPage => SelectedPage == EmulatorConfigPage.Downloads;
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }
    public string PlatformNote => OperatingSystem.IsWindows()
        ? "Built-in profiles and automatic detection use Playnite's bundled emulator definitions."
        : "Built-in definitions remain available on this platform. Use custom profiles when a definition targets a Windows executable.";

    public ObservableCollection<Emulator> EditingEmulators
    {
        get => editingEmulators;
        private set => SetField(ref editingEmulators, value);
    }

    public ObservableCollection<GameScannerConfig> EditingScanners
    {
        get => editingScanners;
        private set => SetField(ref editingScanners, value);
    }

    public ObservableCollection<ScannedEmulator> DetectedEmulators
    {
        get => detectedEmulators;
        private set => SetField(ref detectedEmulators, value);
    }

    public IReadOnlyList<ScannedEmulator> VisibleDetectedEmulators
    {
        get => visibleDetectedEmulators;
        private set => SetField(ref visibleDetectedEmulators, value);
    }

    public IReadOnlyList<EmulatorDefinition> EmulatorDefinitions { get; }
    public ObservableCollection<EmulatorPlatformOption> CustomProfilePlatforms { get; } = new();
    public ObservableCollection<EmulatorProfile> ScannerProfiles { get; } = new();
    public IReadOnlyList<Platform> Platforms { get; }
    public IReadOnlyList<TrackingMode> TrackingModes { get; } = Enum.GetValues<TrackingMode>();
    public IReadOnlyList<ScannerConfigPlayActionSettings> PlayActionSettings { get; } =
        Enum.GetValues<ScannerConfigPlayActionSettings>();
    public IReadOnlyList<EmulatorDownloadOption> DownloadOptions { get; }

    public Emulator SelectedEmulator
    {
        get => selectedEmulator;
        set
        {
            if (!SetField(ref selectedEmulator, value))
            {
                return;
            }

            SelectedDefinition = EmulatorDefinitions.FirstOrDefault(definition =>
                string.Equals(definition.Id, value?.BuiltInConfigId, StringComparison.Ordinal));
            SelectedProfile = value?.AllProfiles.FirstOrDefault();
            OnPropertyChanged(nameof(AvailableBuiltInProfileNames));
        }
    }

    public EmulatorProfile SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (SetField(ref selectedProfile, value))
            {
                RebuildCustomProfilePlatforms();
                OnPropertyChanged(nameof(SelectedCustomProfile));
                OnPropertyChanged(nameof(SelectedBuiltInProfile));
                OnPropertyChanged(nameof(IsCustomProfileSelected));
                OnPropertyChanged(nameof(IsBuiltInProfileSelected));
                OnPropertyChanged(nameof(SelectedBuiltInProfilePlatforms));
                OnPropertyChanged(nameof(CustomProfileImageExtensionsText));
            }
        }
    }

    public CustomEmulatorProfile SelectedCustomProfile => SelectedProfile as CustomEmulatorProfile;
    public BuiltInEmulatorProfile SelectedBuiltInProfile => SelectedProfile as BuiltInEmulatorProfile;
    public bool IsCustomProfileSelected => SelectedCustomProfile != null;
    public bool IsBuiltInProfileSelected => SelectedBuiltInProfile != null;

    public EmulatorDefinition SelectedDefinition
    {
        get => selectedDefinition;
        set
        {
            if (!SetField(ref selectedDefinition, value))
            {
                return;
            }

            if (SelectedEmulator != null)
            {
                SelectedEmulator.BuiltInConfigId = value?.Id;
            }
            SelectedBuiltInProfileName = AvailableBuiltInProfileNames.FirstOrDefault();
            OnPropertyChanged(nameof(AvailableBuiltInProfileNames));
        }
    }

    public IReadOnlyList<string> AvailableBuiltInProfileNames =>
        SelectedDefinition?.Profiles?.Select(profile => profile.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<string>();

    public string SelectedBuiltInProfileName
    {
        get => selectedBuiltInProfileName;
        set => SetField(ref selectedBuiltInProfileName, value);
    }

    public string SelectedBuiltInProfilePlatforms
    {
        get
        {
            var profile = SelectedBuiltInProfile == null || SelectedEmulator == null
                ? null
                : Emulation.GetProfile(
                    SelectedEmulator.BuiltInConfigId,
                    SelectedBuiltInProfile.BuiltInProfileName);
            return profile?.Platforms == null
                ? string.Empty
                : string.Join(", ", profile.Platforms
                    .Select(id => Emulation.GetPlatform(id)?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
        }
    }

    public string CustomProfileImageExtensionsText
    {
        get => JoinList(SelectedCustomProfile?.ImageExtensions);
        set
        {
            if (SelectedCustomProfile != null)
            {
                SelectedCustomProfile.ImageExtensions = ParseList(value)
                    .Select(extension => extension.TrimStart('.', '*'))
                    .Where(extension => extension.Length > 0)
                    .ToList();
            }
        }
    }

    public GameScannerConfig SelectedScanner
    {
        get => selectedScanner;
        set
        {
            if (!SetField(ref selectedScanner, value))
            {
                return;
            }

            selectedScannerEmulator = EditingEmulators.FirstOrDefault(emulator => emulator.Id == value?.EmulatorId);
            OnPropertyChanged(nameof(SelectedScannerEmulator));
            RebuildScannerProfiles();
            selectedScannerProfile = ScannerProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, value?.EmulatorProfileId, StringComparison.Ordinal));
            OnPropertyChanged(nameof(SelectedScannerProfile));
            selectedScannerOverridePlatform = Platforms.FirstOrDefault(platform => platform.Id == value?.OverridePlatformId) ??
                Platforms.FirstOrDefault(platform => platform.Id == Guid.Empty);
            OnPropertyChanged(nameof(SelectedScannerOverridePlatform));
            RaiseScannerTextProperties();
        }
    }

    public Emulator SelectedScannerEmulator
    {
        get => selectedScannerEmulator;
        set
        {
            if (!SetField(ref selectedScannerEmulator, value))
            {
                return;
            }

            if (SelectedScanner != null)
            {
                SelectedScanner.EmulatorId = value?.Id ?? Guid.Empty;
                SelectedScanner.EmulatorProfileId = null;
            }
            RebuildScannerProfiles();
            SelectedScannerProfile = ScannerProfiles.FirstOrDefault();
        }
    }

    public EmulatorProfile SelectedScannerProfile
    {
        get => selectedScannerProfile;
        set
        {
            if (SetField(ref selectedScannerProfile, value) && SelectedScanner != null)
            {
                SelectedScanner.EmulatorProfileId = value?.Id;
            }
        }
    }

    public Platform SelectedScannerOverridePlatform
    {
        get => selectedScannerOverridePlatform;
        set
        {
            if (SetField(ref selectedScannerOverridePlatform, value) && SelectedScanner != null)
            {
                SelectedScanner.OverridePlatformId = value?.Id ?? Guid.Empty;
            }
        }
    }

    public bool HideAlreadyImported
    {
        get => hideAlreadyImported;
        set
        {
            if (SetField(ref hideAlreadyImported, value))
            {
                RebuildVisibleDetectedEmulators();
            }
        }
    }

    public string GlobalCrcExcludeFileTypesText
    {
        get => globalCrcExcludeFileTypesText;
        set => SetField(ref globalCrcExcludeFileTypesText, value ?? string.Empty);
    }

    public string ScannerCrcExcludeFileTypesText
    {
        get => JoinList(SelectedScanner?.CrcExcludeFileTypes);
        set => SetScannerList(value, list => SelectedScanner.CrcExcludeFileTypes = list);
    }

    public string ScannerExcludedFilesText
    {
        get => JoinList(SelectedScanner?.ExcludedFiles, Environment.NewLine);
        set => SetScannerPathList(value, list => SelectedScanner.ExcludedFiles = list);
    }

    public string ScannerExcludedDirectoriesText
    {
        get => JoinList(SelectedScanner?.ExcludedDirectories, Environment.NewLine);
        set => SetScannerPathList(value, list => SelectedScanner.ExcludedDirectories = list);
    }

    public ICommand OpenPageCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand AddEmulatorCommand { get; }
    public ICommand CopyEmulatorCommand { get; }
    public ICommand RemoveEmulatorCommand { get; }
    public ICommand BrowseEmulatorDirectoryCommand { get; }
    public ICommand AddCustomProfileCommand { get; }
    public ICommand AddBuiltInProfileCommand { get; }
    public ICommand CopyProfileCommand { get; }
    public ICommand RemoveProfileCommand { get; }
    public ICommand BrowseProfileExecutableCommand { get; }
    public ICommand AddScannerCommand { get; }
    public ICommand CopyScannerCommand { get; }
    public ICommand RemoveScannerCommand { get; }
    public ICommand BrowseScannerDirectoryCommand { get; }
    public ICommand ScanForEmulatorsCommand { get; }
    public ICommand SelectAllDetectedCommand { get; }
    public ICommand DeselectAllDetectedCommand { get; }
    public ICommand ImportDetectedCommand { get; }
    public ICommand OpenDownloadCommand { get; }

    public EmulatorConfigViewModel(
        GameDatabase database,
        Func<DesktopDialogService> dialogs,
        Action<string, bool> showMessage)
    {
        this.database = database;
        this.dialogs = dialogs ?? (() => null);
        this.showMessage = showMessage ?? ((_, _) => { });
        EmulatorDefinitions = Emulation.Definitions
            .OrderBy(definition => definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Platforms = BuildPlatformOptions(database);
        DownloadOptions = BuildDownloads();

        OpenPageCommand = new AppRelayCommand(parameter => OpenPage(parameter as string));
        SaveCommand = new AppRelayCommand(() => Save());
        CloseCommand = new AppRelayCommand(Close);
        AddEmulatorCommand = new AppRelayCommand(AddEmulator);
        CopyEmulatorCommand = new AppRelayCommand(CopyEmulator);
        RemoveEmulatorCommand = new AppRelayCommand(RemoveEmulator);
        BrowseEmulatorDirectoryCommand = new AppRelayCommand(BrowseEmulatorDirectory);
        AddCustomProfileCommand = new AppRelayCommand(AddCustomProfile);
        AddBuiltInProfileCommand = new AppRelayCommand(AddBuiltInProfile);
        CopyProfileCommand = new AppRelayCommand(CopyProfile);
        RemoveProfileCommand = new AppRelayCommand(RemoveProfile);
        BrowseProfileExecutableCommand = new AppRelayCommand(BrowseProfileExecutable);
        AddScannerCommand = new AppRelayCommand(AddScanner);
        CopyScannerCommand = new AppRelayCommand(CopyScanner);
        RemoveScannerCommand = new AppRelayCommand(RemoveScanner);
        BrowseScannerDirectoryCommand = new AppRelayCommand(BrowseScannerDirectory);
        ScanForEmulatorsCommand = new AppRelayCommand(ScanForEmulators);
        SelectAllDetectedCommand = new AppRelayCommand(() => SetDetectedSelection(true));
        DeselectAllDetectedCommand = new AppRelayCommand(() => SetDetectedSelection(false));
        ImportDetectedCommand = new AppRelayCommand(ImportDetected);
        OpenDownloadCommand = new AppRelayCommand(parameter => OpenDownload(parameter as EmulatorDownloadOption));
    }

    public bool Open(string page = null)
    {
        if (database == null || IsVisible)
        {
            return false;
        }

        EditingEmulators = new ObservableCollection<Emulator>(database.Emulators
            .Select(emulator => NormalizeEmulator(emulator.GetClone()))
            .OrderBy(emulator => emulator.Name, StringComparer.CurrentCultureIgnoreCase));
        EditingScanners = new ObservableCollection<GameScannerConfig>(database.GameScanners
            .Select(scanner => NormalizeScanner(scanner.GetClone()))
            .OrderBy(scanner => scanner.Name, StringComparer.CurrentCultureIgnoreCase));
        GlobalCrcExcludeFileTypesText = JoinList(database.GetGameScannersSettings()?.CrcExcludeFileTypes);
        DetectedEmulators = new ObservableCollection<ScannedEmulator>();
        RebuildVisibleDetectedEmulators();
        SelectedEmulator = EditingEmulators.FirstOrDefault();
        SelectedScanner = EditingScanners.FirstOrDefault();
        StatusText = $"{EditingEmulators.Count:N0} emulator(s) and {EditingScanners.Count:N0} scanner(s) loaded.";
        OpenPage(page ?? EmulatorConfigPage.Emulators);
        originalState = CaptureState();
        IsVisible = true;
        return true;
    }

    public void Close()
    {
        if (!IsVisible)
        {
            return;
        }

        if (HasChanges())
        {
            var result = dialogs()?.ShowMessage(
                "Save emulator and scanner changes before closing?",
                "Unsaved emulation changes",
                new[] { "Save", "Discard", "Cancel" },
                0,
                2);
            if (result == "Cancel")
            {
                return;
            }
            if (result == "Save")
            {
                Save();
                return;
            }
        }

        IsVisible = false;
    }

    public bool Save()
    {
        if (!Validate(out var validationError))
        {
            StatusText = validationError;
            showMessage(validationError, true);
            return false;
        }

        try
        {
            using (database.BufferedUpdate())
            {
                SynchronizeCollection(database.Emulators, EditingEmulators);
                SynchronizeCollection(database.GameScanners, EditingScanners);
            }

            database.SetGameScannersSettings(new GameScannersSettings
            {
                CrcExcludeFileTypes = ParseList(GlobalCrcExcludeFileTypesText)
            });
            originalState = CaptureState();
            StatusText = "Emulator and scanner configuration saved.";
            showMessage(StatusText, false);
            IsVisible = false;
            return true;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Failed to save Avalonia emulation configuration.");
            StatusText = $"Emulation configuration could not be saved: {exception.Message}";
            showMessage(StatusText, true);
            return false;
        }
    }

    internal static IReadOnlyList<ScannedEmulator> DetectEmulators(
        string path,
        IList<EmulatorDefinition> definitions,
        CancellationToken cancellationToken) =>
        EmulatorScanner.SearchForEmulators(path, definitions, cancellationToken);

    private void OpenPage(string page)
    {
        SelectedPage = page is EmulatorConfigPage.Emulators or EmulatorConfigPage.Scanners or
            EmulatorConfigPage.Detect or EmulatorConfigPage.Downloads
            ? page
            : EmulatorConfigPage.Emulators;
        OnPropertyChanged(nameof(IsEmulatorsPage));
        OnPropertyChanged(nameof(IsScannersPage));
        OnPropertyChanged(nameof(IsDetectPage));
        OnPropertyChanged(nameof(IsDownloadsPage));
    }

    private void AddEmulator()
    {
        var emulator = NormalizeEmulator(new Emulator("New Emulator"));
        EditingEmulators.Add(emulator);
        SelectedEmulator = emulator;
    }

    private void CopyEmulator()
    {
        if (SelectedEmulator == null)
        {
            return;
        }

        var copy = NormalizeEmulator(SelectedEmulator.GetClone());
        copy.Id = Guid.NewGuid();
        copy.Name = $"{copy.Name} Copy";
        foreach (var profile in copy.CustomProfiles)
        {
            profile.Id = $"#custom_{Guid.NewGuid()}";
        }
        foreach (var profile in copy.BuiltinProfiles)
        {
            profile.Id = $"#builtin_{Guid.NewGuid()}";
        }
        EditingEmulators.Add(copy);
        SelectedEmulator = copy;
    }

    private void RemoveEmulator()
    {
        if (SelectedEmulator == null)
        {
            return;
        }

        var affectedGames = database.Games.Count(game => game.GameActions?.Any(action =>
            action.Type == GameActionType.Emulator && action.EmulatorId == SelectedEmulator.Id) == true);
        if (affectedGames > 0)
        {
            var result = dialogs()?.ShowMessage(
                $"{SelectedEmulator.Name} is referenced by {affectedGames:N0} game(s). Remove it anyway?",
                "Remove emulator",
                new[] { "Remove", "Cancel" },
                1,
                1);
            if (result != "Remove")
            {
                return;
            }
        }

        var removedId = SelectedEmulator.Id;
        EditingEmulators.Remove(SelectedEmulator);
        foreach (var scanner in EditingScanners.Where(scanner => scanner.EmulatorId == removedId).ToList())
        {
            EditingScanners.Remove(scanner);
        }
        SelectedEmulator = EditingEmulators.FirstOrDefault();
        SelectedScanner = EditingScanners.FirstOrDefault();
    }

    private void BrowseEmulatorDirectory()
    {
        if (SelectedEmulator == null)
        {
            return;
        }
        var path = dialogs()?.SelectFolder(SelectedEmulator.InstallDir);
        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedEmulator.InstallDir = path;
        }
    }

    private void AddCustomProfile()
    {
        if (SelectedEmulator == null)
        {
            return;
        }
        var profile = new CustomEmulatorProfile
        {
            Name = "New Profile",
            WorkingDirectory = ExpandableVariables.EmulatorDirectory,
            Platforms = new List<Guid>(),
            ImageExtensions = new List<string>()
        };
        SelectedEmulator.CustomProfiles.Add(profile);
        SelectedProfile = profile;
        RebuildScannerProfiles();
    }

    private void AddBuiltInProfile()
    {
        if (SelectedEmulator == null || string.IsNullOrWhiteSpace(SelectedBuiltInProfileName))
        {
            return;
        }
        var profile = new BuiltInEmulatorProfile
        {
            Name = SelectedBuiltInProfileName,
            BuiltInProfileName = SelectedBuiltInProfileName
        };
        SelectedEmulator.BuiltinProfiles.Add(profile);
        SelectedProfile = profile;
        RebuildScannerProfiles();
    }

    private void CopyProfile()
    {
        if (SelectedEmulator == null || SelectedCustomProfile == null)
        {
            return;
        }
        var copy = SelectedCustomProfile.GetClone();
        copy.Id = $"#custom_{Guid.NewGuid()}";
        copy.Name = $"{copy.Name} Copy";
        SelectedEmulator.CustomProfiles.Add(copy);
        SelectedProfile = copy;
        RebuildScannerProfiles();
    }

    private void RemoveProfile()
    {
        if (SelectedEmulator == null || SelectedProfile == null)
        {
            return;
        }
        if (SelectedProfile is CustomEmulatorProfile custom)
        {
            SelectedEmulator.CustomProfiles.Remove(custom);
        }
        else if (SelectedProfile is BuiltInEmulatorProfile builtIn)
        {
            SelectedEmulator.BuiltinProfiles.Remove(builtIn);
        }
        foreach (var scanner in EditingScanners.Where(scanner =>
                     scanner.EmulatorId == SelectedEmulator.Id &&
                     scanner.EmulatorProfileId == SelectedProfile.Id))
        {
            scanner.EmulatorProfileId = null;
        }
        SelectedProfile = SelectedEmulator.AllProfiles.FirstOrDefault();
        RebuildScannerProfiles();
    }

    private void BrowseProfileExecutable()
    {
        if (SelectedCustomProfile == null)
        {
            return;
        }
        var selected = dialogs()?.SelectFiles("All files|*.*", false, SelectedEmulator?.InstallDir).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SelectedCustomProfile.Executable = selected;
        }
    }

    private void AddScanner()
    {
        var scanner = NormalizeScanner(new GameScannerConfig { Name = "New Scanner" });
        EditingScanners.Add(scanner);
        SelectedScanner = scanner;
    }

    private void CopyScanner()
    {
        if (SelectedScanner == null)
        {
            return;
        }
        var copy = NormalizeScanner(SelectedScanner.GetClone());
        copy.Id = Guid.NewGuid();
        copy.Name = $"{copy.Name} Copy";
        EditingScanners.Add(copy);
        SelectedScanner = copy;
    }

    private void RemoveScanner()
    {
        if (SelectedScanner == null)
        {
            return;
        }
        EditingScanners.Remove(SelectedScanner);
        SelectedScanner = EditingScanners.FirstOrDefault();
    }

    private void BrowseScannerDirectory()
    {
        if (SelectedScanner == null)
        {
            return;
        }
        var selected = dialogs()?.SelectFolder(SelectedScanner.Directory);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SelectedScanner.Directory = selected;
        }
    }

    private void ScanForEmulators()
    {
        var selectedDirectory = dialogs()?.SelectFolder();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }
        if (EmulatorDefinitions.Count == 0)
        {
            StatusText = "No bundled emulator definitions are available.";
            showMessage(StatusText, true);
            return;
        }

        IReadOnlyList<ScannedEmulator> found = Array.Empty<ScannedEmulator>();
        var result = dialogs()?.ActivateGlobalProgress(args =>
        {
            args.Text = $"Scanning {selectedDirectory}";
            found = DetectEmulators(selectedDirectory, EmulatorDefinitions.ToList(), args.CancelToken);
        }, new GlobalProgressOptions("Scanning for emulators")
        {
            Cancelable = true,
            IsIndeterminate = true
        });
        if (result?.Error != null)
        {
            logger.Error(result.Error, "Avalonia emulator auto-detection failed.");
            StatusText = $"Emulator scan failed: {result.Error.Message}";
            showMessage(StatusText, true);
            return;
        }
        if (result?.Canceled == true)
        {
            StatusText = "Emulator scan cancelled.";
            return;
        }

        DetectedEmulators = new ObservableCollection<ScannedEmulator>(found
            .OrderBy(emulator => emulator.Name, StringComparer.CurrentCultureIgnoreCase));
        RebuildVisibleDetectedEmulators();
        StatusText = $"Found {DetectedEmulators.Count:N0} emulator installation(s).";
        OpenPage(EmulatorConfigPage.Detect);
    }

    private void SetDetectedSelection(bool selected)
    {
        foreach (var emulator in VisibleDetectedEmulators)
        {
            emulator.Import = selected;
            foreach (var profile in emulator.Profiles ?? new List<ScannedEmulator.ScannedEmulatorProfile>())
            {
                profile.Import = selected;
            }
        }
    }

    private void ImportDetected()
    {
        var imported = 0;
        foreach (var detected in VisibleDetectedEmulators.Where(emulator => emulator.Import))
        {
            var profiles = detected.Profiles?.Where(profile => profile.Import).ToList() ?? new List<ScannedEmulator.ScannedEmulatorProfile>();
            if (profiles.Count == 0 || IsDetectedImported(detected))
            {
                continue;
            }
            var emulator = NormalizeEmulator(new Emulator(detected.Name)
            {
                BuiltInConfigId = detected.Id,
                InstallDir = detected.InstallDir,
                BuiltinProfiles = new ObservableCollection<BuiltInEmulatorProfile>(profiles.Select(profile =>
                    new BuiltInEmulatorProfile
                    {
                        Name = profile.Name,
                        BuiltInProfileName = profile.ProfileName
                    }))
            });
            EditingEmulators.Add(emulator);
            imported++;
        }
        SelectedEmulator = EditingEmulators.LastOrDefault();
        RebuildVisibleDetectedEmulators();
        StatusText = imported == 0
            ? "No new selected emulator profiles were available to import."
            : $"Imported {imported:N0} emulator installation(s) into the pending configuration.";
    }

    private void OpenDownload(EmulatorDownloadOption option)
    {
        if (string.IsNullOrWhiteSpace(option?.Website))
        {
            return;
        }
        try
        {
            ProcessStarter.StartUrl(option.Website);
        }
        catch (Exception exception)
        {
            logger.Error(exception, $"Failed to open emulator website {option.Website}.");
            StatusText = $"The emulator website could not be opened: {exception.Message}";
            showMessage(StatusText, true);
        }
    }

    private bool Validate(out string error)
    {
        foreach (var emulator in EditingEmulators)
        {
            NormalizeEmulator(emulator);
            if (string.IsNullOrWhiteSpace(emulator.Name))
            {
                error = "Every emulator must have a name.";
                return false;
            }
        }
        foreach (var scanner in EditingScanners)
        {
            NormalizeScanner(scanner);
            var emulator = EditingEmulators.FirstOrDefault(item => item.Id == scanner.EmulatorId);
            if (string.IsNullOrWhiteSpace(scanner.Name))
            {
                error = "Every scanner must have a name.";
                return false;
            }
            if (emulator == null || emulator.GetProfile(scanner.EmulatorProfileId) == null)
            {
                error = $"Scanner '{scanner.Name}' must reference an existing emulator profile.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(scanner.Directory))
            {
                error = $"Scanner '{scanner.Name}' must specify a folder.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private bool HasChanges() => originalState != null && !string.Equals(originalState, CaptureState(), StringComparison.Ordinal);

    private string CaptureState() => Serialization.ToJson(new EmulatorConfigSnapshot
    {
        Emulators = EditingEmulators.ToList(),
        Scanners = EditingScanners.ToList(),
        GlobalCrcExcludeFileTypes = ParseList(GlobalCrcExcludeFileTypesText)
    });

    private void RebuildCustomProfilePlatforms()
    {
        CustomProfilePlatforms.Clear();
        var selectedIds = SelectedCustomProfile?.Platforms?.ToHashSet() ?? new HashSet<Guid>();
        foreach (var platform in Platforms.Where(platform => platform.Id != Guid.Empty))
        {
            CustomProfilePlatforms.Add(new EmulatorPlatformOption(
                platform,
                selectedIds.Contains(platform.Id),
                (item, selected) =>
                {
                    if (SelectedCustomProfile == null)
                    {
                        return;
                    }
                    SelectedCustomProfile.Platforms ??= new List<Guid>();
                    if (selected)
                    {
                        if (!SelectedCustomProfile.Platforms.Contains(item.Id))
                        {
                            SelectedCustomProfile.Platforms.Add(item.Id);
                        }
                    }
                    else
                    {
                        SelectedCustomProfile.Platforms.Remove(item.Id);
                    }
                }));
        }
    }

    private void RebuildScannerProfiles()
    {
        ScannerProfiles.Clear();
        foreach (var profile in SelectedScannerEmulator?.AllProfiles ?? new List<EmulatorProfile>())
        {
            ScannerProfiles.Add(profile);
        }
    }

    private void RebuildVisibleDetectedEmulators()
    {
        VisibleDetectedEmulators = DetectedEmulators
            .Where(emulator => !HideAlreadyImported || !IsDetectedImported(emulator))
            .ToList();
    }

    private bool IsDetectedImported(ScannedEmulator detected) => EditingEmulators.Any(emulator =>
        EmulationConfigUtilities.PathsEqual(emulator.InstallDir, detected.InstallDir));

    private void SetScannerList(string text, Action<List<string>> setter)
    {
        if (SelectedScanner == null)
        {
            return;
        }
        setter(ParseList(text));
    }

    private void RaiseScannerTextProperties()
    {
        OnPropertyChanged(nameof(ScannerCrcExcludeFileTypesText));
        OnPropertyChanged(nameof(ScannerExcludedFilesText));
        OnPropertyChanged(nameof(ScannerExcludedDirectoriesText));
    }

    private static Emulator NormalizeEmulator(Emulator emulator)
    {
        emulator.CustomProfiles ??= new ObservableCollection<CustomEmulatorProfile>();
        emulator.BuiltinProfiles ??= new ObservableCollection<BuiltInEmulatorProfile>();
        foreach (var profile in emulator.CustomProfiles)
        {
            profile.Platforms ??= new List<Guid>();
            profile.ImageExtensions ??= new List<string>();
        }
        return emulator;
    }

    internal static GameScannerConfig NormalizeScanner(GameScannerConfig scanner)
    {
        scanner.CrcExcludeFileTypes ??= new List<string>();
        scanner.ExcludedFiles ??= new List<string>();
        scanner.ExcludedDirectories ??= new List<string>();
        return scanner;
    }

    internal static List<string> ParseList(string text) => (text ?? string.Empty)
        .Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        .ToList();

    internal static List<string> ParsePathList(string text) => (text ?? string.Empty)
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(EmulationConfigUtilities.PathComparer)
        .ToList();

    private void SetScannerPathList(string text, Action<List<string>> setter)
    {
        if (SelectedScanner != null)
        {
            setter(ParsePathList(text));
        }
    }

    private static string JoinList(IEnumerable<string> values, string separator = "; ") =>
        string.Join(separator, values ?? Array.Empty<string>());

    private static IReadOnlyList<Platform> BuildPlatformOptions(GameDatabase database)
    {
        var result = new List<Platform> { new("None") { Id = Guid.Empty } };
        if (database != null)
        {
            result.AddRange(database.Platforms.OrderBy(platform => platform.Name, StringComparer.CurrentCultureIgnoreCase));
        }
        return result;
    }

    private static IReadOnlyList<EmulatorDownloadOption> BuildDownloads() => Emulation.Platforms
        .Where(platform => platform.Emulators?.Count > 0)
        .SelectMany(platform => platform.Emulators.Select(Emulation.GetDefition)
            .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Website))
            .Select(definition => new EmulatorDownloadOption(definition.Name, definition.Website, platform.Name)))
        .OrderBy(option => option.Platform, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private static void SynchronizeCollection<T>(
        Playnite.SDK.IItemCollection<T> destination,
        IEnumerable<T> source)
        where T : DatabaseObject
    {
        var materialized = source.ToList();
        var sourceIds = materialized.Select(item => item.Id).ToHashSet();
        var removed = destination.Where(item => !sourceIds.Contains(item.Id)).ToList();
        if (removed.Count > 0)
        {
            destination.Remove(removed);
        }
        var added = materialized.Where(item => destination[item.Id] == null).ToList();
        if (added.Count > 0)
        {
            destination.Add(added);
        }
        foreach (var item in materialized)
        {
            var saved = destination[item.Id];
            if (saved != null && !item.IsEqualJson(saved))
            {
                destination.Update(item);
            }
        }
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

    private sealed class EmulatorConfigSnapshot
    {
        public List<Emulator> Emulators { get; set; }
        public List<GameScannerConfig> Scanners { get; set; }
        public List<string> GlobalCrcExcludeFileTypes { get; set; }
    }
}

public sealed class EmulatorPlatformOption : INotifyPropertyChanged
{
    private readonly Action<Platform, bool> changed;
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public Platform Platform { get; }
    public string Name => Platform.Name;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            changed(Platform, value);
        }
    }

    public EmulatorPlatformOption(Platform platform, bool selected, Action<Platform, bool> changed)
    {
        Platform = platform;
        isSelected = selected;
        this.changed = changed ?? ((_, _) => { });
    }
}

public sealed record EmulatorDownloadOption(string Name, string Website, string Platform);

internal static class EmulationConfigUtilities
{
    public static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return string.Equals(first, second, PathComparison);
        }
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                PathComparison);
        }
        catch
        {
            return string.Equals(first, second, PathComparison);
        }
    }
}
