using Playnite.Controllers;
using Playnite.Avalonia.App.Services;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenAppViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> filterOptions =
        new[] { "All", "Installed", "Favorites", "Recent", "Unplayed", "Hidden" };

    private readonly List<GameItemViewModel> allGames;
    private readonly FullscreenSettings settings;
    private FullscreenRuntimeHost runtimeHost;
    private GameItemViewModel selectedGame;
    private IReadOnlyList<GameItemViewModel> games;
    private bool isDetailsVisible;
    private bool isMenuVisible;
    private bool isSearchVisible;
    private bool isFiltersVisible;
    private bool isNotificationsVisible;
    private bool isActionPickerVisible;
    private bool isDialogVisible;
    private string statusText;
    private string searchText = string.Empty;
    private string selectedFilterOption;
    private string selectedActionChoice;
    private string dialogCaption;
    private string dialogMessage;
    private string selectedDialogOption;
    private Action<string> dialogCompleted;
    private int dialogCancelIndex;
    private string pluginSummary = "Plugins have not been initialized";
    private GameOperationKind pendingOperation;
    private int activateCount;
    private string clockText;
    private string batteryText;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler ExitRequested;
    public event EventHandler ToggleFullscreenRequested;
    public event EventHandler LibraryFocusRequested;
    public event EventHandler SettingsChanged;
    public event EventHandler NavigationRequested;
    public event EventHandler ActivationRequested;
    public event EventHandler GameLaunchSucceeded;
    public event EventHandler MinimizeRequested;
    public event Action<SystemPowerAction> PowerActionRequested;

    public IReadOnlyList<GameItemViewModel> Games
    {
        get => games;
        private set
        {
            games = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LibrarySummary));
        }
    }

    public IReadOnlyList<string> FilterOptions => filterOptions;
    public ObservableCollection<string> ActionChoices { get; } = new();
    public ObservableCollection<string> DialogOptions { get; } = new();
    public ObservableCollection<NotificationMessage> Notifications { get; private set; } = new();
    public string LibrarySummary => $"{Games.Count:N0} of {allGames.Count:N0} games";
    public string PluginSummary => pluginSummary;
    public int NotificationCount => Notifications.Count;
    public int ActivateCount => activateCount;
    public AvaloniaSearchSession PluginSearch { get; }
    public FullscreenSettingsViewModel Settings { get; }
    public bool IsPluginSearchVisible => PluginSearch.IsVisible;
    public bool ShowClock => settings.ShowClock;
    public bool ShowBattery => settings.ShowBattery && !string.IsNullOrWhiteSpace(BatteryText);
    public string ClockText { get => clockText; private set => SetField(ref clockText, value); }
    public string BatteryText
    {
        get => batteryText;
        private set
        {
            if (SetField(ref batteryText, value))
            {
                OnPropertyChanged(nameof(ShowBattery));
            }
        }
    }
    public int Rows => settings.Rows;
    public int Columns => settings.Columns;
    public double ItemSpacing => settings.FullscreenItemSpacing;
    public Orientation LayoutOrientation => settings.HorizontalLayout ? Orientation.Horizontal : Orientation.Vertical;
    public bool SmoothScrolling => settings.SmoothScrolling;
    public bool ShowMainBackground => settings.EnableMainBackgroundImage &&
        !string.IsNullOrWhiteSpace(SelectedGame?.BackgroundPath);
    public string MainBackgroundPath => SelectedGame?.BackgroundPath;
    public IEffect MainBackgroundBlurEffect => settings.MainBackgroundImageBlurAmount > 0
        ? new BlurEffect { Radius = settings.MainBackgroundImageBlurAmount }
        : null;
    public double MainBackgroundDarkOpacity => settings.MainBackgroundImageDarkAmount / 100;
    public string DetailsPromptGlyph => settings.SwapStartDetailsAction ? ActionPromptGlyphCore : ConfirmPromptGlyphCore;
    public string PlayPromptGlyph => settings.SwapStartDetailsAction ? ConfirmPromptGlyphCore : ActionPromptGlyphCore;
    private string ConfirmPromptGlyphCore => settings.ButtonPrompts == FullscreenButtonPrompts.PlayStation ? "×" : "A";
    private string ActionPromptGlyphCore => settings.ButtonPrompts == FullscreenButtonPrompts.PlayStation ? "□" : "X";
    public bool MainMenuShowRestart => settings.MainMenuShowRestart;
    public bool MainMenuShowShutdown => settings.MainMenuShowShutdown;
    public bool MainMenuShowSuspend => settings.MainMenuShowSuspend;
    public bool MainMenuShowHibernate => settings.MainMenuShowHibernate;
    public bool MainMenuShowMinimize => settings.MainMenuShowMinimize;
    public bool MainMenuShowLogout => settings.MainMenuShowLogout;
    public bool MainMenuShowLock => settings.MainMenuShowLock;
    public bool MainMenuShowTools => settings.MainMenuShowTools;
    public bool MainMenuShowExtensions => settings.MainMenuShowExtensions;
    public bool MainMenuShowClients => settings.MainMenuShowClients;

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
            OnPropertyChanged(nameof(ShowMainBackground));
            OnPropertyChanged(nameof(MainBackgroundPath));
            if (selectedGame != null)
            {
                NavigationRequested?.Invoke(this, EventArgs.Empty);
            }
            ((RelayCommand)ShowDetailsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ActivateCommand).RaiseCanExecuteChanged();
            ((RelayCommand)InstallCommand).RaiseCanExecuteChanged();
            ((RelayCommand)UninstallCommand).RaiseCanExecuteChanged();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetField(ref searchText, value ?? string.Empty))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedFilterOption
    {
        get => selectedFilterOption;
        set => SetField(ref selectedFilterOption, value);
    }

    public string SelectedActionChoice
    {
        get => selectedActionChoice;
        set => SetField(ref selectedActionChoice, value);
    }

    public string DialogCaption { get => dialogCaption; private set => SetField(ref dialogCaption, value); }
    public string DialogMessage { get => dialogMessage; private set => SetField(ref dialogMessage, value); }
    public string SelectedDialogOption
    {
        get => selectedDialogOption;
        set
        {
            if (SetField(ref selectedDialogOption, value))
            {
                ((RelayCommand)ConfirmDialogCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowHiddenGames
    {
        get => settings.ShowHiddenGames;
        set
        {
            if (settings.ShowHiddenGames == value)
            {
                return;
            }

            settings.ShowHiddenGames = value;
            OnPropertyChanged();
            ApplyFilters();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool SwapConfirmCancelButtons
    {
        get => settings.SwapConfirmCancelButtons;
        set
        {
            if (settings.SwapConfirmCancelButtons == value)
            {
                return;
            }

            settings.SwapConfirmCancelButtons = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool AudioEnabled
    {
        get => settings.AudioEnabled;
        set
        {
            if (settings.AudioEnabled == value)
            {
                return;
            }

            settings.AudioEnabled = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsDetailsVisible { get => isDetailsVisible; private set => SetField(ref isDetailsVisible, value); }
    public bool IsMenuVisible { get => isMenuVisible; private set => SetField(ref isMenuVisible, value); }
    public bool IsSearchVisible { get => isSearchVisible; private set => SetField(ref isSearchVisible, value); }
    public bool IsFiltersVisible { get => isFiltersVisible; private set => SetField(ref isFiltersVisible, value); }
    public bool IsSettingsVisible => Settings.IsVisible;
    public bool IsNotificationsVisible { get => isNotificationsVisible; private set => SetField(ref isNotificationsVisible, value); }
    public bool IsActionPickerVisible { get => isActionPickerVisible; private set => SetField(ref isActionPickerVisible, value); }
    public bool IsDialogVisible { get => isDialogVisible; private set => SetField(ref isDialogVisible, value); }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public ICommand ShowDetailsCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand ActivateCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand ToggleMenuCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ToggleFullscreenCommand { get; }
    public ICommand SelectPreviousCommand { get; }
    public ICommand SelectNextCommand { get; }
    public ICommand OpenSearchCommand { get; }
    public ICommand CloseSearchCommand { get; }
    public ICommand ToggleFiltersCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ToggleNotificationsCommand { get; }
    public ICommand ConfirmActionChoiceCommand { get; }
    public ICommand DismissNotificationCommand { get; }
    public ICommand ConfirmDialogCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand ShutdownCommand { get; }
    public ICommand SuspendCommand { get; }
    public ICommand HibernateCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand OpenToolsCommand { get; }
    public ICommand OpenExtensionsCommand { get; }
    public ICommand OpenClientsCommand { get; }

    public FullscreenAppViewModel(
        IReadOnlyList<GameItemViewModel> sourceGames,
        FullscreenSettings settings,
        string startupError)
    {
        this.settings = settings ?? new FullscreenSettings();
        allGames = sourceGames?.ToList() ?? new List<GameItemViewModel>();
        selectedFilterOption = filterOptions.Contains(this.settings.ActiveFilter)
            ? this.settings.ActiveFilter
            : "All";
        games = allGames;
        selectedGame = games.FirstOrDefault();
        statusText = startupError == null
            ? "A Details   X Play   Y Search   Start Menu"
            : $"Library unavailable: {startupError}";
        PluginSearch = new AvaloniaSearchSession(
            () => (true, ShowHiddenGames),
            (message, exception) => StatusText = $"{message} {exception.Message}");
        Settings = new FullscreenSettingsViewModel(this.settings, ApplySavedSettings);
        Settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FullscreenSettingsViewModel.IsVisible))
            {
                OnPropertyChanged(nameof(IsSettingsVisible));
            }
        };
        PluginSearch.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AvaloniaSearchSession.IsVisible))
            {
                OnPropertyChanged(nameof(IsPluginSearchVisible));
            }
        };

        ShowDetailsCommand = new RelayCommand(ShowDetails, () => SelectedGame != null);
        ConfirmCommand = new RelayCommand(Confirm);
        ActivateCommand = new RelayCommand(ActivateSelected, () => SelectedGame != null);
        InstallCommand = new RelayCommand(
            () => RunOperation(GameOperationKind.Install),
            () => SelectedGame != null && !SelectedGame.IsInstalled);
        UninstallCommand = new RelayCommand(
            () => RunOperation(GameOperationKind.Uninstall),
            () => SelectedGame?.IsInstalled == true);
        ToggleMenuCommand = new RelayCommand(ToggleMenu);
        BackCommand = new RelayCommand(Back);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        ToggleFullscreenCommand = new RelayCommand(() => ToggleFullscreenRequested?.Invoke(this, EventArgs.Empty));
        SelectPreviousCommand = new RelayCommand(() => SelectOffset(-1), () => Games.Count > 0);
        SelectNextCommand = new RelayCommand(() => SelectOffset(1), () => Games.Count > 0);
        OpenSearchCommand = new RelayCommand(OpenSearch);
        CloseSearchCommand = new RelayCommand(CloseSearch);
        ToggleFiltersCommand = new RelayCommand(() =>
        {
            CloseOverlays();
            IsFiltersVisible = true;
        });
        ApplyFilterCommand = new RelayCommand(ApplySelectedFilter);
        OpenSettingsCommand = new RelayCommand(() =>
        {
            CloseOverlays();
            Settings.Open();
        });
        ToggleNotificationsCommand = new RelayCommand(() =>
        {
            CloseOverlays();
            IsNotificationsVisible = true;
        });
        ConfirmActionChoiceCommand = new RelayCommand(ConfirmActionChoice, () => SelectedActionChoice != null);
        ConfirmDialogCommand = new RelayCommand(
            () => CompleteDialog(SelectedDialogOption),
            () => SelectedDialogOption != null);
        CancelDialogCommand = new RelayCommand(() => CompleteDialog(
            DialogOptions.Count == 0 ? null : DialogOptions[Math.Clamp(dialogCancelIndex, 0, DialogOptions.Count - 1)]));
        DismissNotificationCommand = new RelayCommand(parameter =>
        {
            if (parameter is NotificationMessage message)
            {
                runtimeHost?.Notifications.Remove(message.Id);
            }
        });
        MinimizeCommand = new RelayCommand(() => MinimizeRequested?.Invoke(this, EventArgs.Empty));
        RestartCommand = new RelayCommand(() => RequestPowerAction(SystemPowerAction.Restart));
        ShutdownCommand = new RelayCommand(() => RequestPowerAction(SystemPowerAction.Shutdown));
        SuspendCommand = new RelayCommand(() => RequestPowerAction(SystemPowerAction.Suspend));
        HibernateCommand = new RelayCommand(() => RequestPowerAction(SystemPowerAction.Hibernate));
        LockCommand = new RelayCommand(() => RequestPowerAction(SystemPowerAction.Lock));
        LogoutCommand = new RelayCommand(() => RequestPowerAction(SystemPowerAction.Logout));
        OpenToolsCommand = new RelayCommand(() => OpenMenuInformation(
            "Tools",
            "Library search, filters, notifications, settings, and display controls are available from this Fullscreen menu."));
        OpenExtensionsCommand = new RelayCommand(() => OpenMenuInformation(
            "Extensions",
            PluginSummary));
        OpenClientsCommand = new RelayCommand(() => OpenMenuInformation(
            "Library clients",
            "Library-client lifecycle and shutdown policy are managed by the loaded library extensions."));

        ApplyFilters();
        ApplyGameVisualSettings();
    }

    public void AttachRuntime(FullscreenRuntimeHost host)
    {
        runtimeHost = host;
        Notifications.CollectionChanged -= Notifications_CollectionChanged;
        Notifications = host.Notifications.Messages;
        Notifications.CollectionChanged += Notifications_CollectionChanged;
        OnPropertyChanged(nameof(Notifications));
        OnPropertyChanged(nameof(NotificationCount));
    }

    public void SelectGame(Guid gameId)
    {
        var match = allGames.FirstOrDefault(item => item.Game.Id == gameId);
        if (match == null)
        {
            return;
        }

        if (!Games.Contains(match))
        {
            SearchText = string.Empty;
            SelectedFilterOption = "All";
            ApplySelectedFilter();
        }

        SelectedGame = match;
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SwitchToLibraryView()
    {
        CloseOverlays();
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshGame(Guid gameId)
    {
        allGames.FirstOrDefault(item => item.Game.Id == gameId)?.Refresh();
        ApplyFilters();
    }

    public void SetPluginSummary(string summary)
    {
        pluginSummary = summary;
        OnPropertyChanged(nameof(PluginSummary));
    }

    internal void SetStatusMessage(string message) => StatusText = message;

    internal void OpenDialog(
        string caption,
        string message,
        IReadOnlyList<string> options,
        int defaultIndex,
        int cancelIndex,
        Action<string> completed)
    {
        CloseOverlays();
        DialogCaption = caption;
        DialogMessage = message;
        DialogOptions.Clear();
        foreach (var option in options)
        {
            DialogOptions.Add(option);
        }

        dialogCancelIndex = cancelIndex;
        dialogCompleted = completed;
        SelectedDialogOption = DialogOptions.Count == 0
            ? null
            : DialogOptions[Math.Clamp(defaultIndex, 0, DialogOptions.Count - 1)];
        IsDialogVisible = true;
    }

    private void ActivateSelected()
    {
        ActivationRequested?.Invoke(this, EventArgs.Empty);
        activateCount++;
        RunOperation(SelectedGame?.IsInstalled == true ? GameOperationKind.Play : GameOperationKind.Install);
    }

    private void RunOperation(GameOperationKind kind, int choiceIndex = -1)
    {
        if (runtimeHost == null || SelectedGame == null)
        {
            StatusText = "The game-operation host is unavailable.";
            return;
        }

        var result = kind switch
        {
            GameOperationKind.Play => runtimeHost.Play(SelectedGame.Game, choiceIndex),
            GameOperationKind.Install => runtimeHost.Install(SelectedGame.Game, choiceIndex),
            GameOperationKind.Uninstall => runtimeHost.Uninstall(SelectedGame.Game, choiceIndex),
            _ => throw new NotSupportedException()
        };

        if (result.SelectionRequired)
        {
            pendingOperation = kind;
            ActionChoices.Clear();
            foreach (var choice in result.Choices)
            {
                ActionChoices.Add(choice);
            }

            SelectedActionChoice = ActionChoices.FirstOrDefault();
            CloseOverlays();
            IsActionPickerVisible = true;
        }
        else
        {
            StatusText = result.Message;
            if (kind == GameOperationKind.Play && result.Success)
            {
                GameLaunchSucceeded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void ConfirmActionChoice()
    {
        var index = ActionChoices.IndexOf(SelectedActionChoice);
        if (index < 0)
        {
            return;
        }

        IsActionPickerVisible = false;
        ActivationRequested?.Invoke(this, EventArgs.Empty);
        RunOperation(pendingOperation, index);
    }

    private void Confirm()
    {
        if (IsDialogVisible)
        {
            ConfirmDialogCommand.Execute(null);
        }
        else if (IsActionPickerVisible)
        {
            ConfirmActionChoiceCommand.Execute(null);
        }
        else if (IsFiltersVisible)
        {
            ApplyFilterCommand.Execute(null);
        }
        else if (IsSearchVisible)
        {
            CloseSearchCommand.Execute(null);
        }
        else if (PluginSearch.IsVisible)
        {
            PluginSearch.PrimaryCommand.Execute(null);
        }
        else
        {
            ShowDetailsCommand.Execute(null);
        }
    }

    private void CompleteDialog(string result)
    {
        if (!IsDialogVisible)
        {
            return;
        }

        IsDialogVisible = false;
        var completed = dialogCompleted;
        dialogCompleted = null;
        completed?.Invoke(result);
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowDetails()
    {
        ActivationRequested?.Invoke(this, EventArgs.Empty);
        CloseOverlays();
        IsDetailsVisible = true;
    }

    private void ToggleMenu()
    {
        var open = !IsMenuVisible;
        CloseOverlays();
        IsMenuVisible = open;
        if (!open)
        {
            LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OpenSearch()
    {
        CloseOverlays();
        IsSearchVisible = true;
    }

    private void CloseSearch()
    {
        IsSearchVisible = false;
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySelectedFilter()
    {
        settings.ActiveFilter = SelectedFilterOption ?? "All";
        IsFiltersVisible = false;
        ApplyFilters();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFilters()
    {
        IEnumerable<GameItemViewModel> filtered = allGames;
        if (!settings.ShowHiddenGames && SelectedFilterOption != "Hidden")
        {
            filtered = filtered.Where(item => !item.Game.Hidden);
        }

        filtered = (SelectedFilterOption ?? settings.ActiveFilter) switch
        {
            "Installed" => filtered.Where(item => item.IsInstalled),
            "Favorites" => filtered.Where(item => item.Favorite),
            "Recent" => filtered.Where(item => item.Game.LastActivity >= DateTime.Now.AddDays(-30)),
            "Unplayed" => filtered.Where(item => item.Game.Playtime == 0),
            "Hidden" => allGames.Where(item => item.Game.Hidden),
            _ => filtered
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                item.MetadataLine.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
        }

        var previous = SelectedGame;
        Games = filtered.ToList();
        SelectedGame = previous != null && Games.Contains(previous) ? previous : Games.FirstOrDefault();
    }

    private void Back()
    {
        if (PluginSearch.IsVisible)
        {
            if (PluginSearch.CanGoBack)
            {
                PluginSearch.BackCommand.Execute(null);
            }
            else
            {
                PluginSearch.Close();
                LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        if (IsDetailsVisible || IsMenuVisible || IsSearchVisible || IsFiltersVisible ||
            IsSettingsVisible || IsNotificationsVisible || IsActionPickerVisible || IsDialogVisible)
        {
            if (IsDialogVisible)
            {
                CancelDialogCommand.Execute(null);
            }
            else
            {
                CloseOverlays();
                LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void CloseOverlays()
    {
        PluginSearch.Close();
        IsDetailsVisible = false;
        IsMenuVisible = false;
        IsSearchVisible = false;
        IsFiltersVisible = false;
        Settings.Close();
        IsNotificationsVisible = false;
        IsActionPickerVisible = false;
        IsDialogVisible = false;
    }

    private void ApplySavedSettings()
    {
        OnPropertyChanged(nameof(ShowHiddenGames));
        OnPropertyChanged(nameof(SwapConfirmCancelButtons));
        OnPropertyChanged(nameof(AudioEnabled));
        OnPropertyChanged(nameof(ShowClock));
        OnPropertyChanged(nameof(ShowBattery));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(ItemSpacing));
        OnPropertyChanged(nameof(LayoutOrientation));
        OnPropertyChanged(nameof(SmoothScrolling));
        OnPropertyChanged(nameof(ShowMainBackground));
        OnPropertyChanged(nameof(MainBackgroundPath));
        OnPropertyChanged(nameof(MainBackgroundBlurEffect));
        OnPropertyChanged(nameof(MainBackgroundDarkOpacity));
        OnPropertyChanged(nameof(DetailsPromptGlyph));
        OnPropertyChanged(nameof(PlayPromptGlyph));
        OnPropertyChanged(nameof(MainMenuShowRestart));
        OnPropertyChanged(nameof(MainMenuShowShutdown));
        OnPropertyChanged(nameof(MainMenuShowSuspend));
        OnPropertyChanged(nameof(MainMenuShowHibernate));
        OnPropertyChanged(nameof(MainMenuShowMinimize));
        OnPropertyChanged(nameof(MainMenuShowLogout));
        OnPropertyChanged(nameof(MainMenuShowLock));
        OnPropertyChanged(nameof(MainMenuShowTools));
        OnPropertyChanged(nameof(MainMenuShowExtensions));
        OnPropertyChanged(nameof(MainMenuShowClients));
        ApplyGameVisualSettings();
        ApplyFilters();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RequestPowerAction(SystemPowerAction action)
    {
        OpenDialog(
            action.ToString(),
            $"Are you sure you want to {action.ToString().ToLowerInvariant()}?",
            new[] { "Yes", "Cancel" },
            1,
            1,
            result =>
            {
                if (result == "Yes")
                {
                    PowerActionRequested?.Invoke(action);
                }
            });
    }

    private void OpenMenuInformation(string caption, string message) =>
        OpenDialog(caption, message, new[] { "OK" }, 0, 0, _ => { });

    private void ApplyGameVisualSettings()
    {
        foreach (var game in allGames)
        {
            game.ApplyVisualSettings(settings.ShowGameTitles, settings.DarkenUninstalledGamesGrid);
        }
    }

    internal void UpdateStatusWidgets(DateTime now, BatteryStatus battery)
    {
        ClockText = now.ToString("t");
        BatteryText = battery.IsPresent
            ? battery.Format(settings.ShowBatteryPercentage)
            : null;
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

    private void Notifications_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(NotificationCount));
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
