using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private string description;
    private string notes;
    private bool favorite;
    private bool hidden;
    private DesktopMetadataOption selectedSource;
    private DesktopMetadataOption selectedCompletionStatus;
    private string validationMessage;
    private bool applyReleaseDate;
    private bool applyUserScore;
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

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
    public bool IsSingleEdit => editingGameIds.Count == 1;
    public bool IsBulkEdit => editingGameIds.Count > 1;
    public string Title => IsBulkEdit ? $"Edit metadata for {editingGameIds.Count:N0} games" : "Edit game metadata";
    public string Name { get => name; set => SetField(ref name, value); }
    public string SortingName { get => sortingName; set => SetField(ref sortingName, value); }
    public string ReleaseDate { get => releaseDate; set => SetField(ref releaseDate, value); }
    public string UserScore { get => userScore; set => SetField(ref userScore, value); }
    public string Description { get => description; set => SetField(ref description, value); }
    public string Notes { get => notes; set => SetField(ref notes, value); }
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

    public bool CanEditReleaseDate => IsSingleEdit || ApplyReleaseDate;
    public bool CanEditUserScore => IsSingleEdit || ApplyUserScore;
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
    public bool CanEditCoverImage => IsSingleEdit || ApplyCoverImage;
    public bool CanEditBackgroundImage => IsSingleEdit || ApplyBackgroundImage;
    public bool CanEditIcon => IsSingleEdit || ApplyIcon;
    public bool CanEditLinks => IsSingleEdit || ApplyLinks;
    public bool CanEditIncludeLibraryPluginAction => IsSingleEdit || ApplyIncludeLibraryPluginAction;
    public bool CanEditGameActions => IsSingleEdit || ApplyGameActions;
    public bool CanEditRoms => IsSingleEdit || ApplyRoms;

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
        Description = CommonValue(games, game => game.Description ?? string.Empty);
        Notes = CommonValue(games, game => game.Notes ?? string.Empty);
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

        if (!TryParseReleaseDate(out var parsedReleaseDate) || !TryParseUserScore(out var parsedScore))
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
                            parsedScore,
                            preparedLinks,
                            preparedGameActions,
                            preparedRoms);
                    }
                    else
                    {
                        ApplyBulkValues(
                            game,
                            parsedReleaseDate,
                            parsedScore,
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
        int? parsedScore,
        IReadOnlyList<Link> preparedLinks,
        IReadOnlyList<GameAction> preparedGameActions,
        IReadOnlyList<GameRom> preparedRoms)
    {
        game.Name = Name.Trim();
        game.SortingName = NullIfWhiteSpace(SortingName);
        game.ReleaseDate = parsedReleaseDate;
        game.UserScore = parsedScore;
        game.Description = Description ?? string.Empty;
        game.Notes = Notes ?? string.Empty;
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
        game.Links = new ObservableCollection<Link>(preparedLinks.Select(link => link.GetCopy()));
        game.IncludeLibraryPluginAction = IncludeLibraryPluginAction;
        game.GameActions = new ObservableCollection<GameAction>(
            preparedGameActions.Select(action => action.GetCopy()));
        game.Roms = new ObservableCollection<GameRom>(preparedRoms.Select(rom => rom.GetCopy()));
    }

    private void ApplyBulkValues(
        Game game,
        ReleaseDate? parsedReleaseDate,
        int? parsedScore,
        IReadOnlyList<Link> preparedLinks,
        IReadOnlyList<GameAction> preparedGameActions,
        IReadOnlyList<GameRom> preparedRoms)
    {
        if (ApplyReleaseDate) game.ReleaseDate = parsedReleaseDate;
        if (ApplyUserScore) game.UserScore = parsedScore;
        if (ApplyDescription) game.Description = Description ?? string.Empty;
        if (ApplyNotes) game.Notes = Notes ?? string.Empty;
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

    private bool TryParseUserScore(out int? parsedScore)
    {
        parsedScore = null;
        if (IsBulkEdit && !ApplyUserScore || string.IsNullOrWhiteSpace(UserScore))
        {
            return true;
        }

        if (!int.TryParse(UserScore.Trim(), out var score) || score is < 0 or > 100)
        {
            ValidationMessage = "User score must be a whole number from 0 to 100.";
            return false;
        }

        parsedScore = score;
        return true;
    }

    private bool HasBulkChanges() =>
        ApplyReleaseDate || ApplyUserScore || ApplyDescription || ApplyNotes ||
        ApplyFavorite || ApplyHidden || ApplySource || ApplyCompletionStatus ||
        ApplyGenres || ApplyPlatforms || ApplyCategories || ApplyTags ||
        ApplyDevelopers || ApplyPublishers || ApplyCoverImage ||
        ApplyBackgroundImage || ApplyIcon || ApplyLinks ||
        ApplyIncludeLibraryPluginAction || ApplyGameActions || ApplyRoms;

    private void ResetApplyFlags()
    {
        ApplyReleaseDate = false;
        ApplyUserScore = false;
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
        ApplyCoverImage = false;
        ApplyBackgroundImage = false;
        ApplyIcon = false;
        ApplyLinks = false;
        ApplyIncludeLibraryPluginAction = false;
        ApplyGameActions = false;
        ApplyRoms = false;
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
        OnPropertyChanged(nameof(CanEditCoverImage));
        OnPropertyChanged(nameof(CanEditBackgroundImage));
        OnPropertyChanged(nameof(CanEditIcon));
        OnPropertyChanged(nameof(CanEditLinks));
        OnPropertyChanged(nameof(CanEditIncludeLibraryPluginAction));
        OnPropertyChanged(nameof(CanEditGameActions));
        OnPropertyChanged(nameof(CanEditRoms));
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
