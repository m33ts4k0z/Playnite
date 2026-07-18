using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenAppViewModel : INotifyPropertyChanged
{
    private GameItemViewModel selectedGame;
    private bool isDetailsVisible;
    private bool isMenuVisible;
    private string statusText;
    private int activateCount;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler ExitRequested;
    public event EventHandler ToggleFullscreenRequested;
    public event EventHandler LibraryFocusRequested;

    public IReadOnlyList<GameItemViewModel> Games { get; }
    public string LibrarySummary => $"{Games.Count:N0} games";
    public int ActivateCount => activateCount;

    public GameItemViewModel SelectedGame
    {
        get => selectedGame;
        set
        {
            if (ReferenceEquals(selectedGame, value))
            {
                return;
            }

            selectedGame = value;
            OnPropertyChanged();
            ((RelayCommand)ShowDetailsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ActivateCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsDetailsVisible
    {
        get => isDetailsVisible;
        private set => SetField(ref isDetailsVisible, value);
    }

    public bool IsMenuVisible
    {
        get => isMenuVisible;
        private set => SetField(ref isMenuVisible, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public ICommand ShowDetailsCommand { get; }
    public ICommand ActivateCommand { get; }
    public ICommand ToggleMenuCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ToggleFullscreenCommand { get; }
    public ICommand SelectPreviousCommand { get; }
    public ICommand SelectNextCommand { get; }

    public FullscreenAppViewModel(IReadOnlyList<GameItemViewModel> games, string startupError)
    {
        Games = games ?? Array.Empty<GameItemViewModel>();
        selectedGame = Games.FirstOrDefault();
        statusText = startupError == null
            ? "A Details   X Play   Start Menu"
            : $"Library unavailable: {startupError}";

        ShowDetailsCommand = new RelayCommand(ShowDetails, () => SelectedGame != null);
        ActivateCommand = new RelayCommand(ActivateSelected, () => SelectedGame != null);
        ToggleMenuCommand = new RelayCommand(ToggleMenu);
        BackCommand = new RelayCommand(Back);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        ToggleFullscreenCommand = new RelayCommand(() => ToggleFullscreenRequested?.Invoke(this, EventArgs.Empty));
        SelectPreviousCommand = new RelayCommand(() => SelectOffset(-1), () => Games.Count > 0);
        SelectNextCommand = new RelayCommand(() => SelectOffset(1), () => Games.Count > 0);
    }

    private void ShowDetails()
    {
        IsMenuVisible = false;
        IsDetailsVisible = true;
    }

    private void ActivateSelected()
    {
        activateCount++;
        StatusText = $"Play requested for {SelectedGame.Name}. Game launching is the next parity slice.";
    }

    internal void SetStatusMessage(string message) => StatusText = message;

    private void ToggleMenu()
    {
        IsDetailsVisible = false;
        IsMenuVisible = !IsMenuVisible;
        if (!IsMenuVisible)
        {
            LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Back()
    {
        if (IsDetailsVisible)
        {
            IsDetailsVisible = false;
            LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (IsMenuVisible)
        {
            IsMenuVisible = false;
            LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectOffset(int offset)
    {
        if (Games.Count == 0)
        {
            return;
        }

        var current = SelectedGame == null ? 0 : Games.IndexOf(SelectedGame);
        var target = Math.Clamp(current + offset, 0, Games.Count - 1);
        SelectedGame = Games[target];
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> source, T item)
    {
        for (var index = 0; index < source.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(source[index], item))
            {
                return index;
            }
        }

        return -1;
    }
}
