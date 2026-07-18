using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopAppViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<DesktopGameItemViewModel> allGames;
    private IReadOnlyList<DesktopGameItemViewModel> games;
    private DesktopGameItemViewModel selectedGame;
    private string searchText = string.Empty;
    private bool installedOnly;
    private bool favoritesOnly;

    public event PropertyChangedEventHandler PropertyChanged;

    public IReadOnlyList<DesktopGameItemViewModel> Games
    {
        get => games;
        private set
        {
            games = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LibrarySummary));
        }
    }

    public DesktopGameItemViewModel SelectedGame
    {
        get => selectedGame;
        set
        {
            selectedGame = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            searchText = value ?? string.Empty;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public bool InstalledOnly
    {
        get => installedOnly;
        set
        {
            installedOnly = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public bool FavoritesOnly
    {
        get => favoritesOnly;
        set
        {
            favoritesOnly = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public string LibrarySummary => $"{Games.Count:N0} of {allGames.Count:N0} games";
    public string StatusText { get; }

    public DesktopAppViewModel(IReadOnlyList<DesktopGameItemViewModel> games, string startupError)
    {
        allGames = games ?? Array.Empty<DesktopGameItemViewModel>();
        this.games = allGames;
        selectedGame = allGames.FirstOrDefault();
        StatusText = startupError == null
            ? "Phase 5 pilot — library browsing and details use Playnite.Core"
            : $"Library unavailable: {startupError}";
    }

    private void ApplyFilters()
    {
        IEnumerable<DesktopGameItemViewModel> filtered = allGames.Where(game => !game.Game.Hidden);
        if (InstalledOnly)
        {
            filtered = filtered.Where(game => game.IsInstalled);
        }

        if (FavoritesOnly)
        {
            filtered = filtered.Where(game => game.Favorite);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(game =>
                game.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                game.MetadataLine.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
        }

        var previous = SelectedGame;
        Games = filtered.ToList();
        SelectedGame = previous != null && Games.Contains(previous) ? previous : Games.FirstOrDefault();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
