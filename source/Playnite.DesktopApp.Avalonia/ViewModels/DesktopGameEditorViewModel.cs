using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.ViewModels;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopGameEditorViewModel : INotifyPropertyChanged
{
    private readonly GameDatabase database;
    private readonly Action<IReadOnlyList<Guid>> refreshGames;
    private readonly Action<string> setStatus;
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
    public IReadOnlyList<DesktopMetadataOption> Sources { get; }
    public IReadOnlyList<DesktopMetadataOption> CompletionStatuses { get; }
    public IReadOnlyList<DesktopMetadataOption> Genres { get; }
    public IReadOnlyList<DesktopMetadataOption> Platforms { get; }
    public IReadOnlyList<DesktopMetadataOption> Categories { get; }
    public IReadOnlyList<DesktopMetadataOption> Tags { get; }
    public IReadOnlyList<DesktopMetadataOption> Developers { get; }
    public IReadOnlyList<DesktopMetadataOption> Publishers { get; }
    public IReadOnlyList<DesktopMetadataOption> Features { get; }
    public IReadOnlyList<DesktopMetadataOption> Series { get; }
    public IReadOnlyList<DesktopMetadataOption> AgeRatings { get; }
    public IReadOnlyList<DesktopMetadataOption> Regions { get; }
    public IReadOnlyList<DesktopEmulatorOption> Emulators { get; }
    public ObservableCollection<DesktopLinkEditorItem> Links { get; } = new();
    public ObservableCollection<DesktopGameActionEditorItem> GameActions { get; } = new();
    public ObservableCollection<DesktopRomEditorItem> Roms { get; } = new();
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddLinkCommand { get; }
    public ICommand AddGameActionCommand { get; }
    public ICommand AddRomCommand { get; }

    public DesktopGameEditorViewModel(
        GameDatabase database,
        Action<IReadOnlyList<Guid>> refreshGames,
        Action<string> setStatus)
    {
        this.database = database;
        this.refreshGames = refreshGames ?? (_ => { });
        this.setStatus = setStatus ?? (_ => { });
        Sources = BuildOptions(
            database?.Sources?.Select(source => new DesktopMetadataOption(source.Id, source.Name)),
            "No source");
        CompletionStatuses = BuildOptions(
            database?.CompletionStatuses?.Select(status => new DesktopMetadataOption(status.Id, status.Name)),
            "No status");
        Genres = BuildMultiOptions(database?.Genres?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Platforms = BuildMultiOptions(database?.Platforms?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Categories = BuildMultiOptions(database?.Categories?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Tags = BuildMultiOptions(database?.Tags?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Developers = BuildMultiOptions(database?.Companies?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Publishers = BuildMultiOptions(database?.Companies?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Features = BuildMultiOptions(database?.Features?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Series = BuildMultiOptions(database?.Series?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        AgeRatings = BuildMultiOptions(database?.AgeRatings?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Regions = BuildMultiOptions(database?.Regions?.Select(item => new DesktopMetadataOption(item.Id, item.Name)));
        Emulators = BuildEmulatorOptions(database?.Emulators);
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        AddLinkCommand = new RelayCommand(
            () => Links.Add(new DesktopLinkEditorItem(null, item => Links.Remove(item))),
            () => CanEditLinks);
        AddGameActionCommand = new RelayCommand(
            () => AddGameActionItem(null),
            () => CanEditGameActions);
        AddRomCommand = new RelayCommand(
            () => AddRomItem(null),
            () => CanEditRoms);
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
        ResetApplyFlags();
        LoadCommonValues(games);
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
        SetSelected(Genres, CommonIds(games, game => game.GenreIds));
        SetSelected(Platforms, CommonIds(games, game => game.PlatformIds));
        SetSelected(Categories, CommonIds(games, game => game.CategoryIds));
        SetSelected(Tags, CommonIds(games, game => game.TagIds));
        SetSelected(Developers, CommonIds(games, game => game.DeveloperIds));
        SetSelected(Publishers, CommonIds(games, game => game.PublisherIds));
        SetSelected(Features, CommonIds(games, game => game.FeatureIds));
        SetSelected(Series, CommonIds(games, game => game.SeriesIds));
        SetSelected(AgeRatings, CommonIds(games, game => game.AgeRatingIds));
        SetSelected(Regions, CommonIds(games, game => game.RegionIds));
        CoverImage = CommonValue(games, game => game.CoverImage ?? string.Empty);
        BackgroundImage = CommonValue(games, game => game.BackgroundImage ?? string.Empty);
        Icon = CommonValue(games, game => game.Icon ?? string.Empty);
        Links.Clear();
        foreach (var link in CommonLinks(games))
        {
            Links.Add(new DesktopLinkEditorItem(link, item => Links.Remove(item)));
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
        if (ApplyGenres) game.GenreIds = SelectedIds(Genres);
        if (ApplyPlatforms) game.PlatformIds = SelectedIds(Platforms);
        if (ApplyCategories) game.CategoryIds = SelectedIds(Categories);
        if (ApplyTags) game.TagIds = SelectedIds(Tags);
        if (ApplyDevelopers) game.DeveloperIds = SelectedIds(Developers);
        if (ApplyPublishers) game.PublisherIds = SelectedIds(Publishers);
        if (ApplyFeatures) game.FeatureIds = SelectedIds(Features);
        if (ApplySeries) game.SeriesIds = SelectedIds(Series);
        if (ApplyAgeRatings) game.AgeRatingIds = SelectedIds(AgeRatings);
        if (ApplyRegions) game.RegionIds = SelectedIds(Regions);
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

    private void Cancel() => Complete(false);

    private void Complete(bool? result)
    {
        if (!IsVisible)
        {
            return;
        }

        IsVisible = false;
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
            item => GameActions.Remove(item)));
    }

    private void AddRomItem(GameRom rom)
    {
        Roms.Add(new DesktopRomEditorItem(
            rom,
            item => MoveItem(Roms, item, -1),
            item => MoveItem(Roms, item, 1),
            item => Roms.Remove(item)));
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

    private static IReadOnlyCollection<Guid> CommonIds(
        IReadOnlyList<Game> games,
        Func<Game, IReadOnlyCollection<Guid>> selector)
    {
        var first = new HashSet<Guid>(selector(games[0]) ?? Array.Empty<Guid>());
        return games.Skip(1).All(game => first.SetEquals(selector(game) ?? Array.Empty<Guid>()))
            ? first
            : Array.Empty<Guid>();
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

    private static void SetSelected(
        IEnumerable<DesktopMetadataOption> options,
        IReadOnlyCollection<Guid> selectedIds)
    {
        var selected = selectedIds is HashSet<Guid> set ? set : new HashSet<Guid>(selectedIds);
        foreach (var option in options)
        {
            option.IsSelected = selected.Contains(option.Id);
        }
    }

    private static List<Guid> SelectedIds(IEnumerable<DesktopMetadataOption> options) =>
        options.Where(option => option.IsSelected).Select(option => option.Id).ToList();

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
