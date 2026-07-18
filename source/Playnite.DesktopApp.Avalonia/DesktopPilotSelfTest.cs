using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Threading;
using Playnite.Avalonia.Markup;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.WpfPluginSupport;
using InstalledProgram = Playnite.Common.Program;

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

        Record(results, "Avalonia 12 native window chrome contract applies", () =>
            window.WindowDecorations == WindowDecorations.BorderOnly &&
            window.ExtendClientAreaToDecorationsHint &&
            window.Chrome.TemplateAppliedCount > 0 &&
            WindowDecorationProperties.GetElementRole(window.Chrome.TitleBar) ==
                WindowDecorationsElementRole.TitleBar &&
            WindowDecorationProperties.GetElementRole(window.Chrome.MinimizeButton) ==
                WindowDecorationsElementRole.MinimizeButton &&
            WindowDecorationProperties.GetElementRole(window.Chrome.MaximizeButton) ==
                WindowDecorationsElementRole.MaximizeButton &&
            WindowDecorationProperties.GetElementRole(window.Chrome.CloseButton) ==
                WindowDecorationsElementRole.CloseButton
                ? "the loose theme supplied drag, minimize, maximize, and close non-client roles"
                : throw new InvalidOperationException("The custom chrome did not expose every native role."));

        window.TrayService.RefreshMenu();
        Record(results, "Native tray menu exposes quick launch and lifecycle actions", () =>
        {
            var items = window.TrayService.Menu.Items.OfType<NativeMenuItem>().ToList();
            var favoriteMenu = items.FirstOrDefault(item => item.Header == "Favorites")?.Menu;
            return window.TrayService.IsEnabled &&
                window.TrayService.QuickLaunchItemCount == 5 &&
                window.TrayService.FavoriteItemCount > 0 &&
                favoriteMenu?.Items.Count == window.TrayService.FavoriteItemCount &&
                items.Any(item => item.Header == "Open Playnite") &&
                items.Any(item => item.Header == "Open Fullscreen") &&
                items.Any(item => item.Header == "Exit Playnite")
                    ? $"{window.TrayService.QuickLaunchItemCount} recent and " +
                      $"{window.TrayService.FavoriteItemCount} favorite games are available"
                    : throw new InvalidOperationException("The tray menu lifecycle contract is incomplete.");
        });

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

        Record(results, "Legacy plugin resources bridge into Avalonia", () =>
            Playnite.SDK.ResourceProvider.GetString("LOCDesktopPlay") == "Play" &&
            Playnite.SDK.ResourceProvider.GetResource("FontIcoFont") is System.Windows.Media.FontFamily &&
            System.Windows.Application.Current?.TryFindResource("BaseTextBlockStyle") is System.Windows.Style
                ? "localized strings and WPF-compatible theme primitives are available during plugin construction"
                : throw new InvalidOperationException("The static legacy resource bridge is incomplete."));

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
            viewModel.Editor.Features.Count == 2 &&
            viewModel.Editor.Series.Count == 2 &&
            viewModel.Editor.AgeRatings.Count == 2 &&
            viewModel.Editor.Regions.Any(option => option.Name == "Worldwide") &&
            viewModel.Editor.Regions.Any(option => option.Name == "Europe") &&
            viewModel.Editor.Platforms.Count(option => option.IsSelected) == 1
                ? "all ten multi-value metadata collections loaded from Core"
                : throw new InvalidOperationException(
                    $"Counts were G{viewModel.Editor.Genres.Count}/P{viewModel.Editor.Platforms.Count}/" +
                    $"C{viewModel.Editor.Categories.Count}/T{viewModel.Editor.Tags.Count}/" +
                    $"D{viewModel.Editor.Developers.Count}/P{viewModel.Editor.Publishers.Count}/" +
                    $"F{viewModel.Editor.Features.Count}/S{viewModel.Editor.Series.Count}/" +
                    $"A{viewModel.Editor.AgeRatings.Count}/R{viewModel.Editor.Regions.Count}; " +
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

        viewModel.Editor.AddGameActionCommand.Execute(null);
        var trackedAction = viewModel.Editor.GameActions.Last();
        trackedAction.Name = "Tracked launch";
        trackedAction.Type = Playnite.SDK.Models.GameActionType.File;
        trackedAction.Path = library.SelfTestMediaPath;
        trackedAction.Arguments = "--pilot";
        trackedAction.WorkingDir = library.ActiveUserDataDirectory;
        trackedAction.IsPlayAction = true;
        trackedAction.TrackingMode = Playnite.SDK.Models.TrackingMode.Directory;
        trackedAction.TrackingPath = string.Empty;
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Desktop action validation requires directory tracking paths", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.HasValidationError &&
            viewModel.Editor.ValidationMessage.Contains("tracking directory", StringComparison.OrdinalIgnoreCase) &&
            library.Database.Games[editorGame.Game.Id].GameActions?.Count is null or 0
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("An invalid tracked action reached the Core database."));
        trackedAction.TrackingPath = library.ActiveUserDataDirectory;

        viewModel.Editor.AddGameActionCommand.Execute(null);
        var emulatorAction = viewModel.Editor.GameActions.Last();
        emulatorAction.Name = "Emulated launch";
        emulatorAction.Type = Playnite.SDK.Models.GameActionType.Emulator;
        emulatorAction.IsPlayAction = true;
        var pilotEmulator = emulatorAction.Emulators.Single(option => option.Name == "Pilot Emulator");
        emulatorAction.SelectedEmulator = pilotEmulator;
        var pilotProfile = emulatorAction.EmulatorProfiles.Single(option => option.Name == "Pilot Profile");
        emulatorAction.SelectedEmulatorProfile = pilotProfile;
        emulatorAction.OverrideDefaultArgs = true;
        emulatorAction.Arguments = "{ImagePath} --fullscreen";
        emulatorAction.AdditionalArguments = "--pilot-profile";
        emulatorAction.MoveUpCommand.Execute(null);

        viewModel.Editor.AddRomCommand.Execute(null);
        var firstRom = viewModel.Editor.Roms.Last();
        firstRom.Name = "Disc One";
        firstRom.Path = "{InstallDir}\\roms\\disc1.iso";
        viewModel.Editor.AddRomCommand.Execute(null);
        var secondRom = viewModel.Editor.Roms.Last();
        secondRom.Name = string.Empty;
        secondRom.Path = "{InstallDir}\\roms\\disc2.iso";
        secondRom.MoveUpCommand.Execute(null);
        viewModel.Editor.IncludeLibraryPluginAction = false;

        viewModel.Editor.InstallSize = "not-a-number";
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Desktop runtime validation rejects invalid unsigned values", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.HasValidationError &&
            viewModel.Editor.ValidationMessage.Contains("Install size", StringComparison.OrdinalIgnoreCase) &&
            library.Database.Games[editorGame.Game.Id].InstallSize == null
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("An invalid install size reached the Core database."));
        viewModel.Editor.InstallSize = string.Empty;
        viewModel.Editor.CriticScore = "101";
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Desktop score validation covers critic and community fields", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.HasValidationError &&
            viewModel.Editor.ValidationMessage.Contains("Critic score", StringComparison.OrdinalIgnoreCase) &&
            library.Database.Games[editorGame.Game.Id].CriticScore == null
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("An out-of-range critic score reached the Core database."));

        var editedName = originalEditorName + " — Edited";
        const string singleBackgroundUrl = "https://example.invalid/desktop-background.jpg";
        const string singleLinkUrl = "https://example.com/desktop-pilot";
        var singleInstallDirectory = Path.Combine(library.ActiveUserDataDirectory, "games", "desktop-pilot");
        var singleLastActivity = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Unspecified);
        var singleAdded = new DateTime(2023, 4, 5, 6, 7, 8, DateTimeKind.Unspecified);
        viewModel.Editor.Name = editedName;
        viewModel.Editor.SortingName = "Edited Pilot";
        viewModel.Editor.ReleaseDate = "2024-7-18";
        viewModel.Editor.UserScore = "88";
        viewModel.Editor.CriticScore = "74";
        viewModel.Editor.CommunityScore = "83";
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
        SelectOnly(viewModel.Editor.Features, "Achievements");
        SelectOnly(viewModel.Editor.Series, "Pilot Saga");
        SelectOnly(viewModel.Editor.AgeRatings, "Mature");
        SelectOnly(viewModel.Editor.Regions, "Europe");
        viewModel.Editor.CoverImage = library.SelfTestMediaPath;
        viewModel.Editor.BackgroundImage = singleBackgroundUrl;
        viewModel.Editor.Icon = library.SelfTestMediaPath;
        viewModel.Editor.InstallDirectory = singleInstallDirectory;
        viewModel.Editor.IsInstalled = false;
        viewModel.Editor.OverrideInstallState = true;
        viewModel.Editor.InstallSize = "123456789";
        viewModel.Editor.Version = "1.2.3-pilot";
        viewModel.Editor.Manual = "https://example.com/desktop-manual";
        viewModel.Editor.EnableSystemHdr = true;
        viewModel.Editor.LastActivity = singleLastActivity;
        viewModel.Editor.PlaytimeSeconds = "4321";
        viewModel.Editor.PlayCount = "12";
        viewModel.Editor.Added = singleAdded;
        viewModel.Editor.PreScript = "$env:PLAYNITE_PILOT = 'pre'";
        viewModel.Editor.GameStartedScript = "$env:PLAYNITE_PILOT = 'started'";
        viewModel.Editor.PostScript = "$env:PLAYNITE_PILOT = 'post'";
        viewModel.Editor.UseGlobalPreScript = false;
        viewModel.Editor.UseGlobalGameStartedScript = true;
        viewModel.Editor.UseGlobalPostScript = false;
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
            savedEditorGame.CriticScore == 74 &&
            savedEditorGame.CommunityScore == 83 &&
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
            savedEditorGame.FeatureIds.SequenceEqual(
                viewModel.Editor.Features.Where(option => option.Name == "Achievements").Select(option => option.Id)) &&
            savedEditorGame.SeriesIds.SequenceEqual(
                viewModel.Editor.Series.Where(option => option.Name == "Pilot Saga").Select(option => option.Id)) &&
            savedEditorGame.AgeRatingIds.SequenceEqual(
                viewModel.Editor.AgeRatings.Where(option => option.Name == "Mature").Select(option => option.Id)) &&
            savedEditorGame.RegionIds.SequenceEqual(
                viewModel.Editor.Regions.Where(option => option.Name == "Europe").Select(option => option.Id)) &&
            editorGame.Name == editedName &&
            editorGame.GenresText.Contains("Action", StringComparison.Ordinal) &&
            editorGame.PlatformsText.Contains("Linux", StringComparison.Ordinal)
                ? $"{editedName} persisted scalar and multi-value metadata"
                : throw new InvalidOperationException("The edited metadata did not round-trip through Core."));

        Record(results, "Remaining scores and multi-value metadata refresh Desktop details", () =>
            editorGame.CriticScoreText.Contains("74/100", StringComparison.Ordinal) &&
            editorGame.CommunityScoreText.Contains("83/100", StringComparison.Ordinal) &&
            editorGame.FeaturesText.Contains("Achievements", StringComparison.Ordinal) &&
            editorGame.SeriesText.Contains("Pilot Saga", StringComparison.Ordinal) &&
            editorGame.AgeRatingsText.Contains("Mature", StringComparison.Ordinal) &&
            editorGame.RegionsText.Contains("Europe", StringComparison.Ordinal)
                ? "critic/community scores and feature, series, rating, and region sets refreshed"
                : throw new InvalidOperationException("The remaining metadata did not refresh Desktop details."));

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

        Record(results, "Desktop actions and ROMs round-trip through Core", () =>
            !savedEditorGame.IncludeLibraryPluginAction &&
            savedEditorGame.GameActions.Count == 2 &&
            savedEditorGame.GameActions[0].Type == Playnite.SDK.Models.GameActionType.Emulator &&
            savedEditorGame.GameActions[0].EmulatorId == pilotEmulator.Id &&
            savedEditorGame.GameActions[0].EmulatorProfileId == pilotProfile.Id &&
            savedEditorGame.GameActions[0].OverrideDefaultArgs &&
            savedEditorGame.GameActions[0].Arguments == "{ImagePath} --fullscreen" &&
            savedEditorGame.GameActions[1].Type == Playnite.SDK.Models.GameActionType.File &&
            savedEditorGame.GameActions[1].TrackingMode == Playnite.SDK.Models.TrackingMode.Directory &&
            savedEditorGame.GameActions[1].TrackingPath == library.ActiveUserDataDirectory &&
            savedEditorGame.Roms.Count == 2 &&
            savedEditorGame.Roms[0].Path.EndsWith("disc2.iso", StringComparison.Ordinal) &&
            savedEditorGame.Roms[1].Name == "Disc One" &&
            editorGame.GameActionsText.Contains("2 custom", StringComparison.Ordinal) &&
            editorGame.RomsText.Contains("disc2.iso", StringComparison.OrdinalIgnoreCase)
                ? "ordered file/emulator actions, plugin-action policy, and ROM records persisted and refreshed"
                : throw new InvalidOperationException("Actions or ROM records did not round-trip through the Desktop editor."));

        Record(results, "Desktop installation, runtime, and scripts round-trip through Core", () =>
            savedEditorGame.InstallDirectory == singleInstallDirectory &&
            !savedEditorGame.IsInstalled &&
            savedEditorGame.OverrideInstallState &&
            savedEditorGame.InstallSize == 123456789 &&
            savedEditorGame.LastSizeScanDate.HasValue &&
            savedEditorGame.Version == "1.2.3-pilot" &&
            savedEditorGame.Manual == "https://example.com/desktop-manual" &&
            savedEditorGame.EnableSystemHdr &&
            savedEditorGame.LastActivity == singleLastActivity &&
            savedEditorGame.Playtime == 4321 &&
            savedEditorGame.PlayCount == 12 &&
            savedEditorGame.Added == singleAdded &&
            savedEditorGame.PreScript == "$env:PLAYNITE_PILOT = 'pre'" &&
            savedEditorGame.GameStartedScript == "$env:PLAYNITE_PILOT = 'started'" &&
            savedEditorGame.PostScript == "$env:PLAYNITE_PILOT = 'post'" &&
            !savedEditorGame.UseGlobalPreScript &&
            savedEditorGame.UseGlobalGameStartedScript &&
            !savedEditorGame.UseGlobalPostScript &&
            editorGame.InstallationDetailsText.Contains("1.2.3-pilot", StringComparison.Ordinal) &&
            editorGame.ScriptsText.Contains("System HDR", StringComparison.Ordinal) &&
            editorGame.PlaytimeText.Contains("hours played", StringComparison.Ordinal)
                ? "installation state, statistics, HDR/manual fields, and all script policies persisted and refreshed"
                : throw new InvalidOperationException("Installation, runtime, or script fields did not round-trip."));

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
        var originalBulkManuals = bulkGameIds.ToDictionary(id => id, id => library.Database.Games[id].Manual);
        var originalBulkPostScripts = bulkGameIds.ToDictionary(id => id, id => library.Database.Games[id].PostScript);
        var originalBulkAdded = bulkGameIds.ToDictionary(id => id, id => library.Database.Games[id].Added);
        var originalSizeScanDate = library.Database.Games[editorGame.Game.Id].LastSizeScanDate;
        const string bulkBackgroundUrl = "https://example.invalid/bulk-background.jpg";
        const string bulkLinkUrl = "https://example.com/shared-pilot-link";
        Dispatcher.UIThread.Post(() =>
        {
            viewModel.Editor.ApplyUserScore = true;
            viewModel.Editor.UserScore = "77";
            viewModel.Editor.ApplyCriticScore = true;
            viewModel.Editor.CriticScore = string.Empty;
            viewModel.Editor.ApplyCommunityScore = true;
            viewModel.Editor.CommunityScore = "66";
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
            viewModel.Editor.ApplyFeatures = true;
            SelectOnly(viewModel.Editor.Features, "Cloud saves");
            viewModel.Editor.ApplySeries = true;
            SelectOnly(viewModel.Editor.Series, "Standalone Stories");
            viewModel.Editor.ApplyAgeRatings = true;
            SelectOnly(viewModel.Editor.AgeRatings, "Teen");
            viewModel.Editor.ApplyRegions = true;
            SelectOnly(viewModel.Editor.Regions, "Worldwide");
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
            viewModel.Editor.ApplyIncludeLibraryPluginAction = true;
            viewModel.Editor.IncludeLibraryPluginAction = true;
            viewModel.Editor.ApplyGameActions = true;
            viewModel.Editor.GameActions.Clear();
            viewModel.Editor.AddGameActionCommand.Execute(null);
            var sharedAction = viewModel.Editor.GameActions.Last();
            sharedAction.Name = "Shared guide";
            sharedAction.Type = Playnite.SDK.Models.GameActionType.URL;
            sharedAction.IsPlayAction = false;
            sharedAction.Path = "https://example.com/shared-action";
            viewModel.Editor.ApplyRoms = true;
            viewModel.Editor.Roms.Clear();
            viewModel.Editor.AddRomCommand.Execute(null);
            var sharedRom = viewModel.Editor.Roms.Last();
            sharedRom.Name = "Shared ROM";
            sharedRom.Path = "{InstallDir}\\shared.rom";
            viewModel.Editor.ApplyIsInstalled = true;
            viewModel.Editor.IsInstalled = true;
            viewModel.Editor.ApplyInstallSize = true;
            viewModel.Editor.InstallSize = string.Empty;
            viewModel.Editor.ApplyVersion = true;
            viewModel.Editor.Version = "2.0-bulk";
            viewModel.Editor.ApplyPlayCount = true;
            viewModel.Editor.PlayCount = "99";
            viewModel.Editor.ApplyLastActivity = true;
            viewModel.Editor.LastActivity = null;
            viewModel.Editor.ApplyEnableSystemHdr = true;
            viewModel.Editor.EnableSystemHdr = false;
            viewModel.Editor.ApplyPreScript = true;
            viewModel.Editor.PreScript = "$env:PLAYNITE_BULK = 'pre'";
            viewModel.Editor.ApplyUseGlobalPreScript = true;
            viewModel.Editor.UseGlobalPreScript = false;
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

        Record(results, "Plugin bulk editing replaces remaining score and metadata fields", () =>
            bulkGameIds.All(id =>
            {
                var game = library.Database.Games[id];
                return game.CriticScore == null &&
                    game.CommunityScore == 66 &&
                    game.FeatureIds.SequenceEqual(
                        viewModel.Editor.Features.Where(option => option.Name == "Cloud saves").Select(option => option.Id)) &&
                    game.SeriesIds.SequenceEqual(
                        viewModel.Editor.Series.Where(option => option.Name == "Standalone Stories").Select(option => option.Id)) &&
                    game.AgeRatingIds.SequenceEqual(
                        viewModel.Editor.AgeRatings.Where(option => option.Name == "Teen").Select(option => option.Id)) &&
                    game.RegionIds.SequenceEqual(
                        viewModel.Editor.Regions.Where(option => option.Name == "Worldwide").Select(option => option.Id));
            })
                ? "nullable critic scores cleared and four Core ID collections were replaced explicitly"
                : throw new InvalidOperationException("Bulk score or remaining multi-value metadata did not persist."));

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

        Record(results, "Plugin bulk editing replaces actions and ROMs independently", () =>
        {
            var editedGames = bulkGameIds.Select(id => library.Database.Games[id]).ToList();
            return editedGames.All(game =>
                    game.IncludeLibraryPluginAction &&
                    game.GameActions.Count == 1 &&
                    game.GameActions[0].Name == "Shared guide" &&
                    game.GameActions[0].Type == Playnite.SDK.Models.GameActionType.URL &&
                    game.GameActions[0].Path == "https://example.com/shared-action" &&
                    !game.GameActions[0].IsPlayAction &&
                    game.Roms.Count == 1 &&
                    game.Roms[0].Name == "Shared ROM" &&
                    game.Roms[0].Path.EndsWith("shared.rom", StringComparison.Ordinal)) &&
                !ReferenceEquals(editedGames[0].GameActions, editedGames[1].GameActions) &&
                !ReferenceEquals(editedGames[0].Roms, editedGames[1].Roms)
                    ? "the SDK list overload copied action and ROM collections without sharing mutable instances"
                    : throw new InvalidOperationException("Bulk actions or ROM records were not independently applied.");
        });

        Record(results, "Plugin bulk editing applies runtime and script fields selectively", () =>
            bulkGameIds.All(id =>
            {
                var game = library.Database.Games[id];
                return game.IsInstalled &&
                    game.InstallSize == null &&
                    game.Version == "2.0-bulk" &&
                    game.PlayCount == 99 &&
                    game.LastActivity == null &&
                    !game.EnableSystemHdr &&
                    game.PreScript == "$env:PLAYNITE_BULK = 'pre'" &&
                    !game.UseGlobalPreScript &&
                    game.Manual == originalBulkManuals[id] &&
                    game.PostScript == originalBulkPostScripts[id] &&
                    game.Added == originalBulkAdded[id];
            }) &&
            library.Database.Games[editorGame.Game.Id].LastSizeScanDate > originalSizeScanDate
                ? "nullable install size/date fields cleared while unselected manual, post-script, and added values stayed intact"
                : throw new InvalidOperationException("Bulk runtime or script fields overwrote unselected metadata."));

        var metadataGame = library.Database.Games
            .First(game => game.Id != editorGame.Game.Id && !bulkGameIds.Contains(game.Id));
        var metadataGameCopy = metadataGame.GetCopy();
        metadataGameCopy.Description = "Existing metadata description";
        metadataGameCopy.CriticScore = null;
        metadataGameCopy.CoverImage = null;
        metadataGameCopy.Icon = null;
        library.Database.Games.Update(metadataGameCopy);
        viewModel.RefreshGame(metadataGame.Id);
        viewModel.SelectGame(metadataGame.Id);
        var originalMetadataName = metadataGame.Name;
        await using var metadataServer = new LoopbackImageServer(File.ReadAllBytes(library.SelfTestMediaPath));
        var metadataPlugin = new PilotMetadataPlugin(window.RuntimeHost.PluginApi, metadataServer.BaseUrl);
        viewModel.MetadataDownload.ConfigureProvidersForTesting(
            new MetadataPlugin[] { metadataPlugin },
            Array.Empty<LibraryPlugin>());
        viewModel.OpenMetadataDownloadCommand.Execute(null);

        Record(results, "Native metadata workflow exposes provider, field, and scope policy", () =>
            viewModel.MetadataDownload.IsVisible &&
            viewModel.MetadataDownload.Sources.Count == 2 &&
            viewModel.MetadataDownload.Sources.Any(source =>
                source.Id == metadataPlugin.Id && source.Name == metadataPlugin.Name) &&
            viewModel.MetadataDownload.Fields.Count == Enum.GetValues<MetadataField>().Length &&
            !viewModel.MetadataDownload.Fields.Single(field => field.Field == MetadataField.Name).IsSelected &&
            viewModel.MetadataDownload.SelectedTarget.Source == Playnite.Metadata.MetadataGamesSource.Selected
                ? $"{viewModel.MetadataDownload.Sources.Count} ordered sources and {viewModel.MetadataDownload.Fields.Count} Core fields are configurable"
                : throw new InvalidOperationException("Metadata options did not mirror the Core provider contract."));

        SelectOnly(viewModel.MetadataDownload.Sources, metadataPlugin.Id);
        SelectOnly(
            viewModel.MetadataDownload.Fields,
            MetadataField.Description,
            MetadataField.CriticScore,
            MetadataField.Genres,
            MetadataField.CoverImage,
            MetadataField.Icon);
        viewModel.MetadataDownload.SkipExistingValues = false;
        viewModel.MetadataDownload.DownloadBackgroundsImmediately = true;
        var metadataDownloaded = await viewModel.MetadataDownload.StartDownloadAsync();
        var downloadedGame = library.Database.Games[metadataGame.Id];
        var downloadedCoverPath = library.Database.GetFullFilePath(downloadedGame.CoverImage);
        var downloadedIconPath = library.Database.GetFullFilePath(downloadedGame.Icon);

        Record(results, "Provider metadata and remote cover/icon files persist through Core", () =>
        {
            var downloadedGenreNames = (downloadedGame.GenreIds ?? new List<Guid>())
                .Select(id => library.Database.Genres[id]?.Name)
                .Where(name => name != null)
                .ToList();
            if (metadataDownloaded &&
                !viewModel.MetadataDownload.IsVisible &&
                downloadedGame.Name == originalMetadataName &&
                downloadedGame.Description == PilotMetadataPlugin.DownloadedDescription &&
                downloadedGame.CriticScore == 93 &&
                downloadedGenreNames.Contains(PilotMetadataPlugin.DownloadedGenre) &&
                !downloadedGame.CoverImage.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !downloadedGame.Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(downloadedCoverPath) &&
                File.Exists(downloadedIconPath) &&
                metadataPlugin.ProviderCreationCount == 1 &&
                metadataPlugin.ProviderDisposeCount == 1 &&
                metadataPlugin.LastRequestWasBackground &&
                metadataServer.RequestCount >= 2 &&
                viewModel.SelectedGame?.DescriptionText?.Contains(
                    PilotMetadataPlugin.DownloadedDescription,
                    StringComparison.Ordinal) == true)
            {
                return $"remote artwork became {downloadedGame.CoverImage} and {downloadedGame.Icon}; provider reused and disposed once";
            }

            throw new InvalidOperationException(
                $"downloaded={metadataDownloaded}, name={downloadedGame.Name}, description={downloadedGame.Description}, " +
                $"critic={downloadedGame.CriticScore}, genres={string.Join(",", downloadedGenreNames)}, " +
                $"cover={downloadedGame.CoverImage}/{File.Exists(downloadedCoverPath)}, " +
                $"icon={downloadedGame.Icon}/{File.Exists(downloadedIconPath)}, " +
                $"providers={metadataPlugin.ProviderCreationCount}/{metadataPlugin.ProviderDisposeCount}, " +
                $"background={metadataPlugin.LastRequestWasBackground}, requests={metadataServer.RequestCount}, " +
                $"details={viewModel.SelectedGame?.DescriptionText}");
        });

        metadataPlugin.Description = "This replacement must be skipped";
        viewModel.OpenMetadataDownloadCommand.Execute(null);
        SelectOnly(viewModel.MetadataDownload.Sources, metadataPlugin.Id);
        SelectOnly(viewModel.MetadataDownload.Fields, MetadataField.Description);
        viewModel.MetadataDownload.SkipExistingValues = true;
        var skipExistingCompleted = await viewModel.MetadataDownload.StartDownloadAsync();
        downloadedGame = library.Database.Games[metadataGame.Id];
        Record(results, "Metadata keep-existing policy avoids unnecessary provider calls", () =>
            skipExistingCompleted &&
            downloadedGame.Description == PilotMetadataPlugin.DownloadedDescription &&
            metadataPlugin.ProviderCreationCount == 1 &&
            metadataPlugin.ProviderDisposeCount == 1
                ? "the populated field was preserved without constructing another provider"
                : throw new InvalidOperationException("Skip-existing metadata policy called or applied the provider unexpectedly."));

        metadataPlugin.Description = PilotMetadataPlugin.DownloadedDescription;
        viewModel.MetadataDownload.ConfigureProvidersForTesting(
            new MetadataPlugin[] { metadataPlugin },
            Array.Empty<LibraryPlugin>());
        SelectOnly(
            viewModel.MetadataDownload.Fields,
            MetadataField.Description,
            MetadataField.CoverImage,
            MetadataField.Icon);
        viewModel.MetadataDownload.SkipExistingValues = false;
        var libraryPlugin = new PilotLibraryPlugin(window.RuntimeHost.PluginApi);
        var libraryUpdatedCount = 0;
        viewModel.LibrarySync.ConfigureProvidersForTesting(
            new LibraryPlugin[] { libraryPlugin },
            () => libraryUpdatedCount++);
        viewModel.SearchText = string.Empty;
        viewModel.InstalledOnly = false;
        viewModel.FavoritesOnly = false;
        viewModel.SelectedFilterPreset = viewModel.FilterPresets.FirstOrDefault(preset => preset.Name == "All");
        viewModel.OpenLibrarySyncCommand.Execute(null);
        SelectOnly(viewModel.LibrarySync.Libraries, libraryPlugin.Id);
        SelectOnly(viewModel.LibrarySync.GameScanners);

        Record(results, "Native library update exposes loaded integrations and import policy", () =>
            viewModel.LibrarySync.IsVisible &&
            viewModel.LibrarySync.Libraries.Count == 1 &&
            viewModel.LibrarySync.Libraries[0].Id == libraryPlugin.Id &&
            viewModel.LibrarySync.Libraries[0].IsSelected &&
            viewModel.LibrarySync.PlaytimeModes.Count == Enum.GetValues<PlaytimeImportMode>().Length &&
            viewModel.LibrarySync.PlaytimeMode == PlaytimeImportMode.NewImportsOnly &&
            viewModel.LibrarySync.DownloadMetadataOnImport
                ? "the loaded library plugin, playtime modes, and metadata-on-import policy are configurable"
                : throw new InvalidOperationException("Library update options did not mirror the SDK/Core import contract."));

        var gamesBeforeLibraryImport = library.Database.Games.Count;
        var metadataProvidersBeforeImport = metadataPlugin.ProviderCreationCount;
        var imageRequestsBeforeImport = metadataServer.RequestCount;
        var firstLibrarySync = await viewModel.LibrarySync.StartSyncAsync();
        var importedLibraryGame = library.Database.Games
            .FirstOrDefault(game => game.PluginId == libraryPlugin.Id && game.GameId == PilotLibraryPlugin.ImportedGameId);

        Record(results, "Library update imports games, metadata, and live Desktop wrappers", () =>
            firstLibrarySync &&
            importedLibraryGame != null &&
            library.Database.Games.Count == gamesBeforeLibraryImport + 1 &&
            importedLibraryGame.Name == PilotLibraryPlugin.ImportedGameName &&
            importedLibraryGame.Playtime == PilotLibraryPlugin.FirstPlaytime &&
            importedLibraryGame.IsInstalled &&
            importedLibraryGame.Description == PilotMetadataPlugin.DownloadedDescription &&
            File.Exists(library.Database.GetFullFilePath(importedLibraryGame.CoverImage)) &&
            File.Exists(library.Database.GetFullFilePath(importedLibraryGame.Icon)) &&
            viewModel.Games.Any(game => game.Game.Id == importedLibraryGame.Id) &&
            metadataPlugin.ProviderCreationCount == metadataProvidersBeforeImport + 1 &&
            metadataServer.RequestCount >= imageRequestsBeforeImport + 2 &&
            libraryPlugin.GetGamesCallCount == 1 &&
            libraryUpdatedCount == 1
                ? $"{importedLibraryGame.Name} joined the live library with owned artwork and metadata"
                : throw new InvalidOperationException("The library import did not reach Core, metadata, wrappers, and update callbacks."));

        libraryPlugin.Revision = 2;
        viewModel.OpenLibrarySyncCommand.Execute(null);
        viewModel.LibrarySync.PlaytimeMode = PlaytimeImportMode.Always;
        var gamesBeforeLibraryRefresh = library.Database.Games.Count;
        var metadataProvidersBeforeRefresh = metadataPlugin.ProviderCreationCount;
        var secondLibrarySync = await viewModel.LibrarySync.StartSyncAsync();
        importedLibraryGame = library.Database.Games[importedLibraryGame.Id];
        var importedWrapper = viewModel.Games.FirstOrDefault(game => game.Game.Id == importedLibraryGame.Id);

        Record(results, "Library refresh updates existing state without duplicating or redownloading", () =>
            secondLibrarySync &&
            library.Database.Games.Count == gamesBeforeLibraryRefresh &&
            importedLibraryGame.Playtime == PilotLibraryPlugin.SecondPlaytime &&
            !importedLibraryGame.IsInstalled &&
            importedLibraryGame.InstallDirectory == PilotLibraryPlugin.SecondInstallDirectory &&
            importedWrapper != null &&
            !importedWrapper.IsInstalled &&
            viewModel.LibrarySync.GameScanners.All(option => !option.IsSelected) &&
            metadataPlugin.ProviderCreationCount == metadataProvidersBeforeRefresh &&
            libraryPlugin.GetGamesCallCount == 2 &&
            libraryUpdatedCount == 2
                ? "the existing plugin game refreshed playtime/install state with no duplicate or metadata request"
                : throw new InvalidOperationException("Existing library state was duplicated, stale, or redownloaded unexpectedly."));

        var scannerConfig = library.Database.GameScanners.Single(scanner => scanner.Name == "Pilot ROM scanner");
        viewModel.OpenLibrarySyncCommand.Execute(null);
        SelectOnly(viewModel.LibrarySync.Libraries);
        SelectOnly(viewModel.LibrarySync.GameScanners, scannerConfig.Id);
        viewModel.LibrarySync.DownloadMetadataOnImport = true;

        Record(results, "Native library update exposes saved emulated-game scanners", () =>
            viewModel.LibrarySync.IsVisible &&
            viewModel.LibrarySync.GameScanners.Count == 1 &&
            viewModel.LibrarySync.GameScanners[0].Id == scannerConfig.Id &&
            viewModel.LibrarySync.GameScanners[0].IsSelected &&
            viewModel.LibrarySync.GameScanners[0].Detail.Contains("Pilot Emulator", StringComparison.Ordinal)
                ? "the saved Core scanner is selectable beside library integrations"
                : throw new InvalidOperationException("The saved scanner configuration was not exposed by the update workflow."));

        var gamesBeforeScannerImport = library.Database.Games.Count;
        var regionsBeforeScannerImport = library.Database.Regions.Count;
        var metadataProvidersBeforeScannerImport = metadataPlugin.ProviderCreationCount;
        var libraryUpdatesBeforeScannerImport = libraryUpdatedCount;
        var firstScannerSync = await viewModel.LibrarySync.StartSyncAsync();
        var scannedGame = library.Database.Games.FirstOrDefault(game => game.Name == "Pilot Scanner Game");
        var scannedRegion = scannedGame?.RegionIds
            ?.Select(id => library.Database.Regions[id])
            .FirstOrDefault(region => region?.Name == "Sweden" && !string.IsNullOrWhiteSpace(region.SpecificationId));
        var scannerEmulator = library.Database.Emulators[scannerConfig.EmulatorId];
        var scannerPlatformId = scannerEmulator.CustomProfiles
            .Single(profile => profile.Id == scannerConfig.EmulatorProfileId)
            .Platforms.Single();
        var defaultCompletionStatusId = library.Database.GetCompletionStatusSettings().DefaultStatus;

        Record(results, "Core scanner imports ROM state, discoveries, metadata, and live wrappers", () =>
        {
            var coverExists = scannedGame != null &&
                File.Exists(library.Database.GetFullFilePath(scannedGame.CoverImage));
            var iconExists = scannedGame != null &&
                File.Exists(library.Database.GetFullFilePath(scannedGame.Icon));
            var wrapperExists = scannedGame != null &&
                viewModel.Games.Any(game => game.Game.Id == scannedGame.Id);
            if (firstScannerSync &&
                scannedGame != null &&
                library.Database.Games.Count == gamesBeforeScannerImport + 1 &&
                library.Database.Regions.Count == regionsBeforeScannerImport + 1 &&
                scannedRegion != null &&
                scannedGame.PlatformIds?.Contains(scannerPlatformId) == true &&
                defaultCompletionStatusId != Guid.Empty &&
                scannedGame.CompletionStatusId == defaultCompletionStatusId &&
                scannedGame.Roms?.Count == 1 &&
                scannedGame.GameActions?.SingleOrDefault()?.EmulatorId == scannerConfig.EmulatorId &&
                scannedGame.Description == PilotMetadataPlugin.DownloadedDescription &&
                coverExists &&
                iconExists &&
                wrapperExists &&
                metadataPlugin.ProviderCreationCount == metadataProvidersBeforeScannerImport + 1 &&
                libraryUpdatedCount == libraryUpdatesBeforeScannerImport + 1)
            {
                return $"{scannedGame.Name} imported with {scannedRegion.Name}, its configured platform, owned artwork, and default status";
            }

            throw new InvalidOperationException(
                $"sync={firstScannerSync}, game={scannedGame?.Name ?? "<null>"}, " +
                $"games={library.Database.Games.Count}/{gamesBeforeScannerImport + 1}, " +
                $"regions={library.Database.Regions.Count}/{regionsBeforeScannerImport + 1}, " +
                $"region={scannedRegion?.Name ?? "<null>"}, platform={scannedGame?.PlatformIds?.Contains(scannerPlatformId)}, " +
                $"status={scannedGame?.CompletionStatusId}/{defaultCompletionStatusId}, roms={scannedGame?.Roms?.Count}, " +
                $"emulator={scannedGame?.GameActions?.SingleOrDefault()?.EmulatorId}/{scannerConfig.EmulatorId}, " +
                $"description={scannedGame?.Description}, media={coverExists}/{iconExists}, wrapper={wrapperExists}, " +
                $"providers={metadataPlugin.ProviderCreationCount}/{metadataProvidersBeforeScannerImport + 1}, " +
                $"updates={libraryUpdatedCount}/{libraryUpdatesBeforeScannerImport + 1}.");
        });

        viewModel.OpenLibrarySyncCommand.Execute(null);
        SelectOnly(viewModel.LibrarySync.Libraries);
        var gamesBeforeScannerRescan = library.Database.Games.Count;
        var metadataProvidersBeforeScannerRescan = metadataPlugin.ProviderCreationCount;
        var libraryUpdatesBeforeScannerRescan = libraryUpdatedCount;
        var secondScannerSync = await viewModel.LibrarySync.StartSyncAsync();

        Record(results, "Core scanner rescan excludes already imported ROM paths", () =>
            secondScannerSync &&
            library.Database.Games.Count == gamesBeforeScannerRescan &&
            library.Database.Games.Count(game => game.Name == "Pilot Scanner Game") == 1 &&
            metadataPlugin.ProviderCreationCount == metadataProvidersBeforeScannerRescan &&
            libraryUpdatedCount == libraryUpdatesBeforeScannerRescan + 1
                ? "the saved scanner completed again without a duplicate game or metadata request"
                : throw new InvalidOperationException("The scanner rescan duplicated or redownloaded an imported ROM."));

        var installedFixtureDirectory = Path.Combine(library.ActiveUserDataDirectory, "installed-import-fixtures");
        Directory.CreateDirectory(installedFixtureDirectory);
        var systemExecutable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "cmd.exe");
        if (!File.Exists(systemExecutable))
        {
            throw new FileNotFoundException("The installed-game fixture executable is unavailable.", systemExecutable);
        }

        var detectedExecutablePath = Path.Combine(installedFixtureDirectory, "Pilot Detected.exe");
        var scannedExecutablePath = Path.Combine(installedFixtureDirectory, "Pilot Scanned.exe");
        var directExecutablePath = Path.Combine(installedFixtureDirectory, "Pilot Direct.exe");
        File.Copy(systemExecutable, detectedExecutablePath, true);
        File.Copy(systemExecutable, scannedExecutablePath, true);
        File.Copy(systemExecutable, directExecutablePath, true);
        var detectedProgram = new InstalledProgram
        {
            Name = "Pilot Detected™ Game",
            Path = detectedExecutablePath,
            Arguments = "--detected",
            WorkDir = installedFixtureDirectory,
            Icon = detectedExecutablePath,
            AppId = "pilot-installed-win32"
        };
        var storeProgram = new InstalledProgram
        {
            Name = "Pilot Store Game",
            Path = "explorer.exe",
            Arguments = "shell:AppsFolder\\Pilot.Store_123!App",
            WorkDir = installedFixtureDirectory,
            Icon = library.SelfTestMediaPath,
            AppId = "Pilot.Store_123"
        };
        var scannedProgram = new InstalledProgram
        {
            Name = "Pilot Scanned Game",
            Path = scannedExecutablePath,
            WorkDir = installedFixtureDirectory,
            Icon = $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll")},0",
            AppId = "pilot-installed-scanned"
        };
        var directProgram = new InstalledProgram
        {
            Name = "Pilot Direct Game",
            Path = directExecutablePath,
            Arguments = "--direct",
            WorkDir = installedFixtureDirectory,
            Icon = directExecutablePath,
            AppId = "pilot-installed-direct"
        };
        var installedLibraryUpdates = 0;
        string scannedDirectory = null;
        viewModel.InstalledGameImport.ConfigureProvidersForTesting(
            token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<DesktopDetectedProgram>>(new[]
                {
                    new DesktopDetectedProgram(detectedProgram, DesktopInstalledProgramType.Win32),
                    new DesktopDetectedProgram(storeProgram, DesktopInstalledProgramType.MicrosoftStore)
                });
            },
            (path, token) =>
            {
                token.ThrowIfCancellationRequested();
                scannedDirectory = path;
                return Task.FromResult<IReadOnlyList<InstalledProgram>>(new[] { scannedProgram });
            },
            path => string.Equals(path, directExecutablePath, StringComparison.OrdinalIgnoreCase)
                ? directProgram
                : throw new InvalidOperationException($"Unexpected executable fixture path: {path}"),
            () => installedLibraryUpdates++);

        viewModel.OpenInstalledGameImportCommand.Execute(null);
        var detectedInstalledPrograms = await viewModel.InstalledGameImport.DetectInstalledAsync();
        Record(results, "Installed-game import exposes desktop and Store discovery", () =>
            detectedInstalledPrograms &&
            viewModel.InstalledGameImport.IsVisible &&
            viewModel.InstalledGameImport.Programs.Count == 2 &&
            viewModel.InstalledGameImport.Programs.Any(program =>
                program.Program.AppId == detectedProgram.AppId &&
                program.Type == DesktopInstalledProgramType.Win32 &&
                !program.IsImported) &&
            viewModel.InstalledGameImport.Programs.Any(program =>
                program.Program.AppId == storeProgram.AppId &&
                program.Type == DesktopInstalledProgramType.MicrosoftStore &&
                !program.IsImported) &&
            !viewModel.OpenLibrarySyncCommand.CanExecute(null)
                ? "Win32 and Microsoft Store candidates share the native cancellable import surface"
                : throw new InvalidOperationException("Installed-program discovery did not expose both source types."));

        viewModel.InstalledGameImport.SelectAll = true;
        var gamesBeforeInstalledImport = library.Database.Games.Count;
        var providersBeforeInstalledImport = metadataPlugin.ProviderCreationCount;
        var requestsBeforeInstalledImport = metadataServer.RequestCount;
        var importedDetectedPrograms = await viewModel.InstalledGameImport.ImportSelectedAsync();
        var detectedGame = library.Database.Games.FirstOrDefault(game => game.GameId == detectedProgram.AppId);
        var storeGame = library.Database.Games.FirstOrDefault(game => game.GameId == storeProgram.AppId);
        var detectedAction = detectedGame?.GameActions?.SingleOrDefault();
        var storeAction = storeGame?.GameActions?.SingleOrDefault();
        var storeSource = storeGame == null ? null : library.Database.Sources[storeGame.SourceId];
        var windowsSpecificationApplied = new[] { detectedGame, storeGame }
            .Where(game => game != null)
            .All(game => game.PlatformIds?.Any(id =>
                library.Database.Platforms[id]?.SpecificationId == "pc_windows") == true);

        Record(results, "Installed discovery imports Core games, metadata, actions, and live wrappers", () =>
        {
            var expectedDetectedPath = Path.Combine(
                ExpandableVariables.InstallationDirectory,
                Path.GetFileName(detectedExecutablePath));
            if (importedDetectedPrograms &&
                !viewModel.InstalledGameImport.IsVisible &&
                detectedGame != null &&
                storeGame != null &&
                library.Database.Games.Count == gamesBeforeInstalledImport + 2 &&
                detectedGame.Name == "Pilot Detected Game" &&
                detectedGame.IsInstalled &&
                detectedGame.CompletionStatusId == defaultCompletionStatusId &&
                detectedAction?.Path == expectedDetectedPath &&
                detectedAction.Arguments == detectedProgram.Arguments &&
                detectedAction.WorkingDir == ExpandableVariables.InstallationDirectory &&
                detectedAction.IsPlayAction &&
                storeSource?.Name == "Microsoft Store" &&
                storeAction?.Path == storeProgram.Path &&
                storeAction.Arguments == storeProgram.Arguments &&
                storeAction.WorkingDir == string.Empty &&
                windowsSpecificationApplied &&
                detectedGame.Description == PilotMetadataPlugin.DownloadedDescription &&
                storeGame.Description == PilotMetadataPlugin.DownloadedDescription &&
                File.Exists(library.Database.GetFullFilePath(detectedGame.CoverImage)) &&
                File.Exists(library.Database.GetFullFilePath(storeGame.Icon)) &&
                viewModel.Games.Any(game => game.Game.Id == detectedGame.Id) &&
                viewModel.Games.Any(game => game.Game.Id == storeGame.Id) &&
                metadataPlugin.ProviderCreationCount == providersBeforeInstalledImport + 2 &&
                metadataServer.RequestCount >= requestsBeforeInstalledImport + 4 &&
                installedLibraryUpdates == 1)
            {
                return "legacy import metadata became owned Core records for both Win32 and Store candidates";
            }

            throw new InvalidOperationException(
                $"import={importedDetectedPrograms}, games={library.Database.Games.Count}/{gamesBeforeInstalledImport + 2}, " +
                $"detected={detectedGame?.Name}/{detectedAction?.Path}, store={storeSource?.Name}/{storeAction?.Path}, " +
                $"platforms={windowsSpecificationApplied}, providers={metadataPlugin.ProviderCreationCount}/{providersBeforeInstalledImport + 2}, " +
                $"requests={metadataServer.RequestCount}/{requestsBeforeInstalledImport + 4}, updates={installedLibraryUpdates}.");
        });

        viewModel.OpenInstalledGameImportCommand.Execute(null);
        var detectedImportedPrograms = await viewModel.InstalledGameImport.DetectInstalledAsync();
        var hiddenImportedCount = viewModel.InstalledGameImport.Programs.Count;
        viewModel.InstalledGameImport.HideImported = false;
        Record(results, "Installed-game discovery filters existing executable identities", () =>
            detectedImportedPrograms &&
            hiddenImportedCount == 0 &&
            viewModel.InstalledGameImport.Programs.Count == 2 &&
            viewModel.InstalledGameImport.Programs.All(program => program.IsImported)
                ? "both absolute launch identities were recognized without offering duplicate imports"
                : throw new InvalidOperationException("Previously imported executables were not detected or filtered."));
        viewModel.InstalledGameImport.Close();

        viewModel.OpenInstalledGameImportCommand.Execute(null);
        viewModel.InstalledGameImport.DownloadMetadataOnImport = false;
        var scannedPrograms = await viewModel.InstalledGameImport.ScanFolderAsync(installedFixtureDirectory);
        var addedDirectExecutable = viewModel.InstalledGameImport.AddExecutable(directExecutablePath);
        viewModel.InstalledGameImport.SelectAll = true;
        var providersBeforeExecutableImport = metadataPlugin.ProviderCreationCount;
        var gamesBeforeExecutableImport = library.Database.Games.Count;
        var importedExecutablePrograms = await viewModel.InstalledGameImport.ImportSelectedAsync();
        var installedScannedGame = library.Database.Games.FirstOrDefault(game => game.GameId == scannedProgram.AppId);
        var directGame = library.Database.Games.FirstOrDefault(game => game.GameId == directProgram.AppId);

        Record(results, "Folder scan and direct executable import preserve local icons without metadata", () =>
            scannedPrograms &&
            addedDirectExecutable &&
            string.Equals(scannedDirectory, installedFixtureDirectory, StringComparison.OrdinalIgnoreCase) &&
            importedExecutablePrograms &&
            library.Database.Games.Count == gamesBeforeExecutableImport + 2 &&
            installedScannedGame != null &&
            directGame != null &&
            File.Exists(library.Database.GetFullFilePath(installedScannedGame.Icon)) &&
            File.Exists(library.Database.GetFullFilePath(directGame.Icon)) &&
            Path.GetExtension(installedScannedGame.Icon).Equals(".ico", StringComparison.OrdinalIgnoreCase) &&
            installedScannedGame.CoverImage == null &&
            directGame.CoverImage == null &&
            metadataPlugin.ProviderCreationCount == providersBeforeExecutableImport &&
            installedLibraryUpdates == 2 &&
            viewModel.Games.Any(game => game.Game.Id == installedScannedGame.Id) &&
            viewModel.Games.Any(game => game.Game.Id == directGame.Id)
                ? "recursive-scan and single-file candidates imported with executable/resource icons converted into owned ICO files"
                : throw new InvalidOperationException("Folder/direct executable imports lost icon, database, or update state."));
        viewModel.InstalledGameImport.DownloadMetadataOnImport = true;

        var gameIdsBeforeManualAdd = library.Database.Games.Select(game => game.Id).ToHashSet();
        var gamesBeforeManualAdd = library.Database.Games.Count;
        viewModel.AddManualGameCommand.Execute(null);
        var stagedManualGame = library.Database.Games.SingleOrDefault(game =>
            !gameIdsBeforeManualAdd.Contains(game.Id));
        Record(results, "Manual add stages a default Core game in the full editor", () =>
            stagedManualGame != null &&
            library.Database.Games.Count == gamesBeforeManualAdd + 1 &&
            stagedManualGame.Name == "New Game" &&
            stagedManualGame.CompletionStatusId == defaultCompletionStatusId &&
            viewModel.Editor.IsVisible &&
            viewModel.Editor.IsSingleEdit &&
            viewModel.Editor.Name == "New Game" &&
            !viewModel.AddManualGameCommand.CanExecute(null) &&
            viewModel.Games.All(game => game.Game.Id != stagedManualGame.Id)
                ? "the provisional record is isolated from live wrappers until its editor is saved"
                : throw new InvalidOperationException("Manual game staging did not match the legacy editor contract."));

        viewModel.Editor.Name = "Pilot Manual Game";
        viewModel.Editor.Description = "Created through the native Avalonia manual-add flow.";
        viewModel.Editor.IsInstalled = true;
        viewModel.Editor.SaveCommand.Execute(null);
        var savedManualGame = stagedManualGame == null
            ? null
            : library.Database.Games[stagedManualGame.Id];
        Record(results, "Manual add keeps, refreshes, and selects a saved game", () =>
            savedManualGame != null &&
            !viewModel.Editor.IsVisible &&
            library.Database.Games.Count == gamesBeforeManualAdd + 1 &&
            savedManualGame.Name == "Pilot Manual Game" &&
            savedManualGame.Description == "Created through the native Avalonia manual-add flow." &&
            savedManualGame.IsInstalled &&
            savedManualGame.CompletionStatusId == defaultCompletionStatusId &&
            viewModel.SelectedGame?.Game.Id == savedManualGame.Id &&
            viewModel.Games.Any(game => game.Game.Id == savedManualGame.Id) &&
            viewModel.AddManualGameCommand.CanExecute(null)
                ? "the saved Core record joined the live library and became the selected game"
                : throw new InvalidOperationException("A saved manual game was not retained, refreshed, or selected."));

        var gamesBeforeManualCancel = library.Database.Games.Count;
        var manualIdsBeforeCancel = library.Database.Games.Select(game => game.Id).ToHashSet();
        viewModel.AddManualGameCommand.Execute(null);
        var cancelledManualGame = library.Database.Games.SingleOrDefault(game =>
            !manualIdsBeforeCancel.Contains(game.Id));
        viewModel.Editor.CancelCommand.Execute(null);
        Record(results, "Manual add removes a provisional game on cancel", () =>
            cancelledManualGame != null &&
            !viewModel.Editor.IsVisible &&
            library.Database.Games.Count == gamesBeforeManualCancel &&
            library.Database.Games[cancelledManualGame.Id] == null &&
            viewModel.Games.All(game => game.Game.Id != cancelledManualGame.Id) &&
            viewModel.StatusText == "Manual game creation cancelled." &&
            viewModel.AddManualGameCommand.CanExecute(null)
                ? "cancel left no database or live-wrapper residue"
                : throw new InvalidOperationException("A cancelled manual game remained in the library."));

        var initialElementGame = viewModel.SelectedGame?.Game ?? viewModel.Games.First().Game;
        var pluginElementHost = new Playnite.Avalonia.Controls.PluginElementHost
        {
            Plugin = WpfPluginSettingsContractPlugin.SourceName,
            Element = WpfPluginSettingsContractPlugin.ElementName,
            GameContext = initialElementGame,
            Width = 480,
            Height = 100
        };
        var pluginElementWindow = new global::Avalonia.Controls.Window
        {
            Title = "Plugin element compatibility contract",
            Width = 520,
            Height = 160,
            ShowInTaskbar = false,
            Content = pluginElementHost
        };
        pluginElementWindow.Show(window);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Plugin element placeholders load before their plugin", () =>
            !pluginElementHost.HasPluginContent && pluginElementHost.Content == null
                ? "the Avalonia host stayed empty while its late-bound plugin was unavailable"
                : throw new InvalidOperationException("An unregistered plugin element resolved unexpectedly."));

        var legacyUiPlugin = new WpfPluginSettingsContractPlugin(window.RuntimeHost.PluginApi);
        window.RuntimeHost.Extensions.Plugins.Add(
            legacyUiPlugin.Id,
            new LoadedPlugin(legacyUiPlugin, new ExtensionManifest
            {
                Id = legacyUiPlugin.Id.ToString(),
                Name = WpfPluginSettingsContractPlugin.DisplayName,
                Version = "1.0.0",
                Type = ExtensionType.GenericPlugin,
                DescriptionPath = Path.Combine(
                    library.ActiveUserDataDirectory,
                    "pilot-legacy-ui",
                    "extension.yaml")
            }));
        await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        Record(results, "Avalonia host retains legacy plugin UI registrations", () =>
        {
            var settingsSupport = window.RuntimeHost.Extensions.SettingsSupportList.SingleOrDefault(item =>
                ReferenceEquals(item.Source, legacyUiPlugin));
            var elementSupport = window.RuntimeHost.Extensions.CustomElementList.SingleOrDefault(item =>
                ReferenceEquals(item.Source, legacyUiPlugin));
            var converterSupport = window.RuntimeHost.Extensions.ConvertersSupportList.SingleOrDefault(item =>
                ReferenceEquals(item.Source, legacyUiPlugin));
            return settingsSupport?.SourceName == WpfPluginSettingsContractPlugin.SourceName &&
                   settingsSupport.SettingsRoot == "PilotSettings" &&
                   elementSupport?.SourceName == WpfPluginSettingsContractPlugin.SourceName &&
                   elementSupport.ElementList.SequenceEqual(new[] { WpfPluginSettingsContractPlugin.ElementName }) &&
                   converterSupport?.SourceName == WpfPluginSettingsContractPlugin.SourceName &&
                   converterSupport.Converters.Count == 1
                ? "settings, custom-element, and converter contracts are retained in Core"
                : throw new InvalidOperationException("A legacy plugin UI registration was discarded.");
        });

        var nativePluginElement = pluginElementHost.Content as WpfPluginElementHost;
        Record(results, "Avalonia embeds registered WPF plugin elements", () =>
            pluginElementHost.HasPluginContent &&
            nativePluginElement != null &&
            nativePluginElement.NativeHandle != IntPtr.Zero &&
            legacyUiPlugin.ElementCreationCount == 1 &&
            legacyUiPlugin.LastElementMode == ApplicationMode.Desktop &&
            legacyUiPlugin.LastElementControl?.LastGameContext?.Id == initialElementGame.Id &&
            legacyUiPlugin.LastElementControl.ContextChangeCount == 1
                ? $"the legacy control owns child HWND {nativePluginElement.NativeHandle} with Desktop game context"
                : throw new InvalidOperationException("The registered legacy element was not hosted with its game context."));

        var replacementElementGame = viewModel.Games
            .Select(game => game.Game)
            .First(game => game.Id != initialElementGame.Id);
        pluginElementHost.GameContext = replacementElementGame;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Plugin element game context updates without recreation", () =>
            ReferenceEquals(pluginElementHost.Content, nativePluginElement) &&
            legacyUiPlugin.ElementCreationCount == 1 &&
            legacyUiPlugin.LastElementControl.LastGameContext?.Id == replacementElementGame.Id &&
            legacyUiPlugin.LastElementControl.ContextChangeCount == 2
                ? "PluginUserControl.GameContext changed on the existing embedded control"
                : throw new InvalidOperationException("The embedded plugin element was recreated or kept stale context."));

        var legacyConverter = new PluginConverterProvider(
            WpfPluginSettingsContractPlugin.SourceName,
            nameof(WpfPluginSettingsContractConverter));
        var legacyConvertedValue = legacyConverter.Convert(
            "pilot",
            typeof(string),
            null,
            CultureInfo.InvariantCulture);
        Record(results, "Avalonia themes resolve registered legacy converters", () =>
            string.Equals(legacyConvertedValue as string, "legacy:pilot", StringComparison.Ordinal)
                ? "the Avalonia markup provider invoked the plugin's existing WPF converter"
                : throw new InvalidOperationException($"Unexpected converter result: {legacyConvertedValue}"));

        viewModel.OpenPluginSettingsListCommand.Execute(null);
        var listedPlugin = viewModel.PluginSettings.Plugins.SingleOrDefault(item => item.Id == legacyUiPlugin.Id);
        Record(results, "Native plugin settings chooser lists loaded extensions", () =>
            viewModel.PluginSettings.IsVisible &&
            listedPlugin != null &&
            listedPlugin.Name == WpfPluginSettingsContractPlugin.DisplayName &&
            listedPlugin.HasAdvertisedSettings &&
            ReferenceEquals(viewModel.PluginSettings.SelectedPlugin, listedPlugin)
                ? "the Avalonia overlay selected the loaded plugin and exposed its settings capability"
                : throw new InvalidOperationException("The native settings chooser did not mirror the loaded plugin."));
        viewModel.PluginSettings.CloseCommand.Execute(null);

        viewModel.PluginSettings.ConfigureAutomationForTesting(PluginSettingsAutomation.Save);
        var pluginSettingsSaved = window.RuntimeHost.PluginApi.MainView.OpenPluginSettings(legacyUiPlugin.Id);
        Record(results, "Legacy plugin settings save through the SDK callback", () =>
            pluginSettingsSaved &&
            legacyUiPlugin.Settings.BeginCount == 1 &&
            legacyUiPlugin.Settings.VerifyCount == 1 &&
            legacyUiPlugin.Settings.EndCount == 1 &&
            legacyUiPlugin.Settings.CancelCount == 0 &&
            legacyUiPlugin.ViewCreationCount == 1
                ? "BeginEdit, validation, EndEdit, and the existing WPF view completed in the compatibility host"
                : throw new InvalidOperationException("The legacy plugin settings save contract was not preserved."));

        viewModel.PluginSettings.ConfigureAutomationForTesting(PluginSettingsAutomation.Cancel);
        var pluginSettingsCancelled = legacyUiPlugin.OpenSettingsView();
        Record(results, "Legacy plugin settings cancel through Plugin.OpenSettingsView", () =>
            !pluginSettingsCancelled &&
            legacyUiPlugin.Settings.BeginCount == 2 &&
            legacyUiPlugin.Settings.VerifyCount == 1 &&
            legacyUiPlugin.Settings.EndCount == 1 &&
            legacyUiPlugin.Settings.CancelCount == 1 &&
            legacyUiPlugin.ViewCreationCount == 2
                ? "the public SDK helper returned false and called CancelEdit without persisting"
                : throw new InvalidOperationException("The legacy plugin settings cancel contract was not preserved."));

        legacyUiPlugin.Settings.IsValid = false;
        viewModel.PluginSettings.ConfigureAutomationForTesting(PluginSettingsAutomation.VerifyFailureThenCancel);
        var invalidSettingsSaved = legacyUiPlugin.OpenSettingsView();
        Record(results, "Legacy plugin settings validation blocks invalid saves", () =>
            !invalidSettingsSaved &&
            legacyUiPlugin.Settings.BeginCount == 3 &&
            legacyUiPlugin.Settings.VerifyCount == 2 &&
            legacyUiPlugin.Settings.EndCount == 1 &&
            legacyUiPlugin.Settings.CancelCount == 2 &&
            legacyUiPlugin.ViewCreationCount == 3
                ? "validation kept the dialog open until cancellation and never called EndEdit"
                : throw new InvalidOperationException("Invalid plugin settings were accepted or left an edit open."));
        legacyUiPlugin.Settings.IsValid = true;

        pluginElementWindow.Close();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Embedded plugin elements release their native window", () =>
            nativePluginElement != null && nativePluginElement.NativeHandle == IntPtr.Zero
                ? "closing the Avalonia surface detached and disposed the WPF child HWND"
                : throw new InvalidOperationException("The embedded plugin HWND remained attached after its host closed."));

        window.RuntimeHost.Extensions.Plugins.Remove(legacyUiPlugin.Id);
        window.RuntimeHost.Extensions.SettingsSupportList.RemoveAll(item =>
            ReferenceEquals(item.Source, legacyUiPlugin));
        window.RuntimeHost.Extensions.CustomElementList.RemoveAll(item =>
            ReferenceEquals(item.Source, legacyUiPlugin));
        window.RuntimeHost.Extensions.ConvertersSupportList.RemoveAll(item =>
            ReferenceEquals(item.Source, legacyUiPlugin));
        legacyUiPlugin.Dispose();

        Record(results, "Web-view adapter rejects unmappable SDK policies", () =>
        {
            var javaScriptError = CaptureNotSupported(() =>
                window.RuntimeHost.PluginApi.WebViews.CreateOffscreenView(new WebViewSettings
                {
                    JavaScriptEnabled = false
                }));
            var responseError = CaptureNotSupported(() =>
                window.RuntimeHost.PluginApi.WebViews.CreateOffscreenView(new WebViewSettings
                {
                    ResourceLoadedCallback = _ => { }
                }));
            return javaScriptError.Contains("JavaScript", StringComparison.Ordinal) &&
                   responseError.Contains("response", StringComparison.OrdinalIgnoreCase)
                ? "JavaScript policy and response-body interception fail explicitly instead of changing semantics"
                : throw new InvalidOperationException(
                    $"Unexpected compatibility messages: JavaScript='{javaScriptError}', response='{responseError}'.");
        });

        await using var webServer = new LoopbackWebServer();
        using var offscreenWebView = window.RuntimeHost.PluginApi.WebViews.CreateOffscreenView(new WebViewSettings
        {
            UserAgent = LoopbackWebServer.ExpectedUserAgent
        });
        var loadingStates = new List<bool>();
        offscreenWebView.LoadingChanged += (_, args) => loadingStates.Add(args.IsLoading);
        offscreenWebView.NavigateAndWait(webServer.PageUrl);
        var initialPageText = offscreenWebView.GetPageText();
        var initialPageSource = await offscreenWebView.GetPageSourceAsync();
        Record(results, "Offscreen plugin web views navigate through Avalonia NativeWebView", () =>
            offscreenWebView.CanExecuteJavascriptInMainFrame &&
            offscreenWebView.WindowHost == null &&
            string.Equals(offscreenWebView.GetCurrentAddress(), webServer.PageUrl, StringComparison.OrdinalIgnoreCase) &&
            initialPageText.Contains(LoopbackWebServer.InitialText, StringComparison.Ordinal) &&
            initialPageSource.Contains("pilot-message", StringComparison.Ordinal) &&
            loadingStates.Contains(true) &&
            loadingStates.Contains(false) &&
            string.Equals(webServer.LastUserAgent, LoopbackWebServer.ExpectedUserAgent, StringComparison.Ordinal)
                ? $"loaded {webServer.PageUrl} with SDK loading events and the configured user agent"
                : throw new InvalidOperationException(
                    $"address={offscreenWebView.GetCurrentAddress()}, states={string.Join(',', loadingStates)}, " +
                    $"userAgent={webServer.LastUserAgent}"));

        var scriptResult = await offscreenWebView.EvaluateScriptAsync(
            $"document.getElementById('pilot-message').innerText = '{LoopbackWebServer.MutatedText}'; 42");
        var failedScriptResult = await offscreenWebView.EvaluateScriptAsync(
            "throw new Error('pilot-script-failure')");
        var mutatedPageText = await offscreenWebView.GetPageTextAsync();
        Record(results, "Plugin web-view JavaScript preserves values, DOM changes, and failures", () =>
            scriptResult.Success &&
            Convert.ToInt64(scriptResult.Result, CultureInfo.InvariantCulture) == 42 &&
            mutatedPageText.Contains(LoopbackWebServer.MutatedText, StringComparison.Ordinal) &&
            !failedScriptResult.Success &&
            failedScriptResult.Message.Contains("pilot-script-failure", StringComparison.Ordinal)
                ? "script results were decoded, DOM mutation remained visible, and JavaScript errors stayed errors"
                : throw new InvalidOperationException(
                    $"success={scriptResult.Success}, value={scriptResult.Result}, failedMessage={failedScriptResult.Message}"));

        offscreenWebView.SetCookies(
            webServer.PageUrl,
            "127.0.0.1",
            "pilot-cookie",
            "pilot-value",
            "/",
            DateTime.UtcNow.AddHours(1));
        var writtenCookie = offscreenWebView.GetCookies().SingleOrDefault(cookie =>
            cookie.Name == "pilot-cookie" && cookie.Domain.TrimStart('.') == "127.0.0.1");
        offscreenWebView.DeleteCookies(webServer.PageUrl, "pilot-cookie");
        var cookieDeleted = offscreenWebView.GetCookies().All(cookie => cookie.Name != "pilot-cookie");
        Record(results, "Plugin web-view cookies round-trip through the native engine", () =>
            writtenCookie?.Value == "pilot-value" &&
            writtenCookie.Path == "/" &&
            cookieDeleted
                ? "the SDK cookie was written, enumerated, and deleted from WebView2"
                : throw new InvalidOperationException(
                    $"written={writtenCookie?.Value ?? "missing"}, deleted={cookieDeleted}"));

        using var visibleWebView = window.RuntimeHost.PluginApi.WebViews.CreateView(new WebViewSettings
        {
            WindowWidth = 520,
            WindowHeight = 320,
            WindowBackground = System.Windows.Media.Colors.Black
        });
        visibleWebView.NavigateAndWait(webServer.PageUrl);
        var windowHostError = CaptureNotSupported(() => _ = visibleWebView.WindowHost);
        Dispatcher.UIThread.Post(visibleWebView.Close, DispatcherPriority.Background);
        var visibleDialogResult = visibleWebView.OpenDialog();
        Record(results, "Visible plugin web views use a synchronous Avalonia dialog", () =>
            visibleDialogResult == null &&
            windowHostError.Contains("WPF Window", StringComparison.Ordinal)
                ? "the native browser opened and closed modally while the WPF-only WindowHost mismatch stayed explicit"
                : throw new InvalidOperationException(
                    $"dialogResult={visibleDialogResult}, WindowHost='{windowHostError}'"));

        viewModel.MetadataDownload.ConfigureProviders(
            () => window.RuntimeHost.Extensions.MetadataPlugins,
            () => window.RuntimeHost.Extensions.LibraryPlugins);
        viewModel.LibrarySync.ConfigureProviders(
            () => window.RuntimeHost.Extensions.LibraryPlugins,
            window.RuntimeHost.Extensions.NotifiyOnLibraryUpdated);

        viewModel.EnableTray = true;
        viewModel.MinimizeToTray = true;
        window.Chrome.Minimize();
        await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var minimizedToTray = !window.IsVisible && !window.ShowInTaskbar && !window.HasClosed;
        window.RestoreFromTray();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Minimize-to-tray preserves and restores the live Desktop window", () =>
            minimizedToTray && window.IsVisible && window.ShowInTaskbar &&
            window.WindowState == WindowState.Normal && !window.HasClosed
                ? "the caption command hid the live window and tray activation restored it"
                : throw new InvalidOperationException("Minimize-to-tray did not preserve the window instance."));
        viewModel.MinimizeToTray = false;

        viewModel.CloseToTray = true;
        window.Chrome.RequestClose();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var closedToTray = !window.IsVisible && !window.ShowInTaskbar && !window.HasClosed;
        window.RestoreFromTray();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Close-to-tray cancels destruction and restores the same window", () =>
            closedToTray && window.IsVisible && window.ShowInTaskbar && !window.HasClosed
                ? "the close role hid the window while explicit application shutdown remains available"
                : throw new InvalidOperationException("Close-to-tray destroyed or failed to restore the window."));

        Record(results, "Desktop settings persist atomically", () =>
        {
            var store = new DesktopSettingsStore(library.ActiveUserDataDirectory);
            store.Save(new DesktopSettings
            {
                ViewMode = "List",
                SortOrder = Playnite.SDK.Models.SortOrder.Playtime,
                SortDirection = Playnite.SDK.Models.SortOrderDirection.Descending,
                Grouping = Playnite.SDK.Models.GroupableField.Platform,
                DisabledPlugins = new List<string> { "pilot-plugin" },
                MetadataGamesSource = Playnite.Metadata.MetadataGamesSource.Filtered,
                MetadataSkipExistingValues = false,
                DownloadBackgroundsImmediately = false,
                MetadataSourceIds = new List<Guid> { metadataPlugin.Id },
                MetadataFields = new List<MetadataField> { MetadataField.Description, MetadataField.CoverImage },
                LibraryPluginIds = new List<Guid> { libraryPlugin.Id },
                LibraryPluginSelectionConfigured = true,
                GameScannerIds = new List<Guid> { scannerConfig.Id },
                GameScannerSelectionConfigured = true,
                LibraryPlaytimeImportMode = PlaytimeImportMode.Always,
                DownloadMetadataOnImport = false,
                EnableTray = false,
                MinimizeToTray = true,
                CloseToTray = false,
                WindowWidth = 1280,
                WindowHeight = 760,
                WindowX = 120,
                WindowY = 80,
                WindowMaximized = true
            });
            var loaded = store.Load();
            if (loaded.ViewMode != "List" ||
                loaded.SortOrder != Playnite.SDK.Models.SortOrder.Playtime ||
                loaded.SortDirection != Playnite.SDK.Models.SortOrderDirection.Descending ||
                loaded.Grouping != Playnite.SDK.Models.GroupableField.Platform ||
                loaded.DisabledPlugins.SingleOrDefault() != "pilot-plugin" ||
                loaded.MetadataGamesSource != Playnite.Metadata.MetadataGamesSource.Filtered ||
                loaded.MetadataSkipExistingValues ||
                loaded.DownloadBackgroundsImmediately ||
                !loaded.MetadataSourceIds.SequenceEqual(new[] { metadataPlugin.Id }) ||
                !loaded.MetadataFields.SequenceEqual(new[] { MetadataField.Description, MetadataField.CoverImage }) ||
                !loaded.LibraryPluginIds.SequenceEqual(new[] { libraryPlugin.Id }) ||
                !loaded.LibraryPluginSelectionConfigured ||
                !loaded.GameScannerIds.SequenceEqual(new[] { scannerConfig.Id }) ||
                !loaded.GameScannerSelectionConfigured ||
                loaded.LibraryPlaytimeImportMode != PlaytimeImportMode.Always ||
                loaded.DownloadMetadataOnImport ||
                loaded.EnableTray ||
                !loaded.MinimizeToTray ||
                loaded.CloseToTray ||
                loaded.WindowWidth != 1280 ||
                loaded.WindowHeight != 760 ||
                loaded.WindowX != 120 ||
                loaded.WindowY != 80 ||
                !loaded.WindowMaximized)
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

    private static string CaptureNotSupported(Action action)
    {
        try
        {
            action();
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }

        throw new InvalidOperationException("The compatibility call unexpectedly succeeded.");
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

    private static void SelectOnly(
        IEnumerable<DesktopMetadataSourceOption> options,
        params Guid[] selectedIds)
    {
        var selected = selectedIds.ToHashSet();
        foreach (var option in options)
        {
            option.IsSelected = selected.Contains(option.Id);
        }
    }

    private static void SelectOnly(
        IEnumerable<DesktopMetadataFieldOption> options,
        params MetadataField[] selectedFields)
    {
        var selected = selectedFields.ToHashSet();
        foreach (var option in options)
        {
            option.IsSelected = selected.Contains(option.Field);
        }
    }

    private static void SelectOnly(
        IEnumerable<DesktopLibraryPluginOption> options,
        params Guid[] selectedIds)
    {
        var selected = selectedIds.ToHashSet();
        foreach (var option in options)
        {
            option.IsSelected = selected.Contains(option.Id);
        }
    }

    private static void SelectOnly(
        IEnumerable<DesktopGameScannerOption> options,
        params Guid[] selectedIds)
    {
        var selected = selectedIds.ToHashSet();
        foreach (var option in options)
        {
            option.IsSelected = selected.Contains(option.Id);
        }
    }

    private sealed class PilotLibraryPlugin : LibraryPlugin
    {
        public const string ImportedGameId = "pilot-library-game-001";
        public const string ImportedGameName = "Imported Pilot Library Game";
        public const string SecondInstallDirectory = @"C:\PilotLibrary\Revision2";
        public const ulong FirstPlaytime = 420;
        public const ulong SecondPlaytime = 840;
        private static readonly Guid pluginId = Guid.Parse("e2848b20-5dcd-43fa-8b89-e10a31d448a4");

        public override Guid Id => pluginId;
        public override string Name => "Pilot library integration";
        public int Revision { get; set; } = 1;
        public int GetGamesCallCount { get; private set; }

        public PilotLibraryPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            Properties = new LibraryPluginProperties();
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            args.CancelToken.ThrowIfCancellationRequested();
            GetGamesCallCount++;
            var secondRevision = Revision >= 2;
            return new[]
            {
                new GameMetadata
                {
                    Name = ImportedGameName,
                    GameId = ImportedGameId,
                    IsInstalled = !secondRevision,
                    InstallDirectory = secondRevision ? SecondInstallDirectory : @"C:\PilotLibrary\Revision1",
                    Playtime = secondRevision ? SecondPlaytime : FirstPlaytime,
                    LastActivity = secondRevision ? new DateTime(2026, 7, 18) : new DateTime(2026, 7, 17)
                }
            };
        }
    }

    private sealed class PilotMetadataPlugin : MetadataPlugin
    {
        public const string DownloadedDescription = "Downloaded pilot metadata description";
        public const string DownloadedGenre = "Downloaded Pilot Genre";
        private static readonly Guid pluginId = Guid.Parse("a565267c-e5e2-4e5e-8528-b3d73d8ce071");
        private readonly string baseUrl;

        public override Guid Id => pluginId;
        public override string Name => "Pilot metadata provider";
        public override List<MetadataField> SupportedFields { get; } = new()
        {
            MetadataField.Name,
            MetadataField.Description,
            MetadataField.CriticScore,
            MetadataField.Genres,
            MetadataField.CoverImage,
            MetadataField.Icon
        };
        public string Description { get; set; } = DownloadedDescription;
        public int ProviderCreationCount { get; private set; }
        public int ProviderDisposeCount { get; private set; }
        public bool LastRequestWasBackground { get; private set; }

        public PilotMetadataPlugin(IPlayniteAPI playniteApi, string baseUrl) : base(playniteApi)
        {
            this.baseUrl = baseUrl;
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            ProviderCreationCount++;
            LastRequestWasBackground = options.IsBackgroundDownload;
            return new PilotMetadataProvider(this);
        }

        private sealed class PilotMetadataProvider : OnDemandMetadataProvider
        {
            private readonly PilotMetadataPlugin plugin;

            public override List<MetadataField> AvailableFields => plugin.SupportedFields;

            public PilotMetadataProvider(PilotMetadataPlugin plugin)
            {
                this.plugin = plugin;
            }

            public override string GetName(GetMetadataFieldArgs args) => "Downloaded name must stay unselected";
            public override string GetDescription(GetMetadataFieldArgs args) => plugin.Description;
            public override int? GetCriticScore(GetMetadataFieldArgs args) => 93;
            public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args) =>
                new[] { new MetadataNameProperty(DownloadedGenre) };
            public override MetadataFile GetCoverImage(GetMetadataFieldArgs args) =>
                new MetadataFile(plugin.baseUrl + "cover.png");
            public override MetadataFile GetIcon(GetMetadataFieldArgs args) =>
                new MetadataFile(plugin.baseUrl + "icon.png");

            public override void Dispose()
            {
                plugin.ProviderDisposeCount++;
            }
        }
    }

    private sealed class LoopbackImageServer : IAsyncDisposable
    {
        private readonly byte[] imageData;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task listenerTask;
        private int requestCount;

        public string BaseUrl { get; }
        public int RequestCount => Volatile.Read(ref requestCount);

        public LoopbackImageServer(byte[] imageData)
        {
            this.imageData = imageData ?? throw new ArgumentNullException(nameof(imageData));
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            BaseUrl = $"http://127.0.0.1:{endpoint.Port}/";
            listenerTask = ListenAsync(cancellation.Token);
        }

        private async Task ListenAsync(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancelToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                try
                {
                    await ServeAsync(client, cancelToken);
                }
                catch (IOException) when (!cancelToken.IsCancellationRequested)
                {
                }
            }
        }

        private async Task ServeAsync(TcpClient client, CancellationToken cancelToken)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                string line;
                do
                {
                    line = await reader.ReadLineAsync(cancelToken);
                }
                while (!string.IsNullOrEmpty(line));

                var response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: image/png\r\n" +
                    $"Content-Length: {imageData.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(response, cancelToken);
                await stream.WriteAsync(imageData, cancelToken);
                await stream.FlushAsync(cancelToken);
                Interlocked.Increment(ref requestCount);
            }
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            listener.Stop();
            try
            {
                await listenerTask;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }

    private sealed class LoopbackWebServer : IAsyncDisposable
    {
        public const string ExpectedUserAgent = "Playnite-Avalonia-WebView-SelfTest/1.0";
        public const string InitialText = "Avalonia web-view pilot";
        public const string MutatedText = "Avalonia web-view script updated";

        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task listenerTask;
        private string lastUserAgent;

        public string PageUrl { get; }
        public string LastUserAgent => Volatile.Read(ref lastUserAgent);

        public LoopbackWebServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            PageUrl = $"http://127.0.0.1:{endpoint.Port}/pilot";
            listenerTask = ListenAsync(cancellation.Token);
        }

        private async Task ListenAsync(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancelToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                try
                {
                    await ServeAsync(client, cancelToken);
                }
                catch (IOException) when (!cancelToken.IsCancellationRequested)
                {
                }
            }
        }

        private async Task ServeAsync(TcpClient client, CancellationToken cancelToken)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                _ = await reader.ReadLineAsync(cancelToken);
                string line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancelToken)))
                {
                    var separator = line.IndexOf(':');
                    if (separator > 0 &&
                        line.AsSpan(0, separator).Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                    {
                        Volatile.Write(ref lastUserAgent, line[(separator + 1)..].Trim());
                    }
                }

                var body = Encoding.UTF8.GetBytes(
                    "<!doctype html><html><head><meta charset=\"utf-8\"><title>Playnite pilot</title></head>" +
                    $"<body><main id=\"pilot-message\">{InitialText}</main></body></html>");
                var headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html; charset=utf-8\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Cache-Control: no-store\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(headers, cancelToken);
                await stream.WriteAsync(body, cancelToken);
                await stream.FlushAsync(cancelToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            listener.Stop();
            try
            {
                await listenerTask;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                cancellation.Dispose();
            }
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
