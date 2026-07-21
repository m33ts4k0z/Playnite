using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.ViewModels;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed partial class DesktopGameEditorViewModel : INotifyPropertyChanged
{
    private readonly GameDatabase database;
    private readonly Action<IReadOnlyList<Guid>> refreshGames;
    private readonly Action<string> setStatus;
    private readonly DesktopSettings settings;
    private readonly Func<string, string, bool> showImagePerformanceWarning;
    private readonly Action settingsChanged;
    private readonly Func<DesktopDialogService> dialogs;
    private readonly Dictionary<Guid, (DatabaseFieldKind Kind, DatabaseObject Model)> pendingTaxonomy = new();
    private readonly Dictionary<string, string> taxonomySearch = new(StringComparer.Ordinal);
    private IReadOnlyList<Guid> editingGameIds = Array.Empty<Guid>();
    private Action<bool?> completed;
    private bool isVisible;
    private string name;
    private string sortingName;
    private string releaseDate;
    private string userScore;
    private string criticScore;
    private string communityScore;
    private string description;
    private string notes;
    private bool favorite;
    private bool hidden;
    private DesktopMetadataOption selectedSource;
    private DesktopMetadataOption selectedCompletionStatus;
    private string validationMessage;
    private bool applyReleaseDate;
    private bool applyUserScore;
    private bool applyCriticScore;
    private bool applyCommunityScore;
    private bool applyDescription;
    private bool applyNotes;
    private bool applyFavorite;
    private bool applyHidden;
    private bool applySource;
    private bool applyCompletionStatus;
    private bool applyGenres;
    private bool applyPlatforms;
    private bool applyCategories;
    private bool applyTags;
    private bool applyDevelopers;
    private bool applyPublishers;
    private bool applyFeatures;
    private bool applySeries;
    private bool applyAgeRatings;
    private bool applyRegions;
    private string coverImage;
    private string backgroundImage;
    private string icon;
    private string originalCoverImage;
    private string originalBackgroundImage;
    private string originalIcon;
    private bool applyCoverImage;
    private bool applyBackgroundImage;
    private bool applyIcon;
    private bool applyLinks;
    private bool includeLibraryPluginAction;
    private bool applyIncludeLibraryPluginAction;
    private bool applyGameActions;
    private bool applyRoms;
    private string installDirectory;
    private bool isInstalled;
    private bool overrideInstallState;
    private string installSize;
    private string version;
    private string manual;
    private bool enableSystemHdr;
    private DateTime? lastActivity;
    private string playtimeSeconds;
    private string playCount;
    private DateTime? added;
    private string preScript;
    private string postScript;
    private string gameStartedScript;
    private bool useGlobalPreScript;
    private bool useGlobalPostScript;
    private bool useGlobalGameStartedScript;
    private bool applyInstallDirectory;
    private bool applyIsInstalled;
    private bool applyOverrideInstallState;
    private bool applyInstallSize;
    private bool applyVersion;
    private bool applyManual;
    private bool applyEnableSystemHdr;
    private bool applyLastActivity;
    private bool applyPlaytime;
    private bool applyPlayCount;
    private bool applyAdded;
    private bool applyPreScript;
    private bool applyPostScript;
    private bool applyGameStartedScript;
    private bool applyUseGlobalPreScript;
    private bool applyUseGlobalPostScript;
    private bool applyUseGlobalGameStartedScript;

    private readonly record struct RuntimeValues(ulong Playtime, ulong PlayCount, ulong? InstallSize);
    private readonly record struct ScoreValues(int? UserScore, int? CriticScore, int? CommunityScore);

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
    public bool IsSingleEdit => editingGameIds.Count == 1;
    public bool IsBulkEdit => editingGameIds.Count > 1;
    public string Title => IsBulkEdit ? $"Edit metadata for {editingGameIds.Count:N0} games" : "Edit game metadata";
    public string Name { get => name; set => SetField(ref name, value); }
    public string SortingName { get => sortingName; set => SetField(ref sortingName, value); }
    public string ReleaseDate { get => releaseDate; set => SetField(ref releaseDate, value); }
    public string UserScore { get => userScore; set => SetField(ref userScore, value); }
    public string CriticScore { get => criticScore; set => SetField(ref criticScore, value); }
    public string CommunityScore { get => communityScore; set => SetField(ref communityScore, value); }
    public string Description { get => description; set => SetField(ref description, value); }
    public string Notes { get => notes; set => SetField(ref notes, value); }
    public string InstallDirectory { get => installDirectory; set => SetField(ref installDirectory, value); }
    public bool IsInstalled { get => isInstalled; set => SetField(ref isInstalled, value); }
    public bool OverrideInstallState { get => overrideInstallState; set => SetField(ref overrideInstallState, value); }
    public string InstallSize { get => installSize; set => SetField(ref installSize, value); }
    public string Version { get => version; set => SetField(ref version, value); }
    public string Manual { get => manual; set => SetField(ref manual, value); }
    public bool EnableSystemHdr { get => enableSystemHdr; set => SetField(ref enableSystemHdr, value); }
    public DateTime? LastActivity { get => lastActivity; set => SetField(ref lastActivity, value); }
    public string PlaytimeSeconds { get => playtimeSeconds; set => SetField(ref playtimeSeconds, value); }
    public string PlayCount { get => playCount; set => SetField(ref playCount, value); }
    public DateTime? Added { get => added; set => SetField(ref added, value); }
    public string PreScript { get => preScript; set => SetField(ref preScript, value); }
    public string PostScript { get => postScript; set => SetField(ref postScript, value); }
    public string GameStartedScript { get => gameStartedScript; set => SetField(ref gameStartedScript, value); }
    public bool UseGlobalPreScript { get => useGlobalPreScript; set => SetField(ref useGlobalPreScript, value); }
    public bool UseGlobalPostScript { get => useGlobalPostScript; set => SetField(ref useGlobalPostScript, value); }
    public bool UseGlobalGameStartedScript
    {
        get => useGlobalGameStartedScript;
        set => SetField(ref useGlobalGameStartedScript, value);
    }
    public string CoverImage
    {
        get => coverImage;
        set
        {
            if (SetField(ref coverImage, value))
            {
                OnPropertyChanged(nameof(CoverPreviewPath));
                RefreshMediaInfo(EditorMediaKind.Cover);
            }
        }
    }

    public string BackgroundImage
    {
        get => backgroundImage;
        set
        {
            if (SetField(ref backgroundImage, value))
            {
                OnPropertyChanged(nameof(BackgroundPreviewPath));
                RefreshMediaInfo(EditorMediaKind.Background);
            }
        }
    }

    public string Icon
    {
        get => icon;
        set
        {
            if (SetField(ref icon, value))
            {
                OnPropertyChanged(nameof(IconPreviewPath));
                RefreshMediaInfo(EditorMediaKind.Icon);
            }
        }
    }

    public string CoverPreviewPath => ResolvePreviewPath(CoverImage);
    public string BackgroundPreviewPath => ResolvePreviewPath(BackgroundImage);
    public string IconPreviewPath => ResolvePreviewPath(Icon);
    public bool Favorite { get => favorite; set => SetField(ref favorite, value); }
    public bool Hidden { get => hidden; set => SetField(ref hidden, value); }
    public DesktopMetadataOption SelectedSource
    {
        get => selectedSource;
        set => SetField(ref selectedSource, value);
    }

    public DesktopMetadataOption SelectedCompletionStatus
    {
        get => selectedCompletionStatus;
        set => SetField(ref selectedCompletionStatus, value);
    }

    public bool ApplyReleaseDate
    {
        get => applyReleaseDate;
        set => SetApplyField(ref applyReleaseDate, value, nameof(CanEditReleaseDate));
    }

    public bool ApplyUserScore
    {
        get => applyUserScore;
        set => SetApplyField(ref applyUserScore, value, nameof(CanEditUserScore));
    }

    public bool ApplyCriticScore
    {
        get => applyCriticScore;
        set => SetApplyField(ref applyCriticScore, value, nameof(CanEditCriticScore));
    }

    public bool ApplyCommunityScore
    {
        get => applyCommunityScore;
        set => SetApplyField(ref applyCommunityScore, value, nameof(CanEditCommunityScore));
    }

    public bool ApplyDescription
    {
        get => applyDescription;
        set => SetApplyField(ref applyDescription, value, nameof(CanEditDescription));
    }

    public bool ApplyNotes
    {
        get => applyNotes;
        set => SetApplyField(ref applyNotes, value, nameof(CanEditNotes));
    }

    public bool ApplyFavorite
    {
        get => applyFavorite;
        set => SetApplyField(ref applyFavorite, value, nameof(CanEditFavorite));
    }

    public bool ApplyHidden
    {
        get => applyHidden;
        set => SetApplyField(ref applyHidden, value, nameof(CanEditHidden));
    }

    public bool ApplySource
    {
        get => applySource;
        set => SetApplyField(ref applySource, value, nameof(CanEditSource));
    }

    public bool ApplyCompletionStatus
    {
        get => applyCompletionStatus;
        set => SetApplyField(ref applyCompletionStatus, value, nameof(CanEditCompletionStatus));
    }

    public bool ApplyGenres
    {
        get => applyGenres;
        set => SetApplyField(ref applyGenres, value, nameof(CanEditGenres));
    }

    public bool ApplyPlatforms
    {
        get => applyPlatforms;
        set => SetApplyField(ref applyPlatforms, value, nameof(CanEditPlatforms));
    }

    public bool ApplyCategories
    {
        get => applyCategories;
        set => SetApplyField(ref applyCategories, value, nameof(CanEditCategories));
    }

    public bool ApplyTags
    {
        get => applyTags;
        set => SetApplyField(ref applyTags, value, nameof(CanEditTags));
    }

    public bool ApplyDevelopers
    {
        get => applyDevelopers;
        set => SetApplyField(ref applyDevelopers, value, nameof(CanEditDevelopers));
    }

    public bool ApplyPublishers
    {
        get => applyPublishers;
        set => SetApplyField(ref applyPublishers, value, nameof(CanEditPublishers));
    }

    public bool ApplyFeatures
    {
        get => applyFeatures;
        set => SetApplyField(ref applyFeatures, value, nameof(CanEditFeatures));
    }

    public bool ApplySeries
    {
        get => applySeries;
        set => SetApplyField(ref applySeries, value, nameof(CanEditSeries));
    }

    public bool ApplyAgeRatings
    {
        get => applyAgeRatings;
        set => SetApplyField(ref applyAgeRatings, value, nameof(CanEditAgeRatings));
    }

    public bool ApplyRegions
    {
        get => applyRegions;
        set => SetApplyField(ref applyRegions, value, nameof(CanEditRegions));
    }

    public bool ApplyCoverImage
    {
        get => applyCoverImage;
        set => SetApplyField(ref applyCoverImage, value, nameof(CanEditCoverImage));
    }

    public bool ApplyBackgroundImage
    {
        get => applyBackgroundImage;
        set => SetApplyField(ref applyBackgroundImage, value, nameof(CanEditBackgroundImage));
    }

    public bool ApplyIcon
    {
        get => applyIcon;
        set => SetApplyField(ref applyIcon, value, nameof(CanEditIcon));
    }

    public bool ApplyLinks
    {
        get => applyLinks;
        set => SetApplyField(ref applyLinks, value, nameof(CanEditLinks));
    }

    public bool IncludeLibraryPluginAction
    {
        get => includeLibraryPluginAction;
        set => SetField(ref includeLibraryPluginAction, value);
    }

    public bool ApplyIncludeLibraryPluginAction
    {
        get => applyIncludeLibraryPluginAction;
        set => SetApplyField(
            ref applyIncludeLibraryPluginAction,
            value,
            nameof(CanEditIncludeLibraryPluginAction));
    }

    public bool ApplyGameActions
    {
        get => applyGameActions;
        set => SetApplyField(ref applyGameActions, value, nameof(CanEditGameActions));
    }

    public bool ApplyRoms
    {
        get => applyRoms;
        set => SetApplyField(ref applyRoms, value, nameof(CanEditRoms));
    }

    public bool ApplyInstallDirectory
    {
        get => applyInstallDirectory;
        set => SetApplyField(ref applyInstallDirectory, value, nameof(CanEditInstallDirectory));
    }

    public bool ApplyIsInstalled
    {
        get => applyIsInstalled;
        set => SetApplyField(ref applyIsInstalled, value, nameof(CanEditIsInstalled));
    }

    public bool ApplyOverrideInstallState
    {
        get => applyOverrideInstallState;
        set => SetApplyField(ref applyOverrideInstallState, value, nameof(CanEditOverrideInstallState));
    }

    public bool ApplyInstallSize
    {
        get => applyInstallSize;
        set => SetApplyField(ref applyInstallSize, value, nameof(CanEditInstallSize));
    }

    public bool ApplyVersion
    {
        get => applyVersion;
        set => SetApplyField(ref applyVersion, value, nameof(CanEditVersion));
    }

    public bool ApplyManual
    {
        get => applyManual;
        set => SetApplyField(ref applyManual, value, nameof(CanEditManual));
    }

    public bool ApplyEnableSystemHdr
    {
        get => applyEnableSystemHdr;
        set => SetApplyField(ref applyEnableSystemHdr, value, nameof(CanEditEnableSystemHdr));
    }

    public bool ApplyLastActivity
    {
        get => applyLastActivity;
        set => SetApplyField(ref applyLastActivity, value, nameof(CanEditLastActivity));
    }

    public bool ApplyPlaytime
    {
        get => applyPlaytime;
        set => SetApplyField(ref applyPlaytime, value, nameof(CanEditPlaytime));
    }

    public bool ApplyPlayCount
    {
        get => applyPlayCount;
        set => SetApplyField(ref applyPlayCount, value, nameof(CanEditPlayCount));
    }

    public bool ApplyAdded
    {
        get => applyAdded;
        set => SetApplyField(ref applyAdded, value, nameof(CanEditAdded));
    }

    public bool ApplyPreScript
    {
        get => applyPreScript;
        set => SetApplyField(ref applyPreScript, value, nameof(CanEditPreScript));
    }

    public bool ApplyPostScript
    {
        get => applyPostScript;
        set => SetApplyField(ref applyPostScript, value, nameof(CanEditPostScript));
    }

    public bool ApplyGameStartedScript
    {
        get => applyGameStartedScript;
        set => SetApplyField(ref applyGameStartedScript, value, nameof(CanEditGameStartedScript));
    }

    public bool ApplyUseGlobalPreScript
    {
        get => applyUseGlobalPreScript;
        set => SetApplyField(ref applyUseGlobalPreScript, value, nameof(CanEditUseGlobalPreScript));
    }

    public bool ApplyUseGlobalPostScript
    {
        get => applyUseGlobalPostScript;
        set => SetApplyField(ref applyUseGlobalPostScript, value, nameof(CanEditUseGlobalPostScript));
    }

    public bool ApplyUseGlobalGameStartedScript
    {
        get => applyUseGlobalGameStartedScript;
        set => SetApplyField(
            ref applyUseGlobalGameStartedScript,
            value,
            nameof(CanEditUseGlobalGameStartedScript));
    }

    public bool CanEditReleaseDate => IsSingleEdit || ApplyReleaseDate;
    public bool CanEditUserScore => IsSingleEdit || ApplyUserScore;
    public bool CanEditCriticScore => IsSingleEdit || ApplyCriticScore;
    public bool CanEditCommunityScore => IsSingleEdit || ApplyCommunityScore;
    public bool CanEditDescription => IsSingleEdit || ApplyDescription;
    public bool CanEditNotes => IsSingleEdit || ApplyNotes;
    public bool CanEditFavorite => IsSingleEdit || ApplyFavorite;
    public bool CanEditHidden => IsSingleEdit || ApplyHidden;
    public bool CanEditSource => IsSingleEdit || ApplySource;
    public bool CanEditCompletionStatus => IsSingleEdit || ApplyCompletionStatus;
    public bool CanEditGenres => IsSingleEdit || ApplyGenres;
    public bool CanEditPlatforms => IsSingleEdit || ApplyPlatforms;
    public bool CanEditCategories => IsSingleEdit || ApplyCategories;
    public bool CanEditTags => IsSingleEdit || ApplyTags;
    public bool CanEditDevelopers => IsSingleEdit || ApplyDevelopers;
    public bool CanEditPublishers => IsSingleEdit || ApplyPublishers;
    public bool CanEditFeatures => IsSingleEdit || ApplyFeatures;
    public bool CanEditSeries => IsSingleEdit || ApplySeries;
    public bool CanEditAgeRatings => IsSingleEdit || ApplyAgeRatings;
    public bool CanEditRegions => IsSingleEdit || ApplyRegions;
    public bool CanEditCoverImage => IsSingleEdit || ApplyCoverImage;
    public bool CanEditBackgroundImage => IsSingleEdit || ApplyBackgroundImage;
    public bool CanEditIcon => IsSingleEdit || ApplyIcon;
    public bool CanEditLinks => IsSingleEdit || ApplyLinks;
    public bool CanEditIncludeLibraryPluginAction => IsSingleEdit || ApplyIncludeLibraryPluginAction;
    public bool CanEditGameActions => IsSingleEdit || ApplyGameActions;
    public bool CanEditRoms => IsSingleEdit || ApplyRoms;
    public bool CanEditInstallDirectory => IsSingleEdit || ApplyInstallDirectory;
    public bool CanEditIsInstalled => IsSingleEdit || ApplyIsInstalled;
    public bool CanEditOverrideInstallState => IsSingleEdit || ApplyOverrideInstallState;
    public bool CanEditInstallSize => IsSingleEdit || ApplyInstallSize;
    public bool CanEditVersion => IsSingleEdit || ApplyVersion;
    public bool CanEditManual => IsSingleEdit || ApplyManual;
    public bool CanEditEnableSystemHdr => IsSingleEdit || ApplyEnableSystemHdr;
    public bool CanEditLastActivity => IsSingleEdit || ApplyLastActivity;
    public bool CanEditPlaytime => IsSingleEdit || ApplyPlaytime;
    public bool CanEditPlayCount => IsSingleEdit || ApplyPlayCount;
    public bool CanEditAdded => IsSingleEdit || ApplyAdded;
    public bool CanEditPreScript => IsSingleEdit || ApplyPreScript;
    public bool CanEditPostScript => IsSingleEdit || ApplyPostScript;
    public bool CanEditGameStartedScript => IsSingleEdit || ApplyGameStartedScript;
    public bool CanEditUseGlobalPreScript => IsSingleEdit || ApplyUseGlobalPreScript;
    public bool CanEditUseGlobalPostScript => IsSingleEdit || ApplyUseGlobalPostScript;
    public bool CanEditUseGlobalGameStartedScript => IsSingleEdit || ApplyUseGlobalGameStartedScript;

    public string ValidationMessage
    {
        get => validationMessage;
        private set
        {
            if (SetField(ref validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
    public ObservableCollection<DesktopMetadataOption> Sources { get; } = new();
    public ObservableCollection<DesktopMetadataOption> CompletionStatuses { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Genres { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Platforms { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Categories { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Tags { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Developers { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Publishers { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Features { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Series { get; } = new();
    public ObservableCollection<DesktopMetadataOption> AgeRatings { get; } = new();
    public ObservableCollection<DesktopMetadataOption> Regions { get; } = new();
    public string GenresSearch { get => GetTaxonomySearch(nameof(Genres)); set => SetTaxonomySearch(nameof(Genres), value, Genres); }
    public string PlatformsSearch { get => GetTaxonomySearch(nameof(Platforms)); set => SetTaxonomySearch(nameof(Platforms), value, Platforms); }
    public string CategoriesSearch { get => GetTaxonomySearch(nameof(Categories)); set => SetTaxonomySearch(nameof(Categories), value, Categories); }
    public string TagsSearch { get => GetTaxonomySearch(nameof(Tags)); set => SetTaxonomySearch(nameof(Tags), value, Tags); }
    public string DevelopersSearch { get => GetTaxonomySearch(nameof(Developers)); set => SetTaxonomySearch(nameof(Developers), value, Developers); }
    public string PublishersSearch { get => GetTaxonomySearch(nameof(Publishers)); set => SetTaxonomySearch(nameof(Publishers), value, Publishers); }
    public string FeaturesSearch { get => GetTaxonomySearch(nameof(Features)); set => SetTaxonomySearch(nameof(Features), value, Features); }
    public string SeriesSearch { get => GetTaxonomySearch(nameof(Series)); set => SetTaxonomySearch(nameof(Series), value, Series); }
    public string AgeRatingsSearch { get => GetTaxonomySearch(nameof(AgeRatings)); set => SetTaxonomySearch(nameof(AgeRatings), value, AgeRatings); }
    public string RegionsSearch { get => GetTaxonomySearch(nameof(Regions)); set => SetTaxonomySearch(nameof(Regions), value, Regions); }
    public IReadOnlyList<DesktopEmulatorOption> Emulators { get; }
    public ObservableCollection<DesktopLinkEditorItem> Links { get; } = new();
    public ObservableCollection<DesktopGameActionEditorItem> GameActions { get; } = new();
    public ObservableCollection<DesktopRomEditorItem> Roms { get; } = new();
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddLinkCommand { get; }
    public ICommand AddGameActionCommand { get; }
    public ICommand AddRomCommand { get; }
    public ICommand AddTaxonomyCommand { get; }

    public DesktopGameEditorViewModel(
        GameDatabase database,
        Action<IReadOnlyList<Guid>> refreshGames,
        Action<string> setStatus,
        DesktopSettings settings,
        Func<string, string, bool> showImagePerformanceWarning,
        Action settingsChanged,
        Func<DesktopDialogService> dialogs)
    {
        this.database = database;
        this.refreshGames = refreshGames ?? (_ => { });
        this.setStatus = setStatus ?? (_ => { });
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.showImagePerformanceWarning = showImagePerformanceWarning ?? ((_, _) => false);
        this.settingsChanged = settingsChanged ?? (() => { });
        this.dialogs = dialogs ?? (() => null);
        ReloadTaxonomyOptions();
        Emulators = BuildEmulatorOptions(database?.Emulators);
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        AddLinkCommand = new RelayCommand(
            () => AddLinkItem(null),
            () => CanEditLinks);
        AddGameActionCommand = new RelayCommand(
            () => AddGameActionItem(null),
            () => CanEditGameActions);
        AddRomCommand = new RelayCommand(
            () => AddRomItem(null),
            () => CanEditRoms);
        AddTaxonomyCommand = new AppRelayCommand(parameter => AddTaxonomy(parameter as string));
        InitializeParityCommands();
    }

    public bool Open(Guid gameId, Action<bool?> onCompleted = null) =>
        Open(new[] { gameId }, onCompleted);

    public bool Open(IReadOnlyList<Guid> gameIds, Action<bool?> onCompleted = null)
    {
        if (database == null || IsVisible || gameIds == null || gameIds.Count == 0)
        {
            return false;
        }

        var distinctIds = gameIds.Distinct().ToList();
        var games = distinctIds.Select(id => database.Games[id]).Where(game => game != null).ToList();
        if (games.Count != distinctIds.Count)
        {
            return false;
        }

        editingGameIds = distinctIds;
        completed = onCompleted;
        pendingTaxonomy.Clear();
        ResetTaxonomySearch();
        ReloadTaxonomyOptions();
        ResetApplyFlags();
        LoadCommonValues(games);
        RefreshEditorParityState(games);
        ValidationMessage = string.Empty;
        OnPropertyChanged(nameof(IsSingleEdit));
        OnPropertyChanged(nameof(IsBulkEdit));
        OnPropertyChanged(nameof(Title));
        RaiseEditabilityProperties();
        IsVisible = true;
        return true;
    }

    private void LoadCommonValues(IReadOnlyList<Game> games)
    {
        var first = games[0];
        Name = IsSingleEdit ? first.Name ?? string.Empty : string.Empty;
        SortingName = IsSingleEdit ? first.SortingName ?? string.Empty : string.Empty;
        ReleaseDate = CommonValue(games, game => game.ReleaseDate?.Serialize() ?? string.Empty);
        UserScore = CommonValue(games, game => game.UserScore?.ToString() ?? string.Empty);
        CriticScore = CommonValue(games, game => game.CriticScore?.ToString() ?? string.Empty);
        CommunityScore = CommonValue(games, game => game.CommunityScore?.ToString() ?? string.Empty);
        Description = CommonValue(games, game => game.Description ?? string.Empty);
        Notes = CommonValue(games, game => game.Notes ?? string.Empty);
        InstallDirectory = CommonValue(games, game => game.InstallDirectory ?? string.Empty);
        IsInstalled = CommonValue(games, game => game.IsInstalled);
        OverrideInstallState = CommonValue(games, game => game.OverrideInstallState);
        InstallSize = CommonValue(games, game => game.InstallSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        Version = CommonValue(games, game => game.Version ?? string.Empty);
        Manual = CommonValue(games, game => game.Manual ?? string.Empty);
        EnableSystemHdr = CommonValue(games, game => game.EnableSystemHdr);
        LastActivity = CommonValue(games, game => game.LastActivity);
        PlaytimeSeconds = CommonValue(games, game => game.Playtime.ToString(CultureInfo.InvariantCulture));
        PlayCount = CommonValue(games, game => game.PlayCount.ToString(CultureInfo.InvariantCulture));
        Added = CommonValue(games, game => game.Added);
        PreScript = CommonValue(games, game => game.PreScript ?? string.Empty);
        PostScript = CommonValue(games, game => game.PostScript ?? string.Empty);
        GameStartedScript = CommonValue(games, game => game.GameStartedScript ?? string.Empty);
        UseGlobalPreScript = CommonValue(games, game => game.UseGlobalPreScript);
        UseGlobalPostScript = CommonValue(games, game => game.UseGlobalPostScript);
        UseGlobalGameStartedScript = CommonValue(games, game => game.UseGlobalGameStartedScript);
        Favorite = CommonValue(games, game => game.Favorite);
        Hidden = CommonValue(games, game => game.Hidden);
        var sourceId = CommonValue(games, game => game.SourceId);
        var completionStatusId = CommonValue(games, game => game.CompletionStatusId);
        SelectedSource = Sources.FirstOrDefault(option => option.Id == sourceId) ?? Sources[0];
        SelectedCompletionStatus = CompletionStatuses
            .FirstOrDefault(option => option.Id == completionStatusId) ?? CompletionStatuses[0];
        SetSelectionState(Genres, games, game => game.GenreIds);
        SetSelectionState(Platforms, games, game => game.PlatformIds);
        SetSelectionState(Categories, games, game => game.CategoryIds);
        SetSelectionState(Tags, games, game => game.TagIds);
        SetSelectionState(Developers, games, game => game.DeveloperIds);
        SetSelectionState(Publishers, games, game => game.PublisherIds);
        SetSelectionState(Features, games, game => game.FeatureIds);
        SetSelectionState(Series, games, game => game.SeriesIds);
        SetSelectionState(AgeRatings, games, game => game.AgeRatingIds);
        SetSelectionState(Regions, games, game => game.RegionIds);
        CoverImage = CommonValue(games, game => game.CoverImage ?? string.Empty);
        BackgroundImage = CommonValue(games, game => game.BackgroundImage ?? string.Empty);
        Icon = CommonValue(games, game => game.Icon ?? string.Empty);
        originalCoverImage = CoverImage;
        originalBackgroundImage = BackgroundImage;
        originalIcon = Icon;
        Links.Clear();
        foreach (var link in CommonLinks(games))
        {
            AddLinkItem(link);
        }

        IncludeLibraryPluginAction = CommonValue(games, game => game.IncludeLibraryPluginAction);
        GameActions.Clear();
        foreach (var action in CommonGameActions(games))
        {
            AddGameActionItem(action);
        }

        Roms.Clear();
        foreach (var rom in CommonRoms(games))
        {
            AddRomItem(rom);
        }
    }

    private void Save()
    {
        if (!IsVisible || database == null)
        {
            return;
        }

        if (IsSingleEdit && string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "A game name is required.";
            return;
        }

        if (IsBulkEdit && !HasBulkChanges())
        {
            ValidationMessage = "Choose at least one metadata field to apply to the selected games.";
            return;
        }

        if (!TryParseReleaseDate(out var parsedReleaseDate) || !TryParseScores(out var scores))
        {
            return;
        }

        if (!TryParseRuntimeValues(out var runtimeValues))
        {
            return;
        }

        CheckImagePerformanceLimits();

        if (!TryBuildLinks(out var preparedLinks))
        {
            return;
        }

        if (!TryBuildGameActions(out var preparedGameActions) || !TryBuildRoms(out var preparedRoms))
        {
            return;
        }

        var games = editingGameIds.Select(id => database.Games[id]?.GetCopy()).ToList();
        if (games.Any(game => game == null))
        {
            ValidationMessage = "One or more selected games no longer exist in the library.";
            return;
        }

        var addedMedia = new List<string>();
        try
        {
            if (!TryPrepareMedia(games, addedMedia))
            {
                RemoveAddedMedia(addedMedia);
                return;
            }
        }
        catch (Exception exception)
        {
            RemoveAddedMedia(addedMedia);
            ValidationMessage = $"Media could not be prepared: {exception.Message}";
            return;
        }

        var changeDate = DateTime.Now;
        try
        {
            PersistPendingTaxonomy();
            database.Games.BeginBufferUpdate();
            try
            {
                foreach (var game in games)
                {
                    if (IsSingleEdit)
                    {
                        ApplySingleGameValues(
                            game,
                            parsedReleaseDate,
                            scores,
                            runtimeValues,
                            preparedLinks,
                            preparedGameActions,
                            preparedRoms);
                    }
                    else
                    {
                        ApplyBulkValues(
                            game,
                            parsedReleaseDate,
                            scores,
                            runtimeValues,
                            preparedLinks,
                            preparedGameActions,
                            preparedRoms);
                    }

                    game.Modified = changeDate;
                    database.Games.Update(game);
                }
            }
            finally
            {
                database.Games.EndBufferUpdate();
            }
        }
        catch (Exception exception)
        {
            ValidationMessage = $"Metadata could not be saved: {exception.Message}";
            return;
        }

        refreshGames(editingGameIds);
        setStatus(IsSingleEdit
            ? $"Saved metadata for {games[0].Name}."
            : $"Saved metadata for {games.Count:N0} games.");
        CleanupEditorTemporaryFiles();
        Complete(true);
    }

    private void ApplySingleGameValues(
        Game game,
        ReleaseDate? parsedReleaseDate,
        ScoreValues scores,
        RuntimeValues runtimeValues,
        IReadOnlyList<Link> preparedLinks,
        IReadOnlyList<GameAction> preparedGameActions,
        IReadOnlyList<GameRom> preparedRoms)
    {
        game.Name = Name.Trim();
        game.SortingName = NullIfWhiteSpace(SortingName);
        game.ReleaseDate = parsedReleaseDate;
        game.UserScore = scores.UserScore;
        game.CriticScore = scores.CriticScore;
        game.CommunityScore = scores.CommunityScore;
        game.Description = Description ?? string.Empty;
        game.Notes = Notes ?? string.Empty;
        game.InstallDirectory = NullIfWhiteSpace(InstallDirectory);
        game.IsInstalled = IsInstalled;
        game.OverrideInstallState = OverrideInstallState;
        if (game.InstallSize != runtimeValues.InstallSize)
        {
            game.LastSizeScanDate = DateTime.Now;
        }

        game.InstallSize = runtimeValues.InstallSize;
        game.Version = NullIfWhiteSpace(Version);
        game.Manual = NullIfWhiteSpace(Manual);
        game.EnableSystemHdr = EnableSystemHdr;
        game.LastActivity = LastActivity;
        game.Playtime = runtimeValues.Playtime;
        game.PlayCount = runtimeValues.PlayCount;
        game.Added = Added;
        game.PreScript = NullIfWhiteSpace(PreScript);
        game.PostScript = NullIfWhiteSpace(PostScript);
        game.GameStartedScript = NullIfWhiteSpace(GameStartedScript);
        game.UseGlobalPreScript = UseGlobalPreScript;
        game.UseGlobalPostScript = UseGlobalPostScript;
        game.UseGlobalGameStartedScript = UseGlobalGameStartedScript;
        game.Favorite = Favorite;
        game.Hidden = Hidden;
        game.SourceId = SelectedSource?.Id ?? Guid.Empty;
        game.CompletionStatusId = SelectedCompletionStatus?.Id ?? Guid.Empty;
        game.GenreIds = SelectedIds(Genres);
        game.PlatformIds = SelectedIds(Platforms);
        game.CategoryIds = SelectedIds(Categories);
        game.TagIds = SelectedIds(Tags);
        game.DeveloperIds = SelectedIds(Developers);
        game.PublisherIds = SelectedIds(Publishers);
        game.FeatureIds = SelectedIds(Features);
        game.SeriesIds = SelectedIds(Series);
        game.AgeRatingIds = SelectedIds(AgeRatings);
        game.RegionIds = SelectedIds(Regions);
        game.Links = new ObservableCollection<Link>(preparedLinks.Select(link => link.GetCopy()));
        game.IncludeLibraryPluginAction = IncludeLibraryPluginAction;
        game.GameActions = new ObservableCollection<GameAction>(
            preparedGameActions.Select(action => action.GetCopy()));
        game.Roms = new ObservableCollection<GameRom>(preparedRoms.Select(rom => rom.GetCopy()));
    }

    private void ApplyBulkValues(
        Game game,
        ReleaseDate? parsedReleaseDate,
        ScoreValues scores,
        RuntimeValues runtimeValues,
        IReadOnlyList<Link> preparedLinks,
        IReadOnlyList<GameAction> preparedGameActions,
        IReadOnlyList<GameRom> preparedRoms)
    {
        if (ApplyReleaseDate) game.ReleaseDate = parsedReleaseDate;
        if (ApplyUserScore) game.UserScore = scores.UserScore;
        if (ApplyCriticScore) game.CriticScore = scores.CriticScore;
        if (ApplyCommunityScore) game.CommunityScore = scores.CommunityScore;
        if (ApplyDescription) game.Description = Description ?? string.Empty;
        if (ApplyNotes) game.Notes = Notes ?? string.Empty;
        if (ApplyInstallDirectory) game.InstallDirectory = NullIfWhiteSpace(InstallDirectory);
        if (ApplyIsInstalled) game.IsInstalled = IsInstalled;
        if (ApplyOverrideInstallState) game.OverrideInstallState = OverrideInstallState;
        if (ApplyInstallSize)
        {
            if (game.InstallSize != runtimeValues.InstallSize)
            {
                game.LastSizeScanDate = DateTime.Now;
            }

            game.InstallSize = runtimeValues.InstallSize;
        }

        if (ApplyVersion) game.Version = NullIfWhiteSpace(Version);
        if (ApplyManual) game.Manual = NullIfWhiteSpace(Manual);
        if (ApplyEnableSystemHdr) game.EnableSystemHdr = EnableSystemHdr;
        if (ApplyLastActivity) game.LastActivity = LastActivity;
        if (ApplyPlaytime) game.Playtime = runtimeValues.Playtime;
        if (ApplyPlayCount) game.PlayCount = runtimeValues.PlayCount;
        if (ApplyAdded) game.Added = Added;
        if (ApplyPreScript) game.PreScript = NullIfWhiteSpace(PreScript);
        if (ApplyPostScript) game.PostScript = NullIfWhiteSpace(PostScript);
        if (ApplyGameStartedScript) game.GameStartedScript = NullIfWhiteSpace(GameStartedScript);
        if (ApplyUseGlobalPreScript) game.UseGlobalPreScript = UseGlobalPreScript;
        if (ApplyUseGlobalPostScript) game.UseGlobalPostScript = UseGlobalPostScript;
        if (ApplyUseGlobalGameStartedScript) game.UseGlobalGameStartedScript = UseGlobalGameStartedScript;
        if (ApplyFavorite) game.Favorite = Favorite;
        if (ApplyHidden) game.Hidden = Hidden;
        if (ApplySource) game.SourceId = SelectedSource?.Id ?? Guid.Empty;
        if (ApplyCompletionStatus) game.CompletionStatusId = SelectedCompletionStatus?.Id ?? Guid.Empty;
        if (ApplyGenres) game.GenreIds = MergeSelectedIds(game.GenreIds, Genres);
        if (ApplyPlatforms) game.PlatformIds = MergeSelectedIds(game.PlatformIds, Platforms);
        if (ApplyCategories) game.CategoryIds = MergeSelectedIds(game.CategoryIds, Categories);
        if (ApplyTags) game.TagIds = MergeSelectedIds(game.TagIds, Tags);
        if (ApplyDevelopers) game.DeveloperIds = MergeSelectedIds(game.DeveloperIds, Developers);
        if (ApplyPublishers) game.PublisherIds = MergeSelectedIds(game.PublisherIds, Publishers);
        if (ApplyFeatures) game.FeatureIds = MergeSelectedIds(game.FeatureIds, Features);
        if (ApplySeries) game.SeriesIds = MergeSelectedIds(game.SeriesIds, Series);
        if (ApplyAgeRatings) game.AgeRatingIds = MergeSelectedIds(game.AgeRatingIds, AgeRatings);
        if (ApplyRegions) game.RegionIds = MergeSelectedIds(game.RegionIds, Regions);
        if (ApplyLinks) game.Links = new ObservableCollection<Link>(preparedLinks.Select(link => link.GetCopy()));
        if (ApplyIncludeLibraryPluginAction) game.IncludeLibraryPluginAction = IncludeLibraryPluginAction;
        if (ApplyGameActions)
        {
            game.GameActions = new ObservableCollection<GameAction>(
                preparedGameActions.Select(action => action.GetCopy()));
        }

        if (ApplyRoms)
        {
            game.Roms = new ObservableCollection<GameRom>(preparedRoms.Select(rom => rom.GetCopy()));
        }
    }

    private bool TryBuildLinks(out IReadOnlyList<Link> preparedLinks)
    {
        var result = new List<Link>();
        preparedLinks = result;
        if (IsBulkEdit && !ApplyLinks)
        {
            return true;
        }

        foreach (var item in Links)
        {
            var name = item.Name?.Trim();
            var url = item.Url?.Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                ValidationMessage = "Every game link requires both a name and URL.";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ValidationMessage = $"Link '{name}' must use an HTTP or HTTPS URL.";
                return false;
            }

            result.Add(new Link(name, uri.AbsoluteUri));
        }

        return true;
    }

    private bool TryBuildGameActions(out IReadOnlyList<GameAction> preparedActions)
    {
        var result = new List<GameAction>();
        preparedActions = result;
        if (IsBulkEdit && !ApplyGameActions)
        {
            return true;
        }

        foreach (var item in GameActions)
        {
            var action = item.ToGameAction();
            if (action.TrackingMode == TrackingMode.Directory &&
                string.IsNullOrWhiteSpace(action.TrackingPath))
            {
                ValidationMessage = $"Action '{action.Name ?? "Unnamed action"}' requires a tracking directory.";
                return false;
            }

            if (action.InitialTrackingDelay < 0 || action.TrackingFrequency < 0)
            {
                ValidationMessage = $"Action '{action.Name ?? "Unnamed action"}' cannot use negative tracking timing values.";
                return false;
            }

            result.Add(action);
        }

        return true;
    }

    private bool TryBuildRoms(out IReadOnlyList<GameRom> preparedRoms)
    {
        preparedRoms = IsBulkEdit && !ApplyRoms
            ? Array.Empty<GameRom>()
            : Roms.Select(rom => rom.ToGameRom()).ToList();
        return true;
    }

    private bool TryPrepareMedia(IReadOnlyList<Game> games, ICollection<string> addedMedia)
    {
        foreach (var game in games)
        {
            if (IsSingleEdit || ApplyCoverImage)
            {
                if (!TryPrepareMediaValue(
                    CoverImage, game.CoverImage, game.Id, false, "cover image", addedMedia, out var cover))
                {
                    return false;
                }

                game.CoverImage = cover;
            }

            if (IsSingleEdit || ApplyBackgroundImage)
            {
                if (!TryPrepareMediaValue(
                    BackgroundImage, game.BackgroundImage, game.Id, true, "background image", addedMedia, out var background))
                {
                    return false;
                }

                game.BackgroundImage = background;
            }

            if (IsSingleEdit || ApplyIcon)
            {
                if (!TryPrepareMediaValue(
                    Icon, game.Icon, game.Id, false, "icon", addedMedia, out var iconValue))
                {
                    return false;
                }

                game.Icon = iconValue;
            }
        }

        return true;
    }

    private bool TryPrepareMediaValue(
        string input,
        string existing,
        Guid gameId,
        bool allowRemoteReference,
        string fieldName,
        ICollection<string> addedMedia,
        out string prepared)
    {
        prepared = null;
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (string.Equals(value, existing, StringComparison.Ordinal))
        {
            prepared = existing;
            return true;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (!allowRemoteReference)
            {
                ValidationMessage = $"The {fieldName} must be a local file. Remote media downloads are the next metadata tranche.";
                return false;
            }

            prepared = uri.AbsoluteUri;
            return true;
        }

        if (!Path.IsPathFullyQualified(value))
        {
            ValidationMessage = $"The {fieldName} must use a fully qualified local file path.";
            return false;
        }

        if (!File.Exists(value))
        {
            ValidationMessage = $"The {fieldName} file does not exist: {value}";
            return false;
        }

        prepared = database.AddFile(value, gameId, true, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(prepared))
        {
            ValidationMessage = $"The {fieldName} could not be imported into the Playnite database.";
            return false;
        }

        addedMedia.Add(prepared);
        return true;
    }

    private void RemoveAddedMedia(IEnumerable<string> addedMedia)
    {
        foreach (var path in addedMedia)
        {
            try
            {
                database.RemoveFile(path);
            }
            catch
            {
            }
        }
    }

    private bool TryParseReleaseDate(out ReleaseDate? parsedReleaseDate)
    {
        parsedReleaseDate = null;
        if (IsBulkEdit && !ApplyReleaseDate || string.IsNullOrWhiteSpace(ReleaseDate))
        {
            return true;
        }

        if (!Playnite.SDK.Models.ReleaseDate.TryDeserialize(ReleaseDate.Trim(), out var date))
        {
            ValidationMessage = "Release date must use YYYY, YYYY-M, or YYYY-M-D.";
            return false;
        }

        parsedReleaseDate = date;
        return true;
    }

    private bool TryParseScores(out ScoreValues scores)
    {
        scores = default;
        if (!TryParseScore(UserScore, IsSingleEdit || ApplyUserScore, "User score", out var user) ||
            !TryParseScore(CriticScore, IsSingleEdit || ApplyCriticScore, "Critic score", out var critic) ||
            !TryParseScore(
                CommunityScore,
                IsSingleEdit || ApplyCommunityScore,
                "Community score",
                out var community))
        {
            return false;
        }

        scores = new ScoreValues(user, critic, community);
        return true;
    }

    private bool TryParseScore(string input, bool shouldParse, string fieldName, out int? value)
    {
        value = null;
        if (!shouldParse || string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        if (!int.TryParse(input.Trim(), out var parsed) || parsed is < 0 or > 100)
        {
            ValidationMessage = $"{fieldName} must be a whole number from 0 to 100.";
            return false;
        }

        value = parsed;
        return true;
    }

    private bool TryParseRuntimeValues(out RuntimeValues values)
    {
        values = default;
        if (!TryParseUnsignedValue(
                PlaytimeSeconds,
                IsSingleEdit || ApplyPlaytime,
                false,
                "Playtime",
                out var parsedPlaytime) ||
            !TryParseUnsignedValue(
                PlayCount,
                IsSingleEdit || ApplyPlayCount,
                false,
                "Play count",
                out var parsedPlayCount) ||
            !TryParseUnsignedValue(
                InstallSize,
                IsSingleEdit || ApplyInstallSize,
                true,
                "Install size",
                out var parsedInstallSize))
        {
            return false;
        }

        values = new RuntimeValues(parsedPlaytime ?? 0, parsedPlayCount ?? 0, parsedInstallSize);
        return true;
    }

    private bool TryParseUnsignedValue(
        string input,
        bool shouldParse,
        bool isNullable,
        string fieldName,
        out ulong? value)
    {
        value = null;
        if (!shouldParse)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            value = isNullable ? null : 0;
            return true;
        }

        if (!ulong.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            ValidationMessage = $"{fieldName} must be a non-negative whole number.";
            return false;
        }

        value = parsed;
        return true;
    }

    private bool HasBulkChanges() =>
        ApplyReleaseDate || ApplyUserScore || ApplyCriticScore || ApplyCommunityScore ||
        ApplyDescription || ApplyNotes ||
        ApplyFavorite || ApplyHidden || ApplySource || ApplyCompletionStatus ||
        ApplyGenres || ApplyPlatforms || ApplyCategories || ApplyTags ||
        ApplyDevelopers || ApplyPublishers || ApplyFeatures || ApplySeries ||
        ApplyAgeRatings || ApplyRegions || ApplyCoverImage ||
        ApplyBackgroundImage || ApplyIcon || ApplyLinks ||
        ApplyIncludeLibraryPluginAction || ApplyGameActions || ApplyRoms ||
        ApplyInstallDirectory || ApplyIsInstalled || ApplyOverrideInstallState ||
        ApplyInstallSize || ApplyVersion || ApplyManual || ApplyEnableSystemHdr ||
        ApplyLastActivity || ApplyPlaytime || ApplyPlayCount || ApplyAdded ||
        ApplyPreScript || ApplyPostScript || ApplyGameStartedScript ||
        ApplyUseGlobalPreScript || ApplyUseGlobalPostScript || ApplyUseGlobalGameStartedScript;

    private void ResetApplyFlags()
    {
        ApplyReleaseDate = false;
        ApplyUserScore = false;
        ApplyCriticScore = false;
        ApplyCommunityScore = false;
        ApplyDescription = false;
        ApplyNotes = false;
        ApplyFavorite = false;
        ApplyHidden = false;
        ApplySource = false;
        ApplyCompletionStatus = false;
        ApplyGenres = false;
        ApplyPlatforms = false;
        ApplyCategories = false;
        ApplyTags = false;
        ApplyDevelopers = false;
        ApplyPublishers = false;
        ApplyFeatures = false;
        ApplySeries = false;
        ApplyAgeRatings = false;
        ApplyRegions = false;
        ApplyCoverImage = false;
        ApplyBackgroundImage = false;
        ApplyIcon = false;
        ApplyLinks = false;
        ApplyIncludeLibraryPluginAction = false;
        ApplyGameActions = false;
        ApplyRoms = false;
        ApplyInstallDirectory = false;
        ApplyIsInstalled = false;
        ApplyOverrideInstallState = false;
        ApplyInstallSize = false;
        ApplyVersion = false;
        ApplyManual = false;
        ApplyEnableSystemHdr = false;
        ApplyLastActivity = false;
        ApplyPlaytime = false;
        ApplyPlayCount = false;
        ApplyAdded = false;
        ApplyPreScript = false;
        ApplyPostScript = false;
        ApplyGameStartedScript = false;
        ApplyUseGlobalPreScript = false;
        ApplyUseGlobalPostScript = false;
        ApplyUseGlobalGameStartedScript = false;
    }

    private void Cancel()
    {
        CleanupEditorTemporaryFiles();
        Complete(false);
    }

    private void Complete(bool? result)
    {
        if (!IsVisible)
        {
            return;
        }

        IsVisible = false;
        pendingTaxonomy.Clear();
        var callback = completed;
        completed = null;
        callback?.Invoke(result);
    }

    private void AddGameActionItem(GameAction action)
    {
        GameActions.Add(new DesktopGameActionEditorItem(
            action,
            Emulators,
            item => MoveItem(GameActions, item, -1),
            item => MoveItem(GameActions, item, 1),
            item => GameActions.Remove(item),
            SelectActionPath,
            SelectActionWorkingDirectory,
            SelectActionTrackingPath,
            TestActionScript));
    }

    private void AddRomItem(GameRom rom)
    {
        Roms.Add(new DesktopRomEditorItem(
            rom,
            item => MoveItem(Roms, item, -1),
            item => MoveItem(Roms, item, 1),
            item => Roms.Remove(item),
            SelectRomPath));
    }

    private void AddLinkItem(Link link)
    {
        Links.Add(new DesktopLinkEditorItem(
            link,
            item => MoveItem(Links, item, -1),
            item => MoveItem(Links, item, 1),
            item => Links.Remove(item)));
    }

    private static void MoveItem<T>(ObservableCollection<T> items, T item, int offset)
    {
        var currentIndex = items.IndexOf(item);
        var targetIndex = currentIndex + offset;
        if (currentIndex >= 0 && targetIndex >= 0 && targetIndex < items.Count)
        {
            items.Move(currentIndex, targetIndex);
        }
    }

    internal bool AddTaxonomyForTest(string field, string name) => AddTaxonomy(field, name, false);

    private void AddTaxonomy(string field)
    {
        var result = dialogs()?.ShowInput(
            "Enter a name for the new library field value.",
            "Add library field",
            string.Empty);
        if (result?.Result == true)
        {
            AddTaxonomy(field, result.SelectedString, true);
        }
    }

    private bool AddTaxonomy(string field, string name, bool reportDuplicate)
    {
        name = name?.Trim();
        if (!IsVisible || string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        var target = GetTaxonomyOptions(field);
        if (target == null)
        {
            return false;
        }
        var existing = target.FirstOrDefault(option => option.Id != Guid.Empty &&
            string.Equals(option.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing != null)
        {
            SelectTaxonomyOption(field, existing);
            if (reportDuplicate)
            {
                setStatus($"'{name}' already exists and was selected.");
            }
            return true;
        }

        var kind = GetTaxonomyKind(field);
        var model = CreateTaxonomyModel(kind, name);
        pendingTaxonomy[model.Id] = (kind, model);
        var option = new DesktopMetadataOption(model.Id, model.Name)
        {
            IsSelected = true,
            AllowIndeterminate = IsBulkEdit
        };
        InsertTaxonomyOption(target, option, field is "Source" or "CompletionStatus");
        if (kind == DatabaseFieldKind.Companies)
        {
            var other = field == "Developers" ? Publishers : Developers;
            InsertTaxonomyOption(other, new DesktopMetadataOption(model.Id, model.Name)
            {
                AllowIndeterminate = IsBulkEdit
            }, false);
        }
        SelectTaxonomyOption(field, option);
        setStatus($"Created '{name}'. It will be saved with the game metadata.");
        return true;
    }

    private ObservableCollection<DesktopMetadataOption> GetTaxonomyOptions(string field) => field switch
    {
        "Source" => Sources,
        "CompletionStatus" => CompletionStatuses,
        "Genres" => Genres,
        "Platforms" => Platforms,
        "Categories" => Categories,
        "Tags" => Tags,
        "Developers" => Developers,
        "Publishers" => Publishers,
        "Features" => Features,
        "Series" => Series,
        "AgeRatings" => AgeRatings,
        "Regions" => Regions,
        _ => null
    };

    private static DatabaseFieldKind GetTaxonomyKind(string field) => field switch
    {
        "Source" => DatabaseFieldKind.Sources,
        "CompletionStatus" => DatabaseFieldKind.CompletionStatuses,
        "Genres" => DatabaseFieldKind.Genres,
        "Platforms" => DatabaseFieldKind.Platforms,
        "Categories" => DatabaseFieldKind.Categories,
        "Tags" => DatabaseFieldKind.Tags,
        "Developers" or "Publishers" => DatabaseFieldKind.Companies,
        "Features" => DatabaseFieldKind.Features,
        "Series" => DatabaseFieldKind.Series,
        "AgeRatings" => DatabaseFieldKind.AgeRatings,
        "Regions" => DatabaseFieldKind.Regions,
        _ => throw new NotSupportedException($"Taxonomy creation is not supported for {field}.")
    };

    private static DatabaseObject CreateTaxonomyModel(DatabaseFieldKind kind, string name) => kind switch
    {
        DatabaseFieldKind.Sources => new GameSource(name),
        DatabaseFieldKind.CompletionStatuses => new CompletionStatus(name),
        DatabaseFieldKind.Genres => new Genre(name),
        DatabaseFieldKind.Platforms => new Platform(name),
        DatabaseFieldKind.Categories => new Category(name),
        DatabaseFieldKind.Tags => new Tag(name),
        DatabaseFieldKind.Companies => new Company(name),
        DatabaseFieldKind.Features => new GameFeature(name),
        DatabaseFieldKind.Series => new Series(name),
        DatabaseFieldKind.AgeRatings => new AgeRating(name),
        DatabaseFieldKind.Regions => new Region(name),
        _ => throw new NotSupportedException($"Taxonomy creation is not supported for {kind}.")
    };

    private static void InsertTaxonomyOption(
        ObservableCollection<DesktopMetadataOption> collection,
        DesktopMetadataOption option,
        bool hasEmptyOption)
    {
        var start = hasEmptyOption && collection.FirstOrDefault()?.Id == Guid.Empty ? 1 : 0;
        var index = start;
        while (index < collection.Count && StringComparer.CurrentCultureIgnoreCase.Compare(
            collection[index].Name, option.Name) < 0)
        {
            index++;
        }
        collection.Insert(index, option);
    }

    private void SelectTaxonomyOption(string field, DesktopMetadataOption option)
    {
        if (field == "Source")
        {
            SelectedSource = option;
            if (IsBulkEdit) ApplySource = true;
            return;
        }
        if (field == "CompletionStatus")
        {
            SelectedCompletionStatus = option;
            if (IsBulkEdit) ApplyCompletionStatus = true;
            return;
        }
        option.IsSelected = true;
        if (!IsBulkEdit)
        {
            return;
        }
        switch (field)
        {
            case "Genres": ApplyGenres = true; break;
            case "Platforms": ApplyPlatforms = true; break;
            case "Categories": ApplyCategories = true; break;
            case "Tags": ApplyTags = true; break;
            case "Developers": ApplyDevelopers = true; break;
            case "Publishers": ApplyPublishers = true; break;
            case "Features": ApplyFeatures = true; break;
            case "Series": ApplySeries = true; break;
            case "AgeRatings": ApplyAgeRatings = true; break;
            case "Regions": ApplyRegions = true; break;
        }
    }

    private void PersistPendingTaxonomy()
    {
        foreach (var pending in pendingTaxonomy.Values)
        {
            switch (pending.Kind)
            {
                case DatabaseFieldKind.Sources:
                    AddPending(database.Sources, (GameSource)pending.Model);
                    break;
                case DatabaseFieldKind.CompletionStatuses:
                    AddPending(database.CompletionStatuses, (CompletionStatus)pending.Model);
                    break;
                case DatabaseFieldKind.Genres:
                    AddPending(database.Genres, (Genre)pending.Model);
                    break;
                case DatabaseFieldKind.Platforms:
                    AddPending(database.Platforms, (Platform)pending.Model);
                    break;
                case DatabaseFieldKind.Categories:
                    AddPending(database.Categories, (Category)pending.Model);
                    break;
                case DatabaseFieldKind.Tags:
                    AddPending(database.Tags, (Tag)pending.Model);
                    break;
                case DatabaseFieldKind.Companies:
                    AddPending(database.Companies, (Company)pending.Model);
                    break;
                case DatabaseFieldKind.Features:
                    AddPending(database.Features, (GameFeature)pending.Model);
                    break;
                case DatabaseFieldKind.Series:
                    AddPending(database.Series, (Series)pending.Model);
                    break;
                case DatabaseFieldKind.AgeRatings:
                    AddPending(database.AgeRatings, (AgeRating)pending.Model);
                    break;
                case DatabaseFieldKind.Regions:
                    AddPending(database.Regions, (Region)pending.Model);
                    break;
            }
        }
    }

    private static void AddPending<T>(Playnite.SDK.IItemCollection<T> collection, T item) where T : DatabaseObject
    {
        if (collection[item.Id] == null)
        {
            collection.Add(item);
        }
    }

    private void ReloadTaxonomyOptions()
    {
        ReplaceOptions(Sources, BuildOptions(
            database?.Sources?.Select(source => new DesktopMetadataOption(source.Id, source.Name)), "No source"));
        ReplaceOptions(CompletionStatuses, BuildOptions(
            database?.CompletionStatuses?.Select(status => new DesktopMetadataOption(status.Id, status.Name)), "No status"));
        ReplaceOptions(Genres, BuildMultiOptions(database?.Genres?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Platforms, BuildMultiOptions(database?.Platforms?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Categories, BuildMultiOptions(database?.Categories?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Tags, BuildMultiOptions(database?.Tags?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Developers, BuildMultiOptions(database?.Companies?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Publishers, BuildMultiOptions(database?.Companies?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Features, BuildMultiOptions(database?.Features?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Series, BuildMultiOptions(database?.Series?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(AgeRatings, BuildMultiOptions(database?.AgeRatings?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
        ReplaceOptions(Regions, BuildMultiOptions(database?.Regions?.Select(item => new DesktopMetadataOption(item.Id, item.Name))));
    }

    private static void ReplaceOptions(
        ObservableCollection<DesktopMetadataOption> destination,
        IEnumerable<DesktopMetadataOption> source)
    {
        destination.Clear();
        foreach (var item in source ?? Array.Empty<DesktopMetadataOption>())
        {
            destination.Add(item);
        }
    }

    private string GetTaxonomySearch(string field) => taxonomySearch.TryGetValue(field, out var value)
        ? value
        : string.Empty;

    private void SetTaxonomySearch(
        string field,
        string value,
        IEnumerable<DesktopMetadataOption> options,
        [CallerMemberName] string propertyName = null)
    {
        value ??= string.Empty;
        if (string.Equals(GetTaxonomySearch(field), value, StringComparison.Ordinal))
        {
            return;
        }
        taxonomySearch[field] = value;
        foreach (var option in options)
        {
            option.IsVisible = string.IsNullOrWhiteSpace(value) ||
                option.Name.Contains(value.Trim(), StringComparison.CurrentCultureIgnoreCase);
        }
        OnPropertyChanged(propertyName);
    }

    private void ResetTaxonomySearch()
    {
        taxonomySearch.Clear();
        foreach (var property in new[]
        {
            nameof(GenresSearch), nameof(PlatformsSearch), nameof(CategoriesSearch), nameof(TagsSearch),
            nameof(DevelopersSearch), nameof(PublishersSearch), nameof(FeaturesSearch), nameof(SeriesSearch),
            nameof(AgeRatingsSearch), nameof(RegionsSearch)
        })
        {
            OnPropertyChanged(property);
        }
    }

    private static IReadOnlyList<DesktopMetadataOption> BuildOptions(
        IEnumerable<DesktopMetadataOption> values,
        string emptyName)
    {
        var result = new List<DesktopMetadataOption> { new(Guid.Empty, emptyName) };
        if (values != null)
        {
            result.AddRange(values.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase));
        }

        return result;
    }

    private static IReadOnlyList<DesktopMetadataOption> BuildMultiOptions(
        IEnumerable<DesktopMetadataOption> values) =>
        values?.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase).ToList() ??
        new List<DesktopMetadataOption>();

    private static IReadOnlyList<DesktopEmulatorOption> BuildEmulatorOptions(
        IEnumerable<Emulator> values) =>
        values?.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(emulator => new DesktopEmulatorOption(
                emulator.Id,
                emulator.Name,
                emulator.AllProfiles.Select(profile =>
                    new DesktopEmulatorProfileOption(profile.Id, profile.Name))))
            .ToList() ?? new List<DesktopEmulatorOption>();

    private static void SetSelectionState(
        IEnumerable<DesktopMetadataOption> options,
        IReadOnlyList<Game> games,
        Func<Game, IReadOnlyCollection<Guid>> selector)
    {
        var selections = games
            .Select(game => new HashSet<Guid>(selector(game) ?? Array.Empty<Guid>()))
            .ToList();
        foreach (var option in options)
        {
            option.AllowIndeterminate = games.Count > 1;
            var selectedCount = selections.Count(selection => selection.Contains(option.Id));
            option.IsSelected = selectedCount == 0
                ? false
                : selectedCount == selections.Count
                    ? true
                    : null;
        }
    }

    private static IReadOnlyList<Link> CommonLinks(IReadOnlyList<Game> games)
    {
        var first = games[0].Links?
            .Where(link => link != null)
            .Select(link => link.GetCopy())
            .ToList() ?? new List<Link>();
        return games.Skip(1).All(game =>
            (game.Links?.Where(link => link != null).ToList() ?? new List<Link>()).SequenceEqual(first))
                ? first
                : Array.Empty<Link>();
    }

    private static IReadOnlyList<GameAction> CommonGameActions(IReadOnlyList<Game> games)
    {
        var first = games[0].GameActions?
            .Where(action => action != null)
            .Select(action => action.GetCopy())
            .ToList() ?? new List<GameAction>();
        return games.Skip(1).All(game =>
            (game.GameActions?.Where(action => action != null).ToList() ?? new List<GameAction>())
                .SequenceEqual(first))
                    ? first
                    : Array.Empty<GameAction>();
    }

    private static IReadOnlyList<GameRom> CommonRoms(IReadOnlyList<Game> games)
    {
        var first = games[0].Roms?
            .Where(rom => rom != null)
            .Select(rom => rom.GetCopy())
            .ToList() ?? new List<GameRom>();
        return games.Skip(1).All(game =>
            (game.Roms?.Where(rom => rom != null).ToList() ?? new List<GameRom>())
                .SequenceEqual(first))
                    ? first
                    : Array.Empty<GameRom>();
    }

    private static List<Guid> SelectedIds(IEnumerable<DesktopMetadataOption> options) =>
        options.Where(option => option.IsSelected == true).Select(option => option.Id).ToList();

    private static List<Guid> MergeSelectedIds(
        IReadOnlyCollection<Guid> currentIds,
        IEnumerable<DesktopMetadataOption> options)
    {
        var result = new List<Guid>(currentIds ?? Array.Empty<Guid>());
        var selected = new HashSet<Guid>(result);
        foreach (var option in options)
        {
            if (option.IsSelected == true && selected.Add(option.Id))
            {
                result.Add(option.Id);
            }
            else if (option.IsSelected == false && selected.Remove(option.Id))
            {
                result.Remove(option.Id);
            }
        }

        return result;
    }

    private string ResolvePreviewPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            return value;
        }

        return Path.IsPathFullyQualified(value) ? value : database?.GetFullFilePath(value);
    }

    private void CheckImagePerformanceLimits()
    {
        if (!settings.ShowImagePerformanceWarning)
        {
            return;
        }

        var oversized =
            HasOversizedImage(CoverImage, originalCoverImage, GameDatabase.MaximumRecommendedCoverSize) ||
            HasOversizedImage(
                BackgroundImage,
                originalBackgroundImage,
                GameDatabase.MaximumRecommendedBackgroundSize) ||
            HasOversizedImage(Icon, originalIcon, GameDatabase.MaximumRecommendedIconSize);
        if (!oversized)
        {
            return;
        }

        var message = DesktopLocalization.Resolve(
            "LOCGameImageSizeWarning",
            "The selected image may be too large for optimal performance. " +
            "Very large images can reduce UI responsiveness and increase memory usage.");
        var caption = DesktopLocalization.Resolve(
            "LOCPerformanceWarningTitle",
            "Performance Warning");

        if (showImagePerformanceWarning(caption, message))
        {
            settings.ShowImagePerformanceWarning = false;
            settingsChanged();
        }
    }

    private bool HasOversizedImage(string value, string originalValue, double maximumMegapixels)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, originalValue, StringComparison.Ordinal) ||
            (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            return false;
        }

        var path = ResolvePreviewPath(value);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var image = new global::Avalonia.Media.Imaging.Bitmap(path);
            var megapixels = image.PixelSize.Width * (double)image.PixelSize.Height / 1_000_000;
            return megapixels > maximumMegapixels;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            setStatus($"Image dimensions could not be inspected for '{path}': {exception.Message}");
            return false;
        }
    }

    private static T CommonValue<T>(IReadOnlyList<Game> games, Func<Game, T> selector)
    {
        var first = selector(games[0]);
        return games.Skip(1).All(game => EqualityComparer<T>.Default.Equals(first, selector(game)))
            ? first
            : default;
    }

    private static string NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void SetApplyField(ref bool field, bool value, string editabilityProperty)
    {
        if (SetField(ref field, value))
        {
            OnPropertyChanged(editabilityProperty);
            RaiseCollectionCommandState(editabilityProperty);
        }
    }

    private void RaiseEditabilityProperties()
    {
        OnPropertyChanged(nameof(CanEditReleaseDate));
        OnPropertyChanged(nameof(CanEditUserScore));
        OnPropertyChanged(nameof(CanEditCriticScore));
        OnPropertyChanged(nameof(CanEditCommunityScore));
        OnPropertyChanged(nameof(CanEditDescription));
        OnPropertyChanged(nameof(CanEditNotes));
        OnPropertyChanged(nameof(CanEditFavorite));
        OnPropertyChanged(nameof(CanEditHidden));
        OnPropertyChanged(nameof(CanEditSource));
        OnPropertyChanged(nameof(CanEditCompletionStatus));
        OnPropertyChanged(nameof(CanEditGenres));
        OnPropertyChanged(nameof(CanEditPlatforms));
        OnPropertyChanged(nameof(CanEditCategories));
        OnPropertyChanged(nameof(CanEditTags));
        OnPropertyChanged(nameof(CanEditDevelopers));
        OnPropertyChanged(nameof(CanEditPublishers));
        OnPropertyChanged(nameof(CanEditFeatures));
        OnPropertyChanged(nameof(CanEditSeries));
        OnPropertyChanged(nameof(CanEditAgeRatings));
        OnPropertyChanged(nameof(CanEditRegions));
        OnPropertyChanged(nameof(CanEditCoverImage));
        OnPropertyChanged(nameof(CanEditBackgroundImage));
        OnPropertyChanged(nameof(CanEditIcon));
        OnPropertyChanged(nameof(CanEditLinks));
        OnPropertyChanged(nameof(CanEditIncludeLibraryPluginAction));
        OnPropertyChanged(nameof(CanEditGameActions));
        OnPropertyChanged(nameof(CanEditRoms));
        OnPropertyChanged(nameof(CanEditInstallDirectory));
        OnPropertyChanged(nameof(CanEditIsInstalled));
        OnPropertyChanged(nameof(CanEditOverrideInstallState));
        OnPropertyChanged(nameof(CanEditInstallSize));
        OnPropertyChanged(nameof(CanEditVersion));
        OnPropertyChanged(nameof(CanEditManual));
        OnPropertyChanged(nameof(CanEditEnableSystemHdr));
        OnPropertyChanged(nameof(CanEditLastActivity));
        OnPropertyChanged(nameof(CanEditPlaytime));
        OnPropertyChanged(nameof(CanEditPlayCount));
        OnPropertyChanged(nameof(CanEditAdded));
        OnPropertyChanged(nameof(CanEditPreScript));
        OnPropertyChanged(nameof(CanEditPostScript));
        OnPropertyChanged(nameof(CanEditGameStartedScript));
        OnPropertyChanged(nameof(CanEditUseGlobalPreScript));
        OnPropertyChanged(nameof(CanEditUseGlobalPostScript));
        OnPropertyChanged(nameof(CanEditUseGlobalGameStartedScript));
        ((RelayCommand)AddLinkCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddGameActionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddRomCommand).RaiseCanExecuteChanged();
    }

    private void RaiseCollectionCommandState(string editabilityProperty)
    {
        if (editabilityProperty == nameof(CanEditLinks))
        {
            ((RelayCommand)AddLinkCommand).RaiseCanExecuteChanged();
        }
        else if (editabilityProperty == nameof(CanEditGameActions))
        {
            ((RelayCommand)AddGameActionCommand).RaiseCanExecuteChanged();
        }
        else if (editabilityProperty == nameof(CanEditRoms))
        {
            ((RelayCommand)AddRomCommand).RaiseCanExecuteChanged();
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
