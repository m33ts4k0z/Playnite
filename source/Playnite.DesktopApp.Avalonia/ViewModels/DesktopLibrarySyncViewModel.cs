using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopLibrarySyncViewModel : INotifyPropertyChanged
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private readonly DesktopMetadataDownloadViewModel metadataDownload;
    private readonly Action synchronizeLibrary;
    private readonly Action<string, bool> showMessage;
    private Func<List<LibraryPlugin>> libraryPlugins = () => new List<LibraryPlugin>();
    private Action notifyLibraryUpdated = () => { };
    private CancellationTokenSource cancellationSource;
    private bool isVisible;
    private bool isRunning;
    private int progressValue;
    private int progressTotal;
    private string progressText = string.Empty;
    private string errorText;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler SettingsChanged;

    public ObservableCollection<DesktopLibraryPluginOption> Libraries { get; } = new();
    public ObservableCollection<DesktopGameScannerOption> GameScanners { get; } = new();
    public IReadOnlyList<PlaytimeImportMode> PlaytimeModes { get; } =
        Enum.GetValues<PlaytimeImportMode>();

    public PlaytimeImportMode PlaytimeMode
    {
        get => settings.LibraryPlaytimeImportMode;
        set
        {
            if (settings.LibraryPlaytimeImportMode == value)
            {
                return;
            }

            settings.LibraryPlaytimeImportMode = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool DownloadMetadataOnImport
    {
        get => settings.DownloadMetadataOnImport;
        set
        {
            if (settings.DownloadMetadataOnImport == value)
            {
                return;
            }

            settings.DownloadMetadataOnImport = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsVisible
    {
        get => isVisible;
        private set => SetField(ref isVisible, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetField(ref isRunning, value))
            {
                OnPropertyChanged(nameof(CanConfigure));
                ((AppRelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((AppRelayCommand)CancelCommand).RaiseCanExecuteChanged();
                ((AppRelayCommand)CloseCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanConfigure => !IsRunning;
    public int ProgressValue { get => progressValue; private set => SetField(ref progressValue, value); }
    public int ProgressTotal { get => progressTotal; private set => SetField(ref progressTotal, value); }
    public string ProgressText { get => progressText; private set => SetField(ref progressText, value); }
    public string ErrorText { get => errorText; private set => SetField(ref errorText, value); }

    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }

    public DesktopLibrarySyncViewModel(
        GameDatabase database,
        DesktopSettings settings,
        DesktopMetadataDownloadViewModel metadataDownload,
        Action synchronizeLibrary,
        Action<string, bool> showMessage)
    {
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        this.metadataDownload = metadataDownload ?? throw new ArgumentNullException(nameof(metadataDownload));
        this.synchronizeLibrary = synchronizeLibrary ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });

        StartCommand = new AppRelayCommand(async () => await StartSyncAsync(), () => IsVisible && !IsRunning);
        CancelCommand = new AppRelayCommand(Cancel, () => IsRunning);
        CloseCommand = new AppRelayCommand(Close, () => !IsRunning);
    }

    public void ConfigureProviders(Func<List<LibraryPlugin>> libraryPluginProvider, Action libraryUpdated)
    {
        libraryPlugins = libraryPluginProvider ?? (() => new List<LibraryPlugin>());
        notifyLibraryUpdated = libraryUpdated ?? (() => { });
    }

    internal void ConfigureProvidersForTesting(
        IReadOnlyList<LibraryPlugin> libraryPluginList,
        Action libraryUpdated)
    {
        libraryPlugins = () => libraryPluginList?.ToList() ?? new List<LibraryPlugin>();
        notifyLibraryUpdated = libraryUpdated ?? (() => { });
    }

    public bool Open()
    {
        if (database == null || IsVisible || IsRunning)
        {
            return false;
        }

        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = 0;
        ProgressText = string.Empty;
        RefreshLibraries();
        RefreshGameScanners();
        if (Libraries.Count == 0 && GameScanners.Count == 0)
        {
            ErrorText = "No library plugins or saved emulated-game scanners are available.";
        }

        IsVisible = true;
        ((AppRelayCommand)StartCommand).RaiseCanExecuteChanged();
        return true;
    }

    public void Close()
    {
        if (!IsRunning)
        {
            IsVisible = false;
        }
    }

    public void Cancel()
    {
        if (IsRunning && cancellationSource?.IsCancellationRequested == false)
        {
            cancellationSource.Cancel();
            ProgressText = "Cancelling library update…";
        }
    }

    public async Task<bool> StartSyncAsync()
    {
        var selectedIds = Libraries.Where(option => option.IsSelected).Select(option => option.Id).ToHashSet();
        var selectedPlugins = (libraryPlugins() ?? new List<LibraryPlugin>())
            .Where(plugin => plugin != null && selectedIds.Contains(plugin.Id))
            .GroupBy(plugin => plugin.Id)
            .Select(group => group.First())
            .ToList();
        var selectedScannerIds = GameScanners
            .Where(option => option.IsSelected)
            .Select(option => option.Id)
            .ToHashSet();
        var selectedScanners = database.GameScanners
            .Where(scanner => selectedScannerIds.Contains(scanner.Id))
            .GroupBy(scanner => scanner.Id)
            .Select(group => group.First())
            .ToList();
        return await RunSyncAsync(
            selectedPlugins,
            selectedScanners,
            selectedPlugins.Count > 0,
            selectedScanners.Count > 0,
            CancellationToken.None);
    }

    public async Task<bool> StartScheduledSyncAsync(
        bool updateLibraries,
        bool updateEmulated,
        CancellationToken cancellationToken = default)
    {
        var selectedPlugins = updateLibraries
            ? (libraryPlugins() ?? new List<LibraryPlugin>())
                .Where(plugin => plugin != null)
                .GroupBy(plugin => plugin.Id)
                .Select(group => group.First())
                .ToList()
            : new List<LibraryPlugin>();
        var selectedScanners = updateEmulated
            ? database.GameScanners
                .Where(scanner => scanner.InGlobalUpdate)
                .GroupBy(scanner => scanner.Id)
                .Select(group => group.First())
                .ToList()
            : new List<GameScannerConfig>();
        return await RunSyncAsync(
            selectedPlugins,
            selectedScanners,
            updateLibraries,
            updateEmulated,
            cancellationToken);
    }

    private async Task<bool> RunSyncAsync(
        List<LibraryPlugin> selectedPlugins,
        List<GameScannerConfig> selectedScanners,
        bool updateLibraries,
        bool updateEmulated,
        CancellationToken externalCancellationToken)
    {
        if (IsRunning)
        {
            return false;
        }

        var sourceCount = selectedPlugins.Count + selectedScanners.Count;
        if (sourceCount == 0)
        {
            if (updateLibraries)
            {
                settings.LastLibraryUpdateCheck = DateTime.Now;
            }
            if (updateEmulated)
            {
                settings.LastEmulatedLibraryUpdateCheck = DateTime.Now;
            }
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            ErrorText = "No loaded library plugins or enabled global emulated-game scanners are available.";
            ProgressText = ErrorText;
            return true;
        }

        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = sourceCount;
        ProgressText = $"Updating libraries [0/{ProgressTotal}]";
        IsRunning = true;
        cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        var token = cancellationSource.Token;
        var addedGames = new List<Game>();
        var failures = new List<string>();
        var completedSources = 0;

        try
        {
            for (var index = 0; index < selectedPlugins.Count; index++)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var plugin = selectedPlugins[index];
                ProgressText = $"Importing {plugin.Name} [{completedSources + 1}/{sourceCount}]";
                try
                {
                    var beforeIds = database.Games.Select(game => game.Id).ToHashSet();
                    var imported = await Task.Run(
                        () => database.ImportGames(plugin, token, PlaytimeMode),
                        token);
                    var newIds = database.Games
                        .Where(game => !beforeIds.Contains(game.Id))
                        .Select(game => game.Id)
                        .ToHashSet();
                    addedGames.AddRange((imported ?? new List<Game>())
                        .Concat(database.Games.Where(game => newIds.Contains(game.Id)))
                        .GroupBy(game => game.Id)
                        .Select(group => group.First()));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Failed to update library integration {plugin.Name}.");
                    failures.Add($"{plugin.Name}: {exception.Message}");
                }

                completedSources++;
                ProgressValue = completedSources;
                await Dispatcher.UIThread.InvokeAsync(synchronizeLibrary);
            }

            foreach (var scanner in selectedScanners)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var scannerName = string.IsNullOrWhiteSpace(scanner.Name)
                    ? scanner.Directory
                    : scanner.Name;
                ProgressText = $"Scanning {scannerName} [{completedSources + 1}/{sourceCount}]";
                try
                {
                    var scanResult = await Task.Run(() =>
                    {
                        var games = new GameScanner(scanner, database).Scan(
                                token,
                                out var newPlatforms,
                                out var newRegions)
                            .Select(result => result.ToGame())
                            .ToList();
                        return new DesktopScannerImportResult(games, newPlatforms, newRegions);
                    }, token);

                    if (!token.IsCancellationRequested && scanResult.Games.Count > 0)
                    {
                        if (scanResult.NewPlatforms.Count > 0)
                        {
                            database.Platforms.Add(scanResult.NewPlatforms);
                        }

                        if (scanResult.NewRegions.Count > 0)
                        {
                            database.Regions.Add(scanResult.NewRegions);
                        }

                        var defaultStatusId = database.GetCompletionStatusSettings().DefaultStatus;
                        if (defaultStatusId != Guid.Empty)
                        {
                            scanResult.Games.ForEach(game => game.CompletionStatusId = defaultStatusId);
                        }

                        database.Games.Add(scanResult.Games);
                        addedGames.AddRange(scanResult.Games);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Failed to import emulated games from {scannerName}.");
                    failures.Add($"{scannerName}: {exception.Message}");
                }

                completedSources++;
                ProgressValue = completedSources;
                await Dispatcher.UIThread.InvokeAsync(synchronizeLibrary);
            }

            addedGames = addedGames
                .Where(game => database.Games[game.Id] != null)
                .GroupBy(game => game.Id)
                .Select(group => database.Games[group.Key])
                .ToList();
            if (!token.IsCancellationRequested && settings.GameSortingNameAutofill && addedGames.Count > 0)
            {
                SortingNameService.FillMissing(
                    database,
                    addedGames,
                    settings.GameSortingNameRemovedArticles);
            }
            if (!token.IsCancellationRequested && DownloadMetadataOnImport && addedGames.Count > 0)
            {
                ProgressValue = 0;
                ProgressTotal = addedGames.Count;
                ProgressText = $"Downloading metadata for {addedGames.Count:N0} new game(s)";
                var configured = await metadataDownload.DownloadConfiguredGamesAsync(
                    addedGames,
                    (game, index, total) => Dispatcher.UIThread.Post(() =>
                    {
                        ProgressValue = Math.Min(index + 1, total);
                        ProgressTotal = total;
                        ProgressText = $"Downloading metadata [{ProgressValue}/{ProgressTotal}]";
                    }),
                    token);
                if (!configured)
                {
                    failures.Add("Metadata-on-import is enabled, but no metadata fields or sources are configured.");
                }
            }

            if (!token.IsCancellationRequested && settings.ScanLibInstallSizeOnLibUpdate)
            {
                var installedGames = database.Games
                    .Where(game => game.IsInstalled && !string.IsNullOrEmpty(game.InstallDirectory))
                    .ToList();
                if (installedGames.Count > 0)
                {
                    ProgressValue = 0;
                    ProgressTotal = installedGames.Count;
                    ProgressText = $"Scanning install sizes for {installedGames.Count:N0} game(s)";
                    // Sizes are computed off the UI thread; the database is updated
                    // back on the UI thread so game property changes stay single-threaded.
                    var sizes = await Task.Run(() =>
                    {
                        var results = new List<KeyValuePair<Game, ulong>>();
                        for (var index = 0; index < installedGames.Count; index++)
                        {
                            if (token.IsCancellationRequested)
                            {
                                break;
                            }

                            var game = installedGames[index];
                            try
                            {
                                if (Directory.Exists(game.InstallDirectory))
                                {
                                    var size = (ulong)Playnite.Common.FileSystem.GetDirectorySize(
                                        game.InstallDirectory, false);
                                    results.Add(new KeyValuePair<Game, ulong>(game, size));
                                }
                            }
                            catch (Exception exception)
                            {
                                logger.Error(exception, $"Failed to scan install size for {game.Name}.");
                            }

                            var scanned = index + 1;
                            Dispatcher.UIThread.Post(() =>
                            {
                                ProgressValue = scanned;
                                ProgressText = $"Scanning install sizes [{scanned}/{installedGames.Count}]";
                            });
                        }

                        return results;
                    }, token);

                    foreach (var pair in sizes)
                    {
                        if (pair.Key.InstallSize != pair.Value)
                        {
                            pair.Key.InstallSize = pair.Value;
                            database.Games.Update(pair.Key);
                        }
                    }
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                synchronizeLibrary();
                notifyLibraryUpdated();
            });

            if (updateLibraries)
            {
                settings.LastLibraryUpdateCheck = DateTime.Now;
            }
            if (updateEmulated)
            {
                settings.LastEmulatedLibraryUpdateCheck = DateTime.Now;
            }
            SettingsChanged?.Invoke(this, EventArgs.Empty);

            if (token.IsCancellationRequested)
            {
                ProgressText = $"Library update cancelled after importing {addedGames.Count:N0} new game(s).";
                showMessage(ProgressText, false);
                return false;
            }

            if (failures.Count > 0)
            {
                ErrorText = string.Join(Environment.NewLine, failures);
                ProgressText = $"Library update completed with {failures.Count:N0} error(s).";
                showMessage($"{ProgressText} {ErrorText}", true);
                return false;
            }

            ProgressValue = ProgressTotal;
            ProgressText = $"Library update finished; {addedGames.Count:N0} new game(s) imported.";
            showMessage(ProgressText, false);
            IsVisible = false;
            return true;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Avalonia Desktop library update failed.");
            ErrorText = $"Library update failed: {exception.Message}";
            ProgressText = ErrorText;
            showMessage(ErrorText, true);
            return false;
        }
        finally
        {
            cancellationSource.Dispose();
            cancellationSource = null;
            IsRunning = false;
        }
    }

    private void RefreshLibraries()
    {
        Libraries.Clear();
        var savedIds = settings.LibraryPluginIds ?? new List<Guid>();
        var selectAll = !settings.LibraryPluginSelectionConfigured && savedIds.Count == 0;
        foreach (var plugin in (libraryPlugins() ?? new List<LibraryPlugin>())
                     .Where(plugin => plugin != null)
                     .GroupBy(plugin => plugin.Id)
                     .Select(group => group.First())
                     .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var option = new DesktopLibraryPluginOption(
                plugin.Id,
                plugin.Name,
                plugin.Properties?.HasCustomizedGameImport == true
                    ? "Uses the plugin's custom import workflow"
                    : "Uses the standard Playnite library import workflow",
                selectAll || savedIds.Contains(plugin.Id));
            option.PropertyChanged += Library_PropertyChanged;
            Libraries.Add(option);
        }
    }

    private void RefreshGameScanners()
    {
        GameScanners.Clear();
        var savedIds = settings.GameScannerIds ?? new List<Guid>();
        var selectGlobalScanners = !settings.GameScannerSelectionConfigured && savedIds.Count == 0;
        foreach (var scanner in database.GameScanners
                     .GroupBy(scanner => scanner.Id)
                     .Select(group => group.First())
                     .OrderBy(scanner => scanner.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var emulatorName = database.Emulators[scanner.EmulatorId]?.Name ?? "Missing emulator";
            var scannerName = string.IsNullOrWhiteSpace(scanner.Name) ? scanner.Directory : scanner.Name;
            var option = new DesktopGameScannerOption(
                scanner.Id,
                scannerName,
                $"{emulatorName} · {scanner.Directory}",
                selectGlobalScanners ? scanner.InGlobalUpdate : savedIds.Contains(scanner.Id));
            option.PropertyChanged += GameScanner_PropertyChanged;
            GameScanners.Add(option);
        }
    }

    private void Library_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopLibraryPluginOption.IsSelected))
        {
            settings.LibraryPluginSelectionConfigured = true;
            settings.LibraryPluginIds = Libraries
                .Where(option => option.IsSelected)
                .Select(option => option.Id)
                .ToList();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void GameScanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopGameScannerOption.IsSelected))
        {
            settings.GameScannerSelectionConfigured = true;
            settings.GameScannerIds = GameScanners
                .Where(option => option.IsSelected)
                .Select(option => option.Id)
                .ToList();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
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

internal sealed record DesktopScannerImportResult(
    List<Game> Games,
    List<Platform> NewPlatforms,
    List<Region> NewRegions);

public sealed class DesktopLibraryPluginOption : INotifyPropertyChanged
{
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid Id { get; }
    public string Name { get; }
    public string Detail { get; }
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
        }
    }

    public DesktopLibraryPluginOption(Guid id, string name, string detail, bool isSelected)
    {
        Id = id;
        Name = name;
        Detail = detail;
        this.isSelected = isSelected;
    }
}

public sealed class DesktopGameScannerOption : INotifyPropertyChanged
{
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid Id { get; }
    public string Name { get; }
    public string Detail { get; }
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
        }
    }

    public DesktopGameScannerOption(Guid id, string name, string detail, bool isSelected)
    {
        Id = id;
        Name = name;
        Detail = detail;
        this.isSelected = isSelected;
    }
}
