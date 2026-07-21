using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.Common;
using Playnite.Controllers;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed partial class DesktopAppViewModel
{
    private readonly List<DesktopGameItemViewModel> selectedGames = new();
    private bool synchronizingLibrarySelection;

    public IReadOnlyList<DesktopGameItemViewModel> SelectedGames => selectedGames;
    public int SelectedGameCount => selectedGames.Count;
    public bool HasMultipleSelectedGames => selectedGames.Count > 1;
    public string SelectedGamesSummary => selectedGames.Count switch
    {
        0 => "No games selected",
        1 => selectedGames[0].Name,
        _ => $"{selectedGames.Count:N0} games selected"
    };

    public ICommand RemoveSelectedGamesCommand { get; private set; }
    public ICommand SetSelectedFavoriteCommand { get; private set; }
    public ICommand SetSelectedHiddenCommand { get; private set; }
    public ICommand SetSelectedHdrCommand { get; private set; }
    public ICommand CreateSelectedShortcutsCommand { get; private set; }
    public ICommand CalculateSelectedInstallSizeCommand { get; private set; }

    private void InitializeLibrarySelection()
    {
        if (selectedGame != null)
        {
            selectedGames.Add(selectedGame);
        }
    }

    private void InitializeLibraryInteractionCommands()
    {
        RemoveSelectedGamesCommand = new AppRelayCommand(
            RemoveSelectedGamesWithConfirmation,
            CanModifySelectedGames);
        SetSelectedFavoriteCommand = new AppRelayCommand(
            parameter => SetFavoriteState(parameter is bool state && state),
            CanModifySelectedGames);
        SetSelectedHiddenCommand = new AppRelayCommand(
            parameter => SetHiddenState(parameter is bool state && state),
            CanModifySelectedGames);
        SetSelectedHdrCommand = new AppRelayCommand(
            parameter => SetHdrState(parameter is bool state && state),
            CanModifySelectedGames);
        CreateSelectedShortcutsCommand = new AppRelayCommand(
            CreateSelectedShortcuts,
            CanModifySelectedGames);
        CalculateSelectedInstallSizeCommand = new AppRelayCommand(
            () => CalculateSelectedInstallSizes(false),
            () => GetSelectedGames().Any(game => game.IsInstalled));
    }

    private bool CanModifySelectedGames() => database != null && selectedGames.Count > 0;

    private void RaiseLibraryInteractionCommandStates()
    {
        (RemoveSelectedGamesCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (SetSelectedFavoriteCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (SetSelectedHiddenCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (SetSelectedHdrCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (CreateSelectedShortcutsCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (CalculateSelectedInstallSizeCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void SynchronizeSelectedGamesWithPrimary(DesktopGameItemViewModel primary)
    {
        if (synchronizingLibrarySelection)
        {
            return;
        }

        selectedGames.Clear();
        if (primary != null)
        {
            selectedGames.Add(primary);
        }

        RaiseLibrarySelectionProperties();
    }

    internal void SetSelectedGames(
        IEnumerable<DesktopGameItemViewModel> selection,
        DesktopGameItemViewModel primary = null)
    {
        var materialized = (selection ?? Array.Empty<DesktopGameItemViewModel>())
            .Where(game => game != null && Games.Contains(game))
            .DistinctBy(game => game.Game.Id)
            .ToList();
        var resolvedPrimary = primary != null && materialized.Contains(primary)
            ? primary
            : materialized.FirstOrDefault();
        if (resolvedPrimary == null)
        {
            resolvedPrimary = primary != null && Games.Contains(primary)
                ? primary
                : selectedGame != null && Games.Contains(selectedGame)
                    ? selectedGame
                    : Games.FirstOrDefault();
            if (resolvedPrimary != null)
            {
                materialized.Add(resolvedPrimary);
            }
        }

        synchronizingLibrarySelection = true;
        try
        {
            selectedGames.Clear();
            selectedGames.AddRange(materialized);
            SelectedGame = resolvedPrimary;
        }
        finally
        {
            synchronizingLibrarySelection = false;
        }

        RaiseLibrarySelectionProperties();
        MetadataDownload?.RefreshTargetSummary();
        RaiseGameCommandStates();
    }

    private void RetainSelectedGames(IReadOnlyList<DesktopGameItemViewModel> availableGames)
    {
        var retained = selectedGames.Where(availableGames.Contains).ToList();
        var primary = selectedGame != null && availableGames.Contains(selectedGame)
            ? selectedGame
            : retained.FirstOrDefault() ?? availableGames.FirstOrDefault();
        SetSelectedGames(retained, primary);
    }

    private void RaiseLibrarySelectionProperties()
    {
        OnPropertyChanged(nameof(SelectedGames));
        OnPropertyChanged(nameof(SelectedGameCount));
        OnPropertyChanged(nameof(HasMultipleSelectedGames));
        OnPropertyChanged(nameof(SelectedGamesSummary));
    }

    private IReadOnlyList<Guid> GetSelectedGameIds() =>
        GetSelectedGames().Select(game => game.Id).ToList();

    public IReadOnlyList<Game> GetSelectedGames()
    {
        if (selectedGames.Count > 0)
        {
            return selectedGames.Select(game => game.Game).ToList();
        }

        return SelectedGame == null ? Array.Empty<Game>() : new[] { SelectedGame.Game };
    }

    public IReadOnlyList<DesktopGameContextMenuEntry> BuildGameContextMenu()
    {
        var games = GetSelectedGames();
        if (games.Count == 0)
        {
            return Array.Empty<DesktopGameContextMenuEntry>();
        }

        var entries = new List<DesktopGameContextMenuEntry>();
        if (games.Count == 1)
        {
            AddSingleGameStartEntries(entries, games[0]);
        }

        AddCommonGameEntries(entries, games);
        return entries;
    }

    private void AddSingleGameStartEntries(List<DesktopGameContextMenuEntry> entries, Game game)
    {
        entries.Add(DesktopGameContextMenuEntry.Command(
            game.IsInstalled ? Localize("LOCPlayGame", "Play") : Localize("LOCInstallGame", "Install"),
            () => RunOperation(game.IsInstalled ? GameOperationKind.Play : GameOperationKind.Install),
            isBold: true));

        var customActions = game.GameActions?
            .Where(action => action != null && !action.IsPlayAction)
            .ToList() ?? new List<GameAction>();
        foreach (var action in customActions)
        {
            var capturedAction = action;
            entries.Add(DesktopGameContextMenuEntry.Command(
                string.IsNullOrWhiteSpace(action.Name) ? Localize("LOCGameAction", "Game action") : action.Name,
                () => ActivateCustomAction(game, capturedAction)));
        }

        if (customActions.Count > 0)
        {
            entries.Add(DesktopGameContextMenuEntry.Separator());
        }

        var links = game.Links?
            .Where(link => link != null && !string.IsNullOrWhiteSpace(link.Url))
            .Select(link => DesktopGameContextMenuEntry.Command(
                string.IsNullOrWhiteSpace(link.Name) ? link.Url : link.Name,
                () => OpenGameLink(game, link)))
            .ToList();
        if (links?.Count > 0)
        {
            entries.Add(DesktopGameContextMenuEntry.Parent(Localize("LOCLinksLabel", "Links"), links));
            entries.Add(DesktopGameContextMenuEntry.Separator());
        }
    }

    private void AddCommonGameEntries(
        List<DesktopGameContextMenuEntry> entries,
        IReadOnlyList<Game> games)
    {
        var single = games.Count == 1 ? games[0] : null;
        if (single?.IsInstalled == true)
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCOpenGameLocation", "Open installation location"),
                () => OpenInstallDirectory(single)));
        }

        entries.Add(DesktopGameContextMenuEntry.Command(
            games.Count == 1
                ? Localize("LOCCreateDesktopShortcut", "Create desktop shortcut")
                : Localize("LOCCreateDesktopShortcuts", "Create desktop shortcuts"),
            CreateSelectedShortcuts));

        if (games.Any(game => game.IsInstalled))
        {
            var sizeItems = new List<DesktopGameContextMenuEntry>
            {
                DesktopGameContextMenuEntry.Command(
                    Localize("LOCCalculateGamesAllInstallSize", "Calculate all install sizes"),
                    () => CalculateSelectedInstallSizes(false)),
                DesktopGameContextMenuEntry.Command(
                    Localize("LOCCalculateGamesMissingInstallSize", "Calculate missing install sizes"),
                    () => CalculateSelectedInstallSizes(true))
            };
            entries.Add(DesktopGameContextMenuEntry.Parent(
                Localize("LOCInstallSizeMenuLabel", "Install size"),
                sizeItems));
        }

        if (!string.IsNullOrWhiteSpace(single?.Manual))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCOpenGameManual", "Open manual"),
                () => OpenManual(single)));
        }

        entries.Add(DesktopGameContextMenuEntry.Separator());
        if (!games.All(game => game.Favorite))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCFavoriteGame", "Add to favorites"),
                () => SetFavoriteState(true)));
        }
        if (games.Any(game => game.Favorite))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCRemoveFavoriteGame", "Remove from favorites"),
                () => SetFavoriteState(false)));
        }
        if (!games.All(game => game.Hidden))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCHideGame", "Hide"),
                () => SetHiddenState(true)));
        }
        if (games.Any(game => game.Hidden))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCUnHideGame", "Unhide"),
                () => SetHiddenState(false)));
        }
        if (!games.All(game => game.EnableSystemHdr))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCEnableSystemHdr", "Enable system HDR"),
                () => SetHdrState(true)));
        }
        if (games.Any(game => game.EnableSystemHdr))
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCDisableSystemHdr", "Disable system HDR"),
                () => SetHdrState(false)));
        }

        entries.Add(DesktopGameContextMenuEntry.Command(
            Localize("LOCEditGame", "Edit"),
            () => OpenGameEditor(games.Select(game => game.Id).ToList())));
        entries.Add(BuildCategoryMenu(games));
        entries.Add(BuildCompletionStatusMenu(games));
        AddPluginGameMenuEntries(entries, games);
        entries.Add(DesktopGameContextMenuEntry.Separator());
        entries.Add(DesktopGameContextMenuEntry.Command(
            Localize("LOCRemoveGame", "Remove from library"),
            RemoveSelectedGamesWithConfirmation));
        if (single != null && !single.IsCustomGame && single.IsInstalled)
        {
            entries.Add(DesktopGameContextMenuEntry.Command(
                Localize("LOCUninstallGame", "Uninstall"),
                () => RunOperation(GameOperationKind.Uninstall)));
        }
    }

    private DesktopGameContextMenuEntry BuildCategoryMenu(IReadOnlyList<Game> games)
    {
        var items = database.Categories
            .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(category =>
            {
                var isSetOnAll = games.All(game => game.CategoryIds?.Contains(category.Id) == true);
                return DesktopGameContextMenuEntry.Command(
                    category.Name,
                    () => SetCategoryState(category.Id, !isSetOnAll),
                    isChecked: isSetOnAll);
            })
            .ToList();
        if (games.Any(game => game.CategoryIds?.Count > 0))
        {
            items.Insert(0, DesktopGameContextMenuEntry.Command(
                Localize("LOCClearSelection", "Clear categories"),
                ClearSelectedCategories));
            items.Insert(1, DesktopGameContextMenuEntry.Separator());
        }

        return DesktopGameContextMenuEntry.Parent(
            Localize("LOCSetGameCategory", "Set category"),
            items);
    }

    private DesktopGameContextMenuEntry BuildCompletionStatusMenu(IReadOnlyList<Game> games)
    {
        var items = database.CompletionStatuses
            .OrderBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(status => DesktopGameContextMenuEntry.Command(
                status.Name,
                () => SetCompletionStatus(status.Id),
                isChecked: games.All(game => game.CompletionStatusId == status.Id)))
            .ToList();
        return DesktopGameContextMenuEntry.Parent(
            Localize("LOCSetCompletionStatus", "Set completion status"),
            items);
    }

    private void AddPluginGameMenuEntries(
        List<DesktopGameContextMenuEntry> entries,
        IReadOnlyList<Game> games)
    {
        var pluginActions = runtimeHost?.GetGameMenuActions(games) ?? Array.Empty<PluginMenuAction>();
        if (pluginActions.Count == 0)
        {
            return;
        }

        entries.Add(DesktopGameContextMenuEntry.Separator());
        var roots = new List<PluginMenuNode>();
        foreach (var action in pluginActions)
        {
            var sections = (action.MenuSection ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(section => section.TrimStart('@'))
                .Where(section => section.Length > 0)
                .ToList();
            var children = roots;
            foreach (var section in sections)
            {
                var parent = children.FirstOrDefault(node => node.Action == null && node.Header == section);
                if (parent == null)
                {
                    parent = new PluginMenuNode(section, null);
                    children.Add(parent);
                }
                children = parent.Children;
            }
            children.Add(new PluginMenuNode(action.Description, action));
        }

        entries.AddRange(roots.Select(ConvertPluginMenuNode));
    }

    private DesktopGameContextMenuEntry ConvertPluginMenuNode(PluginMenuNode node)
    {
        if (node.Action != null)
        {
            return DesktopGameContextMenuEntry.Command(node.Header, () => InvokePluginAction(node.Action));
        }

        return DesktopGameContextMenuEntry.Parent(
            node.Header,
            node.Children.Select(ConvertPluginMenuNode).ToList());
    }

    private void InvokePluginAction(PluginMenuAction action)
    {
        try
        {
            action.Invoke();
            RefreshGames(GetSelectedGameIds());
            StatusText = $"Ran {action.DisplayName} from {action.PluginName}.";
        }
        catch (Exception exception)
        {
            ReportLibraryActionFailure($"Plugin command {action.DisplayName} failed", exception);
        }
    }

    private void SetFavoriteState(bool state) => UpdateSelectedGames(
        game => game.Favorite = state,
        state ? "Added selected games to favorites." : "Removed selected games from favorites.");

    private void SetHiddenState(bool state) => UpdateSelectedGames(
        game => game.Hidden = state,
        state ? "Hid selected games." : "Unhid selected games.");

    private void SetHdrState(bool state) => UpdateSelectedGames(
        game => game.EnableSystemHdr = state,
        state ? "Enabled system HDR for selected games." : "Disabled system HDR for selected games.");

    private void SetCategoryState(Guid categoryId, bool state) => UpdateSelectedGames(game =>
    {
        var categories = game.CategoryIds?.ToList() ?? new List<Guid>();
        if (state && !categories.Contains(categoryId))
        {
            categories.Add(categoryId);
        }
        else if (!state)
        {
            categories.Remove(categoryId);
        }
        game.CategoryIds = categories;
    }, state ? "Assigned the selected category." : "Removed the selected category.");

    private void ClearSelectedCategories() => UpdateSelectedGames(
        game => game.CategoryIds = new List<Guid>(),
        "Cleared categories from selected games.");

    private void SetCompletionStatus(Guid statusId) => UpdateSelectedGames(
        game => game.CompletionStatusId = statusId,
        "Updated completion status for selected games.");

    private void UpdateSelectedGames(Action<Game> update, string status)
    {
        var games = GetSelectedGames();
        if (games.Count == 0)
        {
            return;
        }

        using (database.BufferedUpdate())
        {
            foreach (var game in games)
            {
                update(game);
                database.Games.Update(game);
            }
        }

        RefreshGames(games.Select(game => game.Id).ToList());
        StatusText = status;
    }

    private void RemoveSelectedGamesWithConfirmation()
    {
        var games = GetSelectedGames();
        if (games.Count == 0)
        {
            return;
        }

        if (games.Any(IsGameBusy))
        {
            dialogService?.ShowMessage(
                Localize("LOCGameRemoveRunningError", "A running or active game cannot be removed."),
                Localize("LOCGameError", "Game error"),
                new[] { Localize("LOCOKLabel", "OK") });
            return;
        }

        var label = games.Count == 1 ? games[0].Name : $"{games.Count:N0} selected games";
        var addToExclusions = false;
        if (games.Any(game => !game.IsCustomGame))
        {
            var exclude = Localize("LOCRemoveAskAddToExlusionListYesResponse", "Remove and exclude from future imports");
            var remove = Localize("LOCYesLabel", "Remove");
            var cancel = Localize("LOCNoLabel", "Cancel");
            var result = dialogService?.ShowMessage(
                $"Remove {label} from the library?",
                Localize("LOCGameRemoveAskTitle", "Remove from library"),
                new[] { exclude, remove, cancel },
                2,
                2);
            if (result == cancel || result == null)
            {
                return;
            }
            addToExclusions = result == exclude;
        }
        else
        {
            var remove = Localize("LOCYesLabel", "Remove");
            var cancel = Localize("LOCNoLabel", "Cancel");
            var result = dialogService?.ShowMessage(
                $"Remove {label} from the library?",
                Localize("LOCGameRemoveAskTitle", "Remove from library"),
                new[] { remove, cancel },
                1,
                1);
            if (result != remove)
            {
                return;
            }
        }

        RemoveGamesCore(games, addToExclusions);
    }

    internal int RemoveGamesWithoutConfirmation(IReadOnlyList<Game> games, bool addToExclusions) =>
        RemoveGamesCore(games, addToExclusions);

    private int RemoveGamesCore(IReadOnlyList<Game> games, bool addToExclusions)
    {
        var removable = (games ?? Array.Empty<Game>())
            .Where(game => game != null && !IsGameBusy(game) && database.Games[game.Id] != null)
            .DistinctBy(game => game.Id)
            .ToList();
        foreach (var game in removable)
        {
            if (addToExclusions && !game.IsCustomGame)
            {
                var libraryName = runtimeHost?.LibraryPlugins
                    .FirstOrDefault(plugin => plugin.Id == game.PluginId)?.Name;
                var exclusion = new ImportExclusionItem(game.GameId, game.Name, game.PluginId, libraryName);
                if (database.ImportExclusions[exclusion.Id] == null)
                {
                    database.ImportExclusions.Add(exclusion);
                }
            }
            database.Games.Remove(game);
        }

        SynchronizeLibrary();
        StatusText = removable.Count == 1
            ? $"Removed {removable[0].Name} from the library."
            : $"Removed {removable.Count:N0} games from the library.";
        return removable.Count;
    }

    private static bool IsGameBusy(Game game) =>
        game.IsInstalling || game.IsRunning || game.IsLaunching || game.IsUninstalling;

    private void ActivateCustomAction(Game game, GameAction action)
    {
        if (runtimeHost == null)
        {
            StatusText = "The game-action host is unavailable.";
            return;
        }

        try
        {
            StatusText = runtimeHost.ActivateGameAction(game, action);
        }
        catch (Exception exception)
        {
            ReportLibraryActionFailure($"Game action {action.Name} failed", exception);
        }
    }

    private void OpenGameLink(Game game, Link link)
    {
        try
        {
            ProcessStarter.StartUrl(game.ExpandVariables(link.Url));
        }
        catch (Exception exception)
        {
            ReportLibraryActionFailure($"Link {link.Name} could not be opened", exception);
        }
    }

    private void OpenManual(Game game)
    {
        try
        {
            var manualPath = game.ExpandVariables(game.Manual, true);
            var hasAbsoluteUri = Uri.TryCreate(manualPath, UriKind.Absolute, out var uri);
            if (!hasAbsoluteUri || uri.IsFile)
            {
                if (hasAbsoluteUri && uri.IsFile)
                {
                    manualPath = uri.LocalPath;
                }
                else if (!Path.IsPathRooted(manualPath))
                {
                    manualPath = Path.Combine(database.GetFileStoragePath(game.Id), manualPath);
                }
                ProcessStarter.StartUrl(new Uri(Path.GetFullPath(manualPath)).AbsoluteUri);
            }
            else
            {
                ProcessStarter.StartUrl(manualPath);
            }
        }
        catch (Exception exception)
        {
            ReportLibraryActionFailure($"The manual for {game.Name} could not be opened", exception);
        }
    }

    private void CreateSelectedShortcuts()
    {
        var failures = new List<string>();
        foreach (var game in GetSelectedGames())
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (string.IsNullOrWhiteSpace(desktop))
                {
                    throw new DirectoryNotFoundException("The desktop directory is unavailable.");
                }
                Directory.CreateDirectory(desktop);
                var shortcutPath = Path.Combine(
                    desktop,
                    Paths.GetSafePathName(game.Name) + (OperatingSystem.IsWindows() ? ".url" : ".desktop"));
                var icon = string.IsNullOrWhiteSpace(game.Icon) ? null : database.GetFullFilePath(game.Icon);
                if (string.IsNullOrWhiteSpace(icon) || !File.Exists(icon))
                {
                    icon = CoreRuntime.ApplicationExecutablePath();
                }
                Programs.CreateUrlShortcut($"playnite://playnite/start/{game.Id}", icon ?? string.Empty, shortcutPath);
            }
            catch (Exception exception)
            {
                failures.Add($"{game.Name}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
        {
            var exception = new IOException(string.Join(Environment.NewLine, failures));
            ReportLibraryActionFailure("One or more shortcuts could not be created", exception);
            return;
        }

        StatusText = GetSelectedGames().Count == 1
            ? "Created the desktop shortcut."
            : "Created desktop shortcuts for selected games.";
    }

    private void CalculateSelectedInstallSizes(bool onlyMissing)
    {
        var games = GetSelectedGames()
            .Where(game => game.IsInstalled && (!onlyMissing || !game.InstallSize.HasValue))
            .ToList();
        if (games.Count == 0 || dialogService == null)
        {
            StatusText = games.Count == 0
                ? "No selected games need an install-size scan."
                : "The progress dialog is unavailable.";
            return;
        }

        var failures = new List<string>();
        var updated = new List<Guid>();
        var calculator = new GameInstallSizeCalculator(database);
        var result = dialogService.ActivateGlobalProgress(args =>
        {
            args.ProgressMaxValue = games.Count;
            args.IsIndeterminate = false;
            for (var index = 0; index < games.Count; index++)
            {
                args.CancelToken.ThrowIfCancellationRequested();
                var game = games[index];
                args.Text = $"Calculating install size for {game.Name}…";
                try
                {
                    var size = calculator.Calculate(game, settings.InstallSizeScanUseSizeOnDisk);
                    if (size.HasValue)
                    {
                        game.InstallSize = size;
                        game.LastSizeScanDate = DateTime.Now;
                        database.Games.Update(game);
                        updated.Add(game.Id);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{game.Name}: {exception.Message}");
                }
                args.CurrentProgressValue = index + 1;
            }
        }, new GlobalProgressOptions("Calculating game install sizes", true)
        {
            IsIndeterminate = false
        });

        RefreshGames(updated);
        if (result.Error != null)
        {
            ReportLibraryActionFailure("Install-size calculation failed", result.Error);
            return;
        }
        if (failures.Count > 0)
        {
            ReportLibraryActionFailure(
                "Some install sizes could not be calculated",
                new IOException(string.Join(Environment.NewLine, failures)));
            return;
        }
        StatusText = result.Canceled
            ? $"Install-size scan canceled after updating {updated.Count:N0} games."
            : $"Updated install size for {updated.Count:N0} games.";
    }

    private void OpenInstallDirectory(Game game)
    {
        var path = game?.ExpandVariables(game.InstallDirectory, true);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusText = "The installation directory does not exist.";
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(settings.DirectoryOpenCommand) ||
                string.Equals(
                    settings.DirectoryOpenCommand,
                    DesktopSettings.DefaultDirectoryOpenCommand,
                    StringComparison.Ordinal))
            {
                Explorer.OpenDirectory(path);
            }
            else
            {
                ProcessStarter.ShellExecute(
                    settings.DirectoryOpenCommand.Replace("{Dir}", path, StringComparison.Ordinal));
            }
        }
        catch (Exception exception)
        {
            ReportLibraryActionFailure("The install directory could not be opened", exception);
        }
    }

    private void ReportLibraryActionFailure(string message, Exception exception)
    {
        var detail = $"{message}: {exception.Message}";
        runtimeHost?.ShowMessage(detail, true);
        StatusText = detail;
    }

    private static string Localize(string key, string fallback)
        => DesktopLocalization.Resolve(key, fallback);

    private sealed class PluginMenuNode
    {
        public string Header { get; }
        public PluginMenuAction Action { get; }
        public List<PluginMenuNode> Children { get; } = new();

        public PluginMenuNode(string header, PluginMenuAction action)
        {
            Header = header;
            Action = action;
        }
    }
}
