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
    private readonly Action<Guid> refreshGame;
    private readonly Action<string> setStatus;
    private Guid editingGameId;
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

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
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
        Action<Guid> refreshGame,
        Action<string> setStatus)
    {
        this.database = database;
        this.refreshGame = refreshGame ?? (_ => { });
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

    public bool Open(Guid gameId, Action<bool?> onCompleted = null)
    {
        if (database == null || IsVisible)
        {
            return false;
        }

        var game = database.Games[gameId];
        if (game == null)
        {
            return false;
        }

        editingGameId = gameId;
        completed = onCompleted;
        Name = game.Name ?? string.Empty;
        SortingName = game.SortingName ?? string.Empty;
        ReleaseDate = game.ReleaseDate?.Serialize() ?? string.Empty;
        UserScore = game.UserScore?.ToString() ?? string.Empty;
        Description = game.Description ?? string.Empty;
        Notes = game.Notes ?? string.Empty;
        Favorite = game.Favorite;
        Hidden = game.Hidden;
        SelectedSource = Sources.FirstOrDefault(option => option.Id == game.SourceId) ?? Sources[0];
        SelectedCompletionStatus = CompletionStatuses
            .FirstOrDefault(option => option.Id == game.CompletionStatusId) ?? CompletionStatuses[0];
        ValidationMessage = string.Empty;
        IsVisible = true;
        return true;
    }

    private void Save()
    {
        if (!IsVisible || database == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "A game name is required.";
            return;
        }

        ReleaseDate? parsedReleaseDate = null;
        if (!string.IsNullOrWhiteSpace(ReleaseDate))
        {
            if (!Playnite.SDK.Models.ReleaseDate.TryDeserialize(ReleaseDate.Trim(), out var date))
            {
                ValidationMessage = "Release date must use YYYY, YYYY-M, or YYYY-M-D.";
                return;
            }

            parsedReleaseDate = date;
        }

        int? parsedScore = null;
        if (!string.IsNullOrWhiteSpace(UserScore))
        {
            if (!int.TryParse(UserScore.Trim(), out var score) || score is < 0 or > 100)
            {
                ValidationMessage = "User score must be a whole number from 0 to 100.";
                return;
            }

            parsedScore = score;
        }

        var game = database.Games[editingGameId];
        if (game == null)
        {
            ValidationMessage = "The game no longer exists in the library.";
            return;
        }

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
        game.Modified = DateTime.Now;
        database.Games.Update(game);
        refreshGame(game.Id);
        setStatus($"Saved metadata for {game.Name}.");
        Complete(true);
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

    private static string NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
