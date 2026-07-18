using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia;

internal static class DesktopPilotSelfTest
{
    public static async Task Run(
        MainWindow window,
        DesktopAppViewModel viewModel,
        DesktopLibrary library)
    {
        var results = new List<(string Name, bool Pass, string Detail)>();
        await Task.Delay(500);

        Record(results, "Playnite.Core library opens", () =>
            library.IsOpen && library.Database.GetType().Assembly.GetName().Name == "Playnite.Core"
                ? $"{library.Games.Count:N0} games loaded from Playnite.Core"
                : throw new InvalidOperationException("The concrete Core database is not open."));

        Record(results, "Loose Desktop theme applies", () =>
            window.MainView.TemplateAppliedCount > 0 && window.MainView.GameList != null
                ? "DesktopMainView resolved its runtime template contract"
                : throw new InvalidOperationException("The Desktop theme template was not applied."));

        Record(results, "Grid and list views share the Desktop model", () =>
            window.MainView.GridGameList != null && window.MainView.ListGameList != null
                ? "both virtualized view surfaces resolved from the loose theme"
                : throw new InvalidOperationException("One of the Desktop library views was not resolved."));

        Record(results, "Desktop tile grid stays virtualized", () =>
        {
            var realized = window.MainView.TilePanel?.RealizedCount ?? 0;
            return realized > 0 && realized <= 200
                ? $"{realized} of {library.Games.Count:N0} containers realized"
                : throw new InvalidOperationException($"Realized container count was {realized}.");
        });

        viewModel.SelectedViewMode = "List";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Desktop view switching is live", () =>
            viewModel.IsListView && window.MainView.GameList == window.MainView.ListGameList
                ? "the persisted view selector switched to the list surface"
                : throw new InvalidOperationException("The list surface did not become active."));
        viewModel.SelectedViewMode = "Grid";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        var before = viewModel.Games.Count;
        viewModel.SearchText = "Desktop Pilot 99";
        Record(results, "Desktop search filters live data", () =>
            viewModel.Games.Count > 0 && viewModel.Games.Count < before
                ? $"search reduced {before:N0} games to {viewModel.Games.Count:N0}"
                : throw new InvalidOperationException($"Search returned {viewModel.Games.Count:N0} games."));
        viewModel.SearchText = string.Empty;

        viewModel.InstalledOnly = true;
        Record(results, "Desktop installed filter uses Core state", () =>
            viewModel.Games.Count > 0 && viewModel.Games.All(game => game.IsInstalled)
                ? $"{viewModel.Games.Count:N0} installed games remain"
                : throw new InvalidOperationException("An uninstalled game passed the filter."));
        viewModel.InstalledOnly = false;

        var favoritesPreset = viewModel.FilterPresets.FirstOrDefault(preset => preset.Name == "Favorites");
        viewModel.SelectedFilterPreset = favoritesPreset;
        Record(results, "Core filter presets drive the Desktop library", () =>
            favoritesPreset != null && viewModel.Games.Count > 0 && viewModel.Games.All(game => game.Favorite)
                ? $"the Favorites preset selected {viewModel.Games.Count:N0} games"
                : throw new InvalidOperationException("The Core Favorites preset was not applied."));
        viewModel.SelectedFilterPreset = viewModel.FilterPresets.FirstOrDefault(preset => preset.Name == "All");

        viewModel.SelectedGrouping = Playnite.SDK.Models.GroupableField.None;
        viewModel.SelectedSortOrder = Playnite.SDK.Models.SortOrder.Playtime;
        viewModel.SelectedSortDirection = Playnite.SDK.Models.SortOrderDirection.Descending;
        Record(results, "Desktop sorting uses library metadata", () =>
            viewModel.Games.Count > 1 &&
            viewModel.Games.First().Game.Playtime >= viewModel.Games.Last().Game.Playtime
                ? $"playtime descending spans {viewModel.Games.First().PlaytimeText} to {viewModel.Games.Last().PlaytimeText}"
                : throw new InvalidOperationException("Playtime descending order was not applied."));

        viewModel.SelectedGrouping = Playnite.SDK.Models.GroupableField.InstallationStatus;
        Record(results, "Desktop grouping emits virtualized section headers", () =>
        {
            var headers = viewModel.Games
                .Where(game => game.ShowGroupHeader)
                .Select(game => game.GroupHeader)
                .ToList();
            return headers.Count >= 2 && headers.All(header => !string.IsNullOrWhiteSpace(header))
                ? $"{string.Join(", ", headers)} groups share one flat virtualized collection"
                : throw new InvalidOperationException($"Only {headers.Count} group headers were produced.");
        });
        viewModel.SelectedGrouping = Playnite.SDK.Models.GroupableField.None;
        viewModel.SelectedSortOrder = Playnite.SDK.Models.SortOrder.Name;
        viewModel.SelectedSortDirection = Playnite.SDK.Models.SortOrderDirection.Ascending;

        window.MainView.FocusSelectedGame();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Selection drives Desktop details", () =>
            viewModel.SelectedGame != null && window.MainView.GameList.SelectedItem == viewModel.SelectedGame
                ? $"details bound to {viewModel.SelectedGame.Name}"
                : throw new InvalidOperationException("The selected tile and details model diverged."));

        Record(results, "Shared plugin and game-operation host initializes", () =>
            window.RuntimeHost?.Actions != null &&
            window.RuntimeHost.Extensions != null &&
            window.RuntimeHost.LoadedPluginCount == 0 &&
            viewModel.PluginSummary.Contains("self-test", StringComparison.OrdinalIgnoreCase)
                ? $"{viewModel.PluginSummary}; Core action orchestration is attached"
                : throw new InvalidOperationException("The shared Avalonia runtime host is unavailable."));

        viewModel.SelectedGame = viewModel.Games.First(game => game.IsInstalled);
        viewModel.ActivateCommand.Execute(null);
        Record(results, "Desktop actions fail honestly without a provider", () =>
            viewModel.StatusText.Contains("No play action is available", StringComparison.OrdinalIgnoreCase)
                ? viewModel.StatusText
                : throw new InvalidOperationException($"Unexpected operation status: {viewModel.StatusText}"));

        window.RuntimeHost.Notifications.RemoveAll();
        window.RuntimeHost.ShowMessage("Desktop pilot notification", false);
        var notification = viewModel.Notifications.SingleOrDefault();
        Record(results, "Notifications reach the Desktop surface", () =>
            notification != null && viewModel.NotificationCount == 1 &&
            viewModel.StatusText == "Desktop pilot notification"
                ? "the shared host updated both the observable overlay and status bar"
                : throw new InvalidOperationException("The notification did not reach the Desktop view model."));
        notification?.CloseCommand.Execute(null);
        Record(results, "Desktop notifications close through the host", () =>
            viewModel.NotificationCount == 0
                ? "the SDK notification close command removed the host message"
                : throw new InvalidOperationException("The notification remained in the Desktop collection."));

        Dispatcher.UIThread.Post(
            () => viewModel.ConfirmDialogCommand.Execute(null),
            DispatcherPriority.Background);
        var dialogResult = window.RuntimeHost.Dialogs.ShowMessage(
            "Synchronous Desktop dialog check",
            "Pilot dialog",
            new[] { "Yes", "No" });
        Record(results, "Native Avalonia dialogs complete synchronously", () =>
            dialogResult == "Yes" && !viewModel.IsDialogVisible
                ? "the nested Avalonia dispatcher returned the selected option"
                : throw new InvalidOperationException($"Dialog returned '{dialogResult}'."));

        var editorGame = viewModel.Games.First(game => !game.Game.Hidden);
        viewModel.SelectedGame = editorGame;
        var originalEditorName = editorGame.Name;
        viewModel.EditCommand.Execute(null);
        Record(results, "Desktop metadata editor opens real Core records", () =>
            viewModel.Editor.IsVisible && viewModel.Editor.Name == originalEditorName
                ? $"the editor loaded {originalEditorName} without mutating the database"
                : throw new InvalidOperationException("The selected game was not loaded into the editor."));

        Record(results, "Multi-value Core metadata reaches the editor", () =>
            viewModel.Editor.Genres.Count == 2 &&
            viewModel.Editor.Platforms.Any(option => option.Name == "Windows") &&
            viewModel.Editor.Platforms.Any(option => option.Name == "Linux") &&
            viewModel.Editor.Categories.Count == 2 &&
            viewModel.Editor.Tags.Count == 2 &&
            viewModel.Editor.Developers.Count == 2 &&
            viewModel.Editor.Publishers.Count == 2 &&
            viewModel.Editor.Platforms.Count(option => option.IsSelected) == 1
                ? "genres, platforms, categories, tags, developers, and publishers loaded from Core"
                : throw new InvalidOperationException(
                    $"Counts were G{viewModel.Editor.Genres.Count}/P{viewModel.Editor.Platforms.Count}/" +
                    $"C{viewModel.Editor.Categories.Count}/T{viewModel.Editor.Tags.Count}/" +
                    $"D{viewModel.Editor.Developers.Count}/P{viewModel.Editor.Publishers.Count}; " +
                    $"selected platforms {viewModel.Editor.Platforms.Count(option => option.IsSelected)}."));

        viewModel.Editor.Name = string.Empty;
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Desktop metadata validation blocks invalid saves", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.HasValidationError &&
            library.Database.Games[editorGame.Game.Id].Name == originalEditorName
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("Invalid metadata reached the Core database."));

        viewModel.Editor.Name = originalEditorName;
        viewModel.Editor.AddLinkCommand.Execute(null);
        var invalidLink = viewModel.Editor.Links.Last();
        invalidLink.Name = "Local file";
        invalidLink.Url = "file:///unsafe-link";
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Desktop link validation rejects unsafe URLs", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.HasValidationError &&
            viewModel.Editor.ValidationMessage.Contains("HTTP", StringComparison.OrdinalIgnoreCase) &&
            library.Database.Games[editorGame.Game.Id].Links?.Count is null or 0
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("A non-web link reached the Core database."));
        viewModel.Editor.Links.Clear();

        var editedName = originalEditorName + " — Edited";
        const string singleBackgroundUrl = "https://example.invalid/desktop-background.jpg";
        const string singleLinkUrl = "https://example.com/desktop-pilot";
        viewModel.Editor.Name = editedName;
        viewModel.Editor.SortingName = "Edited Pilot";
        viewModel.Editor.ReleaseDate = "2024-7-18";
        viewModel.Editor.UserScore = "88";
        viewModel.Editor.Description = "Metadata saved by the Avalonia Desktop editor.";
        viewModel.Editor.Notes = "Phase 5 editor contract";
        viewModel.Editor.Favorite = true;
        viewModel.Editor.Hidden = false;
        viewModel.Editor.SelectedCompletionStatus = viewModel.Editor.CompletionStatuses.Skip(1).First();
        SelectOnly(viewModel.Editor.Genres, "Action");
        SelectOnly(viewModel.Editor.Platforms, "Linux");
        SelectOnly(viewModel.Editor.Categories, "Showcase");
        SelectOnly(viewModel.Editor.Tags, "Co-op");
        SelectOnly(viewModel.Editor.Developers, "Pilot Studio");
        SelectOnly(viewModel.Editor.Publishers, "Sample Publishing");
        viewModel.Editor.CoverImage = library.SelfTestMediaPath;
        viewModel.Editor.BackgroundImage = singleBackgroundUrl;
        viewModel.Editor.Icon = library.SelfTestMediaPath;
        viewModel.Editor.AddLinkCommand.Execute(null);
        var websiteLink = viewModel.Editor.Links.Last();
        websiteLink.Name = "Website";
        websiteLink.Url = singleLinkUrl;
        viewModel.Editor.SaveCommand.Execute(null);
        var savedEditorGame = library.Database.Games[editorGame.Game.Id];
        Record(results, "Desktop metadata saves through GameDatabase", () =>
            !viewModel.Editor.IsVisible &&
            savedEditorGame.Name == editedName &&
            savedEditorGame.SortingName == "Edited Pilot" &&
            savedEditorGame.ReleaseDate?.Serialize() == "2024-7-18" &&
            savedEditorGame.UserScore == 88 &&
            savedEditorGame.Favorite &&
            savedEditorGame.Modified.HasValue &&
            savedEditorGame.GenreIds.SequenceEqual(
                viewModel.Editor.Genres.Where(option => option.Name == "Action").Select(option => option.Id)) &&
            savedEditorGame.PlatformIds.SequenceEqual(
                viewModel.Editor.Platforms.Where(option => option.Name == "Linux").Select(option => option.Id)) &&
            savedEditorGame.CategoryIds.SequenceEqual(
                viewModel.Editor.Categories.Where(option => option.Name == "Showcase").Select(option => option.Id)) &&
            savedEditorGame.TagIds.SequenceEqual(
                viewModel.Editor.Tags.Where(option => option.Name == "Co-op").Select(option => option.Id)) &&
            savedEditorGame.DeveloperIds.SequenceEqual(
                viewModel.Editor.Developers.Where(option => option.Name == "Pilot Studio").Select(option => option.Id)) &&
            savedEditorGame.PublisherIds.SequenceEqual(
                viewModel.Editor.Publishers.Where(option => option.Name == "Sample Publishing").Select(option => option.Id)) &&
            editorGame.Name == editedName &&
            editorGame.GenresText.Contains("Action", StringComparison.Ordinal) &&
            editorGame.PlatformsText.Contains("Linux", StringComparison.Ordinal)
                ? $"{editedName} persisted scalar and multi-value metadata"
                : throw new InvalidOperationException("The edited metadata did not round-trip through Core."));

        var importedCoverPath = savedEditorGame.CoverImage;
        var importedIconPath = savedEditorGame.Icon;
        Record(results, "Desktop media and links round-trip through Core", () =>
            !string.IsNullOrWhiteSpace(importedCoverPath) &&
            !string.IsNullOrWhiteSpace(importedIconPath) &&
            File.Exists(library.Database.GetFullFilePath(importedCoverPath)) &&
            File.Exists(library.Database.GetFullFilePath(importedIconPath)) &&
            savedEditorGame.BackgroundImage == singleBackgroundUrl &&
            savedEditorGame.Links.Count == 1 &&
            savedEditorGame.Links[0].Name == "Website" &&
            savedEditorGame.Links[0].Url == singleLinkUrl &&
            File.Exists(editorGame.CoverPath) &&
            editorGame.LinksText.Contains("Website", StringComparison.Ordinal)
                ? "local cover/icon files, a remote background, and a web link persisted and refreshed"
                : throw new InvalidOperationException("Media or links did not round-trip through the Desktop editor."));

        viewModel.SelectedGame = editorGame;
        viewModel.EditCommand.Execute(null);
        viewModel.Editor.CoverImage = string.Empty;
        viewModel.Editor.SaveCommand.Execute(null);
        var clearedMediaGame = library.Database.Games[editorGame.Game.Id];
        Record(results, "Replacing Desktop media removes the old database file", () =>
            !viewModel.Editor.IsVisible &&
            string.IsNullOrWhiteSpace(clearedMediaGame.CoverImage) &&
            !File.Exists(library.Database.GetFullFilePath(importedCoverPath)) &&
            File.Exists(library.Database.GetFullFilePath(importedIconPath))
                ? "Core removed the replaced cover while preserving unchanged icon media"
                : throw new InvalidOperationException("The replaced cover file was retained or unrelated media was removed."));

        var pluginEditGame = viewModel.Games.First(game => game.Game.Id != editorGame.Game.Id);
        var pluginEditedName = pluginEditGame.Name + " — Plugin edit";
        Dispatcher.UIThread.Post(() =>
        {
            viewModel.Editor.Name = pluginEditedName;
            viewModel.Editor.UserScore = "91";
            viewModel.Editor.SaveCommand.Execute(null);
        }, DispatcherPriority.Background);
        var pluginEditResult = window.RuntimeHost.PluginApi.MainView.OpenEditDialog(pluginEditGame.Game.Id);
        Record(results, "Plugin OpenEditDialog uses the Avalonia editor", () =>
            pluginEditResult == true &&
            library.Database.Games[pluginEditGame.Game.Id].Name == pluginEditedName &&
            library.Database.Games[pluginEditGame.Game.Id].UserScore == 91
                ? "the synchronous SDK call returned true after Core persistence"
                : throw new InvalidOperationException("The plugin edit-dialog bridge did not persist its result."));

        var bulkGameIds = new List<Guid> { editorGame.Game.Id, pluginEditGame.Game.Id };
        viewModel.OpenGameEditor(bulkGameIds);
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Bulk editor requires explicit field selection", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.IsBulkEdit &&
            viewModel.Editor.HasValidationError &&
            viewModel.Editor.ValidationMessage.Contains("at least one", StringComparison.OrdinalIgnoreCase)
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("A bulk save without selected fields was accepted."));
        viewModel.Editor.CancelCommand.Execute(null);

        var originalBulkNames = bulkGameIds.ToDictionary(id => id, id => library.Database.Games[id].Name);
        var originalBulkIcons = bulkGameIds.ToDictionary(id => id, id => library.Database.Games[id].Icon);
        const string bulkBackgroundUrl = "https://example.invalid/bulk-background.jpg";
        const string bulkLinkUrl = "https://example.com/shared-pilot-link";
        Dispatcher.UIThread.Post(() =>
        {
            viewModel.Editor.ApplyUserScore = true;
            viewModel.Editor.UserScore = "77";
            viewModel.Editor.ApplyFavorite = true;
            viewModel.Editor.Favorite = true;
            viewModel.Editor.ApplyCompletionStatus = true;
            viewModel.Editor.SelectedCompletionStatus = viewModel.Editor.CompletionStatuses.Skip(1).Last();
            viewModel.Editor.ApplyGenres = true;
            SelectOnly(viewModel.Editor.Genres, "Strategy");
            viewModel.Editor.ApplyPlatforms = true;
            SelectOnly(viewModel.Editor.Platforms, "Windows");
            viewModel.Editor.ApplyTags = true;
            SelectOnly(viewModel.Editor.Tags, "Controller support", "Co-op");
            viewModel.Editor.ApplyCoverImage = true;
            viewModel.Editor.CoverImage = library.SelfTestMediaPath;
            viewModel.Editor.ApplyBackgroundImage = true;
            viewModel.Editor.BackgroundImage = bulkBackgroundUrl;
            viewModel.Editor.ApplyLinks = true;
            viewModel.Editor.Links.Clear();
            viewModel.Editor.AddLinkCommand.Execute(null);
            var sharedLink = viewModel.Editor.Links.Last();
            sharedLink.Name = "Shared website";
            sharedLink.Url = bulkLinkUrl;
            viewModel.Editor.SaveCommand.Execute(null);
        }, DispatcherPriority.Background);
        var bulkEditResult = window.RuntimeHost.PluginApi.MainView.OpenEditDialog(bulkGameIds);
        Record(results, "Plugin bulk editing applies selected fields only", () =>
            bulkEditResult == true && bulkGameIds.All(id =>
            {
                var game = library.Database.Games[id];
                return game.Name == originalBulkNames[id] &&
                    game.Icon == originalBulkIcons[id] &&
                    game.UserScore == 77 &&
                    game.Favorite &&
                    game.CompletionStatusId == viewModel.Editor.CompletionStatuses.Skip(1).Last().Id &&
                    game.GenreIds.SequenceEqual(
                        viewModel.Editor.Genres.Where(option => option.Name == "Strategy").Select(option => option.Id)) &&
                    game.PlatformIds.SequenceEqual(
                        viewModel.Editor.Platforms.Where(option => option.Name == "Windows").Select(option => option.Id)) &&
                    game.TagIds.Count == 2;
            })
                ? "the SDK list overload buffered scalar and multi-value Core updates without overwriting names"
                : throw new InvalidOperationException("The bulk editor did not preserve or apply the selected fields."));

        Record(results, "Plugin bulk editing imports media and replaces links", () =>
        {
            var editedGames = bulkGameIds.Select(id => library.Database.Games[id]).ToList();
            var coverPaths = editedGames.Select(game => game.CoverImage).ToList();
            return editedGames.All(game =>
                    !string.IsNullOrWhiteSpace(game.CoverImage) &&
                    File.Exists(library.Database.GetFullFilePath(game.CoverImage)) &&
                    game.BackgroundImage == bulkBackgroundUrl &&
                    game.Links.Count == 1 &&
                    game.Links[0].Name == "Shared website" &&
                    game.Links[0].Url == bulkLinkUrl) &&
                coverPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == editedGames.Count &&
                !ReferenceEquals(editedGames[0].Links, editedGames[1].Links)
                    ? "each game received an owned Core media file and an independent copied link collection"
                    : throw new InvalidOperationException("Bulk media or link metadata was not safely applied.");
        });

        Record(results, "Desktop settings persist atomically", () =>
        {
            var store = new DesktopSettingsStore(library.ActiveUserDataDirectory);
            store.Save(new DesktopSettings
            {
                ViewMode = "List",
                SortOrder = Playnite.SDK.Models.SortOrder.Playtime,
                SortDirection = Playnite.SDK.Models.SortOrderDirection.Descending,
                Grouping = Playnite.SDK.Models.GroupableField.Platform,
                DisabledPlugins = new List<string> { "pilot-plugin" }
            });
            var loaded = store.Load();
            if (loaded.ViewMode != "List" ||
                loaded.SortOrder != Playnite.SDK.Models.SortOrder.Playtime ||
                loaded.SortDirection != Playnite.SDK.Models.SortOrderDirection.Descending ||
                loaded.Grouping != Playnite.SDK.Models.GroupableField.Platform ||
                loaded.DisabledPlugins.SingleOrDefault() != "pilot-plugin")
            {
                throw new InvalidOperationException("The persisted Desktop settings did not round-trip.");
            }

            return $"settings round-tripped at {store.SettingsPath}";
        });

        var report = BuildReport(results);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "desktop-pilot-results.txt"), report);
        Console.WriteLine(report);
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(
            results.Count(result => !result.Pass));
    }

    private static void Record(
        ICollection<(string Name, bool Pass, string Detail)> results,
        string name,
        Func<string> check)
    {
        try
        {
            results.Add((name, true, check()));
        }
        catch (Exception exception)
        {
            results.Add((name, false, exception.Message));
        }
    }

    private static void SelectOnly(
        IEnumerable<DesktopMetadataOption> options,
        params string[] selectedNames)
    {
        var selected = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            option.IsSelected = selected.Contains(option.Name);
        }
    }

    private static string BuildReport(IEnumerable<(string Name, bool Pass, string Detail)> results)
    {
        var materialized = results.ToList();
        var report = new StringBuilder();
        report.AppendLine("=== Playnite Phase 5 Avalonia Desktop pilot checks ===");
        report.AppendLine($"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version}; .NET {Environment.Version}");
        report.AppendLine();
        foreach (var result in materialized)
        {
            report.AppendLine($"[{(result.Pass ? "PASS" : "FAIL")}] {result.Name}");
            report.AppendLine($"       {result.Detail}");
        }

        report.AppendLine();
        report.AppendLine($"VERDICT: {materialized.Count(result => result.Pass)}/{materialized.Count} checks passed.");
        return report.ToString();
    }
}
