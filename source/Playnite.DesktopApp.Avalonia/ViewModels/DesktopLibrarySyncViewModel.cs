using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
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
        if (Libraries.Count == 0)
        {
            ErrorText = "No library plugins are currently loaded.";
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
        if (IsRunning)
        {
            return false;
        }

        var selectedIds = Libraries.Where(option => option.IsSelected).Select(option => option.Id).ToHashSet();
        var selectedPlugins = (libraryPlugins() ?? new List<LibraryPlugin>())
            .Where(plugin => plugin != null && selectedIds.Contains(plugin.Id))
            .GroupBy(plugin => plugin.Id)
            .Select(group => group.First())
            .ToList();
        if (selectedPlugins.Count == 0)
        {
            ErrorText = "Select at least one loaded library plugin.";
            return false;
        }

        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = selectedPlugins.Count;
        ProgressText = $"Updating libraries [0/{ProgressTotal}]";
        IsRunning = true;
        cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;
        var addedGames = new List<Game>();
        var failures = new List<string>();

        try
        {
            for (var index = 0; index < selectedPlugins.Count; index++)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var plugin = selectedPlugins[index];
                ProgressText = $"Importing {plugin.Name} [{index + 1}/{selectedPlugins.Count}]";
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

                ProgressValue = index + 1;
                await Dispatcher.UIThread.InvokeAsync(synchronizeLibrary);
            }

            addedGames = addedGames
                .Where(game => database.Games[game.Id] != null)
                .GroupBy(game => game.Id)
                .Select(group => database.Games[group.Key])
                .ToList();
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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                synchronizeLibrary();
                notifyLibraryUpdated();
            });

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
        var selectAll = savedIds.Count == 0;
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

    private void Library_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopLibraryPluginOption.IsSelected))
        {
            settings.LibraryPluginIds = Libraries
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
