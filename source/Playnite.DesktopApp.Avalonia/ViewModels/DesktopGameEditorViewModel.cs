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
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

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
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
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

        var games = editingGameIds.Select(id => database.Games[id]).ToList();
        if (games.Any(game => game == null))
        {
            ValidationMessage = "One or more selected games no longer exist in the library.";
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
                        ApplySingleGameValues(game, parsedReleaseDate, parsedScore);
                    }
                    else
                    {
                        ApplyBulkValues(game, parsedReleaseDate, parsedScore);
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

    private void ApplySingleGameValues(Game game, ReleaseDate? parsedReleaseDate, int? parsedScore)
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
    }

    private void ApplyBulkValues(Game game, ReleaseDate? parsedReleaseDate, int? parsedScore)
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
        ApplyDevelopers || ApplyPublishers;

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

    private static IReadOnlyCollection<Guid> CommonIds(
        IReadOnlyList<Game> games,
        Func<Game, IReadOnlyCollection<Guid>> selector)
    {
        var first = new HashSet<Guid>(selector(games[0]) ?? Array.Empty<Guid>());
        return games.Skip(1).All(game => first.SetEquals(selector(game) ?? Array.Empty<Guid>()))
            ? first
            : Array.Empty<Guid>();
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
