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

    public bool CanEditReleaseDate => IsSingleEdit || ApplyReleaseDate;
    public bool CanEditUserScore => IsSingleEdit || ApplyUserScore;
    public bool CanEditDescription => IsSingleEdit || ApplyDescription;
    public bool CanEditNotes => IsSingleEdit || ApplyNotes;
    public bool CanEditFavorite => IsSingleEdit || ApplyFavorite;
    public bool CanEditHidden => IsSingleEdit || ApplyHidden;
    public bool CanEditSource => IsSingleEdit || ApplySource;
    public bool CanEditCompletionStatus => IsSingleEdit || ApplyCompletionStatus;

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
        ApplyFavorite || ApplyHidden || ApplySource || ApplyCompletionStatus;

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
