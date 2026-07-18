using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopAppViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<SortOrder> sortOptions = new[]
    {
        SortOrder.Name,
        SortOrder.LastActivity,
        SortOrder.Playtime,
        SortOrder.Added,
        SortOrder.ReleaseDate,
        SortOrder.IsInstalled,
        SortOrder.Favorite,
        SortOrder.Source,
        SortOrder.Platforms,
        SortOrder.CompletionStatus
    };

    private static readonly IReadOnlyList<GroupableField> groupingOptions = new[]
    {
        GroupableField.None,
        GroupableField.Platform,
        GroupableField.Source,
        GroupableField.CompletionStatus,
        GroupableField.InstallationStatus,
        GroupableField.ReleaseYear,
        GroupableField.Name,
        GroupableField.PlayTime
    };

    private readonly IReadOnlyList<DesktopGameItemViewModel> allGames;
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private IReadOnlyList<DesktopGameItemViewModel> games;
    private DesktopGameItemViewModel selectedGame;
    private string searchText = string.Empty;
    private bool installedOnly;
    private bool favoritesOnly;
    private string selectedViewMode;
    private SortOrder selectedSortOrder;
    private SortOrderDirection selectedSortDirection;
    private GroupableField selectedGrouping;
    private FilterPreset selectedFilterPreset;
    private string statusText;
    private string pluginSummary = "Plugins have not been initialized";
    private AvaloniaRuntimeHost runtimeHost;
    private bool isNotificationsVisible;
    private bool isActionPickerVisible;
    private GameOperationKind pendingOperation;
    private string selectedActionChoice;
    private bool isDialogVisible;
    private string dialogCaption;
    private string dialogMessage;
    private string selectedDialogOption;
    private Action<string> dialogCompleted;
    private int dialogCancelIndex;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler SettingsChanged;

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
            if (ReferenceEquals(selectedGame, value))
            {
                return;
            }

            selectedGame = value;
            OnPropertyChanged();
            RaiseGameCommandStates();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetField(ref searchText, value ?? string.Empty))
            {
                ((AppRelayCommand)ClearSearchCommand).RaiseCanExecuteChanged();
                ApplyFilters();
            }
        }
    }

    public bool InstalledOnly
    {
        get => installedOnly;
        set
        {
            if (SetField(ref installedOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool FavoritesOnly
    {
        get => favoritesOnly;
        set
        {
            if (SetField(ref favoritesOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedViewMode
    {
        get => selectedViewMode;
        set
        {
            var resolved = value is "Grid" or "List" ? value : "Grid";
            if (SetField(ref selectedViewMode, resolved))
            {
                settings.ViewMode = resolved;
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public SortOrder SelectedSortOrder
    {
        get => selectedSortOrder;
        set
        {
            if (SetField(ref selectedSortOrder, value))
            {
                settings.SortOrder = value;
                ApplyFilters();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public SortOrderDirection SelectedSortDirection
    {
        get => selectedSortDirection;
        set
        {
            if (SetField(ref selectedSortDirection, value))
            {
                settings.SortDirection = value;
                ApplyFilters();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public GroupableField SelectedGrouping
    {
        get => selectedGrouping;
        set
        {
            if (SetField(ref selectedGrouping, value))
            {
                settings.Grouping = value;
                ApplyFilters();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public FilterPreset SelectedFilterPreset
    {
        get => selectedFilterPreset;
        set
        {
            if (ReferenceEquals(selectedFilterPreset, value))
            {
                return;
            }

            selectedFilterPreset = value;
            settings.ActiveFilterPreset = value?.Id ?? Guid.Empty;
            OnPropertyChanged();
            if (value?.SortingOrder != null)
            {
                SelectedSortOrder = value.SortingOrder.Value;
            }

            if (value?.SortingOrderDirection != null)
            {
                SelectedSortDirection = value.SortingOrderDirection.Value;
            }

            if (value?.GroupingOrder != null)
            {
                SelectedGrouping = value.GroupingOrder.Value;
            }

            ApplyFilters();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<string> ViewModes { get; } = new[] { "Grid", "List" };
    public IReadOnlyList<SortOrder> SortOptions => sortOptions;
    public IReadOnlyList<SortOrderDirection> SortDirectionOptions { get; } =
        Enum.GetValues<SortOrderDirection>();
    public IReadOnlyList<GroupableField> GroupingOptions => groupingOptions;
    public IReadOnlyList<FilterPreset> FilterPresets { get; }
    public bool IsGridView => SelectedViewMode == "Grid";
    public bool IsListView => SelectedViewMode == "List";
    public string LibrarySummary => $"{Games.Count:N0} of {allGames.Count:N0} games";
    public string PluginSummary => pluginSummary;
    public ObservableCollection<NotificationMessage> Notifications { get; private set; } = new();
    public int NotificationCount => Notifications.Count;
    public ObservableCollection<string> ActionChoices { get; } = new();
    public ObservableCollection<string> DialogOptions { get; } = new();
    public bool IsNotificationsVisible { get => isNotificationsVisible; private set => SetField(ref isNotificationsVisible, value); }
    public bool IsActionPickerVisible { get => isActionPickerVisible; private set => SetField(ref isActionPickerVisible, value); }
    public bool IsDialogVisible { get => isDialogVisible; private set => SetField(ref isDialogVisible, value); }
    public string DialogCaption { get => dialogCaption; private set => SetField(ref dialogCaption, value); }
    public string DialogMessage { get => dialogMessage; private set => SetField(ref dialogMessage, value); }
    public string SelectedActionChoice
    {
        get => selectedActionChoice;
        set
        {
            if (SetField(ref selectedActionChoice, value))
            {
                ((AppRelayCommand)ConfirmActionChoiceCommand).RaiseCanExecuteChanged();
            }
        }
    }
    public string SelectedDialogOption
    {
        get => selectedDialogOption;
        set
        {
            if (SetField(ref selectedDialogOption, value))
            {
                ((AppRelayCommand)ConfirmDialogCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }

    public ICommand ActivateCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand SetGridViewCommand { get; }
    public ICommand SetListViewCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ToggleNotificationsCommand { get; }
    public ICommand CloseOverlayCommand { get; }
    public ICommand ConfirmActionChoiceCommand { get; }
    public ICommand DismissNotificationCommand { get; }
    public ICommand ConfirmDialogCommand { get; }
    public ICommand CancelDialogCommand { get; }

    public DesktopAppViewModel(
        IReadOnlyList<DesktopGameItemViewModel> games,
        GameDatabase database,
        DesktopSettings settings,
        string startupError)
    {
        allGames = games ?? Array.Empty<DesktopGameItemViewModel>();
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        this.games = allGames;
        selectedGame = allGames.FirstOrDefault();
        selectedViewMode = this.settings.ViewMode is "Grid" or "List" ? this.settings.ViewMode : "Grid";
        selectedSortOrder = this.settings.SortOrder;
        selectedSortDirection = this.settings.SortDirection;
        selectedGrouping = this.settings.Grouping;
        FilterPresets = database?.GetSortedFilterPresets() ?? new List<FilterPreset>();
        selectedFilterPreset = FilterPresets.FirstOrDefault(preset => preset.Id == this.settings.ActiveFilterPreset) ??
            FilterPresets.FirstOrDefault(preset => preset.Name == "All") ??
            FilterPresets.FirstOrDefault();
        statusText = startupError == null
            ? "Phase 5 Desktop runtime ready"
            : $"Library unavailable: {startupError}";

        ActivateCommand = new AppRelayCommand(
            () => RunOperation(SelectedGame?.IsInstalled == true ? GameOperationKind.Play : GameOperationKind.Install),
            () => SelectedGame != null);
        InstallCommand = new AppRelayCommand(
            () => RunOperation(GameOperationKind.Install),
            () => SelectedGame != null && !SelectedGame.IsInstalled);
        UninstallCommand = new AppRelayCommand(
            () => RunOperation(GameOperationKind.Uninstall),
            () => SelectedGame?.IsInstalled == true);
        SetGridViewCommand = new AppRelayCommand(() => SelectedViewMode = "Grid");
        SetListViewCommand = new AppRelayCommand(() => SelectedViewMode = "List");
        ClearSearchCommand = new AppRelayCommand(() => SearchText = string.Empty, () => SearchText.Length > 0);
        ToggleNotificationsCommand = new AppRelayCommand(() => IsNotificationsVisible = !IsNotificationsVisible);
        CloseOverlayCommand = new AppRelayCommand(CloseOverlays);
        ConfirmActionChoiceCommand = new AppRelayCommand(ConfirmActionChoice, () => SelectedActionChoice != null);
        DismissNotificationCommand = new AppRelayCommand(parameter =>
        {
            if (parameter is NotificationMessage message)
            {
                runtimeHost?.Notifications.Remove(message.Id);
            }
        });
        ConfirmDialogCommand = new AppRelayCommand(
            () => CompleteDialog(SelectedDialogOption),
            () => SelectedDialogOption != null);
        CancelDialogCommand = new AppRelayCommand(() => CompleteDialog(
            DialogOptions.Count == 0 ? null : DialogOptions[Math.Clamp(dialogCancelIndex, 0, DialogOptions.Count - 1)]));

        ApplyFilters();
    }

    public void AttachRuntime(AvaloniaRuntimeHost host)
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
        var match = allGames.FirstOrDefault(game => game.Game.Id == gameId);
        if (match == null)
        {
            return;
        }

        if (!Games.Contains(match))
        {
            SearchText = string.Empty;
            InstalledOnly = false;
            FavoritesOnly = false;
            SelectedFilterPreset = FilterPresets.FirstOrDefault(preset => preset.Name == "All");
        }

        SelectedGame = match;
    }

    public void RefreshGame(Guid gameId)
    {
        allGames.FirstOrDefault(game => game.Game.Id == gameId)?.Refresh();
        ApplyFilters();
        RaiseGameCommandStates();
    }

    public void ApplyFilterPreset(Guid presetId)
    {
        var preset = FilterPresets.FirstOrDefault(item => item.Id == presetId);
        if (preset != null)
        {
            SelectedFilterPreset = preset;
        }
    }

    public FilterPresetSettings GetCurrentFilterSettings() =>
        SelectedFilterPreset?.Settings ?? new FilterPresetSettings();

    public void SetStatusMessage(string message) => StatusText = message;

    public void SetPluginSummary(string summary)
    {
        pluginSummary = summary;
        OnPropertyChanged(nameof(PluginSummary));
    }

    public void OpenDialog(
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

    private void RunOperation(GameOperationKind kind, int choiceIndex = -1)
    {
        if (runtimeHost == null || SelectedGame == null)
        {
            StatusText = "The Desktop game-operation host is unavailable.";
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
        RunOperation(pendingOperation, index);
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
    }

    private void CloseOverlays()
    {
        IsNotificationsVisible = false;
        IsActionPickerVisible = false;
        IsDialogVisible = false;
    }

    private void ApplyFilters()
    {
        IEnumerable<DesktopGameItemViewModel> filtered = allGames;
        if (database != null)
        {
            var filterSettings = SelectedFilterPreset?.Settings ?? new FilterPresetSettings();
            filtered = filtered.Where(game => database.GetGameMatchesFilter(game.Game, filterSettings));
        }

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

        var materialized = selectedGrouping == GroupableField.None
            ? SortGames(filtered).ToList()
            : filtered
                .GroupBy(GetGroupName)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .SelectMany(group => SortGames(group))
                .ToList();
        string previousGroup = null;
        foreach (var game in materialized)
        {
            var group = selectedGrouping == GroupableField.None ? string.Empty : GetGroupName(game);
            var startsGroup = selectedGrouping != GroupableField.None && group != previousGroup;
            game.SetGroup(group, startsGroup);
            previousGroup = group;
        }

        var previous = SelectedGame;
        Games = materialized;
        SelectedGame = previous != null && Games.Contains(previous) ? previous : Games.FirstOrDefault();
    }

    private IEnumerable<DesktopGameItemViewModel> SortGames(IEnumerable<DesktopGameItemViewModel> source)
    {
        IOrderedEnumerable<DesktopGameItemViewModel> ordered = selectedSortOrder switch
        {
            SortOrder.LastActivity => source.OrderBy(game => game.Game.LastActivity ?? DateTime.MinValue),
            SortOrder.Playtime => source.OrderBy(game => game.Game.Playtime),
            SortOrder.Added => source.OrderBy(game => game.Game.Added ?? DateTime.MinValue),
            SortOrder.ReleaseDate => source.OrderBy(game => game.Game.ReleaseYear ?? 0),
            SortOrder.IsInstalled => source.OrderBy(game => game.IsInstalled),
            SortOrder.Favorite => source.OrderBy(game => game.Favorite),
            SortOrder.Source => source.OrderBy(game => game.SourceName, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Platforms => source.OrderBy(game => game.PlatformName, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.CompletionStatus => source.OrderBy(game => game.CompletionStatusName, StringComparer.CurrentCultureIgnoreCase),
            _ => source.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        return selectedSortDirection == SortOrderDirection.Descending ? ordered.Reverse() : ordered;
    }

    private string GetGroupName(DesktopGameItemViewModel game) => selectedGrouping switch
    {
        GroupableField.Platform => game.PlatformName,
        GroupableField.Source => game.SourceName,
        GroupableField.CompletionStatus => game.CompletionStatusName,
        GroupableField.InstallationStatus => game.StateText,
        GroupableField.ReleaseYear => game.ReleaseYearText,
        GroupableField.Name => string.IsNullOrWhiteSpace(game.Name) ? "#" : game.Name[..1].ToUpperInvariant(),
        GroupableField.PlayTime => game.Game.Playtime == 0
            ? "Not played"
            : TimeSpan.FromSeconds(game.Game.Playtime).TotalHours < 10 ? "Under 10 hours" : "10+ hours",
        _ => string.Empty
    };

    private void RaiseGameCommandStates()
    {
        ((AppRelayCommand)ActivateCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)InstallCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)UninstallCommand).RaiseCanExecuteChanged();
    }

    private void Notifications_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(NotificationCount));

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
