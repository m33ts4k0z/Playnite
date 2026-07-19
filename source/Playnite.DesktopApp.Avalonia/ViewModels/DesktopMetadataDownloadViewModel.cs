using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Playnite.Avalonia.App.ViewModels;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Metadata;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopMetadataDownloadViewModel : INotifyPropertyChanged
{
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private readonly Func<MetadataGamesSource, IReadOnlyList<Game>> resolveGames;
    private readonly Action<IReadOnlyList<Guid>> refreshGames;
    private readonly Action<string, bool> showMessage;
    private Func<List<MetadataPlugin>> metadataPlugins = () => new List<MetadataPlugin>();
    private Func<List<LibraryPlugin>> libraryPlugins = () => new List<LibraryPlugin>();
    private CancellationTokenSource cancellationSource;
    private bool isVisible;
    private bool isRunning;
    private int progressValue;
    private int progressTotal;
    private string progressText = string.Empty;
    private string errorText;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler SettingsChanged;

    public ObservableCollection<DesktopMetadataSourceOption> Sources { get; } = new();
    public ObservableCollection<DesktopMetadataFieldOption> Fields { get; }
    public IReadOnlyList<DesktopMetadataTargetOption> Targets { get; } = new[]
    {
        new DesktopMetadataTargetOption(MetadataGamesSource.Selected, "Selected game"),
        new DesktopMetadataTargetOption(MetadataGamesSource.Filtered, "Filtered games"),
        new DesktopMetadataTargetOption(MetadataGamesSource.AllFromDB, "Entire library")
    };

    public DesktopMetadataTargetOption SelectedTarget
    {
        get => Targets.FirstOrDefault(option => option.Source == settings.MetadataGamesSource) ?? Targets[0];
        set
        {
            if (value == null || value.Source == settings.MetadataGamesSource)
            {
                return;
            }

            settings.MetadataGamesSource = value.Source;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TargetSummary));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool SkipExistingValues
    {
        get => settings.MetadataSkipExistingValues;
        set
        {
            if (settings.MetadataSkipExistingValues == value)
            {
                return;
            }

            settings.MetadataSkipExistingValues = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool DownloadBackgroundsImmediately
    {
        get => settings.DownloadBackgroundsImmediately;
        set
        {
            if (settings.DownloadBackgroundsImmediately == value)
            {
                return;
            }

            settings.DownloadBackgroundsImmediately = value;
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
                ((RelayCommand)DownloadCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CloseCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanConfigure => !IsRunning;
    public int ProgressValue { get => progressValue; private set => SetField(ref progressValue, value); }
    public int ProgressTotal { get => progressTotal; private set => SetField(ref progressTotal, value); }
    public string ProgressText { get => progressText; private set => SetField(ref progressText, value); }
    public string ErrorText { get => errorText; private set => SetField(ref errorText, value); }
    public string TargetSummary => $"{ResolveTargetGames().Count:N0} game(s) selected for metadata download";

    public ICommand DownloadCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }

    public DesktopMetadataDownloadViewModel(
        GameDatabase database,
        DesktopSettings settings,
        Func<MetadataGamesSource, IReadOnlyList<Game>> resolveGames,
        Action<IReadOnlyList<Guid>> refreshGames,
        Action<string, bool> showMessage)
    {
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        this.resolveGames = resolveGames ?? throw new ArgumentNullException(nameof(resolveGames));
        this.refreshGames = refreshGames ?? (_ => { });
        this.showMessage = showMessage ?? ((_, _) => { });

        Fields = new ObservableCollection<DesktopMetadataFieldOption>(CreateFieldOptions());
        foreach (var field in Fields)
        {
            field.PropertyChanged += Field_PropertyChanged;
        }

        DownloadCommand = new RelayCommand(async () => await StartDownloadAsync(), () => IsVisible && !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        CloseCommand = new RelayCommand(Close, () => !IsRunning);
    }

    public void ConfigureProviders(
        Func<List<MetadataPlugin>> metadataPluginProvider,
        Func<List<LibraryPlugin>> libraryPluginProvider)
    {
        metadataPlugins = metadataPluginProvider ?? (() => new List<MetadataPlugin>());
        libraryPlugins = libraryPluginProvider ?? (() => new List<LibraryPlugin>());
    }

    public void RefreshTargetSummary() => OnPropertyChanged(nameof(TargetSummary));

    internal void ConfigureProvidersForTesting(
        IReadOnlyList<MetadataPlugin> metadataPluginList,
        IReadOnlyList<LibraryPlugin> libraryPluginList)
    {
        metadataPlugins = () => metadataPluginList?.ToList() ?? new List<MetadataPlugin>();
        libraryPlugins = () => libraryPluginList?.ToList() ?? new List<LibraryPlugin>();
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
        RefreshSources();
        OnPropertyChanged(nameof(SelectedTarget));
        OnPropertyChanged(nameof(TargetSummary));
        IsVisible = true;
        ((RelayCommand)DownloadCommand).RaiseCanExecuteChanged();
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
            ProgressText = "Cancelling metadata download…";
        }
    }

    public async Task<bool> StartDownloadAsync()
    {
        if (IsRunning)
        {
            return false;
        }

        var games = ResolveTargetGames()
            .Where(game => game != null)
            .GroupBy(game => game.Id)
            .Select(group => group.First())
            .ToList();
        var sourceIds = Sources.Where(source => source.IsSelected).Select(source => source.Id).ToList();
        var selectedFields = Fields.Where(field => field.IsSelected).Select(field => field.Field).ToList();
        if (games.Count == 0)
        {
            ErrorText = "There are no games in the selected scope.";
            return false;
        }

        if (sourceIds.Count == 0)
        {
            ErrorText = "Select at least one metadata source.";
            return false;
        }

        if (selectedFields.Count == 0)
        {
            ErrorText = "Select at least one metadata field.";
            return false;
        }

        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = games.Count;
        ProgressText = $"Downloading metadata [0/{games.Count}]";
        IsRunning = true;
        cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;
        var fieldSettings = BuildSettings(sourceIds, selectedFields);
        var gameIds = games.Select(game => game.Id).ToList();

        try
        {
            await DownloadGamesAsync(
                games,
                fieldSettings,
                (game, index, total) => Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = Math.Min(index + 1, total);
                    ProgressTotal = total;
                    ProgressText = $"Downloading metadata [{ProgressValue}/{ProgressTotal}]";
                }), token);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                refreshGames(gameIds);
                OnPropertyChanged(nameof(TargetSummary));
            });

            if (token.IsCancellationRequested)
            {
                ProgressText = $"Metadata download cancelled after {ProgressValue} of {ProgressTotal} game(s).";
                showMessage(ProgressText, false);
                return false;
            }

            ProgressValue = ProgressTotal;
            ProgressText = $"Metadata download finished for {games.Count:N0} game(s).";
            showMessage(ProgressText, false);
            IsVisible = false;
            return true;
        }
        catch (Exception exception)
        {
            ErrorText = $"Metadata download failed: {exception.Message}";
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

    private IReadOnlyList<Game> ResolveTargetGames() =>
        resolveGames(settings.MetadataGamesSource) ?? Array.Empty<Game>();

    internal async Task<bool> DownloadConfiguredGamesAsync(
        IReadOnlyList<Game> games,
        Action<Game, int, int> progressCallback,
        CancellationToken cancelToken)
    {
        MetadataDownloaderSettings fieldSettings;
        if (settings.UsePerFieldMetadataSettings)
        {
            fieldSettings = MetadataSettingsUtilities.Clone(settings.MetadataSettings);
            fieldSettings.GamesSource = settings.MetadataGamesSource;
            fieldSettings.SkipExistingValues = settings.MetadataSkipExistingValues;
            var configured = MetadataSettingsUtilities.SupportedFields
                .Select(field => MetadataSettingsUtilities.GetField(fieldSettings, field))
                .Any(field => field.Import && field.Sources?.Count > 0);
            if (!configured)
            {
                return false;
            }
        }
        else
        {
            var selectedFields = settings.MetadataFields ?? new List<MetadataField>();
            if (selectedFields.Count == 0)
            {
                return false;
            }

            var sourceIds = settings.MetadataSourceIds?.Count > 0
                ? settings.MetadataSourceIds.ToList()
                : new[] { Guid.Empty }
                    .Concat((metadataPlugins() ?? new List<MetadataPlugin>())
                        .Where(plugin => plugin != null)
                        .Select(plugin => plugin.Id))
                    .Distinct()
                    .ToList();
            if (sourceIds.Count == 0)
            {
                return false;
            }

            fieldSettings = BuildSettings(sourceIds, selectedFields);
        }

        await DownloadGamesAsync(games, fieldSettings, progressCallback, cancelToken);
        return true;
    }

    private async Task DownloadGamesAsync(
        IReadOnlyList<Game> games,
        MetadataDownloaderSettings fieldSettings,
        Action<Game, int, int> progressCallback,
        CancellationToken cancelToken)
    {
        using var downloader = new MetadataDownloader(
            database,
            (metadataPlugins() ?? new List<MetadataPlugin>()).Where(plugin => plugin != null).ToList(),
            (libraryPlugins() ?? new List<LibraryPlugin>()).Where(plugin => plugin != null).ToList());
        await downloader.DownloadMetadataAsync(
            games?.ToList() ?? new List<Game>(),
            fieldSettings,
            new MetadataRuntimeSettings(DownloadBackgroundsImmediately),
            progressCallback,
            cancelToken);
    }

    private void RefreshSources()
    {
        Sources.Clear();
        var savedIds = settings.MetadataSourceIds ?? new List<Guid>();
        var selectAll = savedIds.Count == 0;
        AddSource(new DesktopMetadataSourceOption(
            Guid.Empty,
            "Official library metadata",
            "Used when the game's library plugin supplies metadata",
            selectAll || savedIds.Contains(Guid.Empty)));

        foreach (var plugin in (metadataPlugins() ?? new List<MetadataPlugin>())
                     .Where(plugin => plugin != null)
                     .GroupBy(plugin => plugin.Id)
                     .Select(group => group.First())
                     .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var supported = plugin.SupportedFields?.Count > 0
                ? string.Join(", ", plugin.SupportedFields.Select(GetFieldName))
                : "No advertised fields";
            AddSource(new DesktopMetadataSourceOption(
                plugin.Id,
                plugin.Name,
                supported,
                selectAll || savedIds.Contains(plugin.Id)));
        }
    }

    private void AddSource(DesktopMetadataSourceOption source)
    {
        source.PropertyChanged += Source_PropertyChanged;
        Sources.Add(source);
    }

    private void Source_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopMetadataSourceOption.IsSelected))
        {
            settings.MetadataSourceIds = Sources.Where(source => source.IsSelected).Select(source => source.Id).ToList();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Field_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopMetadataFieldOption.IsSelected))
        {
            settings.MetadataFields = Fields.Where(field => field.IsSelected).Select(field => field.Field).ToList();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private MetadataDownloaderSettings BuildSettings(List<Guid> sourceIds, List<MetadataField> selectedFields)
    {
        var result = new MetadataDownloaderSettings
        {
            GamesSource = settings.MetadataGamesSource,
            SkipExistingValues = SkipExistingValues
        };
        result.ConfigureFields(sourceIds, false);
        foreach (var field in selectedFields)
        {
            GetFieldSettings(result, field).Import = true;
        }

        return result;
    }

    private IEnumerable<DesktopMetadataFieldOption> CreateFieldOptions()
    {
        var selected = settings.MetadataFields ?? DesktopSettings.GetDefaultMetadataFields();
        foreach (var field in Enum.GetValues<MetadataField>())
        {
            yield return new DesktopMetadataFieldOption(field, GetFieldName(field), selected.Contains(field));
        }
    }

    private static MetadataFieldSettings GetFieldSettings(MetadataDownloaderSettings settings, MetadataField field) =>
        field switch
        {
            MetadataField.Name => settings.Name,
            MetadataField.Genres => settings.Genre,
            MetadataField.ReleaseDate => settings.ReleaseDate,
            MetadataField.Developers => settings.Developer,
            MetadataField.Publishers => settings.Publisher,
            MetadataField.Tags => settings.Tag,
            MetadataField.Description => settings.Description,
            MetadataField.Links => settings.Links,
            MetadataField.CriticScore => settings.CriticScore,
            MetadataField.CommunityScore => settings.CommunityScore,
            MetadataField.Icon => settings.Icon,
            MetadataField.CoverImage => settings.CoverImage,
            MetadataField.BackgroundImage => settings.BackgroundImage,
            MetadataField.Features => settings.Feature,
            MetadataField.AgeRating => settings.AgeRating,
            MetadataField.Series => settings.Series,
            MetadataField.Region => settings.Region,
            MetadataField.Platform => settings.Platform,
            MetadataField.InstallSize => settings.InstallSize,
            _ => throw new NotSupportedException($"Unsupported metadata field {field}.")
        };

    private static string GetFieldName(MetadataField field) => field switch
    {
        MetadataField.Name => "Name",
        MetadataField.Genres => "Genres",
        MetadataField.ReleaseDate => "Release date",
        MetadataField.Developers => "Developers",
        MetadataField.Publishers => "Publishers",
        MetadataField.Tags => "Tags",
        MetadataField.Description => "Description",
        MetadataField.Links => "Links",
        MetadataField.CriticScore => "Critic score",
        MetadataField.CommunityScore => "Community score",
        MetadataField.Icon => "Icon",
        MetadataField.CoverImage => "Cover image",
        MetadataField.BackgroundImage => "Background image",
        MetadataField.Features => "Features",
        MetadataField.AgeRating => "Age ratings",
        MetadataField.Series => "Series",
        MetadataField.Region => "Regions",
        MetadataField.Platform => "Platforms",
        MetadataField.InstallSize => "Install size",
        _ => field.ToString()
    };

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

    private sealed class MetadataRuntimeSettings : IMetadataDownloadSettings
    {
        public bool DownloadBackgroundsImmediately { get; }

        public MetadataRuntimeSettings(bool downloadBackgroundsImmediately)
        {
            DownloadBackgroundsImmediately = downloadBackgroundsImmediately;
        }
    }
}

public sealed class DesktopMetadataTargetOption
{
    public MetadataGamesSource Source { get; }
    public string Name { get; }

    public DesktopMetadataTargetOption(MetadataGamesSource source, string name)
    {
        Source = source;
        Name = name;
    }

    public override string ToString() => Name;
}

public sealed class DesktopMetadataSourceOption : INotifyPropertyChanged
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

    public DesktopMetadataSourceOption(Guid id, string name, string detail, bool isSelected)
    {
        Id = id;
        Name = name;
        Detail = detail;
        this.isSelected = isSelected;
    }
}

public sealed class DesktopMetadataFieldOption : INotifyPropertyChanged
{
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public MetadataField Field { get; }
    public string Name { get; }
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

    public DesktopMetadataFieldOption(MetadataField field, string name, bool isSelected)
    {
        Field = field;
        Name = name;
        this.isSelected = isSelected;
    }
}
