using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Playnite.Avalonia.App.Services;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
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

    private readonly List<DesktopGameItemViewModel> allGames;
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private readonly DesktopGlobalSearchService globalSearch;
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
    private DesktopDialogService dialogService;
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
    private bool isPluginMenuVisible;
    private string pluginMenuTitle;
    private PluginMenuAction selectedPluginMenuItem;
    private bool isPluginSidebarVisible;
    private string pluginSidebarTitle;
    private global::Avalonia.Controls.Control pluginSidebarContent;
    private AvaloniaPluginSidebarItem activePluginSidebarItem;

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

    internal IReadOnlyList<DesktopGameItemViewModel> LibraryGames => allGames;

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
            OnPropertyChanged(nameof(ShowWindowBackgroundImage));
            MetadataDownload?.RefreshTargetSummary();
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
                OnPropertyChanged(nameof(ShowWindowBackgroundImage));
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
    public double GridItemWidth => settings.GridItemWidth;
    public double GridItemHeight =>
        settings.GridItemWidth * settings.GridItemHeightRatio / settings.GridItemWidthRatio +
        (settings.ShowNamesUnderCovers ? 52 : 0);
    public double GridItemSpacing => settings.GridItemSpacing;
    public double GridViewScrollSensitivity => settings.GridViewScrollSensitivity;
    public TimeSpan GridViewScrollDuration =>
        TimeSpan.FromMilliseconds(settings.GridViewScrollDurationMilliseconds);
    public bool GridViewSmoothScrollEnabled => settings.GridViewSmoothScrollEnabled;
    public double ListViewScrollSensitivity => settings.ListViewScrollSensitivity;
    public TimeSpan ListViewScrollDuration =>
        TimeSpan.FromMilliseconds(settings.ListViewScrollDurationMilliseconds);
    public bool ListViewSmoothScrollEnabled => settings.ListViewSmoothScrollEnabled;
    public Playnite.Avalonia.App.Services.DetailsVisibilitySettings DetailsVisibility => settings.DetailsVisibility;
    public double DetailsViewScrollSensitivity => settings.DetailsViewScrollSensitivity;
    public TimeSpan DetailsViewScrollDuration =>
        TimeSpan.FromMilliseconds(settings.DetailsViewScrollDurationMilliseconds);
    public bool DetailsViewSmoothScrollEnabled => settings.DetailsViewSmoothScrollEnabled;
    public double GameDetailsCoverHeight => settings.GameDetailsCoverHeight;
    public double ListIconHeight => settings.DetailsViewListIconSize;
    public double ListIconWidth => settings.DetailsViewListIconSize * 0.75;
    public Thickness DetailsContentMargin => settings.IndentGameDetails
        ? new Thickness(settings.GameDetailsIndentation, 26, 26, 26)
        : new Thickness(26);
    public int LibraryContentColumn => settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left ? 2 : 1;
    public int DetailsContentColumn => settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left ? 1 : 2;
    public global::Avalonia.Controls.GridLength FirstContentColumnWidth =>
        settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
            ? new global::Avalonia.Controls.GridLength(settings.GridDetailsWidth)
            : new global::Avalonia.Controls.GridLength(1, global::Avalonia.Controls.GridUnitType.Star);
    public global::Avalonia.Controls.GridLength SecondContentColumnWidth =>
        settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
            ? new global::Avalonia.Controls.GridLength(1, global::Avalonia.Controls.GridUnitType.Star)
            : new global::Avalonia.Controls.GridLength(settings.GridDetailsWidth);
    public Thickness SidebarBorderThickness => settings.ShowPanelSeparators
        ? new Thickness(0, 0, 1, 0)
        : default;
    public Thickness DetailsBorderThickness => !settings.ShowPanelSeparators
        ? default
        : settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
            ? new Thickness(0, 0, 1, 0)
            : new Thickness(1, 0, 0, 0);
    public bool ShowWindowBackgroundImage =>
        settings.ShowBackgroundImageOnWindow && (!IsGridView || settings.ShowBackImageOnGridView);
    public double BackgroundImageBlurRadius => settings.BlurWindowBackgroundImage
        ? settings.BackgroundImageBlurAmount
        : 0;
    public double BackgroundImageDarkOpacity => settings.DarkenWindowBackgroundImage
        ? settings.BackgroundImageDarkAmount
        : 0;
    public TimeSpan BackgroundImageFadeDuration => settings.BackgroundImageAnimation
        ? TimeSpan.FromMilliseconds(250)
        : TimeSpan.Zero;
    public bool ShowPluginTopPanelItemsLeft => settings.PluginTopPanelAlignment == global::Avalonia.Controls.Dock.Left;
    public bool ShowPluginTopPanelItemsRight => !ShowPluginTopPanelItemsLeft;
    public bool EnableTray
    {
        get => settings.EnableTray;
        set
        {
            if (settings.EnableTray == value)
            {
                return;
            }

            settings.EnableTray = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool MinimizeToTray
    {
        get => settings.MinimizeToTray;
        set
        {
            if (settings.MinimizeToTray == value)
            {
                return;
            }

            settings.MinimizeToTray = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CloseToTray
    {
        get => settings.CloseToTray;
        set
        {
            if (settings.CloseToTray == value)
            {
                return;
            }

            settings.CloseToTray = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public string LibrarySummary => $"{Games.Count:N0} of {allGames.Count:N0} games";
    public string PluginSummary => pluginSummary;
    public ObservableCollection<NotificationMessage> Notifications { get; private set; } = new();
    public int NotificationCount => Notifications.Count;
    public ObservableCollection<string> ActionChoices { get; } = new();
    public ObservableCollection<string> DialogOptions { get; } = new();
    public ObservableCollection<PluginMenuAction> PluginMenuItems { get; } = new();
    public ObservableCollection<DesktopPluginSidebarItem> PluginSidebarItems { get; } = new();
    public ObservableCollection<DesktopPluginTopPanelItem> PluginTopPanelItems { get; } = new();
    public bool IsNotificationsVisible { get => isNotificationsVisible; private set => SetField(ref isNotificationsVisible, value); }
    public bool IsActionPickerVisible { get => isActionPickerVisible; private set => SetField(ref isActionPickerVisible, value); }
    public bool IsDialogVisible { get => isDialogVisible; private set => SetField(ref isDialogVisible, value); }
    public bool IsPluginMenuVisible { get => isPluginMenuVisible; private set => SetField(ref isPluginMenuVisible, value); }
    public string PluginMenuTitle { get => pluginMenuTitle; private set => SetField(ref pluginMenuTitle, value); }
    public bool IsPluginSidebarVisible
    {
        get => isPluginSidebarVisible;
        private set => SetField(ref isPluginSidebarVisible, value);
    }
    public string PluginSidebarTitle
    {
        get => pluginSidebarTitle;
        private set => SetField(ref pluginSidebarTitle, value);
    }
    public global::Avalonia.Controls.Control PluginSidebarContent
    {
        get => pluginSidebarContent;
        private set => SetField(ref pluginSidebarContent, value);
    }
    public PluginMenuAction SelectedPluginMenuItem
    {
        get => selectedPluginMenuItem;
        set
        {
            if (SetField(ref selectedPluginMenuItem, value))
            {
                ((AppRelayCommand)InvokePluginMenuItemCommand).RaiseCanExecuteChanged();
            }
        }
    }
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
    public DesktopGameEditorViewModel Editor { get; }
    public DesktopMetadataDownloadViewModel MetadataDownload { get; }
    public DesktopLibrarySyncViewModel LibrarySync { get; }
    public DesktopUpdateCoordinator Updates { get; }
    public DesktopInstalledGameImportViewModel InstalledGameImport { get; }
    public DesktopPluginSettingsViewModel PluginSettings { get; }
    public DesktopSettingsViewModel Settings { get; }
    public DesktopScriptService Scripts { get; }
    public AvaloniaSearchSession PluginSearch { get; }
    public bool IsPluginSearchVisible => PluginSearch.IsVisible;

    public ICommand ActivateCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand OpenMetadataDownloadCommand { get; }
    public ICommand OpenLibrarySyncCommand { get; }
    public ICommand OpenInstalledGameImportCommand { get; }
    public ICommand AddManualGameCommand { get; }
    public ICommand OpenPluginSettingsListCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenGlobalSearchCommand { get; }
    public ICommand OpenPluginMainMenuCommand { get; }
    public ICommand OpenPluginGameMenuCommand { get; }
    public ICommand InvokePluginMenuItemCommand { get; }
    public ICommand ClosePluginSidebarCommand { get; }
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
        allGames = games?.ToList() ?? new List<DesktopGameItemViewModel>();
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        foreach (var game in allGames)
        {
            game.ApplyAppearance(this.settings);
        }
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
        PluginSearch = new AvaloniaSearchSession(
            () => settings.SaveGlobalSearchFilterSettings
                ? (settings.GlobalSearchIncludeUninstalled, settings.GlobalSearchIncludeHidden)
                : (true, false),
            (message, exception) =>
            {
                var failure = $"{message} {exception.Message}";
                runtimeHost?.ShowMessage(failure, true);
                StatusText = failure;
            });
        if (database != null)
        {
            globalSearch = new DesktopGlobalSearchService(
                database,
                this.settings,
                () => runtimeHost?.Extensions.Plugins.Values.ToList() ?? new List<LoadedPlugin>(),
                () => runtimeHost?.V7Plugins ?? Array.Empty<V7LoadedPlugin>(),
                GetGlobalSearchCommands,
                InvokeGlobalSearchGameAction,
                game => allGames.FirstOrDefault(item => item.Game.Id == game.Id)?.IconPath,
                pluginId => runtimeHost?.Extensions.Plugins.TryGetValue(pluginId, out var plugin) == true
                    ? plugin.PluginIcon
                    : null);
        }
        PluginSearch.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AvaloniaSearchSession.IsVisible))
            {
                OnPropertyChanged(nameof(IsPluginSearchVisible));
            }
        };
        PluginSearch.FiltersChanged += (_, _) =>
        {
            if (!this.settings.SaveGlobalSearchFilterSettings)
            {
                return;
            }

            this.settings.GlobalSearchIncludeUninstalled = PluginSearch.IncludeUninstalled;
            this.settings.GlobalSearchIncludeHidden = PluginSearch.IncludeHidden;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
        Editor = new DesktopGameEditorViewModel(database, RefreshGames, SetStatusMessage);
        MetadataDownload = new DesktopMetadataDownloadViewModel(
            database,
            this.settings,
            ResolveMetadataGames,
            RefreshGames,
            (message, error) =>
            {
                if (runtimeHost != null)
                {
                    runtimeHost.ShowMessage(message, error);
                }
                else
                {
                    StatusText = message;
                }
            });
        MetadataDownload.SettingsChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        MetadataDownload.PropertyChanged += MetadataDownload_PropertyChanged;
        LibrarySync = new DesktopLibrarySyncViewModel(
            database,
            this.settings,
            MetadataDownload,
            SynchronizeLibrary,
            (message, error) =>
            {
                if (runtimeHost != null)
                {
                    runtimeHost.ShowMessage(message, error);
                }
                else
                {
                    StatusText = message;
                }
            });
        LibrarySync.SettingsChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        LibrarySync.PropertyChanged += LibrarySync_PropertyChanged;
        Updates = new DesktopUpdateCoordinator(
            this.settings,
            LibrarySync,
            () => SettingsChanged?.Invoke(this, EventArgs.Empty),
            notification => runtimeHost?.Notifications.Add(notification),
            (message, error) =>
            {
                if (runtimeHost != null)
                {
                    runtimeHost.ShowMessage(message, error);
                }
                else
                {
                    StatusText = message;
                }
            },
            (caption, message) => dialogService?.ShowMessage(
                message,
                caption,
                new[] { "Accept", "Decline" },
                1,
                1) == "Accept",
            () =>
            {
                if (Settings.Open())
                {
                    Settings.SelectedSection = Settings.Updates;
                }
            });
        InstalledGameImport = new DesktopInstalledGameImportViewModel(
            database,
            this.settings,
            MetadataDownload,
            SynchronizeLibrary,
            (message, error) =>
            {
                if (runtimeHost != null)
                {
                    runtimeHost.ShowMessage(message, error);
                }
                else
                {
                    StatusText = message;
                }
            });
        InstalledGameImport.SettingsChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        InstalledGameImport.PropertyChanged += InstalledGameImport_PropertyChanged;
        PluginSettings = new DesktopPluginSettingsViewModel((message, error) =>
        {
            if (runtimeHost != null)
            {
                runtimeHost.ShowMessage(message, error);
            }
            else
            {
                StatusText = message;
            }
        });
        PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
        Scripts = new DesktopScriptService(
            () => runtimeHost?.PluginApi,
            () => SelectedGame?.Game);
        Settings = new DesktopSettingsViewModel(
            this.settings,
            database,
            () => runtimeHost?.MetadataPlugins.ToList() ?? new List<MetadataPlugin>(),
            () => runtimeHost?.Extensions.Plugins.Values.ToList() ?? new List<LoadedPlugin>(),
            () => runtimeHost?.V7Plugins ?? Array.Empty<V7LoadedPlugin>(),
            () => runtimeHost?.LibraryPlugins ?? Array.Empty<LibraryPlugin>(),
            Updates,
            Scripts.TestGameScript,
            SynchronizeLibrary,
            () =>
            {
                ApplyAppearanceSettings();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            },
            (message, error) =>
            {
                if (runtimeHost != null)
                {
                    runtimeHost.ShowMessage(message, error);
                }
                else
                {
                    StatusText = message;
                }
            });
        Settings.PropertyChanged += Settings_PropertyChanged;

        ActivateCommand = new AppRelayCommand(
            () => RunOperation(SelectedGame?.IsInstalled == true ? GameOperationKind.Play : GameOperationKind.Install),
            () => SelectedGame != null);
        InstallCommand = new AppRelayCommand(
            () => RunOperation(GameOperationKind.Install),
            () => SelectedGame != null && !SelectedGame.IsInstalled);
        UninstallCommand = new AppRelayCommand(
            () => RunOperation(GameOperationKind.Uninstall),
            () => SelectedGame?.IsInstalled == true);
        EditCommand = new AppRelayCommand(
            () => OpenGameEditor(SelectedGame.Game.Id),
            () => SelectedGame != null && database != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        OpenMetadataDownloadCommand = new AppRelayCommand(OpenMetadataDownload,
            () => SelectedGame != null && database != null && runtimeHost != null &&
                !Editor.IsVisible && !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        OpenLibrarySyncCommand = new AppRelayCommand(OpenLibrarySync,
            () => database != null && runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        OpenInstalledGameImportCommand = new AppRelayCommand(OpenInstalledGameImport,
            () => database != null && runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        AddManualGameCommand = new AppRelayCommand(AddManualGame,
            () => database != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        OpenPluginSettingsListCommand = new AppRelayCommand(OpenPluginSettingsList,
            () => database != null && runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        OpenSettingsCommand = new AppRelayCommand(OpenSettings,
            () => !Editor.IsVisible &&
                !MetadataDownload.IsRunning && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsRunning && !PluginSettings.IsRunning &&
                !Settings.IsVisible);
        OpenGlobalSearchCommand = new AppRelayCommand(
            () => OpenGlobalSearch(string.Empty),
            () => database != null && !Editor.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsRunning && !InstalledGameImport.IsRunning && !PluginSettings.IsRunning &&
                !Settings.IsVisible);
        OpenPluginMainMenuCommand = new AppRelayCommand(
            () => OpenPluginMenu(false),
            () => runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        OpenPluginGameMenuCommand = new AppRelayCommand(
            () => OpenPluginMenu(true),
            () => runtimeHost != null && SelectedGame != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible);
        InvokePluginMenuItemCommand = new AppRelayCommand(
            InvokeSelectedPluginMenuItem,
            () => IsPluginMenuVisible && SelectedPluginMenuItem != null);
        ClosePluginSidebarCommand = new AppRelayCommand(ClosePluginSidebar);
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
        MetadataDownload.ConfigureProviders(
            () => host.MetadataPlugins.ToList(),
            () => host.LibraryPlugins.ToList());
        LibrarySync.ConfigureProviders(
            () => host.LibraryPlugins.ToList(),
            host.NotifyLibraryUpdated);
        InstalledGameImport.ConfigureLibraryUpdated(host.NotifyLibraryUpdated);
        PluginSettings.Configure(host.Extensions, host.V7Plugins);
        foreach (var game in allGames)
        {
            ConfigureLibraryMedia(game);
        }
        RefreshPluginSurfaces();
        RaiseGameCommandStates();
    }

    public void AttachDialogs(DesktopDialogService dialogs) =>
        dialogService = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public void RefreshPluginSurfaces()
    {
        if (runtimeHost == null)
        {
            return;
        }

        PluginSidebarItems.Clear();
        foreach (var item in runtimeHost.PluginSidebarItems)
        {
            PluginSidebarItems.Add(new DesktopPluginSidebarItem(item, ActivatePluginSidebarItem));
        }
        PluginTopPanelItems.Clear();
        foreach (var item in runtimeHost.PluginTopPanelItems)
        {
            PluginTopPanelItems.Add(new DesktopPluginTopPanelItem(item, ActivatePluginTopPanelItem));
        }
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

    public void SwitchToLibraryView() => CloseOverlays();

    internal void ActivateGame(Guid gameId)
    {
        SelectGame(gameId);
        if (ActivateCommand.CanExecute(null))
        {
            ActivateCommand.Execute(null);
        }
    }

    public void RefreshGame(Guid gameId)
    {
        RefreshGames(new[] { gameId });
    }

    public void RefreshGames(IReadOnlyList<Guid> gameIds)
    {
        foreach (var gameId in gameIds ?? Array.Empty<Guid>())
        {
            allGames.FirstOrDefault(game => game.Game.Id == gameId)?.Refresh();
        }

        ApplyFilters();
        RaiseGameCommandStates();
    }

    public bool OpenGameEditor(Guid gameId, Action<bool?> completed = null)
    {
        return OpenGameEditor(new[] { gameId }, completed);
    }

    public bool OpenGameEditor(IReadOnlyList<Guid> gameIds, Action<bool?> completed = null)
    {
        if (MetadataDownload.IsVisible || MetadataDownload.IsRunning ||
            LibrarySync.IsVisible || LibrarySync.IsRunning ||
            InstalledGameImport.IsVisible || InstalledGameImport.IsRunning ||
            PluginSettings.IsVisible || PluginSettings.IsRunning)
        {
            StatusText = "Finish or close the active library task before editing games.";
            return false;
        }

        CloseOverlays();
        var opened = Editor.Open(gameIds, result =>
        {
            RaiseGameCommandStates();
            completed?.Invoke(result);
        });
        if (!opened)
        {
            StatusText = Editor.IsVisible
                ? "Another game editor is already open."
                : "The selected game is no longer available.";
        }

        RaiseGameCommandStates();
        return opened;
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

    public bool OpenPluginSettings(Guid pluginId) => PluginSettings.OpenSettings(pluginId);

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

    private void OpenMetadataDownload()
    {
        CloseOverlays();
        if (!MetadataDownload.Open())
        {
            StatusText = "The metadata download view is unavailable.";
        }

        RaiseGameCommandStates();
    }

    private void OpenLibrarySync()
    {
        CloseOverlays();
        if (!LibrarySync.Open())
        {
            StatusText = "The library update view is unavailable.";
        }

        RaiseGameCommandStates();
    }

    private void OpenInstalledGameImport()
    {
        CloseOverlays();
        if (!InstalledGameImport.Open())
        {
            StatusText = "The installed-game import view is unavailable.";
        }

        RaiseGameCommandStates();
    }

    private void AddManualGame()
    {
        var game = new Game
        {
            Name = "New Game",
            CompletionStatusId = database.GetCompletionStatusSettings().DefaultStatus
        };
        database.Games.Add(game);
        var opened = OpenGameEditor(game.Id, result =>
        {
            if (result == true)
            {
                SynchronizeLibrary();
                SelectGame(game.Id);
            }
            else
            {
                database.Games.Remove(game);
                SynchronizeLibrary();
                StatusText = "Manual game creation cancelled.";
            }
        });
        if (!opened)
        {
            database.Games.Remove(game);
            SynchronizeLibrary();
        }
    }

    private void OpenSettings()
    {
        CloseOverlays();
        Settings.Open();
        RaiseGameCommandStates();
    }

    public void OpenGlobalSearch(string initialTerm)
    {
        if (globalSearch == null)
        {
            StatusText = "Global search is unavailable because the library database is not open.";
            return;
        }

        CloseOverlays();
        PluginSearch.Open(globalSearch.CreateContext(), initialTerm ?? string.Empty);
        RaiseGameCommandStates();
    }

    private IReadOnlyList<DesktopSearchCommand> GetGlobalSearchCommands() =>
    [
        new("Open settings", "Configure Playnite", OpenSettings),
        new("Update libraries", "Import changes from library plugins and scanners", OpenLibrarySync),
        new("Import installed games", "Discover installed desktop applications", OpenInstalledGameImport),
        new("Add game manually", "Create a new library game", AddManualGame),
        new("Switch to grid view", "Show library covers", () => SelectedViewMode = "Grid"),
        new("Switch to list view", "Show the compact game list", () => SelectedViewMode = "List")
    ];

    private void InvokeGlobalSearchGameAction(Guid gameId, GameSearchItemAction action)
    {
        SelectGame(gameId);
        if (SelectedGame?.Game.Id != gameId)
        {
            return;
        }

        switch (action)
        {
            case GameSearchItemAction.Play:
                RunOperation(SelectedGame.IsInstalled ? GameOperationKind.Play : GameOperationKind.Install);
                break;
            case GameSearchItemAction.SwitchTo:
                break;
            case GameSearchItemAction.OpenMenu:
                OpenPluginMenu(true);
                break;
            case GameSearchItemAction.Edit:
                OpenGameEditor(gameId);
                break;
            case GameSearchItemAction.None:
                break;
            default:
                throw new NotSupportedException($"Unsupported game search action {action}.");
        }
    }

    private void OpenPluginSettingsList()
    {
        CloseOverlays();
        if (!PluginSettings.Open())
        {
            StatusText = "The plugin settings list is unavailable.";
        }

        RaiseGameCommandStates();
    }

    private void OpenPluginMenu(bool forSelectedGame)
    {
        if (runtimeHost == null || (forSelectedGame && SelectedGame == null))
        {
            return;
        }

        CloseOverlays();
        PluginMenuItems.Clear();
        var items = forSelectedGame
            ? runtimeHost.GetGameMenuActions([SelectedGame.Game])
            : runtimeHost.GetMainMenuActions();
        foreach (var item in items)
        {
            PluginMenuItems.Add(item);
        }

        if (PluginMenuItems.Count == 0)
        {
            StatusText = forSelectedGame
                ? "No plugin commands are available for the selected game."
                : "No plugin main-menu commands are available.";
            return;
        }

        PluginMenuTitle = forSelectedGame
            ? $"Plugin commands — {SelectedGame.Name}"
            : "Plugin commands";
        SelectedPluginMenuItem = PluginMenuItems[0];
        IsPluginMenuVisible = true;
        RaiseGameCommandStates();
    }

    private void InvokeSelectedPluginMenuItem()
    {
        var item = SelectedPluginMenuItem;
        if (item == null)
        {
            return;
        }

        try
        {
            item.Invoke();
            StatusText = $"Ran {item.DisplayName} from {item.PluginName}.";
            if (SelectedGame != null)
            {
                RefreshGame(SelectedGame.Game.Id);
            }
            CloseOverlays();
        }
        catch (Exception exception)
        {
            var message = $"Plugin command {item.DisplayName} failed: {exception.Message}";
            runtimeHost?.ShowMessage(message, true);
            StatusText = message;
        }
    }

    private void ActivatePluginSidebarItem(AvaloniaPluginSidebarItem item)
    {
        try
        {
            if (!item.IsView)
            {
                item.Activate();
                StatusText = $"Ran {item.Title} from {item.PluginName}.";
                return;
            }

            CloseOverlays();
            var content = item.Open();
            if (content == null)
            {
                StatusText = $"Plugin view {item.Title} did not return any content.";
                return;
            }

            activePluginSidebarItem = item;
            PluginSidebarTitle = item.Title;
            PluginSidebarContent = content;
            IsPluginSidebarVisible = true;
            StatusText = $"Opened {item.Title} from {item.PluginName}.";
            RaiseGameCommandStates();
        }
        catch (Exception exception)
        {
            var message = $"Plugin sidebar item {item.Title} failed: {exception.Message}";
            runtimeHost?.ShowMessage(message, true);
            StatusText = message;
        }
    }

    private void ActivatePluginTopPanelItem(AvaloniaPluginTopPanelItem item)
    {
        try
        {
            item.Activate();
            StatusText = $"Ran {item.Title} from {item.PluginName}.";
        }
        catch (Exception exception)
        {
            var message = $"Plugin top-panel item {item.Title} failed: {exception.Message}";
            runtimeHost?.ShowMessage(message, true);
            StatusText = message;
        }
    }

    private void ClosePluginSidebar()
    {
        var item = activePluginSidebarItem;
        activePluginSidebarItem = null;
        PluginSidebarContent = null;
        IsPluginSidebarVisible = false;
        if (item != null)
        {
            try
            {
                item.Close();
            }
            catch (Exception exception)
            {
                var message = $"Plugin sidebar item {item.Title} failed to close: {exception.Message}";
                runtimeHost?.ShowMessage(message, true);
                StatusText = message;
            }
        }
        RaiseGameCommandStates();
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
        PluginSearch.Close();
        IsNotificationsVisible = false;
        IsActionPickerVisible = false;
        IsDialogVisible = false;
        IsPluginMenuVisible = false;
        SelectedPluginMenuItem = null;
        ClosePluginSidebar();
        Settings.Close();
    }

    private void ApplyFilters()
    {
        IEnumerable<DesktopGameItemViewModel> filtered = allGames;
        if (database != null)
        {
            var filterSettings = SelectedFilterPreset?.Settings ?? new FilterPresetSettings();
            filtered = filtered.Where(game =>
                database.GetGameMatchesFilter(game.Game, filterSettings, settings.FuzzyMatchingInNameFilter));
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
        var groupCounts = selectedGrouping == GroupableField.None
            ? new Dictionary<string, int>()
            : materialized.GroupBy(GetGroupName).ToDictionary(group => group.Key, group => group.Count());
        string previousGroup = null;
        foreach (var game in materialized)
        {
            var group = selectedGrouping == GroupableField.None ? string.Empty : GetGroupName(game);
            var startsGroup = selectedGrouping != GroupableField.None && group != previousGroup;
            var displayGroup = settings.ShowGroupCount && groupCounts.TryGetValue(group, out var count)
                ? $"{group} ({count:N0})"
                : group;
            game.SetGroup(displayGroup, startsGroup);
            previousGroup = group;
        }

        var previous = SelectedGame;
        Games = materialized;
        SelectedGame = previous != null && Games.Contains(previous) ? previous : Games.FirstOrDefault();
        MetadataDownload?.RefreshTargetSummary();
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

    private void ApplyAppearanceSettings()
    {
        foreach (var game in allGames)
        {
            game.ApplyAppearance(settings);
        }

        OnPropertyChanged(nameof(GridItemWidth));
        OnPropertyChanged(nameof(GridItemHeight));
        OnPropertyChanged(nameof(GridItemSpacing));
        OnPropertyChanged(nameof(GridViewScrollSensitivity));
        OnPropertyChanged(nameof(GridViewScrollDuration));
        OnPropertyChanged(nameof(GridViewSmoothScrollEnabled));
        OnPropertyChanged(nameof(ListViewScrollSensitivity));
        OnPropertyChanged(nameof(ListViewScrollDuration));
        OnPropertyChanged(nameof(ListViewSmoothScrollEnabled));
        OnPropertyChanged(nameof(DetailsVisibility));
        OnPropertyChanged(nameof(DetailsViewScrollSensitivity));
        OnPropertyChanged(nameof(DetailsViewScrollDuration));
        OnPropertyChanged(nameof(DetailsViewSmoothScrollEnabled));
        OnPropertyChanged(nameof(GameDetailsCoverHeight));
        OnPropertyChanged(nameof(ListIconHeight));
        OnPropertyChanged(nameof(ListIconWidth));
        OnPropertyChanged(nameof(DetailsContentMargin));
        OnPropertyChanged(nameof(LibraryContentColumn));
        OnPropertyChanged(nameof(DetailsContentColumn));
        OnPropertyChanged(nameof(FirstContentColumnWidth));
        OnPropertyChanged(nameof(SecondContentColumnWidth));
        OnPropertyChanged(nameof(SidebarBorderThickness));
        OnPropertyChanged(nameof(DetailsBorderThickness));
        OnPropertyChanged(nameof(ShowWindowBackgroundImage));
        OnPropertyChanged(nameof(BackgroundImageBlurRadius));
        OnPropertyChanged(nameof(BackgroundImageDarkOpacity));
        OnPropertyChanged(nameof(BackgroundImageFadeDuration));
        OnPropertyChanged(nameof(ShowPluginTopPanelItemsLeft));
        OnPropertyChanged(nameof(ShowPluginTopPanelItemsRight));
        ApplyFilters();
    }

    private void RaiseGameCommandStates()
    {
        ((AppRelayCommand)ActivateCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)InstallCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)UninstallCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)EditCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenMetadataDownloadCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenLibrarySyncCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenInstalledGameImportCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)AddManualGameCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenPluginSettingsListCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenSettingsCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenGlobalSearchCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenPluginMainMenuCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenPluginGameMenuCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)InvokePluginMenuItemCommand).RaiseCanExecuteChanged();
    }

    private void SynchronizeLibrary()
    {
        if (database == null)
        {
            return;
        }

        var databaseGames = database.Games.ToList();
        var databaseIds = databaseGames.Select(game => game.Id).ToHashSet();
        allGames.RemoveAll(game => !databaseIds.Contains(game.Game.Id));
        var existingIds = allGames.Select(game => game.Game.Id).ToHashSet();
        var newGames = databaseGames.Where(game => !existingIds.Contains(game.Id)).ToList();
        if (settings.GameSortingNameAutofill && newGames.Count > 0)
        {
            SortingNameService.FillMissing(database, newGames, settings.GameSortingNameRemovedArticles);
        }

        foreach (var game in newGames)
        {
            var wrapper = new DesktopGameItemViewModel(game, database);
            wrapper.ApplyAppearance(settings);
            ConfigureLibraryMedia(wrapper);
            allGames.Add(wrapper);
        }

        foreach (var game in allGames)
        {
            game.Refresh();
        }

        ApplyFilters();
    }

    private IReadOnlyList<Game> ResolveMetadataGames(Playnite.Metadata.MetadataGamesSource source) => source switch
    {
        Playnite.Metadata.MetadataGamesSource.Selected => SelectedGame == null
            ? Array.Empty<Game>()
            : new[] { SelectedGame.Game },
        Playnite.Metadata.MetadataGamesSource.Filtered => Games.Select(game => game.Game).ToList(),
        Playnite.Metadata.MetadataGamesSource.AllFromDB => allGames.Select(game => game.Game).ToList(),
        _ => Array.Empty<Game>()
    };

    private void ConfigureLibraryMedia(DesktopGameItemViewModel game)
    {
        var libraryPlugin = runtimeHost?.LibraryPlugins.FirstOrDefault(plugin => plugin.Id == game.Game.PluginId);
        game.ConfigureLibraryMedia(libraryPlugin?.LibraryIcon, libraryPlugin?.LibraryBackground);
    }

    private void MetadataDownload_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesktopMetadataDownloadViewModel.IsVisible) or
            nameof(DesktopMetadataDownloadViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void LibrarySync_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesktopLibrarySyncViewModel.IsVisible) or
            nameof(DesktopLibrarySyncViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void InstalledGameImport_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesktopInstalledGameImportViewModel.IsVisible) or
            nameof(DesktopInstalledGameImportViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void PluginSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesktopPluginSettingsViewModel.IsVisible) or
            nameof(DesktopPluginSettingsViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopSettingsViewModel.IsVisible))
        {
            RaiseGameCommandStates();
        }
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
