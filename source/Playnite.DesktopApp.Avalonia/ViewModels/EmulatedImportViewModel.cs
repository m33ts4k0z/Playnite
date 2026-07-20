using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class EmulatedImportViewModel : INotifyPropertyChanged
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly GameDatabase database;
    private readonly Func<DesktopDialogService> dialogs;
    private readonly Action synchronizeLibrary;
    private readonly Action notifyLibraryUpdated;
    private readonly Action<string, bool> showMessage;
    private readonly List<Platform> pendingPlatforms = new();
    private readonly List<Region> pendingRegions = new();
    private EmulatedImportScannerRow selectedScanner;
    private GameScannerConfig selectedSavedScanner;
    private EmulatedImportGameRow selectedGame;
    private bool isVisible;
    private bool isSetup = true;
    private string statusText = string.Empty;

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
    public bool IsSetup
    {
        get => isSetup;
        private set
        {
            if (SetField(ref isSetup, value))
            {
                OnPropertyChanged(nameof(IsReview));
            }
        }
    }
    public bool IsReview => !IsSetup;
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }
    public ObservableCollection<EmulatedImportScannerRow> ScannerConfigs { get; } = new();
    public ObservableCollection<EmulatedImportGameRow> Games { get; } = new();
    public ObservableCollection<EmulatorProfile> SelectedScannerProfiles { get; } = new();
    public IReadOnlyList<Emulator> Emulators => database?.Emulators
        .OrderBy(emulator => emulator.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList() ?? new List<Emulator>();
    public IReadOnlyList<Platform> Platforms => database?.Platforms
        .OrderBy(platform => platform.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList() ?? new List<Platform>();
    public IReadOnlyList<Region> Regions => database?.Regions
        .OrderBy(region => region.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList() ?? new List<Region>();
    public IReadOnlyList<GameScannerConfig> SavedScanners => database?.GameScanners
        .OrderBy(scanner => scanner.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList() ?? new List<GameScannerConfig>();
    public IReadOnlyList<ScannerConfigPlayActionSettings> PlayActionSettings { get; } =
        Enum.GetValues<ScannerConfigPlayActionSettings>();

    public EmulatedImportScannerRow SelectedScanner
    {
        get => selectedScanner;
        set
        {
            if (SetField(ref selectedScanner, value))
            {
                RebuildSelectedScannerProfiles();
            }
        }
    }

    public GameScannerConfig SelectedSavedScanner
    {
        get => selectedSavedScanner;
        set => SetField(ref selectedSavedScanner, value);
    }

    public EmulatedImportGameRow SelectedGame
    {
        get => selectedGame;
        set => SetField(ref selectedGame, value);
    }

    public ICommand AddScannerCommand { get; }
    public ICommand AddSavedScannerCommand { get; }
    public ICommand RemoveScannerCommand { get; }
    public ICommand BrowseScannerDirectoryCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand BackToSetupCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand SplitGameCommand { get; }
    public ICommand MergeMarkedCommand { get; }
    public ICommand ExcludeGameFilesCommand { get; }
    public ICommand ExcludeGameFoldersCommand { get; }
    public ICommand ExcludeSelectedFilesCommand { get; }
    public ICommand ExcludeSelectedFoldersCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CloseCommand { get; }

    public EmulatedImportViewModel(
        GameDatabase database,
        Func<DesktopDialogService> dialogs,
        Action synchronizeLibrary,
        Action notifyLibraryUpdated,
        Action<string, bool> showMessage)
    {
        this.database = database;
        this.dialogs = dialogs ?? (() => null);
        this.synchronizeLibrary = synchronizeLibrary ?? (() => { });
        this.notifyLibraryUpdated = notifyLibraryUpdated ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });

        AddScannerCommand = new AppRelayCommand(AddScanner);
        AddSavedScannerCommand = new AppRelayCommand(AddSavedScanner);
        RemoveScannerCommand = new AppRelayCommand(RemoveScanner);
        BrowseScannerDirectoryCommand = new AppRelayCommand(BrowseScannerDirectory);
        ScanCommand = new AppRelayCommand(Scan);
        BackToSetupCommand = new AppRelayCommand(() => IsSetup = true);
        SelectAllCommand = new AppRelayCommand(() => SetImportSelection(true));
        DeselectAllCommand = new AppRelayCommand(() => SetImportSelection(false));
        SplitGameCommand = new AppRelayCommand(parameter => SplitGame(parameter as EmulatedImportGameRow));
        MergeMarkedCommand = new AppRelayCommand(MergeMarked);
        ExcludeGameFilesCommand = new AppRelayCommand(parameter => ExcludeFiles(new[] { parameter as EmulatedImportGameRow }));
        ExcludeGameFoldersCommand = new AppRelayCommand(parameter => ExcludeFolders(new[] { parameter as EmulatedImportGameRow }));
        ExcludeSelectedFilesCommand = new AppRelayCommand(() => ExcludeFiles(Games.Where(game => game.IsMarked)));
        ExcludeSelectedFoldersCommand = new AppRelayCommand(() => ExcludeFolders(Games.Where(game => game.IsMarked)));
        ImportCommand = new AppRelayCommand(Import);
        CloseCommand = new AppRelayCommand(Close);
    }

    public bool Open()
    {
        if (database == null || IsVisible)
        {
            return false;
        }

        ScannerConfigs.Clear();
        Games.Clear();
        pendingPlatforms.Clear();
        pendingRegions.Clear();
        SelectedSavedScanner = SavedScanners.FirstOrDefault();
        if (SelectedSavedScanner != null)
        {
            AddSavedScanner();
        }
        else
        {
            AddScanner();
        }
        StatusText = SavedScanners.Count == 0
            ? "Add a folder scanner to find emulated games."
            : "A saved scanner is ready. Add or edit scanners, then start the scan.";
        IsSetup = true;
        IsVisible = true;
        return true;
    }

    public void Close()
    {
        IsVisible = false;
        IsSetup = true;
        Games.Clear();
        pendingPlatforms.Clear();
        pendingRegions.Clear();
    }

    internal EmulatedImportScanResult ScanCore(CancellationToken cancellationToken, Action<string> progress = null)
    {
        var games = new List<ScannedGame>();
        var platforms = new Dictionary<Guid, Platform>();
        var regions = new Dictionary<Guid, Region>();
        foreach (var row in ScannerConfigs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke(row.Config.Directory);
            var scanned = new GameScanner(row.Config, database).Scan(
                cancellationToken,
                out var newPlatforms,
                out var newRegions,
                progress);
            games.AddRange(scanned);
            foreach (var platform in newPlatforms ?? new List<Platform>())
            {
                platforms[platform.Id] = platform;
            }
            foreach (var region in newRegions ?? new List<Region>())
            {
                regions[region.Id] = region;
            }
        }
        return new EmulatedImportScanResult(games, platforms.Values.ToList(), regions.Values.ToList());
    }

    private void AddScanner()
    {
        var config = EmulatorConfigViewModel.NormalizeScanner(new GameScannerConfig
        {
            Name = "New emulated-game scanner",
            ScanSubfolders = true,
            ScanInsideArchives = true,
            ImportWithRelativePaths = true,
            MergeRelatedFiles = true
        });
        var row = new EmulatedImportScannerRow(config, Emulators, Platforms, false, true);
        ScannerConfigs.Add(row);
        SelectedScanner = row;
    }

    private void AddSavedScanner()
    {
        if (SelectedSavedScanner == null)
        {
            return;
        }
        if (ScannerConfigs.Any(row => row.Config.Id == SelectedSavedScanner.Id))
        {
            SelectedScanner = ScannerConfigs.First(row => row.Config.Id == SelectedSavedScanner.Id);
            return;
        }
        var row = new EmulatedImportScannerRow(
            EmulatorConfigViewModel.NormalizeScanner(SelectedSavedScanner.GetClone()),
            Emulators,
            Platforms,
            true,
            false);
        ScannerConfigs.Add(row);
        SelectedScanner = row;
    }

    private void RemoveScanner()
    {
        if (SelectedScanner == null)
        {
            return;
        }
        ScannerConfigs.Remove(SelectedScanner);
        SelectedScanner = ScannerConfigs.FirstOrDefault();
    }

    private void BrowseScannerDirectory()
    {
        if (SelectedScanner == null)
        {
            return;
        }
        var path = dialogs()?.SelectFolder(SelectedScanner.Config.Directory);
        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedScanner.Config.Directory = path;
        }
    }

    private void Scan()
    {
        if (!ValidateScannerConfigs(out var error))
        {
            StatusText = error;
            showMessage(error, true);
            return;
        }
        try
        {
            SaveRequestedScannerConfigs();
            EmulatedImportScanResult scanResult = null;
            var result = dialogs()?.ActivateGlobalProgress(args =>
            {
                scanResult = ScanCore(args.CancelToken, path =>
                    args.Text = $"Scanning {path}");
            }, new GlobalProgressOptions("Scanning emulated-game folders")
            {
                Cancelable = true,
                IsIndeterminate = true
            });
            if (result?.Error != null)
            {
                throw result.Error;
            }
            if (result?.Canceled == true)
            {
                StatusText = "Emulated-game scan cancelled.";
                return;
            }
            ApplyScanResult(scanResult ?? new EmulatedImportScanResult(
                new List<ScannedGame>(), new List<Platform>(), new List<Region>()));
        }
        catch (OperationCanceledException)
        {
            StatusText = "Emulated-game scan cancelled.";
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Avalonia emulated-game scan failed.");
            StatusText = $"Emulated-game scan failed: {exception.Message}";
            showMessage(StatusText, true);
        }
    }

    internal void ApplyScanResult(EmulatedImportScanResult result)
    {
        pendingPlatforms.Clear();
        pendingPlatforms.AddRange(result.NewPlatforms ?? new List<Platform>());
        pendingRegions.Clear();
        pendingRegions.AddRange(result.NewRegions ?? new List<Region>());
        var availablePlatforms = Platforms.Concat(pendingPlatforms)
            .GroupBy(platform => platform.Id)
            .Select(group => group.First())
            .OrderBy(platform => platform.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var availableRegions = Regions.Concat(pendingRegions)
            .GroupBy(region => region.Id)
            .Select(group => group.First())
            .OrderBy(region => region.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Games.Clear();
        foreach (var game in (result.Games ?? new List<ScannedGame>())
                     .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Games.Add(new EmulatedImportGameRow(game, availablePlatforms, availableRegions));
        }
        SelectedGame = Games.FirstOrDefault();
        IsSetup = false;
        StatusText = $"Review {Games.Count:N0} detected game(s), adjust ROM selection, then import.";
    }

    private bool ValidateScannerConfigs(out string error)
    {
        if (ScannerConfigs.Count == 0)
        {
            error = "Add at least one scanner configuration.";
            return false;
        }
        foreach (var row in ScannerConfigs)
        {
            var config = row.Config;
            var emulator = database.Emulators[config.EmulatorId];
            if (string.IsNullOrWhiteSpace(config.Name))
            {
                error = "Every scanner selected for import must have a name.";
                return false;
            }
            if (emulator == null || emulator.GetProfile(config.EmulatorProfileId) == null)
            {
                error = $"Scanner '{config.Name}' must reference an existing emulator profile.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(config.Directory))
            {
                error = $"Scanner '{config.Name}' must specify a folder.";
                return false;
            }
            var expanded = PlaynitePaths.ExpandVariables(config.Directory, emulator.InstallDir, true);
            if (!Directory.Exists(expanded))
            {
                error = $"Scanner folder '{expanded}' does not exist.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private void SaveRequestedScannerConfigs()
    {
        foreach (var row in ScannerConfigs.Where(row => row.SaveConfig))
        {
            PersistScanner(row);
        }
        OnPropertyChanged(nameof(SavedScanners));
    }

    private void PersistScanner(EmulatedImportScannerRow row)
    {
        var config = EmulatorConfigViewModel.NormalizeScanner(row.Config);
        var saved = database.GameScanners[config.Id];
        if (saved == null)
        {
            saved = database.GameScanners.FirstOrDefault(candidate =>
                candidate.EmulatorId == config.EmulatorId &&
                string.Equals(candidate.EmulatorProfileId, config.EmulatorProfileId, StringComparison.Ordinal) &&
                EmulationConfigUtilities.PathsEqual(candidate.Directory, config.Directory));
        }
        if (saved == null)
        {
            var copy = config.GetClone();
            database.GameScanners.Add(copy);
            row.UsePersistedConfig(copy.GetClone());
        }
        else
        {
            config.Id = saved.Id;
            if (!config.IsEqualJson(saved))
            {
                database.GameScanners.Update(config);
            }
            row.UsePersistedConfig(config.GetClone());
        }
    }

    private void SetImportSelection(bool selected)
    {
        foreach (var game in Games)
        {
            game.Import = selected;
            foreach (var rom in game.Roms)
            {
                rom.Import = selected;
            }
        }
    }

    private void SplitGame(EmulatedImportGameRow row)
    {
        if (row?.Model.Roms?.Count <= 1)
        {
            return;
        }
        var source = row.Model;
        var roms = source.Roms.ToList();
        source.Roms = new ObservableCollection<ScannedRom> { roms[0] };
        var insertAt = Games.IndexOf(row) + 1;
        foreach (var rom in roms.Skip(1))
        {
            var split = new ScannedGame
            {
                Import = source.Import,
                Name = rom.Name?.Name ?? Path.GetFileNameWithoutExtension(rom.Path),
                Roms = new ObservableCollection<ScannedRom> { rom },
                Platforms = source.Platforms?.ToList(),
                Regions = source.Regions?.ToList(),
                SourceConfig = source.SourceConfig,
                SourceEmulator = source.SourceEmulator,
                ReleaseDate = source.ReleaseDate
            };
            Games.Insert(insertAt++, new EmulatedImportGameRow(split, row.PlatformOptions, row.RegionOptions));
        }
        row.Refresh();
        StatusText = $"Split '{source.Name}' into {roms.Count:N0} game entries.";
    }

    private void MergeMarked()
    {
        var selected = Games.Where(game => game.IsMarked).ToList();
        if (selected.Count < 2)
        {
            StatusText = "Mark at least two games to merge.";
            return;
        }
        var target = selected[0];
        var knownPaths = target.Roms.Select(rom => rom.Path).ToHashSet(EmulationConfigUtilities.PathComparer);
        foreach (var source in selected.Skip(1))
        {
            foreach (var rom in source.Roms.Where(rom => knownPaths.Add(rom.Path)))
            {
                target.Model.Roms.Add(rom);
            }
            Games.Remove(source);
        }
        target.IsMarked = false;
        target.Refresh();
        SelectedGame = target;
        StatusText = $"Merged {selected.Count:N0} entries into '{target.Name}'.";
    }

    private void ExcludeFiles(IEnumerable<EmulatedImportGameRow> rows)
    {
        var selected = rows?.Where(row => row != null).ToList() ?? new List<EmulatedImportGameRow>();
        if (selected.Count == 0)
        {
            StatusText = "Mark at least one game to add file exclusions.";
            return;
        }
        foreach (var group in selected.GroupBy(row => row.Model.SourceConfig.Id))
        {
            var rowConfig = FindScanner(group.Key);
            if (rowConfig == null)
            {
                continue;
            }
            var root = ExpandScannerRoot(rowConfig.Config);
            var paths = group.SelectMany(game => game.Roms)
                .Select(rom => ToScannerRelativePath(root, rom.Path, false))
                .Where(path => !string.IsNullOrWhiteSpace(path));
            AddMissing(rowConfig.Config.ExcludedFiles, paths);
            PersistExclusionScanner(rowConfig);
        }
        foreach (var game in selected)
        {
            game.Import = false;
            game.IsMarked = false;
            foreach (var rom in game.Roms)
            {
                rom.Import = false;
            }
        }
        StatusText = $"Added ROM file exclusions for {selected.Count:N0} game(s).";
    }

    private void ExcludeFolders(IEnumerable<EmulatedImportGameRow> rows)
    {
        var selected = rows?.Where(row => row != null).ToList() ?? new List<EmulatedImportGameRow>();
        if (selected.Count == 0)
        {
            StatusText = "Mark at least one game to add folder exclusions.";
            return;
        }
        foreach (var group in selected.GroupBy(row => row.Model.SourceConfig.Id))
        {
            var rowConfig = FindScanner(group.Key);
            if (rowConfig == null)
            {
                continue;
            }
            var root = ExpandScannerRoot(rowConfig.Config);
            var paths = group.SelectMany(game => game.Roms)
                .Select(rom => ToScannerRelativePath(root, rom.Path, true))
                .Where(path => !string.IsNullOrWhiteSpace(path));
            AddMissing(rowConfig.Config.ExcludedDirectories, paths);
            PersistExclusionScanner(rowConfig);
        }
        foreach (var game in selected)
        {
            game.Import = false;
            game.IsMarked = false;
            foreach (var rom in game.Roms)
            {
                rom.Import = false;
            }
        }
        StatusText = $"Added ROM folder exclusions for {selected.Count:N0} game(s).";
    }

    private EmulatedImportScannerRow FindScanner(Guid id) => ScannerConfigs.FirstOrDefault(row => row.Config.Id == id);

    private void PersistExclusionScanner(EmulatedImportScannerRow row)
    {
        row.SaveConfig = true;
        PersistScanner(row);
        OnPropertyChanged(nameof(SavedScanners));
    }

    private string ExpandScannerRoot(GameScannerConfig config)
    {
        var emulator = database.Emulators[config.EmulatorId];
        return PlaynitePaths.ExpandVariables(config.Directory, emulator?.InstallDir, true);
    }

    internal static string ToScannerRelativePath(string root, string romPath, bool directory)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(romPath))
        {
            return null;
        }
        var path = directory ? Path.GetDirectoryName(romPath) : romPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var relative = Path.GetRelativePath(root, path)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relative == "." || relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return null;
        }
        return relative;
    }

    private static void AddMissing(List<string> destination, IEnumerable<string> values)
    {
        var known = destination.ToHashSet(EmulationConfigUtilities.PathComparer);
        foreach (var value in values)
        {
            if (known.Add(value))
            {
                destination.Add(value);
            }
        }
    }

    private void Import()
    {
        var selected = Games.Where(game => game.Import && game.Roms.Any(rom => rom.Import)).ToList();
        if (selected.Count == 0)
        {
            StatusText = "Select at least one game and ROM to import.";
            return;
        }
        try
        {
            var imported = selected.Select(row => row.Model.ToGame()).ToList();
            var selectedPlatformIds = selected.SelectMany(row => row.Model.Platforms ?? new List<Platform>())
                .Select(platform => platform.Id)
                .ToHashSet();
            var selectedRegionIds = selected.SelectMany(row => row.Model.Regions ?? new List<Region>())
                .Select(region => region.Id)
                .ToHashSet();
            using (database.BufferedUpdate())
            {
                var platforms = pendingPlatforms.Where(platform =>
                    selectedPlatformIds.Contains(platform.Id) && database.Platforms[platform.Id] == null).ToList();
                if (platforms.Count > 0)
                {
                    database.Platforms.Add(platforms);
                }
                var regions = pendingRegions.Where(region =>
                    selectedRegionIds.Contains(region.Id) && database.Regions[region.Id] == null).ToList();
                if (regions.Count > 0)
                {
                    database.Regions.Add(regions);
                }
                var defaultStatus = database.GetCompletionStatusSettings().DefaultStatus;
                if (defaultStatus != Guid.Empty)
                {
                    imported.ForEach(game => game.CompletionStatusId = defaultStatus);
                }
                database.Games.Add(imported);
            }
            synchronizeLibrary();
            notifyLibraryUpdated();
            StatusText = $"Imported {imported.Count:N0} emulated game(s).";
            showMessage(StatusText, false);
            IsVisible = false;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Avalonia emulated-game import failed.");
            StatusText = $"Emulated games could not be imported: {exception.Message}";
            showMessage(StatusText, true);
        }
    }

    private void RebuildSelectedScannerProfiles()
    {
        SelectedScannerProfiles.Clear();
        foreach (var profile in SelectedScanner?.SelectedEmulator?.AllProfiles ?? new List<EmulatorProfile>())
        {
            SelectedScannerProfiles.Add(profile);
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
}

public sealed class EmulatedImportScannerRow : INotifyPropertyChanged
{
    private readonly IReadOnlyList<Emulator> emulators;
    private readonly IReadOnlyList<Platform> platforms;
    private GameScannerConfig config;
    private Emulator selectedEmulator;
    private EmulatorProfile selectedProfile;
    private Platform selectedOverridePlatform;
    private bool saveConfig;
    private bool isSaved;

    public event PropertyChangedEventHandler PropertyChanged;
    public GameScannerConfig Config => config;
    public IReadOnlyList<Emulator> Emulators => emulators;
    public IReadOnlyList<Platform> Platforms => platforms;
    public ObservableCollection<EmulatorProfile> Profiles { get; } = new();
    public IReadOnlyList<ScannerConfigPlayActionSettings> PlayActionSettings { get; } =
        Enum.GetValues<ScannerConfigPlayActionSettings>();

    public Emulator SelectedEmulator
    {
        get => selectedEmulator;
        set
        {
            if (ReferenceEquals(selectedEmulator, value))
            {
                return;
            }
            selectedEmulator = value;
            config.EmulatorId = value?.Id ?? Guid.Empty;
            RebuildProfiles();
            SelectedProfile = Profiles.FirstOrDefault();
            OnPropertyChanged();
        }
    }

    public EmulatorProfile SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (ReferenceEquals(selectedProfile, value))
            {
                return;
            }
            selectedProfile = value;
            config.EmulatorProfileId = value?.Id;
            OnPropertyChanged();
        }
    }

    public Platform SelectedOverridePlatform
    {
        get => selectedOverridePlatform;
        set
        {
            if (ReferenceEquals(selectedOverridePlatform, value))
            {
                return;
            }
            selectedOverridePlatform = value;
            config.OverridePlatformId = value?.Id ?? Guid.Empty;
            OnPropertyChanged();
        }
    }

    public bool SaveConfig
    {
        get => saveConfig;
        set
        {
            if (saveConfig != value)
            {
                saveConfig = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Status));
            }
        }
    }
    public bool IsSaved
    {
        get => isSaved;
        private set
        {
            if (isSaved != value)
            {
                isSaved = value;
                OnPropertyChanged();
            }
        }
    }
    public string Status => IsSaved ? "Saved scanner" : SaveConfig ? "Will be saved" : "One-time scanner";

    public string CrcExcludeFileTypesText
    {
        get => string.Join("; ", config.CrcExcludeFileTypes);
        set => config.CrcExcludeFileTypes = EmulatorConfigViewModel.ParseList(value);
    }
    public string ExcludedFilesText
    {
        get => string.Join(Environment.NewLine, config.ExcludedFiles);
        set => config.ExcludedFiles = EmulatorConfigViewModel.ParsePathList(value);
    }
    public string ExcludedDirectoriesText
    {
        get => string.Join(Environment.NewLine, config.ExcludedDirectories);
        set => config.ExcludedDirectories = EmulatorConfigViewModel.ParsePathList(value);
    }

    public EmulatedImportScannerRow(
        GameScannerConfig config,
        IReadOnlyList<Emulator> emulators,
        IReadOnlyList<Platform> platforms,
        bool saved,
        bool save)
    {
        this.config = config;
        this.emulators = emulators;
        this.platforms = platforms;
        isSaved = saved;
        saveConfig = save;
        selectedEmulator = emulators.FirstOrDefault(emulator => emulator.Id == config.EmulatorId);
        RebuildProfiles();
        selectedProfile = Profiles.FirstOrDefault(profile => profile.Id == config.EmulatorProfileId);
        selectedOverridePlatform = platforms.FirstOrDefault(platform => platform.Id == config.OverridePlatformId);
    }

    public void UsePersistedConfig(GameScannerConfig persisted)
    {
        config = EmulatorConfigViewModel.NormalizeScanner(persisted);
        IsSaved = true;
        SaveConfig = true;
        selectedEmulator = emulators.FirstOrDefault(emulator => emulator.Id == config.EmulatorId);
        RebuildProfiles();
        selectedProfile = Profiles.FirstOrDefault(profile => profile.Id == config.EmulatorProfileId);
        selectedOverridePlatform = platforms.FirstOrDefault(platform => platform.Id == config.OverridePlatformId);
        OnPropertyChanged(nameof(Config));
        OnPropertyChanged(nameof(SelectedEmulator));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(SelectedOverridePlatform));
        OnPropertyChanged(nameof(Status));
    }

    private void RebuildProfiles()
    {
        Profiles.Clear();
        foreach (var profile in selectedEmulator?.AllProfiles ?? new List<EmulatorProfile>())
        {
            Profiles.Add(profile);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class EmulatedImportGameRow : INotifyPropertyChanged
{
    private bool isMarked;
    private Platform selectedPlatform;
    private Region selectedRegion;

    public event PropertyChangedEventHandler PropertyChanged;
    public ScannedGame Model { get; }
    public IReadOnlyList<Platform> PlatformOptions { get; }
    public IReadOnlyList<Region> RegionOptions { get; }
    public ObservableCollection<ScannedRom> Roms => Model.Roms;
    public string Name { get => Model.Name; set { Model.Name = value; OnPropertyChanged(); } }
    public bool Import { get => Model.Import; set { Model.Import = value; OnPropertyChanged(); } }
    public bool IsMarked
    {
        get => isMarked;
        set
        {
            if (isMarked != value)
            {
                isMarked = value;
                OnPropertyChanged();
            }
        }
    }
    public int RomCount => Roms?.Count ?? 0;
    public bool CanSplit => RomCount > 1;
    public string SourceName => Model.SourceConfig?.Name ?? Model.SourceConfig?.Directory ?? "Scanner";
    public string RomSummary => string.Join(", ", (Roms ?? new ObservableCollection<ScannedRom>())
        .Select(rom => Path.GetFileName(rom.Path)));
    public Platform SelectedPlatform
    {
        get => selectedPlatform;
        set
        {
            if (ReferenceEquals(selectedPlatform, value))
            {
                return;
            }
            selectedPlatform = value;
            Model.Platforms = value == null ? new List<Platform>() : new List<Platform> { value };
            OnPropertyChanged();
        }
    }
    public Region SelectedRegion
    {
        get => selectedRegion;
        set
        {
            if (ReferenceEquals(selectedRegion, value))
            {
                return;
            }
            selectedRegion = value;
            Model.Regions = value == null ? new List<Region>() : new List<Region> { value };
            OnPropertyChanged();
        }
    }

    public EmulatedImportGameRow(
        ScannedGame model,
        IReadOnlyList<Platform> platformOptions,
        IReadOnlyList<Region> regionOptions)
    {
        Model = model;
        Model.Roms ??= new ObservableCollection<ScannedRom>();
        PlatformOptions = platformOptions;
        RegionOptions = regionOptions;
        selectedPlatform = model.Platforms?.FirstOrDefault();
        selectedRegion = model.Regions?.FirstOrDefault();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Roms));
        OnPropertyChanged(nameof(RomCount));
        OnPropertyChanged(nameof(CanSplit));
        OnPropertyChanged(nameof(RomSummary));
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record EmulatedImportScanResult(
    List<ScannedGame> Games,
    List<Platform> NewPlatforms,
    List<Region> NewRegions);
