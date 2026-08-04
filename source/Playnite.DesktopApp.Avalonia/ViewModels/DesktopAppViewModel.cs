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

public sealed partial class DesktopAppViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<SortOrder> sortOptions = Enum.GetValues<SortOrder>();
    private static readonly IReadOnlyList<GroupableField> groupingOptions = Enum.GetValues<GroupableField>();

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
            if (games != null && value != null && games.SequenceEqual(value))
            {
                // Preserve the bound collection when filtering and sorting produced
                // the same ordered wrappers. Replacing it makes Avalonia rebuild the
                // selection model and can feed a stale visual selection back into the
                // view model while metadata refreshes are completing.
                OnPropertyChanged(nameof(LibrarySummary));
                return;
            }

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
            value ??= Games.FirstOrDefault();
            if (ReferenceEquals(selectedGame, value))
            {
                return;
            }

            selectedGame = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DetailsFilterLinks));
            OnPropertyChanged(nameof(DetailsFilterLinkGroups));
            OnPropertyChanged(nameof(PrimaryGameActionText));
            OnPropertyChanged(nameof(ShowWindowBackgroundImage));
            SynchronizeSelectedGamesWithPrimary(value);
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
            var resolved = value is "Grid" or "List" or "Details" ? value : "Grid";
            if (SetField(ref selectedViewMode, resolved))
            {
                settings.ViewMode = resolved;
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
                OnPropertyChanged(nameof(IsListViewSelected));
                OnPropertyChanged(nameof(IsDetailsView));
                OnPropertyChanged(nameof(FirstContentColumnWidth));
                OnPropertyChanged(nameof(SecondContentColumnWidth));
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
            LoadWorkingFilter(value?.Settings ?? new FilterPresetSettings(), false);
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

    public IReadOnlyList<string> ViewModes { get; } = new[] { "Grid", "List", "Details" };
    public IReadOnlyList<SortOrder> SortOptions => sortOptions;
    public IReadOnlyList<SortOrderDirection> SortDirectionOptions { get; } =
        Enum.GetValues<SortOrderDirection>();
    public IReadOnlyList<GroupableField> GroupingOptions => groupingOptions;
    public ObservableCollection<FilterPreset> FilterPresets { get; }
    public bool IsGridView => SelectedViewMode == "Grid";
    public bool IsListView => SelectedViewMode is "List" or "Details";
    public bool IsListViewSelected => SelectedViewMode == "List";
    public bool IsDetailsView => SelectedViewMode == "Details";
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
    public double GameDetailsCoverWidth => Math.Clamp(settings.GameDetailsCoverHeight * 0.65, 90, 240);
    public double ListIconHeight => settings.DetailsViewListIconSize;
    public double ListIconWidth => settings.DetailsViewListIconSize * 0.75;
    public Thickness DetailsContentMargin => settings.IndentGameDetails
        ? new Thickness(settings.GameDetailsIndentation, 26, 26, 26)
        : new Thickness(26);
    public int LibraryContentColumn => settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left ? 2 : 1;
    public int DetailsContentColumn => settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left ? 1 : 2;
    public global::Avalonia.Controls.GridLength FirstContentColumnWidth =>
        IsDetailsView
            ? settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
                ? new global::Avalonia.Controls.GridLength(1, global::Avalonia.Controls.GridUnitType.Star)
                : new global::Avalonia.Controls.GridLength(Math.Clamp(settings.GridDetailsWidth, 300, 520))
            : settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
            ? new global::Avalonia.Controls.GridLength(settings.GridDetailsWidth)
            : new global::Avalonia.Controls.GridLength(1, global::Avalonia.Controls.GridUnitType.Star);
    public global::Avalonia.Controls.GridLength SecondContentColumnWidth =>
        IsDetailsView
            ? settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
                ? new global::Avalonia.Controls.GridLength(Math.Clamp(settings.GridDetailsWidth, 300, 520))
                : new global::Avalonia.Controls.GridLength(1, global::Avalonia.Controls.GridUnitType.Star)
            : settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
            ? new global::Avalonia.Controls.GridLength(1, global::Avalonia.Controls.GridUnitType.Star)
            : new global::Avalonia.Controls.GridLength(settings.GridDetailsWidth);
    public global::Avalonia.Controls.GridLength LeftSidebarWidth =>
        settings.SidebarVisible && settings.SidebarPosition == global::Avalonia.Controls.Dock.Left
            ? new global::Avalonia.Controls.GridLength(270)
            : new global::Avalonia.Controls.GridLength(0);
    public global::Avalonia.Controls.GridLength RightSidebarWidth =>
        settings.SidebarVisible && settings.SidebarPosition == global::Avalonia.Controls.Dock.Right
            ? new global::Avalonia.Controls.GridLength(270)
            : new global::Avalonia.Controls.GridLength(0);
    public int SidebarContentColumn => settings.SidebarPosition == global::Avalonia.Controls.Dock.Right ? 3 : 0;
    public Thickness SidebarBorderThickness => settings.ShowPanelSeparators
        ? settings.SidebarPosition == global::Avalonia.Controls.Dock.Right
            ? new Thickness(1, 0, 0, 0)
            : new Thickness(0, 0, 1, 0)
        : default;
    public Thickness DetailsBorderThickness => !settings.ShowPanelSeparators
        ? default
        : settings.GridViewDetailsPosition == global::Avalonia.Controls.Dock.Left
            ? new Thickness(0, 0, 1, 0)
            : new Thickness(1, 0, 0, 0);
    public bool ShowLibrarySummaryInTopPanel => settings.TopPanelShowLibrarySummary;
    public bool ShowNotificationsInTopPanel => settings.TopPanelShowNotifications;
    public bool ShowFilterStatusInTopPanel => settings.TopPanelShowFilterStatus;
    public bool ShowUpdateStatusInTopPanel => settings.TopPanelShowUpdateStatus;
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
    public ObservableCollection<DesktopDialogOption> DialogButtons { get; } = new();
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
    public DesktopBackupCoordinator Backups { get; }
    public DesktopInstalledGameImportViewModel InstalledGameImport { get; }
    public DesktopPluginSettingsViewModel PluginSettings { get; }
    public DesktopSettingsViewModel Settings { get; }
    public AddonStoreViewModel AddonStore { get; }
    public EmulatorConfigViewModel EmulatorConfig { get; }
    public EmulatedImportViewModel EmulatedImport { get; }
    public DatabaseFieldsViewModel DatabaseFields { get; }
    public DesktopStatisticsViewModel Statistics { get; }
    public DesktopScriptService Scripts { get; }
    public AvaloniaSearchSession PluginSearch { get; }
    public bool IsPluginSearchVisible => PluginSearch.IsVisible;
    private bool IsLibraryManagerVisible => EmulatorConfig.IsVisible || EmulatedImport.IsVisible ||
        DatabaseFields.IsVisible || ToolsConfig?.IsVisible == true || IsExplorerVisible;

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
    public ICommand OpenAddonStoreCommand { get; }
    public ICommand OpenEmulatorConfigCommand { get; }
    public ICommand OpenEmulatedImportCommand { get; }
    public ICommand OpenDatabaseFieldsCommand { get; }
    public ICommand OpenGlobalSearchCommand { get; }
    public ICommand OpenPluginMainMenuCommand { get; }
    public ICommand OpenPluginGameMenuCommand { get; }
    public ICommand OpenInstallDirectoryCommand { get; }
    public ICommand InvokePluginMenuItemCommand { get; }
    public ICommand ClosePluginSidebarCommand { get; }
    public ICommand SetGridViewCommand { get; }
    public ICommand SetListViewCommand { get; }
    public ICommand SetDetailsViewCommand { get; }
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
        DesktopBackupCoordinator backups,
        string startupError)
    {
        allGames = games?.ToList() ?? new List<DesktopGameItemViewModel>();
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        Backups = backups ?? throw new ArgumentNullException(nameof(backups));
        foreach (var game in allGames)
        {
            game.ApplyAppearance(this.settings);
        }
        this.games = allGames;
        selectedGame = allGames.FirstOrDefault();
        InitializeLibrarySelection();
        selectedViewMode = this.settings.ViewMode is "Grid" or "List" or "Details" ? this.settings.ViewMode : "Grid";
        selectedSortOrder = this.settings.SortOrder;
        selectedSortDirection = this.settings.SortDirection;
        selectedGrouping = this.settings.Grouping;
        FilterPresets = new ObservableCollection<FilterPreset>(
            database?.GetSortedFilterPresets() ?? new List<FilterPreset>());
        // No stored preset means no filter — the full library. Guessing a preset
        // (by name or list position) silently hides games on first launch when
        // the shared database carries the user's legacy presets.
        selectedFilterPreset = FilterPresets.FirstOrDefault(preset =>
            preset.Id == this.settings.ActiveFilterPreset);
        InitializeFilterPanel();
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
        Editor = new DesktopGameEditorViewModel(
            database,
            RefreshGames,
            SetStatusMessage,
            this.settings,
            (caption, message) =>
            {
                var ok = DesktopLocalization.Resolve("LOCOKLabel", "OK");
                var dontShowAgain = DesktopLocalization.Resolve(
                    "LOCDontShowAgainTitle",
                    "Don't show again");
                return dialogService?.ShowMessage(
                    message,
                    caption,
                    new[] { ok, dontShowAgain },
                    0,
                    0) == dontShowAgain;
            },
            () => SettingsChanged?.Invoke(this, EventArgs.Empty),
            () => dialogService);
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
        Editor.ConfigureScriptTester(Scripts.TestGameScript);
        Settings = new DesktopSettingsViewModel(
            this.settings,
            database,
            () => runtimeHost?.MetadataPlugins.ToList() ?? new List<MetadataPlugin>(),
            () => runtimeHost?.Extensions.Plugins.Values.ToList() ?? new List<LoadedPlugin>(),
            () => runtimeHost?.V7Plugins ?? Array.Empty<V7LoadedPlugin>(),
            () => runtimeHost?.LibraryPlugins ?? Array.Empty<LibraryPlugin>(),
            Updates,
            Backups,
            () => dialogService?.SelectFolder(),
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
        AddonStore = new AddonStoreViewModel(
            this.settings,
            new DesktopAddonStoreService(),
            () => dialogService,
            () => SettingsChanged?.Invoke(this, EventArgs.Empty),
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
        EmulatorConfig = new EmulatorConfigViewModel(
            database,
            () => dialogService,
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
        EmulatorConfig.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EmulatorConfigViewModel.IsVisible))
            {
                RaiseGameCommandStates();
            }
        };
        EmulatedImport = new EmulatedImportViewModel(
            database,
            () => dialogService,
            SynchronizeLibrary,
            () => runtimeHost?.NotifyLibraryUpdated(),
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
        EmulatedImport.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EmulatedImportViewModel.IsVisible))
            {
                RaiseGameCommandStates();
            }
        };
        DatabaseFields = new DatabaseFieldsViewModel(
            database,
            () => dialogService,
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
        DatabaseFields.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DatabaseFieldsViewModel.IsVisible))
            {
                RaiseGameCommandStates();
            }
        };
        Statistics = new DesktopStatisticsViewModel(
            database,
            settings,
            GetLibraryName,
            SelectGame);
        Settings.Updates.ConfigureAddonStore(OpenAddonStore);

        ActivateCommand = new AppRelayCommand(
            () => RunOperation(SelectedGame?.IsInstalled == true ? GameOperationKind.Play : GameOperationKind.Install),
            () => SelectedGame != null && !IsLibraryManagerVisible);
        InstallCommand = new AppRelayCommand(
            () => RunOperation(GameOperationKind.Install),
            () => SelectedGame != null && !SelectedGame.IsInstalled && !IsLibraryManagerVisible);
        UninstallCommand = new AppRelayCommand(
            () => RunOperation(GameOperationKind.Uninstall),
            () => SelectedGame?.IsInstalled == true && !IsLibraryManagerVisible);
        EditCommand = new AppRelayCommand(
            () => OpenGameEditor(GetSelectedGameIds()),
            () => SelectedGame != null && database != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenMetadataDownloadCommand = new AppRelayCommand(OpenMetadataDownload,
            () => SelectedGame != null && database != null && runtimeHost != null &&
                !Editor.IsVisible && !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenLibrarySyncCommand = new AppRelayCommand(OpenLibrarySync,
            () => database != null && runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenInstalledGameImportCommand = new AppRelayCommand(OpenInstalledGameImport,
            () => database != null && runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        AddManualGameCommand = new AppRelayCommand(AddManualGame,
            () => database != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenPluginSettingsListCommand = new AppRelayCommand(OpenPluginSettingsList,
            () => database != null && runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenSettingsCommand = new AppRelayCommand(OpenSettings,
            () => !Editor.IsVisible &&
                !MetadataDownload.IsRunning && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsRunning && !PluginSettings.IsRunning &&
                !Settings.IsVisible && !IsLibraryManagerVisible);
        OpenAddonStoreCommand = new AppRelayCommand(
            OpenAddonStore,
            () => !Editor.IsVisible && !MetadataDownload.IsRunning && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsRunning && !PluginSettings.IsRunning && !Settings.IsVisible &&
                !IsLibraryManagerVisible);
        OpenEmulatorConfigCommand = new AppRelayCommand(
            () => OpenEmulatorConfig(EmulatorConfigViewModel.EmulatorConfigPage.Emulators),
            () => database != null && !Editor.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsRunning && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsRunning && !Settings.IsVisible && !EmulatedImport.IsVisible &&
                !DatabaseFields.IsVisible && !EmulatorConfig.IsVisible);
        OpenEmulatedImportCommand = new AppRelayCommand(
            OpenEmulatedImport,
            () => database != null && !Editor.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsRunning && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsRunning && !Settings.IsVisible && !EmulatorConfig.IsVisible &&
                !DatabaseFields.IsVisible && !EmulatedImport.IsVisible);
        OpenDatabaseFieldsCommand = new AppRelayCommand(
            () => OpenDatabaseFields(null),
            () => database != null && !Editor.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsRunning && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsRunning && !Settings.IsVisible && !EmulatorConfig.IsVisible &&
                !EmulatedImport.IsVisible && !DatabaseFields.IsVisible);
        OpenGlobalSearchCommand = new AppRelayCommand(
            () => OpenGlobalSearch(string.Empty),
            () => database != null && !Editor.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsRunning && !InstalledGameImport.IsRunning && !PluginSettings.IsRunning &&
                !Settings.IsVisible && !IsLibraryManagerVisible);
        OpenPluginMainMenuCommand = new AppRelayCommand(
            () => OpenPluginMenu(false),
            () => runtimeHost != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenPluginGameMenuCommand = new AppRelayCommand(
            () => OpenPluginMenu(true),
            () => runtimeHost != null && SelectedGame != null && !Editor.IsVisible &&
                !MetadataDownload.IsVisible && !MetadataDownload.IsRunning &&
                !LibrarySync.IsVisible && !LibrarySync.IsRunning &&
                !InstalledGameImport.IsVisible && !InstalledGameImport.IsRunning &&
                !PluginSettings.IsVisible && !PluginSettings.IsRunning && !IsPluginMenuVisible &&
                !IsLibraryManagerVisible);
        OpenInstallDirectoryCommand = new AppRelayCommand(
            OpenInstallDirectory,
            () => SelectedGame != null &&
                !string.IsNullOrWhiteSpace(SelectedGame.Game.InstallDirectory) &&
                Directory.Exists(SelectedGame.Game.InstallDirectory) && !IsLibraryManagerVisible);
        InvokePluginMenuItemCommand = new AppRelayCommand(
            InvokeSelectedPluginMenuItem,
            () => IsPluginMenuVisible && SelectedPluginMenuItem != null);
        ClosePluginSidebarCommand = new AppRelayCommand(ClosePluginSidebar);
        SetGridViewCommand = new AppRelayCommand(() => SelectedViewMode = "Grid");
        SetListViewCommand = new AppRelayCommand(() => SelectedViewMode = "List");
        SetDetailsViewCommand = new AppRelayCommand(() => SelectedViewMode = "Details");
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
        InitializeLibraryInteractionCommands();
        InitializeChromeParity();

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
        Editor.ConfigureMetadataProviders(
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
            SelectedFilterPreset = null;
        }

        SelectedGame = match;
    }

    public void SwitchToLibraryView() => CloseOverlays();

    private void OpenInstallDirectory() => OpenInstallDirectory(SelectedGame?.Game);

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
        CloneFilterSettings(GetWorkingFilterSettings());

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
        DialogButtons.Clear();
        foreach (var option in options)
        {
            DialogOptions.Add(option);
            DialogButtons.Add(new DesktopDialogOption(
                option,
                () => CompleteDialog(option)));
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

    private void OpenAddonStore()
    {
        CloseOverlays();
        AddonStore.Open();
        RaiseGameCommandStates();
    }

    private void OpenEmulatorConfig(string page)
    {
        CloseOverlays();
        if (!EmulatorConfig.Open(page))
        {
            StatusText = "The emulation configuration view is unavailable.";
        }
        RaiseGameCommandStates();
    }

    private void OpenEmulatedImport()
    {
        CloseOverlays();
        if (!EmulatedImport.Open())
        {
            StatusText = "The emulated-game import view is unavailable.";
        }
        RaiseGameCommandStates();
    }

    private void OpenDatabaseFields(string kind)
    {
        CloseOverlays();
        if (!DatabaseFields.Open(kind))
        {
            StatusText = "The library fields manager is unavailable.";
        }
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
        new("Open add-on store", "Browse and manage extensions and themes", OpenAddonStore),
        new("Configure emulators", "Manage emulator profiles and ROM scanners", () =>
            OpenEmulatorConfig(EmulatorConfigViewModel.EmulatorConfigPage.Emulators)),
        new("Import emulated games", "Scan ROM folders and review detected games", OpenEmulatedImport),
        new("Manage library fields", "Create and organize taxonomy values", () => OpenDatabaseFields(null)),
        new("Update libraries", "Import changes from library plugins and scanners", OpenLibrarySync),
        new("Import installed games", "Discover installed desktop applications", OpenInstalledGameImport),
        new("Add game manually", "Create a new library game", AddManualGame),
        new("Switch to grid view", "Show library covers", () => SelectedViewMode = "Grid"),
        new("Switch to list view", "Show the compact game list", () => SelectedViewMode = "List"),
        new("Switch to details view", "Show a narrow list and full game overview", () => SelectedViewMode = "Details")
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
        var selectedGames = GetSelectedGames();
        var items = forSelectedGame
            ? runtimeHost.GetGameMenuActions(selectedGames)
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
        IsFilterPanelVisible = false;
        AddonStore.Close();
        EmulatorConfig.Close();
        EmulatedImport.Close();
        DatabaseFields.Close();
        Statistics.Close();
        CloseChromeParityOverlays();
        SelectedPluginMenuItem = null;
        ClosePluginSidebar();
        Settings.Close();
    }

    private void ApplyFilters()
    {
        IEnumerable<DesktopGameItemViewModel> filtered = allGames;
        if (database != null)
        {
            var filterSettings = GetWorkingFilterSettings();
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
            var nameFilter = new FilterPresetSettings { Name = $"!{SearchText}" };
            filtered = database == null
                ? filtered.Where(game => game.Name.Contains(
                    SearchText,
                    StringComparison.CurrentCultureIgnoreCase))
                : filtered.Where(game => database.GetGameMatchesFilter(
                    game.Game,
                    nameFilter,
                    settings.FuzzyMatchingInNameFilter));
        }

        List<DesktopGameItemViewModel> materialized;
        if (selectedGrouping == GroupableField.None)
        {
            materialized = SortGames(filtered).ToList();
            foreach (var game in materialized)
            {
                game.SetGroup(string.Empty, false);
            }
        }
        else
        {
            materialized = new List<DesktopGameItemViewModel>();
            foreach (var group in filtered
                .GroupBy(GetGroupName)
                .OrderBy(entry => entry.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                var gamesInGroup = SortGames(group).ToList();
                var expanded = !settings.CollapsedGameGroups.Contains(GetCollapsedGroupKey(group.Key), StringComparer.Ordinal);
                var displayGroup = settings.ShowGroupCount
                    ? $"{group.Key} ({gamesInGroup.Count:N0})"
                    : group.Key;
                for (var index = 0; index < gamesInGroup.Count; index++)
                {
                    var game = gamesInGroup[index];
                    game.SetGroup(
                        displayGroup,
                        index == 0,
                        expanded,
                        index == 0 ? () => ToggleGroup(group.Key) : null);
                    if (expanded || index == 0)
                    {
                        materialized.Add(game);
                    }
                }
            }
        }

        Games = materialized;
        RetainSelectedGames(Games);
        MetadataDownload?.RefreshTargetSummary();
    }

    private string GetCollapsedGroupKey(string group) => $"{(int)selectedGrouping}:{group}";

    private void ToggleGroup(string group)
    {
        var key = GetCollapsedGroupKey(group);
        if (!settings.CollapsedGameGroups.Remove(key))
        {
            settings.CollapsedGameGroups.Add(key);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
        ApplyFilters();
    }

    private IEnumerable<DesktopGameItemViewModel> SortGames(IEnumerable<DesktopGameItemViewModel> source)
    {
        IOrderedEnumerable<DesktopGameItemViewModel> ordered = selectedSortOrder switch
        {
            SortOrder.Platforms => source.OrderBy(game => GetNames(game.Game.PlatformIds, id => database.Platforms[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Library => source.OrderBy(game => GetLibraryName(game.Game.PluginId), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Categories => source.OrderBy(game => GetNames(game.Game.CategoryIds, id => database.Categories[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.LastActivity => source.OrderBy(game => game.Game.LastActivity ?? DateTime.MinValue),
            SortOrder.Genres => source.OrderBy(game => GetNames(game.Game.GenreIds, id => database.Genres[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Playtime => source.OrderBy(game => game.Game.Playtime),
            SortOrder.Added => source.OrderBy(game => game.Game.Added ?? DateTime.MinValue),
            SortOrder.ReleaseDate => source.OrderBy(game => game.Game.ReleaseDate?.Date ?? DateTime.MinValue),
            SortOrder.Developers => source.OrderBy(game => GetNames(game.Game.DeveloperIds, id => database.Companies[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Publishers => source.OrderBy(game => GetNames(game.Game.PublisherIds, id => database.Companies[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Tags => source.OrderBy(game => GetNames(game.Game.TagIds, id => database.Tags[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Series => source.OrderBy(game => GetNames(game.Game.SeriesIds, id => database.Series[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.AgeRatings => source.OrderBy(game => GetNames(game.Game.AgeRatingIds, id => database.AgeRatings[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Version => source.OrderBy(game => game.Game.Version, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Regions => source.OrderBy(game => GetNames(game.Game.RegionIds, id => database.Regions[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.PlayCount => source.OrderBy(game => game.Game.PlayCount),
            SortOrder.IsInstalled => source.OrderBy(game => game.IsInstalled),
            SortOrder.Hidden => source.OrderBy(game => game.Game.Hidden),
            SortOrder.Favorite => source.OrderBy(game => game.Favorite),
            SortOrder.Source => source.OrderBy(game => game.SourceName, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.CompletionStatus => source.OrderBy(game => game.CompletionStatusName, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.UserScore => source.OrderBy(game => game.Game.UserScore ?? -1),
            SortOrder.CriticScore => source.OrderBy(game => game.Game.CriticScore ?? -1),
            SortOrder.CommunityScore => source.OrderBy(game => game.Game.CommunityScore ?? -1),
            SortOrder.Modified => source.OrderBy(game => game.Game.Modified ?? DateTime.MinValue),
            SortOrder.InstallDirectory => source.OrderBy(game => game.Game.InstallDirectory, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Features => source.OrderBy(game => GetNames(game.Game.FeatureIds, id => database.Features[id]?.Name), StringComparer.CurrentCultureIgnoreCase),
            SortOrder.InstallSize => source.OrderBy(game => game.Game.InstallSize ?? 0),
            SortOrder.RecentActivity => source.OrderBy(game => game.Game.RecentActivity ?? DateTime.MinValue),
            SortOrder.RomList => source.OrderBy(game => string.Join(", ", game.Game.Roms?.Select(rom => rom.Path) ?? Array.Empty<string>()), StringComparer.CurrentCultureIgnoreCase),
            _ => source.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        return selectedSortDirection == SortOrderDirection.Descending ? ordered.Reverse() : ordered;
    }

    private string GetGroupName(DesktopGameItemViewModel game) => selectedGrouping switch
    {
        GroupableField.Platform => GetNames(game.Game.PlatformIds, id => database.Platforms[id]?.Name),
        GroupableField.Library => GetLibraryName(game.Game.PluginId),
        GroupableField.Category => GetNames(game.Game.CategoryIds, id => database.Categories[id]?.Name),
        GroupableField.LastActivity => game.Game.LastActivitySegment.ToString(),
        GroupableField.Genre => GetNames(game.Game.GenreIds, id => database.Genres[id]?.Name),
        GroupableField.Developer => GetNames(game.Game.DeveloperIds, id => database.Companies[id]?.Name),
        GroupableField.Publisher => GetNames(game.Game.PublisherIds, id => database.Companies[id]?.Name),
        GroupableField.Tag => GetNames(game.Game.TagIds, id => database.Tags[id]?.Name),
        GroupableField.Series => GetNames(game.Game.SeriesIds, id => database.Series[id]?.Name),
        GroupableField.AgeRating => GetNames(game.Game.AgeRatingIds, id => database.AgeRatings[id]?.Name),
        GroupableField.Region => GetNames(game.Game.RegionIds, id => database.Regions[id]?.Name),
        GroupableField.Source => game.SourceName,
        GroupableField.CompletionStatus => game.CompletionStatusName,
        GroupableField.UserScore => game.Game.UserScoreGroup.ToString(),
        GroupableField.CriticScore => game.Game.CriticScoreGroup.ToString(),
        GroupableField.CommunityScore => game.Game.CommunityScoreGroup.ToString(),
        GroupableField.Added => game.Game.AddedSegment.ToString(),
        GroupableField.Modified => game.Game.ModifiedSegment.ToString(),
        GroupableField.Feature => GetNames(game.Game.FeatureIds, id => database.Features[id]?.Name),
        GroupableField.InstallationStatus => game.Game.InstallationStatus.ToString(),
        GroupableField.ReleaseYear => game.ReleaseYearText,
        GroupableField.Name => game.Game.GetNameGroup().ToString(),
        GroupableField.PlayTime => game.Game.PlaytimeCategory.ToString(),
        GroupableField.InstallDrive => game.Game.GetInstallDriveGroup(),
        GroupableField.InstallSize => game.Game.GetInstallSizeGroup().ToString(),
        GroupableField.RecentActivity => game.Game.RecentActivitySegment.ToString(),
        _ => string.Empty
    };

    private string GetLibraryName(Guid pluginId)
    {
        if (pluginId == Guid.Empty)
        {
            return Localize("LOCUndefined", "Undefined");
        }

        if (runtimeHost?.Extensions.Plugins.TryGetValue(pluginId, out var loaded) == true)
        {
            return loaded.Description?.Name ?? pluginId.ToString();
        }

        return runtimeHost?.V7Plugins.FirstOrDefault(plugin => plugin.Id == pluginId)?.Name ?? pluginId.ToString();
    }

    private static string GetNames(IEnumerable<Guid> ids, Func<Guid, string> resolve)
    {
        var value = string.Join(", ", (ids ?? Array.Empty<Guid>())
            .Select(resolve)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
        return string.IsNullOrWhiteSpace(value) ? "Undefined" : value;
    }

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
        OnPropertyChanged(nameof(DetailsFilterLinkGroups));
        OnPropertyChanged(nameof(DetailsViewScrollSensitivity));
        OnPropertyChanged(nameof(DetailsViewScrollDuration));
        OnPropertyChanged(nameof(DetailsViewSmoothScrollEnabled));
        OnPropertyChanged(nameof(GameDetailsCoverHeight));
        OnPropertyChanged(nameof(GameDetailsCoverWidth));
        OnPropertyChanged(nameof(ListIconHeight));
        OnPropertyChanged(nameof(ListIconWidth));
        OnPropertyChanged(nameof(DetailsContentMargin));
        OnPropertyChanged(nameof(LibraryContentColumn));
        OnPropertyChanged(nameof(DetailsContentColumn));
        OnPropertyChanged(nameof(FirstContentColumnWidth));
        OnPropertyChanged(nameof(SecondContentColumnWidth));
        OnPropertyChanged(nameof(LeftSidebarWidth));
        OnPropertyChanged(nameof(RightSidebarWidth));
        OnPropertyChanged(nameof(SidebarContentColumn));
        OnPropertyChanged(nameof(SidebarBorderThickness));
        OnPropertyChanged(nameof(DetailsBorderThickness));
        OnPropertyChanged(nameof(ShowWindowBackgroundImage));
        OnPropertyChanged(nameof(BackgroundImageBlurRadius));
        OnPropertyChanged(nameof(BackgroundImageDarkOpacity));
        OnPropertyChanged(nameof(BackgroundImageFadeDuration));
        OnPropertyChanged(nameof(ShowPluginTopPanelItemsLeft));
        OnPropertyChanged(nameof(ShowPluginTopPanelItemsRight));
        OnPropertyChanged(nameof(ShowLibrarySummaryInTopPanel));
        OnPropertyChanged(nameof(ShowNotificationsInTopPanel));
        OnPropertyChanged(nameof(ShowFilterStatusInTopPanel));
        OnPropertyChanged(nameof(ShowUpdateStatusInTopPanel));
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
        ((AppRelayCommand)OpenAddonStoreCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenEmulatorConfigCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenEmulatedImportCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenDatabaseFieldsCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenGlobalSearchCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenPluginMainMenuCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenPluginGameMenuCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)OpenInstallDirectoryCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)InvokePluginMenuItemCommand).RaiseCanExecuteChanged();
        RaiseLibraryInteractionCommandStates();
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
        Statistics.RefreshIfVisible();
    }

    private IReadOnlyList<Game> ResolveMetadataGames(Playnite.Metadata.MetadataGamesSource source) => source switch
    {
        Playnite.Metadata.MetadataGamesSource.Selected => GetSelectedGames(),
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
        RaiseGlobalProgressProperties();
        if (e.PropertyName is nameof(DesktopMetadataDownloadViewModel.IsVisible) or
            nameof(DesktopMetadataDownloadViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void LibrarySync_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RaiseGlobalProgressProperties();
        if (e.PropertyName is nameof(DesktopLibrarySyncViewModel.IsVisible) or
            nameof(DesktopLibrarySyncViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void InstalledGameImport_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RaiseGlobalProgressProperties();
        if (e.PropertyName is nameof(DesktopInstalledGameImportViewModel.IsVisible) or
            nameof(DesktopInstalledGameImportViewModel.IsRunning))
        {
            RaiseGameCommandStates();
        }
    }

    private void PluginSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RaiseGlobalProgressProperties();
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

    private void Notifications_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(NotificationCount));
        (ClearNotificationsCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
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

public sealed class DesktopDialogOption
{
    public string Title { get; }
    public ICommand SelectCommand { get; }

    public DesktopDialogOption(string title, Action select)
    {
        Title = title ?? string.Empty;
        SelectCommand = new AppRelayCommand(select ?? (() => { }));
    }
}
