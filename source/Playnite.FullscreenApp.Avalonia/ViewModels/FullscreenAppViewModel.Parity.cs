using System.Collections.ObjectModel;
using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.Controllers;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed partial class FullscreenAppViewModel
{
    private readonly Stack<(string Title, IReadOnlyList<FullscreenMenuItemViewModel> Items)> menuHistory = new();
    private bool isCommandMenuVisible;
    private string commandMenuTitle;
    private FullscreenMenuItemViewModel selectedCommandMenuItem;
    private bool isTextInputVisible;
    private string textInputCaption;
    private string textInputMessage;
    private string textInputText;
    private Action<bool, string> textInputCompleted;
    private bool isGameStatusVisible;
    private string gameStatusText;
    private GameItemViewModel gameStatusGame;

    public event EventHandler SwitchToDesktopRequested;
    public event EventHandler RestoreRequested;

    public ObservableCollection<FullscreenMenuItemViewModel> CommandMenuItems { get; } = new();
    public ObservableCollection<MessageBoxToggle> TextInputToggles { get; } = new();

    public bool IsCommandMenuVisible
    {
        get => isCommandMenuVisible;
        private set => SetField(ref isCommandMenuVisible, value);
    }

    public string CommandMenuTitle
    {
        get => commandMenuTitle;
        private set => SetField(ref commandMenuTitle, value);
    }

    public FullscreenMenuItemViewModel SelectedCommandMenuItem
    {
        get => selectedCommandMenuItem;
        set => SetField(ref selectedCommandMenuItem, value);
    }

    public bool IsTextInputVisible
    {
        get => isTextInputVisible;
        private set => SetField(ref isTextInputVisible, value);
    }

    public string TextInputCaption
    {
        get => textInputCaption;
        private set => SetField(ref textInputCaption, value);
    }

    public string TextInputMessage
    {
        get => textInputMessage;
        private set => SetField(ref textInputMessage, value);
    }

    public string TextInputText
    {
        get => textInputText;
        set => SetField(ref textInputText, value ?? string.Empty);
    }

    public bool IsGameStatusVisible
    {
        get => isGameStatusVisible;
        private set => SetField(ref isGameStatusVisible, value);
    }

    public string GameStatusText
    {
        get => gameStatusText;
        private set => SetField(ref gameStatusText, value);
    }

    public GameItemViewModel GameStatusGame
    {
        get => gameStatusGame;
        private set => SetField(ref gameStatusGame, value);
    }

    public ICommand OpenGameMenuCommand { get; private set; }
    public ICommand ConfirmCommandMenuItemCommand { get; private set; }
    public ICommand ConfirmTextInputCommand { get; private set; }
    public ICommand CancelTextInputCommand { get; private set; }
    public ICommand CloseGameStatusCommand { get; private set; }
    public ICommand SwitchToDesktopCommand { get; private set; }
    public ICommand UpdateLibraryCommand { get; private set; }
    public ICommand SelectRandomGameCommand { get; private set; }
    public ICommand CyclePreviousPresetCommand { get; private set; }
    public ICommand CycleNextPresetCommand { get; private set; }

    private void InitializeParityCommands()
    {
        OpenGameMenuCommand = new RelayCommand(OpenGameMenu, () => SelectedGame != null);
        ConfirmCommandMenuItemCommand = new RelayCommand(ConfirmCommandMenuItem, () => SelectedCommandMenuItem != null);
        ConfirmTextInputCommand = new RelayCommand(() => CompleteTextInput(true));
        CancelTextInputCommand = new RelayCommand(() => CompleteTextInput(false));
        CloseGameStatusCommand = new RelayCommand(() => IsGameStatusVisible = false);
        SwitchToDesktopCommand = new RelayCommand(() => SwitchToDesktopRequested?.Invoke(this, EventArgs.Empty));
        UpdateLibraryCommand = new RelayCommand(UpdateLibrary, () => runtimeHost != null && database != null);
        SelectRandomGameCommand = new RelayCommand(SelectRandomGame, () => Games.Count > 0);
        CyclePreviousPresetCommand = new RelayCommand(() => Filters.CycleQuickPreset(-1));
        CycleNextPresetCommand = new RelayCommand(() => Filters.CycleQuickPreset(1));
    }

    private void AttachParityRuntime(FullscreenRuntimeHost host)
    {
        host.Actions.GameStateChanged += HandleGameStateChanged;
        ((RelayCommand)UpdateLibraryCommand).RaiseCanExecuteChanged();
    }

    internal void OpenTextInput(
        string caption,
        string message,
        string defaultInput,
        IReadOnlyList<MessageBoxToggle> toggles,
        Action<bool, string> completed)
    {
        CloseOverlays();
        TextInputCaption = string.IsNullOrWhiteSpace(caption) ? "Playnite" : caption;
        TextInputMessage = message ?? string.Empty;
        TextInputText = defaultInput ?? string.Empty;
        TextInputToggles.Clear();
        foreach (var toggle in toggles ?? Array.Empty<MessageBoxToggle>())
        {
            TextInputToggles.Add(toggle);
        }

        textInputCompleted = completed;
        IsTextInputVisible = true;
    }

    private void CompleteTextInput(bool confirmed)
    {
        if (!IsTextInputVisible)
        {
            return;
        }

        IsTextInputVisible = false;
        var completed = textInputCompleted;
        textInputCompleted = null;
        completed?.Invoke(confirmed, TextInputText);
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool ConfirmParityOverlay()
    {
        if (IsTextInputVisible)
        {
            ConfirmTextInputCommand.Execute(null);
            return true;
        }

        if (IsCommandMenuVisible)
        {
            ConfirmCommandMenuItemCommand.Execute(null);
            return true;
        }

        return false;
    }

    private bool BackParityOverlay()
    {
        if (IsTextInputVisible)
        {
            CancelTextInputCommand.Execute(null);
            return true;
        }

        if (!IsCommandMenuVisible)
        {
            return false;
        }

        if (menuHistory.Count > 0)
        {
            var previous = menuHistory.Pop();
            SetCommandMenu(previous.Title, previous.Items, false);
        }
        else
        {
            IsCommandMenuVisible = false;
            LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    private void CloseParityOverlays()
    {
        IsCommandMenuVisible = false;
        IsTextInputVisible = false;
        menuHistory.Clear();
    }

    private void OpenGameMenu()
    {
        if (SelectedGame == null)
        {
            return;
        }

        var game = SelectedGame.Game;
        var items = new List<FullscreenMenuItemViewModel>
        {
            new(game.IsInstalled ? "Play" : "Install", () =>
                RunOperation(game.IsInstalled ? GameOperationKind.Play : GameOperationKind.Install))
        };
        foreach (var action in game.GameActions?.Where(action => !action.IsPlayAction) ?? [])
        {
            var captured = action;
            items.Add(new FullscreenMenuItemViewModel(captured.Name, () => RunCustomAction(game, captured)));
        }

        items.Add(new FullscreenMenuItemViewModel(
            game.Favorite ? "Remove from favorites" : "Add to favorites",
            () => UpdateGame(game, () => game.Favorite = !game.Favorite)));
        items.Add(new FullscreenMenuItemViewModel(
            game.Hidden ? "Unhide game" : "Hide game",
            () => UpdateGame(game, () => game.Hidden = !game.Hidden)));
        if (HdrUtilities.IsHdrSupported())
        {
            items.Add(new FullscreenMenuItemViewModel(
                game.EnableSystemHdr ? "Disable HDR" : "Enable HDR",
                () => UpdateGame(game, () => game.EnableSystemHdr = !game.EnableSystemHdr)));
        }

        items.Add(new FullscreenMenuItemViewModel("Set fields", children: BuildSetFieldItems(game)));
        items.Add(new FullscreenMenuItemViewModel("Extensions", children: BuildPluginItems(
            runtimeHost?.GetGameMenuActions(game) ?? Array.Empty<PluginMenuAction>())));
        items.Add(new FullscreenMenuItemViewModel("Remove game", () => RemoveGame(game)));
        if (!game.IsCustomGame && game.IsInstalled)
        {
            items.Add(new FullscreenMenuItemViewModel("Uninstall", () => RunOperation(GameOperationKind.Uninstall)));
        }

        OpenCommandMenu(game.Name, items);
    }

    private void OpenCommandMenu(string title, IReadOnlyList<FullscreenMenuItemViewModel> items)
    {
        CloseOverlays();
        menuHistory.Clear();
        SetCommandMenu(title, items, true);
    }

    private void SetCommandMenu(string title, IReadOnlyList<FullscreenMenuItemViewModel> items, bool visible)
    {
        CommandMenuTitle = title;
        CommandMenuItems.Clear();
        foreach (var item in items)
        {
            CommandMenuItems.Add(item);
        }

        SelectedCommandMenuItem = CommandMenuItems.FirstOrDefault();
        IsCommandMenuVisible = visible || IsCommandMenuVisible;
    }

    private void ConfirmCommandMenuItem()
    {
        var item = SelectedCommandMenuItem;
        if (item == null)
        {
            return;
        }

        if (item.HasChildren)
        {
            menuHistory.Push((CommandMenuTitle, CommandMenuItems.ToList()));
            SetCommandMenu(item.Title, item.Children, true);
            return;
        }

        IsCommandMenuVisible = false;
        menuHistory.Clear();
        try
        {
            item.Command?.Execute(null);
        }
        catch (Exception exception)
        {
            StatusText = $"{item.Title} failed: {exception.Message}";
        }
    }

    private IReadOnlyList<FullscreenMenuItemViewModel> BuildPluginItems(IReadOnlyList<PluginMenuAction> actions)
    {
        var items = actions.Select(action => new FullscreenMenuItemViewModel(
            action.DisplayName,
            action.Invoke,
            description: action.PluginName)).ToList();
        if (items.Count == 0)
        {
            items.Add(new FullscreenMenuItemViewModel("No extension actions are available"));
        }

        return items;
    }

    private IReadOnlyList<FullscreenMenuItemViewModel> BuildSetFieldItems(Game game) =>
        new List<FullscreenMenuItemViewModel>
        {
            new("Completion status", () => SelectSingleField(database.CompletionStatuses, game.CompletionStatusId,
                value => game.CompletionStatusId = value, game, "Completion status")),
            new("User score", () => SelectUserScore(game)),
            new("Categories", () => SelectMultipleField(database.Categories, game.CategoryIds,
                value => game.CategoryIds = value, game, "Categories")),
            new("Tags", () => SelectMultipleField(database.Tags, game.TagIds,
                value => game.TagIds = value, game, "Tags")),
            new("Features", () => SelectMultipleField(database.Features, game.FeatureIds,
                value => game.FeatureIds = value, game, "Features")),
            new("Platforms", () => SelectMultipleField(database.Platforms, game.PlatformIds,
                value => game.PlatformIds = value, game, "Platforms")),
            new("Genres", () => SelectMultipleField(database.Genres, game.GenreIds,
                value => game.GenreIds = value, game, "Genres")),
            new("Developers", () => SelectMultipleField(database.Companies, game.DeveloperIds,
                value => game.DeveloperIds = value, game, "Developers")),
            new("Publishers", () => SelectMultipleField(database.Companies, game.PublisherIds,
                value => game.PublisherIds = value, game, "Publishers")),
            new("Series", () => SelectMultipleField(database.Series, game.SeriesIds,
                value => game.SeriesIds = value, game, "Series")),
            new("Age ratings", () => SelectMultipleField(database.AgeRatings, game.AgeRatingIds,
                value => game.AgeRatingIds = value, game, "Age ratings")),
            new("Regions", () => SelectMultipleField(database.Regions, game.RegionIds,
                value => game.RegionIds = value, game, "Regions")),
            new("Source", () => SelectSingleField(database.Sources, game.SourceId,
                value => game.SourceId = value, game, "Source"))
        };

    private void SelectSingleField<T>(
        IEnumerable<T> values,
        Guid selected,
        Action<Guid> setter,
        Game game,
        string caption) where T : DatabaseObject
    {
        var items = new List<AvaloniaSelectionItem<Guid>>
        {
            new("None", Guid.Empty, selected: selected == Guid.Empty)
        };
        items.AddRange(values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new AvaloniaSelectionItem<Guid>(item.Name, item.Id, selected: item.Id == selected)));
        var result = runtimeHost.Dialogs.SelectSingle(caption, string.Empty, items);
        if (result.Confirmed)
        {
            UpdateGame(game, () => setter(result.SelectedItem));
        }
    }

    private void SelectMultipleField<T>(
        IEnumerable<T> values,
        IReadOnlyCollection<Guid> selected,
        Action<List<Guid>> setter,
        Game game,
        string caption) where T : DatabaseObject
    {
        var selectedIds = selected?.ToHashSet() ?? new HashSet<Guid>();
        var items = values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new AvaloniaSelectionItem<Guid>(
                item.Name,
                item.Id,
                selected: selectedIds.Contains(item.Id)))
            .ToList();
        var result = runtimeHost.Dialogs.SelectMultiple(caption, string.Empty, items);
        if (result.Confirmed)
        {
            UpdateGame(game, () => setter(result.SelectedItems.Count == 0 ? null : result.SelectedItems.ToList()));
        }
    }

    private void SelectUserScore(Game game)
    {
        var items = new List<AvaloniaSelectionItem<int?>>
        {
            new("None", null, selected: !game.UserScore.HasValue)
        };
        items.AddRange(Enumerable.Range(0, 101).Reverse().Select(score =>
            new AvaloniaSelectionItem<int?>(score.ToString(), score, selected: game.UserScore == score)));
        var result = runtimeHost.Dialogs.SelectSingle("User score", string.Empty, items);
        if (result.Confirmed)
        {
            UpdateGame(game, () => game.UserScore = result.SelectedItem);
        }
    }

    private void UpdateGame(Game game, Action update)
    {
        update();
        database.Games.Update(game);
        RefreshGame(game.Id);
    }

    private void RunCustomAction(Game game, GameAction action)
    {
        StatusText = runtimeHost.ActivateGameAction(game, action);
    }

    private void RemoveGame(Game game)
    {
        if (runtimeHost.Dialogs.ShowMessage(
            $"Remove {game.Name} from the library?",
            "Remove game",
            new[] { "Yes", "No" },
            1,
            1) != "Yes")
        {
            return;
        }

        database.Games.Remove(game);
        var item = allGames.FirstOrDefault(candidate => candidate.Game.Id == game.Id);
        if (item != null)
        {
            allGames.Remove(item);
        }

        ApplyFilters();
    }

    private void OpenToolsMenu()
    {
        var items = database?.SoftwareApps
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(app => new FullscreenMenuItemViewModel(app.Name, () => runtimeHost.StartSoftwareTool(app)))
            .ToList() ?? new List<FullscreenMenuItemViewModel>();
        if (items.Count == 0)
        {
            items.Add(new FullscreenMenuItemViewModel("No software tools are configured"));
        }

        OpenCommandMenu("Tools", items);
    }

    private void OpenExtensionsMenu() => OpenCommandMenu(
        "Extensions",
        BuildPluginItems(runtimeHost?.GetMainMenuActions() ?? Array.Empty<PluginMenuAction>()));

    private void OpenClientsMenu()
    {
        var items = new List<FullscreenMenuItemViewModel>();
        foreach (var plugin in runtimeHost?.LibraryPlugins ?? Array.Empty<LibraryPlugin>())
        {
            try
            {
                var client = plugin.Client;
                if (client?.IsInstalled == true)
                {
                    items.Add(new FullscreenMenuItemViewModel(plugin.Name, client.Open));
                }
            }
            catch (Exception exception)
            {
                StatusText = $"Client discovery for {plugin.Name} failed: {exception.Message}";
            }
        }

        if (items.Count == 0)
        {
            items.Add(new FullscreenMenuItemViewModel("No installed library clients are available"));
        }

        OpenCommandMenu("Library clients", items);
    }

    private void UpdateLibrary()
    {
        var plugins = runtimeHost?.LibraryPlugins ?? Array.Empty<LibraryPlugin>();
        var failures = new List<string>();
        var result = runtimeHost.Dialogs.ActivateGlobalProgress(args =>
        {
            args.ProgressMaxValue = plugins.Count;
            args.IsIndeterminate = false;
            foreach (var plugin in plugins)
            {
                args.CancelToken.ThrowIfCancellationRequested();
                args.Text = $"Updating {plugin.Name}…";
                try
                {
                    database.ImportGames(plugin, args.CancelToken, PlaytimeImportMode.Always);
                }
                catch (Exception exception)
                {
                    failures.Add($"{plugin.Name}: {exception.Message}");
                }

                args.CurrentProgressValue++;
            }
        }, new GlobalProgressOptions("Updating library…", true) { IsIndeterminate = false });

        if (result.Error != null)
        {
            StatusText = $"Library update failed: {result.Error.Message}";
            return;
        }

        SynchronizeGamesFromDatabase();
        runtimeHost.NotifyLibraryUpdated();
        StatusText = result.Canceled
            ? "Library update canceled."
            : failures.Count == 0
                ? "Library update completed."
                : $"Library update completed with {failures.Count} error(s): {string.Join("; ", failures)}";
    }

    private void SynchronizeGamesFromDatabase()
    {
        var databaseIds = database.Games.Select(game => game.Id).ToHashSet();
        allGames.RemoveAll(item => !databaseIds.Contains(item.Game.Id));
        var knownIds = allGames.Select(item => item.Game.Id).ToHashSet();
        foreach (var game in database.Games.Where(game => !knownIds.Contains(game.Id)))
        {
            var item = new GameItemViewModel(game, database);
            item.ApplyVisualSettings(settings.ShowGameTitles, settings.DarkenUninstalledGamesGrid);
            allGames.Add(item);
        }

        ApplyFilters();
    }

    private void SelectRandomGame()
    {
        if (Games.Count == 0)
        {
            return;
        }

        SelectedGame = Games[Random.Shared.Next(Games.Count)];
        CloseOverlays();
        LibraryFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HandleGameStateChanged(object sender, Game game)
    {
        var item = allGames.FirstOrDefault(candidate => candidate.Game.Id == game.Id);
        item?.Refresh();
        if (game.IsLaunching)
        {
            GameStatusGame = item;
            GameStatusText = $"Starting {game.Name}…";
            IsGameStatusVisible = true;
        }
        else if (game.IsRunning)
        {
            GameStatusGame = item;
            GameStatusText = $"{game.Name} is running";
            IsGameStatusVisible = true;
        }
        else if (GameStatusGame?.Game.Id == game.Id)
        {
            IsGameStatusVisible = false;
            GameStatusGame = null;
            GameStatusText = string.Empty;
            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void SetSyntheticGameStateForTest(Game game) => HandleGameStateChanged(this, game);

    private IEnumerable<GameItemViewModel> SortGames(IEnumerable<GameItemViewModel> source)
    {
        IOrderedEnumerable<GameItemViewModel> ordered = Filters.SelectedSortOrder switch
        {
            SortOrder.Platforms => source.OrderBy(item => Names(database.Platforms, item.Game.PlatformIds)),
            SortOrder.Library => source.OrderBy(item => item.Game.PluginId),
            SortOrder.Categories => source.OrderBy(item => Names(database.Categories, item.Game.CategoryIds)),
            SortOrder.LastActivity => source.OrderBy(item => item.Game.LastActivity ?? DateTime.MinValue),
            SortOrder.Genres => source.OrderBy(item => Names(database.Genres, item.Game.GenreIds)),
            SortOrder.ReleaseDate => source.OrderBy(item => item.Game.ReleaseDate?.Date ?? DateTime.MinValue),
            SortOrder.Developers => source.OrderBy(item => Names(database.Companies, item.Game.DeveloperIds)),
            SortOrder.Publishers => source.OrderBy(item => Names(database.Companies, item.Game.PublisherIds)),
            SortOrder.Tags => source.OrderBy(item => Names(database.Tags, item.Game.TagIds)),
            SortOrder.Series => source.OrderBy(item => Names(database.Series, item.Game.SeriesIds)),
            SortOrder.AgeRatings => source.OrderBy(item => Names(database.AgeRatings, item.Game.AgeRatingIds)),
            SortOrder.Version => source.OrderBy(item => item.Game.Version, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Regions => source.OrderBy(item => Names(database.Regions, item.Game.RegionIds)),
            SortOrder.Source => source.OrderBy(item => database.Sources[item.Game.SourceId]?.Name, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.PlayCount => source.OrderBy(item => item.Game.PlayCount),
            SortOrder.Playtime => source.OrderBy(item => item.Game.Playtime),
            SortOrder.CompletionStatus => source.OrderBy(item => database.CompletionStatuses[item.Game.CompletionStatusId]?.Name,
                StringComparer.CurrentCultureIgnoreCase),
            SortOrder.UserScore => source.OrderBy(item => item.Game.UserScore ?? -1),
            SortOrder.CriticScore => source.OrderBy(item => item.Game.CriticScore ?? -1),
            SortOrder.CommunityScore => source.OrderBy(item => item.Game.CommunityScore ?? -1),
            SortOrder.Added => source.OrderBy(item => item.Game.Added ?? DateTime.MinValue),
            SortOrder.Modified => source.OrderBy(item => item.Game.Modified ?? DateTime.MinValue),
            SortOrder.IsInstalled => source.OrderBy(item => item.Game.IsInstalled),
            SortOrder.Hidden => source.OrderBy(item => item.Game.Hidden),
            SortOrder.Favorite => source.OrderBy(item => item.Game.Favorite),
            SortOrder.InstallDirectory => source.OrderBy(item => item.Game.InstallDirectory, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.Features => source.OrderBy(item => Names(database.Features, item.Game.FeatureIds)),
            SortOrder.InstallSize => source.OrderBy(item => item.Game.InstallSize ?? 0),
            SortOrder.RecentActivity => source.OrderBy(item => item.Game.RecentActivity ?? DateTime.MinValue),
            SortOrder.RomList => source.OrderBy(item => item.Game.Roms?.FirstOrDefault()?.Path, StringComparer.CurrentCultureIgnoreCase),
            _ => source.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        return Filters.SelectedSortDirection == SortOrderDirection.Descending
            ? ordered.Reverse()
            : ordered.ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private static string Names<T>(IEnumerable<T> collection, IEnumerable<Guid> ids) where T : DatabaseObject
    {
        if (ids == null)
        {
            return string.Empty;
        }

        var selected = ids.ToHashSet();
        return string.Join(", ", collection.Where(item => selected.Contains(item.Id))
            .Select(item => item.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
    }

    internal void OpenToolsForParity() => OpenToolsMenu();
    internal void OpenExtensionsForParity() => OpenExtensionsMenu();
    internal void OpenClientsForParity() => OpenClientsMenu();
}
