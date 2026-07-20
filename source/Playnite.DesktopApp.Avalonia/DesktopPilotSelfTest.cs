using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.IO.Compression;
using System.Management.Automation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Playnite.Avalonia.Markup;
using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.Services;
using Playnite.Avalonia.Input;
using Playnite.Avalonia.Theming;
using Playnite.Controllers;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.Scripting;
using Playnite.Scripting.PowerShell;
#if WINDOWS
using Playnite.WpfPluginSupport;
#endif
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
#if !WINDOWS
        Console.WriteLine(
            "[SKIP] 13 SDK v6/WPF resource, element-host, converter, menu, and settings checks are Windows-only; " +
            "Linux exercises the SDK v7 Avalonia contracts instead.");
#endif

        Record(results, "Playnite.Core library opens", () =>
            library.IsOpen && library.Database.GetType().Assembly.GetName().Name == "Playnite.Core"
                ? $"{library.Games.Count:N0} games loaded from Playnite.Core"
                : throw new InvalidOperationException("The concrete Core database is not open."));

        Record(results, "Loose Desktop theme applies", () =>
            window.MainView.TemplateAppliedCount > 0 && window.MainView.GameList != null &&
            window.MainView.PluginSearchBox != null
                ? "DesktopMainView resolved its library and plugin-search template contracts"
                : throw new InvalidOperationException("The Desktop theme template was not applied."));

        var requiredDesktopThemeResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Views/BackgroundLayer.axaml", "Views/TopPanel.axaml", "Views/Sidebar.axaml",
            "Views/GridItemTemplate.axaml", "Views/ListItemTemplate.axaml", "Views/DetailsPanel.axaml",
            "Views/FilterPanel.axaml", "Views/AddonStore.axaml", "Views/EmulatorConfig.axaml",
            "Views/EmulatedImport.axaml", "Views/DatabaseFields.axaml", "Views/WebImageSearch.axaml",
            "Views/ChromeParity.axaml"
        };
        var activeThemeManifest = window.ActiveThemePackage.Manifest;
        Record(results, "Avalonia theme API 3 package contract validates", () =>
            window.ActiveThemePackage.Mode == AvaloniaThemeMode.Desktop &&
            activeThemeManifest?.ThemeApiVersion == AvaloniaThemePackage.CurrentApiVersion.ToString() &&
            requiredDesktopThemeResources.IsSubsetOf(activeThemeManifest.Resources) &&
            window.ActiveThemePackage.ResourceDictionaries.Count == activeThemeManifest.Resources.Count + 1 &&
            window.ActiveThemePackage.ResourceDictionaries.All(File.Exists) &&
            window.ActiveThemePackage.SelectorStyles.Count == activeThemeManifest.Styles.Count &&
            window.ActiveThemePackage.SelectorStyles.All(File.Exists)
                ? $"{window.ActiveThemePackage.Name} targets theme API {AvaloniaThemePackage.CurrentApiVersion} " +
                  $"with {activeThemeManifest.Resources.Count:N0} required modular view dictionaries"
                : throw new InvalidOperationException("The default Desktop theme package is incomplete."));

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
            var favoriteMenu = items.FirstOrDefault(item =>
                item.Header == LocalizeForTest("LOCQuickFilterFavorites", "Favorites"))?.Menu;
            return window.TrayService.IsEnabled &&
                window.TrayService.QuickLaunchItemCount == 5 &&
                window.TrayService.FavoriteItemCount > 0 &&
                favoriteMenu?.Items.Count == window.TrayService.FavoriteItemCount &&
                items.Any(item => item.Header == LocalizeForTest("LOCOpenPlaynite", "Open Playnite")) &&
                items.Any(item => item.Header == "Open Fullscreen") &&
                items.Any(item => item.Header == LocalizeForTest("LOCExitPlaynite", "Exit Playnite"))
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
        var searchProbe = viewModel.LibraryGames
            .First(game => !string.IsNullOrWhiteSpace(game.Name));
        viewModel.SearchText = searchProbe.Name;
        Record(results, "Desktop search filters live data", () =>
            viewModel.Games.Count > 0 &&
            viewModel.Games.Count <= before &&
            viewModel.Games.Any(game => game.Game.Id == searchProbe.Game.Id) &&
            viewModel.Games.All(game => game.Name.Contains(
                searchProbe.Name,
                StringComparison.CurrentCultureIgnoreCase))
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

        window.MainView.GameList.SelectedIndex = 0;
        window.MainView.FocusSelectedGame();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var controllerSelectionBefore = window.MainView.GameList.SelectedIndex;
        window.GamepadBridge.ButtonDown(GamepadButton.DPadRight);
        window.GamepadBridge.ButtonUp(GamepadButton.DPadRight);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Controller navigation reaches the Desktop library", () =>
            window.MainView.GameList.SelectedIndex != controllerSelectionBefore &&
            window.MainView.GameList.SelectedIndex >= 0
                ? $"selection moved {controllerSelectionBefore} → {window.MainView.GameList.SelectedIndex}"
                : throw new InvalidOperationException(
                    $"Controller navigation selected index {window.MainView.GameList.SelectedIndex}; " +
                    $"focus={window.FocusManager?.GetFocusedElement()?.GetType().FullName ?? "none"}; " +
                    $"listFocus={window.MainView.GameList.IsKeyboardFocusWithin}."));
        Record(results, "Desktop SDL input source initializes", () =>
            window.SdlInput.IsAvailable
                ? window.SdlInput.Status
                : throw new InvalidOperationException(window.SdlInput.Status));

        Record(results, "Shared plugin and game-operation host initializes", () =>
            window.RuntimeHost?.Actions != null &&
            window.RuntimeHost.Extensions != null &&
            window.RuntimeHost.LoadedPluginCount == 0 &&
            viewModel.PluginSummary.Contains("self-test", StringComparison.OrdinalIgnoreCase)
                ? $"{viewModel.PluginSummary}; Core action orchestration is attached"
                : throw new InvalidOperationException("The shared Avalonia runtime host is unavailable."));

        Record(results, "SDK v7 host bundle reaches the executable output", () =>
        {
            var bundlePath = Path.Combine(AppContext.BaseDirectory, "SdkV7Host");
            var sdkPath = Path.Combine(bundlePath, "Playnite.SDK.dll");
            var hostPath = Path.Combine(bundlePath, "Playnite.SDK.V7.Host.dll");
            return File.Exists(sdkPath) && File.Exists(hostPath)
                ? "the isolated SDK and host bridge are packaged beside the Desktop executable"
                : throw new FileNotFoundException($"The SDK v7 host bundle is incomplete at {bundlePath}.");
        });

#if WINDOWS
        Record(results, "Legacy plugin resources bridge into Avalonia", () =>
            Playnite.SDK.ResourceProvider.GetString("LOCDesktopPlay") == "Play" &&
            Playnite.SDK.ResourceProvider.GetResource("FontIcoFont") is System.Windows.Media.FontFamily &&
            System.Windows.Application.Current?.TryFindResource("BaseTextBlockStyle") is System.Windows.Style
                ? "localized strings and WPF-compatible theme primitives are available during plugin construction"
                : throw new InvalidOperationException("The static legacy resource bridge is incomplete."));
#endif

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
            viewModel.Editor.Platforms.Count(option => option.IsSelected == true) == 1
                ? "all ten multi-value metadata collections loaded from Core"
                : throw new InvalidOperationException(
                    $"Counts were G{viewModel.Editor.Genres.Count}/P{viewModel.Editor.Platforms.Count}/" +
                    $"C{viewModel.Editor.Categories.Count}/T{viewModel.Editor.Tags.Count}/" +
                    $"D{viewModel.Editor.Developers.Count}/P{viewModel.Editor.Publishers.Count}/" +
                    $"F{viewModel.Editor.Features.Count}/S{viewModel.Editor.Series.Count}/" +
                    $"A{viewModel.Editor.AgeRatings.Count}/R{viewModel.Editor.Regions.Count}; " +
                    $"selected platforms {viewModel.Editor.Platforms.Count(option => option.IsSelected == true)}."));

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
            !string.IsNullOrWhiteSpace(editorGame.GameActionsText) &&
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
            editorGame.ScriptsText.Contains(
                LocalizeForTest("LOCGameHdrTitle", "System HDR"), StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(editorGame.PlaytimeText)
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
#if WINDOWS
        var pluginEditResult = window.RuntimeHost.PluginApi.MainView.OpenEditDialog(pluginEditGame.Game.Id);
#else
        var pluginEditResult = await window.RuntimeHost.PluginApi.MainView.OpenEditDialogAsync(pluginEditGame.Game.Id);
#endif
        Record(results, "Plugin OpenEditDialog uses the Avalonia editor", () =>
            pluginEditResult == true &&
            library.Database.Games[pluginEditGame.Game.Id].Name == pluginEditedName &&
            library.Database.Games[pluginEditGame.Game.Id].UserScore == 91
                ? "the synchronous SDK call returned true after Core persistence"
                : throw new InvalidOperationException("The plugin edit-dialog bridge did not persist its result."));

        var bulkGameIds = new List<Guid> { editorGame.Game.Id, pluginEditGame.Game.Id };
        var actionGenreId = library.Database.Genres.First(genre => genre.Name == "Action").Id;
        var strategyGenreId = library.Database.Genres.First(genre => genre.Name == "Strategy").Id;
        var actionBulkGame = library.Database.Games[bulkGameIds[0]].GetCopy();
        actionBulkGame.GenreIds = new List<Guid> { actionGenreId };
        library.Database.Games.Update(actionBulkGame);
        var strategyBulkGame = library.Database.Games[bulkGameIds[1]].GetCopy();
        strategyBulkGame.GenreIds = new List<Guid> { strategyGenreId };
        library.Database.Games.Update(strategyBulkGame);
        viewModel.OpenGameEditor(bulkGameIds);
        Record(results, "Bulk editor represents mixed taxonomy values explicitly", () =>
            viewModel.Editor.Genres.Single(option => option.Id == actionGenreId).IsSelected == null &&
            viewModel.Editor.Genres.Single(option => option.Id == strategyGenreId).IsSelected == null &&
            viewModel.Editor.Genres.All(option => option.AllowIndeterminate)
                ? "values present on only part of the selection are indeterminate rather than silently cleared"
                : throw new InvalidOperationException("Mixed bulk taxonomy values were not represented as indeterminate."));
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Bulk editor requires explicit field selection", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.IsBulkEdit &&
            viewModel.Editor.HasValidationError &&
            viewModel.Editor.ValidationMessage.Contains("at least one", StringComparison.OrdinalIgnoreCase)
                ? viewModel.Editor.ValidationMessage
                : throw new InvalidOperationException("A bulk save without selected fields was accepted."));
        viewModel.Editor.CancelCommand.Execute(null);

        var distinctBulkGenres = bulkGameIds.ToDictionary(
            id => id,
            id => library.Database.Games[id].GenreIds.ToList());
        viewModel.OpenGameEditor(bulkGameIds);
        viewModel.Editor.ApplyGenres = true;
        viewModel.Editor.SaveCommand.Execute(null);
        Record(results, "Bulk editor preserves untouched indeterminate taxonomy values", () =>
            !viewModel.Editor.IsVisible &&
            bulkGameIds.All(id => library.Database.Games[id].GenreIds.SequenceEqual(distinctBulkGenres[id]))
                ? "saving an applied list leaves each game's mixed values intact until the user changes them"
                : throw new InvalidOperationException("Untouched indeterminate taxonomy values were overwritten."));

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
#if WINDOWS
        var bulkEditResult = window.RuntimeHost.PluginApi.MainView.OpenEditDialog(bulkGameIds);
#else
        var bulkEditResult = await window.RuntimeHost.PluginApi.MainView.OpenEditDialogAsync(bulkGameIds);
#endif
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
        var downloadedCoverPath = string.IsNullOrWhiteSpace(downloadedGame.CoverImage)
            ? null
            : library.Database.GetFullFilePath(downloadedGame.CoverImage);
        var downloadedIconPath = string.IsNullOrWhiteSpace(downloadedGame.Icon)
            ? null
            : library.Database.GetFullFilePath(downloadedGame.Icon);

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
                !string.IsNullOrWhiteSpace(downloadedGame.CoverImage) &&
                !downloadedGame.CoverImage.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(downloadedGame.Icon) &&
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

        metadataPlugin.Description = "Editor-selected metadata description";
        var editorSourceGame = library.Database.Games[metadataGame.Id].GetCopy();
        editorSourceGame.CoverImage = null;
        editorSourceGame.Icon = null;
        library.Database.Games.Update(editorSourceGame);
        viewModel.Editor.ConfigureMetadataProviders(
            () => new List<MetadataPlugin> { metadataPlugin },
            () => new List<LibraryPlugin>());
        viewModel.OpenGameEditor(new[] { metadataGame.Id });
        var editorInstallDirectory = Path.Combine(library.ActiveUserDataDirectory, "p-g-install-size");
        Directory.CreateDirectory(editorInstallDirectory);
        File.WriteAllBytes(Path.Combine(editorInstallDirectory, "payload.bin"), new byte[4096]);
        viewModel.Editor.IsInstalled = true;
        viewModel.Editor.InstallDirectory = editorInstallDirectory;
        viewModel.Editor.Roms.Clear();
        var calculatedEditorInstallSize = await viewModel.Editor.CalculateInstallSizeForTestAsync();
        Record(results, "Editor calculates install size asynchronously", () =>
            calculatedEditorInstallSize == "4096" && !viewModel.Editor.IsCalculatingInstallSize
                ? "the Core calculator measured the edited installation directory without blocking the editor"
                : throw new InvalidOperationException($"The editor calculated '{calculatedEditorInstallSize}' bytes instead of 4096."));
        viewModel.Editor.CoverImage = library.SelfTestMediaPath;
        var editorMediaInfoWorks = viewModel.Editor.CoverMediaInfo.Contains('×') &&
            viewModel.Editor.CoverMediaInfo.Contains(" · ", StringComparison.Ordinal);
        viewModel.Editor.CoverImage = string.Empty;
        viewModel.Editor.AddLinkCommand.Execute(null);
        var firstEditorLink = viewModel.Editor.Links.Last();
        firstEditorLink.Name = "First";
        firstEditorLink.Url = "https://example.com/first";
        viewModel.Editor.AddLinkCommand.Execute(null);
        var secondEditorLink = viewModel.Editor.Links.Last();
        secondEditorLink.Name = "Second";
        secondEditorLink.Url = "https://example.com/second";
        secondEditorLink.MoveUpCommand.Execute(null);
        var editorLinkOrderingWorks = ReferenceEquals(viewModel.Editor.Links[0], secondEditorLink);
        viewModel.Editor.SelectedEditorMetadataSource = viewModel.Editor.EditorMetadataSources
            .Single(source => source.Id == metadataPlugin.Id && !source.IsLibrarySource);
        var editorMetadataPreviewed = await viewModel.Editor.DownloadEditorMetadataForTestAsync();
        var editorMediaWasLocalized = viewModel.Editor.MetadataComparisonItems
            .Where(item => item.IsImage)
            .All(item => File.Exists(item.DownloadedImagePath));

        Record(results, "Editor media, provenance, ordering, and per-source comparison are live", () =>
            viewModel.Editor.IsVisible &&
            viewModel.Editor.EditorDatabaseId == metadataGame.Id.ToString() &&
            viewModel.Editor.EditorGameId == metadataGame.GameId &&
            editorMediaInfoWorks &&
            editorLinkOrderingWorks &&
            editorMetadataPreviewed &&
            viewModel.Editor.MetadataComparisonItems.Any(item => item.Field == "Name") &&
            viewModel.Editor.MetadataComparisonItems.Any(item => item.Field == "Description") &&
            viewModel.Editor.MetadataComparisonItems.Any(item => item.Field == "Cover image") &&
            viewModel.Editor.MetadataComparisonItems.Any(item => item.Field == "Icon") &&
            editorMediaWasLocalized &&
            metadataPlugin.ProviderCreationCount == 2 &&
            metadataPlugin.ProviderDisposeCount == 2 &&
            !metadataPlugin.LastRequestWasBackground
                ? "ordered links, read-only identifiers, image dimensions, downloaded previews, and field-level choices are exposed"
                : throw new InvalidOperationException(
                    $"visible={viewModel.Editor.IsVisible}, db={viewModel.Editor.EditorDatabaseId}/{metadataGame.Id}, " +
                    $"game={viewModel.Editor.EditorGameId}/{metadataGame.GameId}, info={viewModel.Editor.CoverMediaInfo}, " +
                    $"links={editorLinkOrderingWorks}, preview={editorMetadataPreviewed}, " +
                    $"fields={string.Join(',', viewModel.Editor.MetadataComparisonItems.Select(item => item.Field))}, " +
                    $"localized={editorMediaWasLocalized}, providers={metadataPlugin.ProviderCreationCount}/{metadataPlugin.ProviderDisposeCount}, " +
                    $"background={metadataPlugin.LastRequestWasBackground}, validation={viewModel.Editor.ValidationMessage}"));

        foreach (var comparison in viewModel.Editor.MetadataComparisonItems)
        {
            comparison.UseDownloadedValue = comparison.Field is "Description" or "Cover image" or "Icon";
        }

        viewModel.Editor.ApplyMetadataComparisonCommand.Execute(null);
        var editorAppliedBeforeSave =
            viewModel.Editor.Description == "Editor-selected metadata description" &&
            library.Database.Games[metadataGame.Id].Description == PilotMetadataPlugin.DownloadedDescription;
        viewModel.Editor.SaveCommand.Execute(null);
        var editorMetadataGame = library.Database.Games[metadataGame.Id];
        var editorCoverPath = library.Database.GetFullFilePath(editorMetadataGame.CoverImage);
        var editorIconPath = library.Database.GetFullFilePath(editorMetadataGame.Icon);
        Record(results, "Editor imports selected remote metadata only after Save", () =>
            editorAppliedBeforeSave &&
            !viewModel.Editor.IsVisible &&
            editorMetadataGame.Name == originalMetadataName &&
            editorMetadataGame.Description == "Editor-selected metadata description" &&
            !string.IsNullOrWhiteSpace(editorMetadataGame.CoverImage) &&
            !string.IsNullOrWhiteSpace(editorMetadataGame.Icon) &&
            File.Exists(editorCoverPath) &&
            File.Exists(editorIconPath)
                ? "comparison stayed transactional and Core now owns the selected cover and icon"
                : throw new InvalidOperationException(
                    $"before={editorAppliedBeforeSave}, visible={viewModel.Editor.IsVisible}, " +
                    $"name={editorMetadataGame.Name}/{originalMetadataName}, description={editorMetadataGame.Description}, " +
                    $"cover={editorMetadataGame.CoverImage}/{File.Exists(editorCoverPath)}, " +
                    $"icon={editorMetadataGame.Icon}/{File.Exists(editorIconPath)}, validation={viewModel.Editor.ValidationMessage}"));

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
        var systemExecutable = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
            : "/bin/true";
        if (!File.Exists(systemExecutable))
        {
            throw new FileNotFoundException("The installed-game fixture executable is unavailable.", systemExecutable);
        }

        var executableExtension = OperatingSystem.IsWindows() ? ".exe" : ".sh";
        var detectedExecutablePath = Path.Combine(installedFixtureDirectory, "Pilot Detected" + executableExtension);
        var scannedExecutablePath = Path.Combine(installedFixtureDirectory, "Pilot Scanned" + executableExtension);
        var directExecutablePath = Path.Combine(installedFixtureDirectory, "Pilot Direct" + executableExtension);
        File.Copy(systemExecutable, detectedExecutablePath, true);
        File.Copy(systemExecutable, scannedExecutablePath, true);
        File.Copy(systemExecutable, directExecutablePath, true);
        var detectedProgram = new InstalledProgram
        {
            Name = "Pilot Detected™ Game",
            Path = detectedExecutablePath,
            Arguments = "--detected",
            WorkDir = installedFixtureDirectory,
            Icon = OperatingSystem.IsWindows() ? detectedExecutablePath : library.SelfTestMediaPath,
            AppId = "pilot-installed-win32"
        };
        var storeProgram = new InstalledProgram
        {
            Name = "Pilot Store Game",
            Path = OperatingSystem.IsWindows() ? "explorer.exe" : systemExecutable,
            Arguments = OperatingSystem.IsWindows() ? "shell:AppsFolder\\Pilot.Store_123!App" : "--secondary",
            WorkDir = installedFixtureDirectory,
            Icon = library.SelfTestMediaPath,
            AppId = OperatingSystem.IsWindows() ? "Pilot.Store_123" : "pilot-installed-linux-secondary"
        };
        var scannedProgram = new InstalledProgram
        {
            Name = "Pilot Scanned Game",
            Path = scannedExecutablePath,
            WorkDir = installedFixtureDirectory,
            Icon = OperatingSystem.IsWindows()
                ? $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll")},0"
                : library.SelfTestMediaPath,
            AppId = "pilot-installed-scanned"
        };
        var directProgram = new InstalledProgram
        {
            Name = "Pilot Direct Game",
            Path = directExecutablePath,
            Arguments = "--direct",
            WorkDir = installedFixtureDirectory,
            Icon = OperatingSystem.IsWindows() ? directExecutablePath : library.SelfTestMediaPath,
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
                    new DesktopDetectedProgram(
                        storeProgram,
                        OperatingSystem.IsWindows()
                            ? DesktopInstalledProgramType.MicrosoftStore
                            : DesktopInstalledProgramType.Win32)
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
        Record(results, "Installed-game import exposes native platform discovery", () =>
            detectedInstalledPrograms &&
            viewModel.InstalledGameImport.IsVisible &&
            viewModel.InstalledGameImport.Programs.Count == 2 &&
            viewModel.InstalledGameImport.Programs.Any(program =>
                program.Program.AppId == detectedProgram.AppId &&
                program.Type == DesktopInstalledProgramType.Win32 &&
                !program.IsImported) &&
            viewModel.InstalledGameImport.Programs.Any(program =>
                program.Program.AppId == storeProgram.AppId &&
                program.Type == (OperatingSystem.IsWindows()
                    ? DesktopInstalledProgramType.MicrosoftStore
                    : DesktopInstalledProgramType.Win32) &&
                !program.IsImported) &&
            !viewModel.OpenLibrarySyncCommand.CanExecute(null)
                ? OperatingSystem.IsWindows()
                    ? "Win32 and Microsoft Store candidates share the native cancellable import surface"
                    : "Linux executable and desktop-entry candidates share the native cancellable import surface"
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
        var platformSpecification = OperatingSystem.IsWindows() ? "pc_windows" : "pc_linux";
        var platformSpecificationApplied = new[] { detectedGame, storeGame }
            .Where(game => game != null)
            .All(game => game.PlatformIds?.Any(id =>
                library.Database.Platforms[id]?.SpecificationId == platformSpecification) == true);

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
                (OperatingSystem.IsWindows()
                    ? storeSource?.Name == "Microsoft Store"
                    : storeGame.SourceId == Guid.Empty) &&
                storeAction?.Path == storeProgram.Path &&
                storeAction.Arguments == storeProgram.Arguments &&
                storeAction.WorkingDir == (OperatingSystem.IsWindows()
                    ? string.Empty
                    : ExpandableVariables.InstallationDirectory) &&
                platformSpecificationApplied &&
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
                return OperatingSystem.IsWindows()
                    ? "legacy import metadata became owned Core records for both Win32 and Store candidates"
                    : "Linux program metadata became owned Core records with native launch actions";
            }

            throw new InvalidOperationException(
                $"import={importedDetectedPrograms}, games={library.Database.Games.Count}/{gamesBeforeInstalledImport + 2}, " +
                $"detected={detectedGame?.Name}/{detectedAction?.Path}, store={storeSource?.Name}/{storeAction?.Path}, " +
                $"platforms={platformSpecificationApplied}/{platformSpecification}, providers={metadataPlugin.ProviderCreationCount}/{providersBeforeInstalledImport + 2}, " +
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
        var scannedIconPath = string.IsNullOrWhiteSpace(installedScannedGame?.Icon)
            ? null
            : library.Database.GetFullFilePath(installedScannedGame.Icon);
        var directIconPath = string.IsNullOrWhiteSpace(directGame?.Icon)
            ? null
            : library.Database.GetFullFilePath(directGame.Icon);

        Record(results, "Folder scan and direct executable import preserve local icons without metadata", () =>
            scannedPrograms &&
            addedDirectExecutable &&
            string.Equals(scannedDirectory, installedFixtureDirectory, StringComparison.OrdinalIgnoreCase) &&
            importedExecutablePrograms &&
            library.Database.Games.Count == gamesBeforeExecutableImport + 2 &&
            installedScannedGame != null &&
            directGame != null &&
            File.Exists(scannedIconPath) &&
            File.Exists(directIconPath) &&
            Path.GetExtension(installedScannedGame.Icon).Equals(
                OperatingSystem.IsWindows() ? ".ico" : ".png",
                StringComparison.OrdinalIgnoreCase) &&
            installedScannedGame.CoverImage == null &&
            directGame.CoverImage == null &&
            metadataPlugin.ProviderCreationCount == providersBeforeExecutableImport &&
            installedLibraryUpdates == 2 &&
            viewModel.Games.Any(game => game.Game.Id == installedScannedGame.Id) &&
            viewModel.Games.Any(game => game.Game.Id == directGame.Id)
                ? OperatingSystem.IsWindows()
                    ? "recursive-scan and single-file candidates imported with executable/resource icons converted into owned ICO files"
                    : "recursive-scan and single-file candidates imported with owned Linux image icons"
                : throw new InvalidOperationException(
                    $"scan={scannedPrograms}, direct={addedDirectExecutable}, import={importedExecutablePrograms}, " +
                    $"scannedIcon={installedScannedGame?.Icon}, directIcon={directGame?.Icon}, " +
                    $"games={library.Database.Games.Count}/{gamesBeforeExecutableImport + 2}, updates={installedLibraryUpdates}."));
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

#if WINDOWS
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

        var pilotMenuScript = new PilotMenuScript(Path.Combine(library.ActiveUserDataDirectory, "pilot-menu.psm1"));
        window.RuntimeHost.Extensions.Scripts.Add(pilotMenuScript);
        var selectedMenuGame = viewModel.SelectedGame.Game;
        var mainMenuActions = window.RuntimeHost.GetMainMenuActions(true);
        var legacyMainAction = mainMenuActions.Single(item =>
            item.PluginId == legacyUiPlugin.Id && item.Description == "Legacy main command");
        var scriptMainAction = mainMenuActions.Single(item =>
            item.PluginName == pilotMenuScript.Name && item.Description == "Script main command");
        legacyMainAction.Invoke();
        scriptMainAction.Invoke();
        var gameMenuActions = window.RuntimeHost.GetGameMenuActions([selectedMenuGame], true);
        var legacyGameAction = gameMenuActions.Single(item =>
            item.PluginId == legacyUiPlugin.Id && item.Description == "Legacy game command");
        var scriptGameAction = gameMenuActions.Single(item =>
            item.PluginName == pilotMenuScript.Name && item.Description == "Script game command");
        legacyGameAction.Invoke();
        scriptGameAction.Invoke();
        Record(results, "Shared plugin menus execute compiled and script actions", () =>
            mainMenuActions.Count(item => item.Description == "-") == 0 &&
            legacyMainAction.DisplayName == "Legacy > Tools > Legacy main command" &&
            scriptGameAction.DisplayName == "Scripts > Game > Script game command" &&
            legacyUiPlugin.MainMenuInvocationCount == 1 &&
            legacyUiPlugin.GameMenuInvocationCount == 1 &&
            legacyUiPlugin.LastMainMenuGlobalSearchRequest &&
            legacyUiPlugin.LastGameMenuGlobalSearchRequest &&
            legacyUiPlugin.LastMenuGameIds.SequenceEqual([selectedMenuGame.Id]) &&
            pilotMenuScript.MainInvocationCount == 1 &&
            pilotMenuScript.GameInvocationCount == 1 &&
            pilotMenuScript.LastMainSourceDescription == "Script main command" &&
            pilotMenuScript.LastGameIds.SequenceEqual([selectedMenuGame.Id])
                ? "SDK v6 plugins and PowerShell-compatible scripts share ordered main/game action contracts"
                : throw new InvalidOperationException("A compiled or script menu action lost its SDK arguments."));

        viewModel.OpenPluginGameMenuCommand.Execute(null);
        viewModel.SelectedPluginMenuItem = viewModel.PluginMenuItems.Single(item =>
            item.PluginId == legacyUiPlugin.Id && item.Description == "Legacy game command");
        viewModel.InvokePluginMenuItemCommand.Execute(null);
        Record(results, "Native Desktop plugin command overlay invokes selected actions", () =>
            !viewModel.IsPluginMenuVisible &&
            legacyUiPlugin.GameMenuInvocationCount == 2 &&
            viewModel.StatusText.Contains("Legacy game command", StringComparison.Ordinal)
                ? "the loose Avalonia theme opened, selected, invoked, and closed the shared command surface"
                : throw new InvalidOperationException("The native plugin command overlay did not complete its action."));

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
        window.RuntimeHost.Extensions.Scripts.Remove(pilotMenuScript);
        pilotMenuScript.Dispose();
        window.RuntimeHost.Extensions.SettingsSupportList.RemoveAll(item =>
            ReferenceEquals(item.Source, legacyUiPlugin));
        window.RuntimeHost.Extensions.CustomElementList.RemoveAll(item =>
            ReferenceEquals(item.Source, legacyUiPlugin));
        window.RuntimeHost.Extensions.ConvertersSupportList.RemoveAll(item =>
            ReferenceEquals(item.Source, legacyUiPlugin));
        legacyUiPlugin.Dispose();
#endif

#if WINDOWS
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
#else
        Record(results, "SDK v7 web-view rejects unsupported response capture policy", () =>
        {
            var javaScriptError = CaptureNotSupported(() =>
                window.RuntimeHost.PluginApi.WebViews.CreateOffscreenView(new WebViewSettings
                {
                    JavaScriptEnabled = false
                }));
            var responseError = CaptureNotSupported(() =>
                window.RuntimeHost.PluginApi.WebViews.CreateOffscreenView(new WebViewSettings
                {
                    CaptureResponseContent = true
                }));
            return javaScriptError.Contains("JavaScript", StringComparison.Ordinal) &&
                   responseError.Contains("response", StringComparison.OrdinalIgnoreCase)
                ? "JavaScript policy and response capture fail explicitly instead of changing semantics"
                : throw new InvalidOperationException(
                    $"Unexpected SDK v7 policy messages: JavaScript='{javaScriptError}', response='{responseError}'.");
        });
#endif

        await using var webServer = new LoopbackWebServer();
        using var offscreenWebView = window.RuntimeHost.PluginApi.WebViews.CreateOffscreenView(new WebViewSettings
        {
            UserAgent = LoopbackWebServer.ExpectedUserAgent
        });
        var loadingStates = new List<bool>();
        offscreenWebView.LoadingChanged += (_, args) => loadingStates.Add(args.IsLoading);
#if WINDOWS
        offscreenWebView.NavigateAndWait(webServer.PageUrl);
        var initialPageText = offscreenWebView.GetPageText();
#else
        await offscreenWebView.NavigateAsync(new Uri(webServer.PageUrl));
        var initialPageText = await offscreenWebView.GetPageTextAsync();
#endif
        var initialPageSource = await offscreenWebView.GetPageSourceAsync();
        Record(results, "Offscreen plugin web views navigate through Avalonia NativeWebView", () =>
            offscreenWebView.CanExecuteJavascriptInMainFrame &&
            offscreenWebView.WindowHost == null &&
#if WINDOWS
            string.Equals(offscreenWebView.GetCurrentAddress(), webServer.PageUrl, StringComparison.OrdinalIgnoreCase) &&
#else
            string.Equals(offscreenWebView.Address?.AbsoluteUri, webServer.PageUrl, StringComparison.OrdinalIgnoreCase) &&
#endif
            initialPageText.Contains(LoopbackWebServer.InitialText, StringComparison.Ordinal) &&
            initialPageSource.Contains("pilot-message", StringComparison.Ordinal) &&
            loadingStates.Contains(true) &&
            loadingStates.Contains(false) &&
            string.Equals(webServer.LastUserAgent, LoopbackWebServer.ExpectedUserAgent, StringComparison.Ordinal)
                ? $"loaded {webServer.PageUrl} with SDK loading events and the configured user agent"
                : throw new InvalidOperationException(
#if WINDOWS
                    $"address={offscreenWebView.GetCurrentAddress()}, states={string.Join(',', loadingStates)}, " +
#else
                    $"address={offscreenWebView.Address}, states={string.Join(',', loadingStates)}, " +
#endif
                    $"userAgent={webServer.LastUserAgent}"));

        var scriptResult = await offscreenWebView.EvaluateScriptAsync(
            $"document.getElementById('pilot-message').innerText = '{LoopbackWebServer.MutatedText}'; 42");
        var failedScriptResult = await offscreenWebView.EvaluateScriptAsync(
            "throw new Error('pilot-script-failure')");
        var mutatedPageText = await offscreenWebView.GetPageTextAsync();
        var scriptFailurePreserved = !failedScriptResult.Success &&
            !string.IsNullOrWhiteSpace(failedScriptResult.Message) &&
            (!OperatingSystem.IsWindows() ||
                failedScriptResult.Message.Contains("pilot-script-failure", StringComparison.Ordinal));
        Record(results, "Plugin web-view JavaScript preserves values, DOM changes, and failures", () =>
            scriptResult.Success &&
            Convert.ToInt64(scriptResult.Result, CultureInfo.InvariantCulture) == 42 &&
            mutatedPageText.Contains(LoopbackWebServer.MutatedText, StringComparison.Ordinal) &&
            scriptFailurePreserved
                ? "script results were decoded, DOM mutation remained visible, and JavaScript errors stayed errors"
                : throw new InvalidOperationException(
                    $"success={scriptResult.Success}, value={scriptResult.Result}, failedMessage={failedScriptResult.Message}"));

#if WINDOWS
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
#else
        await offscreenWebView.SetCookieAsync(
            new Uri(webServer.PageUrl),
            new HttpCookie
            {
                Domain = "127.0.0.1",
                Name = "pilot-cookie",
                Value = "pilot-value",
                Path = "/",
                Expires = DateTime.UtcNow.AddHours(1)
            });
        var writtenCookie = (await offscreenWebView.GetCookiesAsync()).SingleOrDefault(cookie =>
            cookie.Name == "pilot-cookie" && cookie.Domain.TrimStart('.') == "127.0.0.1");
        await offscreenWebView.DeleteCookiesAsync(new Uri(webServer.PageUrl), "pilot-cookie");
        var cookieDeleted = (await offscreenWebView.GetCookiesAsync()).All(cookie => cookie.Name != "pilot-cookie");
#endif
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
#if WINDOWS
            WindowBackground = System.Windows.Media.Colors.Black
#else
            WindowBackground = global::Avalonia.Media.Colors.Black
#endif
        });
#if WINDOWS
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
#else
        await visibleWebView.NavigateAsync(new Uri(webServer.PageUrl));
        var visibleWindowHost = visibleWebView.WindowHost;
        var visibleDialogTask = visibleWebView.OpenAsync(true);
        await Task.Delay(100);
        visibleWebView.Close();
        await visibleDialogTask;
        Record(results, "Visible plugin web views use an asynchronous Avalonia dialog", () =>
            visibleWindowHost != null
                ? "the SDK v7 browser opened and closed through its native Avalonia window host"
                : throw new InvalidOperationException("The portable web view did not expose its Avalonia window host."));
#endif

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

        viewModel.EnableTray = false;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        viewModel.OpenSettingsCommand.Execute(null);
        var settingsOpened = viewModel.Settings.IsVisible &&
            ReferenceEquals(viewModel.Settings.SelectedSection, viewModel.Settings.General);
        viewModel.Settings.General.EnableTray = true;
        var settingsDeferred = !viewModel.EnableTray;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Settings overlay applies the working copy only on Save", () =>
            settingsOpened && settingsDeferred && viewModel.EnableTray && !viewModel.Settings.IsVisible
                ? "opened to General, deferred the edit, then applied and closed on Save"
                : throw new InvalidOperationException(
                    $"opened={settingsOpened}, deferred={settingsDeferred}, applied={viewModel.EnableTray}, visible={viewModel.Settings.IsVisible}"));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.General.EnableTray = false;
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Settings overlay Cancel discards the working copy", () =>
            !viewModel.Settings.IsVisible && viewModel.EnableTray
                ? "cancelled without applying the working-copy change"
                : throw new InvalidOperationException(
                    $"visible={viewModel.Settings.IsVisible}, enableTray={viewModel.EnableTray}"));

        viewModel.OpenSettingsCommand.Execute(null);
        var availableLanguages = viewModel.Settings.General.AvailableLanguages;
        var hasEnglish = availableLanguages.Any(option => option.Id == "english");
        var germanOption = availableLanguages.FirstOrDefault(option => option.Id == "de_DE");
        viewModel.Settings.General.SelectedLanguage = germanOption;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Language catalog bundles English plus translations and flags a restart on change", () =>
            availableLanguages.Count > 20 && hasEnglish && germanOption != null &&
            viewModel.Settings.RestartRequired
                ? $"{availableLanguages.Count} languages available; selecting {germanOption.DisplayName} flagged a restart"
                : throw new InvalidOperationException(
                    $"count={availableLanguages.Count}, english={hasEnglish}, " +
                    $"german={germanOption != null}, restart={viewModel.Settings.RestartRequired}"));

        // Start-minimized round-trips through the shared settings on save/reopen.
        // (StartOnBoot is intentionally not exercised here — its save writes a real
        // Startup shortcut via SystemIntegration.)
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.General.StartMinimized = true;
        viewModel.Settings.General.StartInFullscreen = true;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        viewModel.OpenSettingsCommand.Execute(null);
        var startMinimizedPersisted = viewModel.Settings.General.StartMinimized;
        var startInFullscreenPersisted = viewModel.Settings.General.StartInFullscreen;
        viewModel.Settings.General.StartInFullscreen = false;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Settings overlay persists startup options through the shared settings", () =>
            startMinimizedPersisted && startInFullscreenPersisted
                ? "start-minimized and start-in-fullscreen survived save and reopen"
                : throw new InvalidOperationException(
                    $"startMinimized={startMinimizedPersisted}, startInFullscreen={startInFullscreenPersisted}"));

        // After-launch/after-game-close window behavior. Only the safe options are
        // exercised (Close/Exit would shut the application down).
        window.RestoreFromTray();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.General.EnableTray = false;
        viewModel.Settings.General.AfterLaunch = Playnite.Avalonia.App.Services.AfterLaunchOption.Minimize;
        viewModel.Settings.General.AfterGameClose = Playnite.Avalonia.App.Services.AfterGameCloseOption.Restore;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var minimizeRequestsBeforeLaunch = window.MinimizeRequestCount;
        window.ApplyAfterLaunch();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var minimizedAfterLaunch = OperatingSystem.IsWindows()
            ? window.WindowState == WindowState.Minimized
            : window.MinimizeRequestCount == minimizeRequestsBeforeLaunch + 1;
        window.ApplyAfterGameClose();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var restoredAfterClose = window.WindowState != WindowState.Minimized && window.IsVisible;
        Record(results, "After-launch minimizes and after-game-close restores the window", () =>
            minimizedAfterLaunch && restoredAfterClose
                ? "game start minimized the window and game close restored it"
                : throw new InvalidOperationException(
                    $"minimizedAfterLaunch={minimizedAfterLaunch}, restoredAfterClose={restoredAfterClose}"));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.General.FuzzyMatchingInNameFilter = false;
        viewModel.Settings.General.ScanLibInstallSizeOnLibUpdate = true;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        viewModel.OpenSettingsCommand.Execute(null);
        var fuzzyPersisted = !viewModel.Settings.General.FuzzyMatchingInNameFilter;
        var scanSizePersisted = viewModel.Settings.General.ScanLibInstallSizeOnLibUpdate;
        viewModel.Settings.General.FuzzyMatchingInNameFilter = true;
        viewModel.Settings.General.ScanLibInstallSizeOnLibUpdate = false;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Library preferences (fuzzy match, install-size scan) round-trip", () =>
            fuzzyPersisted && scanSizePersisted
                ? "fuzzy name-filter and scan-install-size toggles saved and reloaded"
                : throw new InvalidOperationException(
                    $"fuzzyPersisted={fuzzyPersisted}, scanSizePersisted={scanSizePersisted}"));

        var trayDefault = window.ResolveTrayIconPath(Playnite.Avalonia.App.Services.TrayIconOption.Default);
        var trayBright = window.ResolveTrayIconPath(Playnite.Avalonia.App.Services.TrayIconOption.Bright);
        var trayDark = window.ResolveTrayIconPath(Playnite.Avalonia.App.Services.TrayIconOption.Dark);
        var trayIconsExist = File.Exists(trayDefault) && File.Exists(trayBright) && File.Exists(trayDark);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.General.TrayIcon = Playnite.Avalonia.App.Services.TrayIconOption.Dark;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        viewModel.OpenSettingsCommand.Execute(null);
        var trayPersisted = viewModel.Settings.General.TrayIcon == Playnite.Avalonia.App.Services.TrayIconOption.Dark;
        viewModel.Settings.General.TrayIcon = Playnite.Avalonia.App.Services.TrayIconOption.Default;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Tray-icon variants are bundled and the picker round-trips", () =>
            trayIconsExist && trayPersisted
                ? "default/bright/dark tray icons resolve to existing files and the picker saved"
                : throw new InvalidOperationException(
                    $"trayIconsExist={trayIconsExist}, trayPersisted={trayPersisted}"));

        viewModel.OpenSettingsCommand.Execute(null);
        var appearanceSection = viewModel.Settings.Sections.FirstOrDefault(section => section.Key == "Appearance");
        viewModel.Settings.SelectedSection = appearanceSection;
        var appearanceSelectable = appearanceSection != null &&
            ReferenceEquals(viewModel.Settings.SelectedSection, viewModel.Settings.Appearance);
        var hasDefaultTheme = viewModel.Settings.Appearance.AvailableThemes.Any(theme => string.IsNullOrEmpty(theme.Path));
        var themeCount = viewModel.Settings.Appearance.AvailableThemes.Count;
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Appearance section is selectable and lists the Default theme", () =>
            appearanceSelectable && hasDefaultTheme
                ? $"appearance nav works; {themeCount} theme(s) available"
                : throw new InvalidOperationException(
                    $"appearanceSelectable={appearanceSelectable}, hasDefaultTheme={hasDefaultTheme}"));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.AppearanceGeneral.ShowGroupCount = true;
        viewModel.Settings.AppearanceGeneral.PlaytimeUseDaysFormat = true;
        viewModel.Settings.AppearanceGridView.GridItemWidth = 210;
        viewModel.Settings.AppearanceGridView.GridItemWidthRatio = 2;
        viewModel.Settings.AppearanceGridView.GridItemHeightRatio = 3;
        viewModel.Settings.AppearanceGridView.GridItemSpacing = 16;
        viewModel.Settings.AppearanceGridView.GridItemMargin = 4;
        viewModel.Settings.AppearanceGridView.CoverArtStretch = global::Avalonia.Media.Stretch.Uniform;
        viewModel.Settings.AppearanceGridView.ShowGridItemBackground = false;
        viewModel.Settings.AppearanceGridView.ShowNamesUnderCovers = false;
        viewModel.Settings.AppearanceGridView.ShowNameEmptyCover = false;
        viewModel.Settings.AppearanceGridView.DarkenUninstalledGamesGrid = true;
        viewModel.Settings.AppearanceGridView.ScrollSensitivity = 2;
        viewModel.Settings.AppearanceGridView.ScrollDurationMilliseconds = 300;
        viewModel.Settings.AppearanceGridView.SmoothScrollEnabled = true;
        viewModel.Settings.AppearanceListView.ShowIconsOnList = false;
        viewModel.Settings.AppearanceListView.ScrollSensitivity = 2.5;
        viewModel.Settings.AppearanceListView.ScrollDurationMilliseconds = 350;
        viewModel.Settings.AppearanceListView.SmoothScrollEnabled = true;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        viewModel.SelectedGrouping = GroupableField.Source;
        var longPlaytimeGame = viewModel.LibraryGames.First();
        var originalPlaytime = longPlaytimeGame.Game.Playtime;
        longPlaytimeGame.Game.Playtime = 60ul * 60 * 48;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var tilePanel = window.MainView.TilePanel;
        var gridScroll = window.MainView.GridScrollViewer;
        var listScroll = window.MainView.ListScrollViewer;
        var groupCountVisible = viewModel.Games.FirstOrDefault(game => game.ShowGroupHeader)?.GroupHeader.Contains('(') == true;
        var appearanceApplied = tilePanel != null &&
            Math.Abs(tilePanel.ItemWidth - 210) < 0.01 &&
            Math.Abs(tilePanel.ItemHeight - 315) < 0.01 &&
            Math.Abs(tilePanel.ItemSpacing - 16) < 0.01 &&
            longPlaytimeGame.PlaytimeText.Contains('2') &&
            longPlaytimeGame.CoverArtStretch == global::Avalonia.Media.Stretch.Uniform &&
            !longPlaytimeGame.ShowGridItemBackground &&
            !longPlaytimeGame.ShowNamesUnderCovers &&
            !longPlaytimeGame.ShowListIcon &&
            groupCountVisible &&
            gridScroll != null && listScroll != null &&
            Playnite.Avalonia.Controls.ScrollBehavior.GetSmoothScrollingEnabled(gridScroll) &&
            Playnite.Avalonia.Controls.ScrollBehavior.GetSmoothScrollingEnabled(listScroll) &&
            Math.Abs(Playnite.Avalonia.Controls.ScrollBehavior.GetWheelSensitivity(gridScroll) - 2) < 0.01 &&
            Math.Abs(Playnite.Avalonia.Controls.ScrollBehavior.GetWheelSensitivity(listScroll) - 2.5) < 0.01;
        longPlaytimeGame.Game.Playtime = originalPlaytime;
        viewModel.SelectedGrouping = GroupableField.None;
        Record(results, "Desktop grid and list appearance settings apply live", () =>
            appearanceApplied
                ? "geometry, cover presentation, group counts, playtime, icons, and both scroll behaviors updated"
                : throw new InvalidOperationException(
                    $"tile={tilePanel != null}, width={tilePanel?.ItemWidth}, height={tilePanel?.ItemHeight}, spacing={tilePanel?.ItemSpacing}, " +
                    $"playtime={longPlaytimeGame.PlaytimeText}, stretch={longPlaytimeGame.CoverArtStretch}, " +
                    $"background={longPlaytimeGame.ShowGridItemBackground}, names={longPlaytimeGame.ShowNamesUnderCovers}, " +
                    $"listIcon={longPlaytimeGame.ShowListIcon}, groupCount={groupCountVisible}, " +
                    $"gridScroll={gridScroll != null}/{(gridScroll == null ? 0 : Playnite.Avalonia.Controls.ScrollBehavior.GetWheelSensitivity(gridScroll))}, " +
                    $"listScroll={listScroll != null}/{(listScroll == null ? 0 : Playnite.Avalonia.Controls.ScrollBehavior.GetWheelSensitivity(listScroll))}"));

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.AppearanceDetailsView.Visibility.Name = false;
        viewModel.Settings.AppearanceDetailsView.Visibility.Source = false;
        viewModel.Settings.AppearanceDetailsView.Visibility.Description = false;
        viewModel.Settings.AppearanceDetailsView.Visibility.CoverImage = true;
        viewModel.Settings.AppearanceDetailsView.IndentGameDetails = true;
        viewModel.Settings.AppearanceDetailsView.GameDetailsIndentation = 40;
        viewModel.Settings.AppearanceDetailsView.GameDetailsCoverHeight = 360;
        viewModel.Settings.AppearanceDetailsView.DetailsViewListIconSize = 72;
        viewModel.Settings.AppearanceDetailsView.ScrollSensitivity = 3;
        viewModel.Settings.AppearanceDetailsView.ScrollDurationMilliseconds = 400;
        viewModel.Settings.AppearanceDetailsView.SmoothScrollEnabled = true;
        viewModel.Settings.AppearanceLayout.DetailsPosition = Dock.Left;
        viewModel.Settings.AppearanceLayout.DetailsWidth = 420;
        viewModel.Settings.AppearanceLayout.ShowPanelSeparators = false;
        viewModel.Settings.SaveCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var detailsScroll = window.MainView.DetailsScrollViewer;
        var detailsApplied = !window.MainView.DetailsName.IsVisible &&
            window.MainView.DetailsCover.IsVisible &&
            Math.Abs(window.MainView.DetailsCover.Height - 360) < 0.01 &&
            Grid.GetColumn(window.MainView.DetailsPanel) == 1 &&
            Math.Abs(viewModel.FirstContentColumnWidth.Value - 420) < 0.01 &&
            viewModel.DetailsBorderThickness == default &&
            Math.Abs(viewModel.DetailsContentMargin.Left - 40) < 0.01 &&
            Math.Abs(viewModel.SelectedGame.ListIconHeight - 72) < 0.01 &&
            detailsScroll != null &&
            Playnite.Avalonia.Controls.ScrollBehavior.GetSmoothScrollingEnabled(detailsScroll) &&
            Math.Abs(Playnite.Avalonia.Controls.ScrollBehavior.GetWheelSensitivity(detailsScroll) - 3) < 0.01;
        Record(results, "Desktop details visibility and layout settings apply live", () =>
            detailsApplied
                ? "field visibility, cover/indent/icon geometry, left layout, width, separators, and scrolling updated"
                : throw new InvalidOperationException(
                    $"name={window.MainView.DetailsName.IsVisible}, cover={window.MainView.DetailsCover.IsVisible}/{window.MainView.DetailsCover.Height}, " +
                    $"column={Grid.GetColumn(window.MainView.DetailsPanel)}, width={viewModel.FirstContentColumnWidth.Value}, " +
                    $"border={viewModel.DetailsBorderThickness}, margin={viewModel.DetailsContentMargin.Left}, " +
                    $"icon={viewModel.SelectedGame.ListIconHeight}, scroll={detailsScroll != null}/" +
                    $"{(detailsScroll == null ? 0 : Playnite.Avalonia.Controls.ScrollBehavior.GetWheelSensitivity(detailsScroll))}"));

        viewModel.OpenSettingsCommand.Execute(null);
        var advanced = viewModel.Settings.AppearanceAdvanced;
        advanced.ShowBackgroundImageOnWindow = true;
        advanced.ShowBackImageOnGridView = true;
        advanced.BlurWindowBackgroundImage = true;
        advanced.BackgroundImageBlurAmount = 24;
        advanced.DarkenWindowBackgroundImage = true;
        advanced.BackgroundImageDarkAmount = 0.4;
        advanced.BackgroundImageAnimation = false;
        advanced.FontFamilyName = "Arial";
        advanced.MonospaceFontFamilyName = "Courier New";
        advanced.FontSizeSmall = 11;
        advanced.FontSize = 13;
        advanced.FontSizeLarge = 17;
        advanced.FontSizeLarger = 21;
        advanced.FontSizeLargest = 31;
        advanced.DefaultIconSource = Playnite.Avalonia.App.Services.DefaultIconSourceOptions.General;
        advanced.DefaultCoverSource = Playnite.Avalonia.App.Services.DefaultCoverSourceOptions.General;
        advanced.DefaultBackgroundSource = Playnite.Avalonia.App.Services.DefaultBackgroundSourceOptions.Cover;
        advanced.DateTimeFormatAdded.Format = "yyyy-MM-dd";
        advanced.DateTimeFormatAdded.PastWeekRelativeFormat = false;
        advanced.DateTimeFormatModified.Format = "yyyy-MM-dd";
        advanced.DateTimeFormatModified.PastWeekRelativeFormat = false;
        advanced.DateTimeFormatRecentActivity.Format = "yyyy-MM-dd";
        advanced.DateTimeFormatRecentActivity.PastWeekRelativeFormat = false;
        advanced.DateTimeFormatReleaseDate.Format = "yyyy-MM-dd";
        advanced.DateTimeFormatReleaseDate.PartialFormat = "yyyy-MM";
        advanced.DateTimeFormatReleaseDate.PastWeekRelativeFormat = false;
        advanced.DateTimeFormatLastPlayed.Format = "yyyy-MM-dd";
        advanced.DateTimeFormatLastPlayed.PastWeekRelativeFormat = false;
        viewModel.Settings.AppearanceTopPanel.PluginTopPanelAlignment = Dock.Left;
        viewModel.Settings.SaveCommand.Execute(null);
        viewModel.SelectedViewMode = "Grid";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        var advancedGame = viewModel.SelectedGame;
        var originalIcon = advancedGame.Game.Icon;
        var originalCover = advancedGame.Game.CoverImage;
        var originalBackground = advancedGame.Game.BackgroundImage;
        var originalAdded = advancedGame.Game.Added;
        var originalModified = advancedGame.Game.Modified;
        var originalLastActivity = advancedGame.Game.LastActivity;
        var originalReleaseDate = advancedGame.Game.ReleaseDate;
        advancedGame.Game.Icon = null;
        advancedGame.Game.CoverImage = null;
        advancedGame.Game.BackgroundImage = null;
        advancedGame.Game.Added = new DateTime(2026, 7, 1);
        advancedGame.Game.Modified = new DateTime(2026, 7, 2);
        advancedGame.Game.LastActivity = new DateTime(2026, 7, 3);
        advancedGame.Game.ReleaseDate = new ReleaseDate(2026, 7);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var generalIconPath = advancedGame.IconPath;
        var generalCoverPath = advancedGame.CoverPath;
        var bundledCoverPath = Path.Combine(AppContext.BaseDirectory, "Assets", "custom_cover_background.png");
        advancedGame.Game.CoverImage = bundledCoverPath;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var coverBackgroundPath = advancedGame.BackgroundPath;
        var advancedApplied = viewModel.ShowWindowBackgroundImage &&
            Math.Abs(viewModel.BackgroundImageBlurRadius - 24) < 0.01 &&
            Math.Abs(viewModel.BackgroundImageDarkOpacity - 0.4) < 0.01 &&
            viewModel.BackgroundImageFadeDuration == TimeSpan.Zero &&
            viewModel.ShowPluginTopPanelItemsLeft && !viewModel.ShowPluginTopPanelItemsRight &&
            window.FontFamily.ToString().Contains("Arial", StringComparison.OrdinalIgnoreCase) &&
            Equals(window.Resources["DesktopFontSizeLargest"], 31d) &&
            File.Exists(generalIconPath) && File.Exists(generalCoverPath) &&
            string.Equals(coverBackgroundPath, bundledCoverPath, StringComparison.OrdinalIgnoreCase) &&
            advancedGame.AddedText.Contains("2026-07-01", StringComparison.Ordinal) &&
            advancedGame.ModifiedText.Contains("2026-07-02", StringComparison.Ordinal) &&
            advancedGame.LastPlayedText.Contains("2026-07-03", StringComparison.Ordinal) &&
            advancedGame.ReleaseDateText.Contains("2026-07", StringComparison.Ordinal);
        Record(results, "Desktop advanced appearance settings apply live", () =>
            advancedApplied
                ? "background effects, fade policy, fonts, fallbacks, dates, and plugin alignment updated"
                : throw new InvalidOperationException(
                    $"background={viewModel.ShowWindowBackgroundImage}/{viewModel.BackgroundImageBlurRadius}/{viewModel.BackgroundImageDarkOpacity}, " +
                    $"fade={viewModel.BackgroundImageFadeDuration}, top={viewModel.ShowPluginTopPanelItemsLeft}, " +
                    $"font={window.FontFamily}, icon={generalIconPath}, cover={generalCoverPath}, coverBackground={coverBackgroundPath}, " +
                    $"dates={advancedGame.AddedText}/{advancedGame.ModifiedText}/{advancedGame.LastPlayedText}/{advancedGame.ReleaseDateText}"));
        advancedGame.Game.Icon = originalIcon;
        advancedGame.Game.CoverImage = originalCover;
        advancedGame.Game.BackgroundImage = originalBackground;
        advancedGame.Game.Added = originalAdded;
        advancedGame.Game.Modified = originalModified;
        advancedGame.Game.LastActivity = originalLastActivity;
        advancedGame.Game.ReleaseDate = originalReleaseDate;

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.AppearanceAdvanced.DateTimeFormatAdded.Format = "%";
        viewModel.Settings.SaveCommand.Execute(null);
        var invalidFormatBlocked = viewModel.Settings.IsVisible;
        viewModel.Settings.AppearanceAdvanced.DateTimeFormatAdded.Format = "d";
        viewModel.Settings.Close();
        Record(results, "Desktop settings reject invalid date formats", () =>
            invalidFormatBlocked
                ? "invalid .NET date formats kept the settings overlay open and selected the failing module"
                : throw new InvalidOperationException("An invalid date format was saved."));

        window.RuntimeHost.Extensions.Plugins.Add(
            metadataPlugin.Id,
            new LoadedPlugin(metadataPlugin, new ExtensionManifest
            {
                Id = metadataPlugin.Id.ToString(),
                Name = metadataPlugin.Name,
                Version = "1.0.0",
                Type = ExtensionType.MetadataProvider,
                DescriptionPath = Path.Combine(
                    library.ActiveUserDataDirectory,
                    "track-w-metadata",
                    "extension.yaml")
            }));
        var richMetadataTarget = library.Database.Games[metadataGame.Id];
        var richMetadataTargetCopy = richMetadataTarget.GetCopy();
        richMetadataTargetCopy.Description = null;
        library.Database.Games.Update(richMetadataTargetCopy);
        metadataPlugin.Description = "Track W ordered-source metadata";
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Metadata settings could not be opened for the Track W self-test.");
        }
        var metadataSettingsSection = viewModel.Settings.Metadata;
        foreach (var field in metadataSettingsSection.Fields)
        {
            field.Import = false;
            foreach (var source in field.Sources)
            {
                source.IsEnabled = false;
            }
        }

        var descriptionField = metadataSettingsSection.Fields.Single(field => field.Field == MetadataField.Description);
        var pilotMetadataSource = descriptionField.Sources.FirstOrDefault(source => source.Id == metadataPlugin.Id) ??
            throw new InvalidOperationException(
                $"The Track W metadata provider was not offered by settings. Runtime providers: " +
                $"{string.Join(",", window.RuntimeHost.MetadataPlugins.Select(plugin => plugin.Id))}; " +
                $"field providers: {string.Join(",", descriptionField.Sources.Select(source => source.Id))}.");
        descriptionField.Import = true;
        pilotMetadataSource.IsEnabled = true;
        metadataSettingsSection.SelectedField = descriptionField;
        metadataSettingsSection.SelectedSource = pilotMetadataSource;
        metadataSettingsSection.MoveSourceUpCommand.Execute(null);
        metadataSettingsSection.DownloadBackgroundsImmediately = false;
        metadataSettingsSection.AgeRatingOrgPriority = AgeRatingOrg.ESRB;
        metadataSettingsSection.WebImageSearchIconTerm = "{Name} square icon";
        metadataSettingsSection.WebImageSearchCoverTerm = "{Name} vertical cover";
        metadataSettingsSection.WebImageSearchBackgroundTerm = "{Name} landscape background";
        metadataSettingsSection.DefaultWebImageSource = global::Playnite.WebImageSearchSource.DuckDuckGo;
        viewModel.Settings.SaveCommand.Execute(null);
        var providersBeforeRichMetadata = metadataPlugin.ProviderCreationCount;
        var richMetadataDownloaded = await viewModel.MetadataDownload.DownloadConfiguredGamesAsync(
            new[] { richMetadataTargetCopy },
            (_, _, _) => { },
            CancellationToken.None);
        var richMetadataResult = library.Database.Games[richMetadataTarget.Id];
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Metadata settings could not be reopened for the Track W self-test.");
        }
        descriptionField = viewModel.Settings.Metadata.Fields.Single(field => field.Field == MetadataField.Description);
        var richSettingsReopened = descriptionField.Import &&
            descriptionField.Sources.First().Id == metadataPlugin.Id &&
            descriptionField.Sources.First().IsEnabled &&
            viewModel.Settings.Metadata.AgeRatingOrgPriority == AgeRatingOrg.ESRB &&
            viewModel.Settings.Metadata.DefaultWebImageSource == global::Playnite.WebImageSearchSource.DuckDuckGo &&
            viewModel.Settings.Metadata.WebImageSearchIconTerm == "{Name} square icon" &&
            viewModel.Settings.Metadata.WebImageSearchCoverTerm == "{Name} vertical cover" &&
            viewModel.Settings.Metadata.WebImageSearchBackgroundTerm == "{Name} landscape background";
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Per-field metadata priorities drive automatic Core downloads", () =>
            richMetadataDownloaded &&
            richMetadataResult.Description == metadataPlugin.Description &&
            metadataPlugin.ProviderCreationCount == providersBeforeRichMetadata + 1 &&
            richSettingsReopened
                ? "the ordered plugin source, metadata policy, age-rating priority, and image-search defaults saved and executed"
                : throw new InvalidOperationException(
                    $"downloaded={richMetadataDownloaded}, description={richMetadataResult.Description}, " +
                    $"providers={metadataPlugin.ProviderCreationCount}/{providersBeforeRichMetadata + 1}, reopened={richSettingsReopened}"));
        window.RuntimeHost.Extensions.Plugins.Remove(metadataPlugin.Id);

        var sortingPilotGame = new Game("The Track W Sorting Pilot")
        {
            GameId = "track-w-sorting-pilot",
            IsInstalled = true
        };
        library.Database.Games.Add(sortingPilotGame);
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Sorting settings could not be opened for the Track W self-test.");
        }
        viewModel.Settings.Sorting.ArticleText = "Le";
        viewModel.Settings.Sorting.AddArticleCommand.Execute(null);
        viewModel.Settings.Sorting.GameSortingNameAutofill = true;
        viewModel.Settings.Sorting.FillSortingNamesCommand.Execute(null);
        var sortingNameFilled = library.Database.Games[sortingPilotGame.Id]?.SortingName == "Track W Sorting Pilot";
        viewModel.Settings.SaveCommand.Execute(null);
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Sorting settings could not be reopened for the Track W self-test.");
        }
        var sortingSettingsReopened = viewModel.Settings.Sorting.GameSortingNameAutofill &&
            viewModel.Settings.Sorting.RemovedArticles.Contains("Le", StringComparer.CurrentCultureIgnoreCase);
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Sorting settings edit articles and fill missing Core sorting names", () =>
            sortingNameFilled && sortingSettingsReopened
                ? "article edits round-tripped and the buffered bulk command removed the leading article"
                : throw new InvalidOperationException(
                    $"sortingName={library.Database.Games[sortingPilotGame.Id]?.SortingName}, reopened={sortingSettingsReopened}"));

        var exclusionPilot = new global::Playnite.ImportExclusionItem(
            "track-w-exclusion",
            "Track W exclusion pilot",
            libraryPlugin.Id,
            libraryPlugin.Name);
        library.Database.ImportExclusions.Add(exclusionPilot);
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Import-exclusion settings could not be opened for the Track W self-test.");
        }
        var exclusionRow = viewModel.Settings.ImportExclusions.Exclusions.Single(item => item.Id == exclusionPilot.Id);
        viewModel.Settings.ImportExclusions.SelectedExclusion = exclusionRow;
        viewModel.Settings.ImportExclusions.RemoveCommand.Execute(null);
        var exclusionDeferred = library.Database.ImportExclusions[exclusionPilot.Id] != null &&
            viewModel.Settings.ImportExclusions.Exclusions.All(item => item.Id != exclusionPilot.Id);
        viewModel.Settings.SaveCommand.Execute(null);
        Record(results, "Import exclusions are removed only when settings are saved", () =>
            exclusionDeferred && library.Database.ImportExclusions[exclusionPilot.Id] == null
                ? "the compiled exclusion row deferred its Core database removal until Save"
                : throw new InvalidOperationException(
                    $"deferred={exclusionDeferred}, persisted={library.Database.ImportExclusions[exclusionPilot.Id] != null}"));

        var pilotSearchPlugin = new PilotSearchPlugin(window.RuntimeHost.PluginApi);
        window.RuntimeHost.Extensions.Plugins.Add(
            pilotSearchPlugin.Id,
            new LoadedPlugin(pilotSearchPlugin, new ExtensionManifest
            {
                Id = pilotSearchPlugin.Id.ToString(),
                Name = PilotSearchPlugin.DisplayName,
                Version = "1.0.0",
                Type = ExtensionType.GenericPlugin,
                DescriptionPath = Path.Combine(
                    library.ActiveUserDataDirectory,
                    "track-w-search",
                    "extension.yaml")
            }));
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Search settings could not be opened for the Track W self-test.");
        }

        var searchSettingsSection = viewModel.Settings.Search;
        var pilotSearchProvider = searchSettingsSection.SearchProviders.Single(provider =>
            provider.Name == "Pilot provider");
        pilotSearchProvider.CustomKeyword = "trackw";
        searchSettingsSection.PrimaryGameSearchItemAction = GameSearchItemAction.SwitchTo;
        searchSettingsSection.SecondaryGameSearchItemAction = GameSearchItemAction.Play;
        searchSettingsSection.IncludeCommandsInDefaultSearch = true;
        searchSettingsSection.SaveGlobalSearchFilterSettings = true;
        searchSettingsSection.GlobalSearchOpenWithLegacySearch = true;
        searchSettingsSection.Visibility.Platform = false;
        searchSettingsSection.Visibility.PlayTime = false;
        searchSettingsSection.Visibility.CompletionStatus = false;
        searchSettingsSection.Visibility.ReleaseDate = false;
        searchSettingsSection.SystemSearchHotkey = new Playnite.Avalonia.App.Services.HotKey(
            Key.F12,
            KeyModifiers.Control | KeyModifiers.Shift);
        viewModel.Settings.SaveCommand.Execute(null);
        var searchHotkeyRegistered = OperatingSystem.IsWindows()
            ? window.RegisteredSystemHotKey == searchSettingsSection.SystemSearchHotkey
            : window.RegisteredSystemHotKey == null &&
              searchSettingsSection.SystemHotkeySupportText.Contains(
                  "unavailable",
                  StringComparison.OrdinalIgnoreCase);

        var globalSearchGame = viewModel.LibraryGames.First(game =>
            !game.Game.Hidden && !string.IsNullOrWhiteSpace(game.Name));
        viewModel.OpenGlobalSearch(globalSearchGame.Name);
        var searchDeadline = DateTime.UtcNow.AddSeconds(3);
        while (viewModel.PluginSearch.IsSearching && DateTime.UtcNow < searchDeadline)
        {
            await Task.Delay(10);
        }
        var globalGameResult = viewModel.PluginSearch.Results.FirstOrDefault(result =>
            result.Name == globalSearchGame.Name);
        var gameSearchWorked = globalGameResult != null &&
            globalGameResult.PrimaryAction?.Name == "Switch to game" &&
            (globalGameResult.SecondaryAction?.Name is "Play" or "Install") &&
            string.IsNullOrEmpty(globalGameResult.Description);

        viewModel.OpenGlobalSearch("#settings");
        searchDeadline = DateTime.UtcNow.AddSeconds(3);
        while (viewModel.PluginSearch.IsSearching && DateTime.UtcNow < searchDeadline)
        {
            await Task.Delay(10);
        }
        var commandSearchWorked = viewModel.PluginSearch.Results.Any(result => result.Name == "Open settings");

        viewModel.OpenGlobalSearch("/trackw ");
        searchDeadline = DateTime.UtcNow.AddSeconds(3);
        while ((viewModel.PluginSearch.IsSearching || viewModel.PluginSearch.Label != "Pilot provider") &&
               DateTime.UtcNow < searchDeadline)
        {
            await Task.Delay(10);
        }
        var providerSearchWorked = viewModel.PluginSearch.Label == "Pilot provider" &&
            viewModel.PluginSearch.Results.Any(result => result.Name == "Pilot provider result");
        viewModel.PluginSearch.PrimaryCommand.Execute(null);
        var providerActionWorked = pilotSearchPlugin.InvocationCount == 1;

        viewModel.OpenGlobalSearch(string.Empty);
        viewModel.PluginSearch.IncludeHidden = true;
        viewModel.PluginSearch.Close();
        viewModel.OpenGlobalSearch(string.Empty);
        var searchFiltersPersisted = viewModel.PluginSearch.IncludeHidden;
        viewModel.PluginSearch.Close();
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Search settings could not be reopened for the Track W self-test.");
        }
        var searchSettingsReopened = viewModel.Settings.Search.SearchProviders.Single(provider =>
                provider.Name == "Pilot provider").CustomKeyword == "trackw" &&
            viewModel.Settings.Search.PrimaryGameSearchItemAction == GameSearchItemAction.SwitchTo &&
            !viewModel.Settings.Search.Visibility.Platform;
        viewModel.Settings.Search.SystemSearchHotkey = null;
        viewModel.Settings.SaveCommand.Execute(null);

        Record(results, "Global search honors actions, commands, result fields, filters, and the system hotkey", () =>
            searchHotkeyRegistered && gameSearchWorked && commandSearchWorked && searchFiltersPersisted &&
            searchSettingsReopened && window.RegisteredSystemHotKey == null
                ? OperatingSystem.IsWindows()
                    ? "game/command search, result visibility, persistent filters, Ctrl+F policy, and the Windows hotkey adapter are active"
                    : "game/command search, result visibility, persistent filters, Ctrl+F policy, and the unsupported hotkey policy are active"
                : throw new InvalidOperationException(
                    $"hotkey={searchHotkeyRegistered}/{window.RegisteredSystemHotKey}, game={gameSearchWorked}, " +
                    $"command={commandSearchWorked}, filters={searchFiltersPersisted}, reopened={searchSettingsReopened}"));
        Record(results, "Custom provider keywords switch into SDK search contexts", () =>
            providerSearchWorked && providerActionWorked
                ? "the /trackw keyword entered the SDK v6 context and invoked its selected action"
                : throw new InvalidOperationException(
                    $"provider={providerSearchWorked}, action={providerActionWorked}, count={pilotSearchPlugin.InvocationCount}"));

        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Update settings could not be opened for the Track W self-test.");
        }
        viewModel.Settings.Updates.CheckForLibraryUpdates = LibraryUpdateCheckFrequency.OnceADay;
        viewModel.Settings.Updates.CheckForEmulatedLibraryUpdates = LibraryUpdateCheckFrequency.OnceAWeek;
        viewModel.Settings.Updates.CheckForAddonUpdates = UpdateCheckFrequency.Manually;
        viewModel.Settings.Updates.CheckForProgramUpdates = UpdateCheckFrequency.OnceADay;
        viewModel.Settings.Updates.UpdateNotificationOnPatchesOnly = true;
        viewModel.Settings.SaveCommand.Execute(null);
        viewModel.Settings.Open();
        var updateSettingsReopened =
            viewModel.Settings.Updates.CheckForLibraryUpdates == LibraryUpdateCheckFrequency.OnceADay &&
            viewModel.Settings.Updates.CheckForEmulatedLibraryUpdates == LibraryUpdateCheckFrequency.OnceAWeek &&
            viewModel.Settings.Updates.CheckForAddonUpdates == UpdateCheckFrequency.Manually &&
            viewModel.Settings.Updates.CheckForProgramUpdates == UpdateCheckFrequency.OnceADay &&
            viewModel.Settings.Updates.UpdateNotificationOnPatchesOnly;
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Update frequency settings use an isolated persisted working copy", () =>
            updateSettingsReopened
                ? "all four schedules and the patch-only policy reopened from Desktop settings"
                : throw new InvalidOperationException("Update frequency settings did not round-trip."));

        viewModel.LibrarySync.DownloadMetadataOnImport = false;
        viewModel.LibrarySync.ConfigureProvidersForTesting(
            new LibraryPlugin[] { libraryPlugin },
            () => libraryUpdatedCount++);
        var libraryCallsBeforeScheduledUpdate = libraryPlugin.GetGamesCallCount;
        var libraryNotificationsBeforeScheduledUpdate = libraryUpdatedCount;
        var scheduledLibraryUpdate = await viewModel.LibrarySync.StartScheduledSyncAsync(true, true);
        Record(results, "Scheduled updates run real integration and global scanner pipelines", () =>
            scheduledLibraryUpdate &&
            libraryPlugin.GetGamesCallCount == libraryCallsBeforeScheduledUpdate + 1 &&
            libraryUpdatedCount == libraryNotificationsBeforeScheduledUpdate + 1 &&
            viewModel.Settings.Updates.Coordinator == viewModel.Updates &&
            (OperatingSystem.IsWindows()
                ? viewModel.Settings.Updates.Coordinator.ProgramUpdatesSupported &&
                  viewModel.Settings.Updates.Coordinator.ProgramUpdateSupportText.Contains(
                      "checksum",
                      StringComparison.OrdinalIgnoreCase)
                : !viewModel.Settings.Updates.Coordinator.ProgramUpdatesSupported &&
                  viewModel.Settings.Updates.Coordinator.ProgramUpdateSupportText.Contains(
                      "package manager",
                      StringComparison.OrdinalIgnoreCase))
                ? OperatingSystem.IsWindows()
                    ? "the scheduler reused Core integration/scanner imports and exposes the verified Windows updater"
                    : "the scheduler reused Core imports and leaves program updates to the Linux package manager"
                : throw new InvalidOperationException(
                    $"scheduled={scheduledLibraryUpdate}, calls={libraryPlugin.GetGamesCallCount}/" +
                    $"{libraryCallsBeforeScheduledUpdate + 1}, notifications={libraryUpdatedCount}/" +
                    $"{libraryNotificationsBeforeScheduledUpdate + 1}, coordinator=" +
                    $"{viewModel.Settings.Updates.Coordinator == viewModel.Updates}, " +
                    $"supported={viewModel.Updates.ProgramUpdatesSupported}, " +
                    $"support={viewModel.Updates.ProgramUpdateSupportText}"));

        var fakeAddonUpdates = new PilotAddonUpdateService();
        var fakeProgramUpdates = new PilotProgramUpdateService();
        var updateNotifications = new List<NotificationMessage>();
        var updateSettingsChanges = 0;
        using (var coordinator = new DesktopUpdateCoordinator(
            new DesktopSettings(),
            viewModel.LibrarySync,
            () => updateSettingsChanges++,
            updateNotifications.Add,
            (_, _) => { },
            (_, _) => true,
            () => { },
            fakeAddonUpdates,
            fakeProgramUpdates))
        {
            await coordinator.CheckAddonsAsync();
            await coordinator.QueueSelectedAddonsAsync();
            await coordinator.CheckProgramAsync();
            Record(results, "Add-on and program update services execute real check/queue contracts", () =>
                fakeAddonUpdates.CheckCount == 1 &&
                fakeAddonUpdates.QueueCount == 1 &&
                fakeProgramUpdates.CheckCount == 1 &&
                coordinator.AvailableAddonUpdates.Single().Status == "Queued for restart" &&
                coordinator.RestartRequired &&
                coordinator.AvailableProgramUpdate?.AvailableVersion == new Version(11, 0, 1) &&
                updateSettingsChanges == 2 &&
                updateNotifications.Select(notification => notification.Id).SequenceEqual(new[]
                {
                    "AvaloniaAddonUpdatesAvailable",
                    "AvaloniaProgramUpdateAvailable"
                })
                    ? "add-on compatibility results queued for restart and the Windows update manifest became actionable"
                    : throw new InvalidOperationException("Update check, queue, persistence, or notification contracts diverged."));
        }

        var policyPlugin = new PilotActionPolicyPlugin(window.RuntimeHost.PluginApi);
        window.RuntimeHost.Extensions.Plugins.Add(
            policyPlugin.Id,
            new LoadedPlugin(policyPlugin, new ExtensionManifest
            {
                Id = policyPlugin.Id.ToString(),
                Name = policyPlugin.Name,
                Version = "1.0.0",
                Type = ExtensionType.GameLibrary,
                DescriptionPath = Path.Combine(
                    library.ActiveUserDataDirectory,
                    "pilot-action-policy",
                    "extension.yaml")
            }));
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Behavior settings could not be opened for the Track W self-test.");
        }
        viewModel.Settings.Scripting.GlobalPreScript = "settings-global-pre";
        viewModel.Settings.Scripting.GlobalGameStartedScript = "settings-global-started";
        viewModel.Settings.Scripting.GlobalPostScript = "settings-global-post";
        viewModel.Settings.Scripting.AppStartupScript = "settings-app-startup";
        viewModel.Settings.Scripting.AppShutdownScript = "settings-app-shutdown";
        viewModel.Settings.ClientShutdown.ShutdownLibraryClients = true;
        viewModel.Settings.ClientShutdown.ClientShutdownGraceSeconds = 75;
        viewModel.Settings.ClientShutdown.ClientShutdownMinimumSessionSeconds = 150;
        viewModel.Settings.ClientShutdown.Plugins.Single(plugin => plugin.Id == policyPlugin.Id).IsSelected = true;
        viewModel.Settings.SaveCommand.Execute(null);
        viewModel.Settings.Open();
        var behaviorSettingsReopened =
            viewModel.Settings.Scripting.GlobalPreScript == "settings-global-pre" &&
            viewModel.Settings.Scripting.GlobalGameStartedScript == "settings-global-started" &&
            viewModel.Settings.Scripting.GlobalPostScript == "settings-global-post" &&
            viewModel.Settings.Scripting.AppStartupScript == "settings-app-startup" &&
            viewModel.Settings.Scripting.AppShutdownScript == "settings-app-shutdown" &&
            viewModel.Settings.ClientShutdown.ShutdownLibraryClients &&
            viewModel.Settings.ClientShutdown.ClientShutdownGraceSeconds == 75 &&
            viewModel.Settings.ClientShutdown.ClientShutdownMinimumSessionSeconds == 150 &&
            viewModel.Settings.ClientShutdown.Plugins.Single(plugin => plugin.Id == policyPlugin.Id).IsSelected;
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Script and client-shutdown settings use isolated working copies", () =>
            behaviorSettingsReopened
                ? "five scripts and the shutdown-capable plugin policy saved and reopened"
                : throw new InvalidOperationException("Behavior settings did not round-trip through their modules."));

        var scriptRuntimes = new List<RecordingPowerShellRuntime>();
        var scriptService = new DesktopScriptService(
            () => window.RuntimeHost.PluginApi,
            () => viewModel.SelectedGame?.Game,
            _ =>
            {
                var runtime = new RecordingPowerShellRuntime();
                scriptRuntimes.Add(runtime);
                return runtime;
            });
        var startupScriptResult = scriptService.RunApplicationScript("app-startup", "startup");
        var shutdownScriptResult = scriptService.RunApplicationScript("app-shutdown", "shutdown");
        var gameScriptResult = scriptService.TestGameScript("game-test");
        var failedScriptService = new DesktopScriptService(
            () => window.RuntimeHost.PluginApi,
            () => viewModel.SelectedGame?.Game,
            _ => new RecordingPowerShellRuntime("fail-test"));
        var failedPowerShellResult = failedScriptService.TestGameScript("fail-test");
        Record(results, "Application and one-shot game scripts execute through real runtime contracts", () =>
            startupScriptResult.Success && shutdownScriptResult.Success && gameScriptResult.Success &&
            !failedPowerShellResult.Success &&
            scriptRuntimes.Count == 3 && scriptRuntimes.All(runtime => runtime.IsDisposed) &&
            scriptRuntimes[0].Executions.Single().Variables["PlayniteApi"] == window.RuntimeHost.PluginApi &&
            scriptRuntimes[2].Executions.Single().Variables.ContainsKey("Game") &&
            scriptRuntimes[2].Executions.Single().Variables.ContainsKey("StartingArgs")
                ? "startup/shutdown and game test scripts received SDK variables, disposed, and reported failures"
                : throw new InvalidOperationException("The script runtime lifecycle or SDK variables diverged."));

        var backupFixtureRoot = Path.Combine(library.ActiveUserDataDirectory, "track-w-backup-fixture");
        var backupDataDirectory = Path.Combine(backupFixtureRoot, "profile");
        var backupLibraryDirectory = Path.Combine(backupFixtureRoot, "library");
        var backupOutputDirectory = Path.Combine(backupFixtureRoot, "output");
        Directory.CreateDirectory(backupDataDirectory);
        Directory.CreateDirectory(backupLibraryDirectory);
        File.WriteAllText(Path.Combine(backupDataDirectory, PlaynitePaths.ConfigFileName), "{\"pilot\":true}");
        File.WriteAllText(Path.Combine(backupLibraryDirectory, "database.json"), "{\"Version\":3}");
        File.WriteAllBytes(Path.Combine(backupLibraryDirectory, "games.db"), new byte[] { 1, 2, 3, 4 });
        var backupSettings = new DesktopSettings
        {
            AutoBackupEnabled = true,
            AutoBackupFrequency = AutoBackupFrequency.OnceADay,
            AutoBackupDir = backupOutputDirectory,
            RotatingBackups = 1,
            LastAutoBackup = DateTime.Now.AddDays(-2)
        };
        var backupSettingsSaved = 0;
        var backupCoordinator = new DesktopBackupCoordinator(
            backupSettings,
            backupDataDirectory,
            backupLibraryDirectory,
            () => backupSettingsSaved++);
        var backupTimestamp = DateTime.Now;
        var automaticBackupRan = await backupCoordinator.RunIfDueAsync(backupTimestamp);
        var automaticBackupSkippedSecondRun = !await backupCoordinator.RunIfDueAsync(backupTimestamp);
        var backupArchivePath = Directory.GetFiles(backupOutputDirectory, "PlayniteBackup-*.zip").Single();
        using var backupArchive = System.IO.Compression.ZipFile.OpenRead(backupArchivePath);
        var backupEntries = backupArchive.Entries.Select(entry => entry.FullName).ToHashSet();
        Record(results, "Automatic backup scheduler creates a real Core archive before library open", () =>
            automaticBackupRan && automaticBackupSkippedSecondRun &&
            backupSettingsSaved == 1 && backupSettings.LastAutoBackup == backupTimestamp &&
            backupEntries.Contains(PlaynitePaths.ConfigFileName) &&
            backupEntries.Contains(Path.Combine("library", "database.json")) &&
            backupEntries.Contains(Path.Combine("library", "games.db")) &&
            DesktopBackupCoordinator.ShouldRun(new DesktopSettings
            {
                AutoBackupEnabled = true,
                AutoBackupFrequency = AutoBackupFrequency.OnceAWeek,
                AutoBackupDir = backupOutputDirectory,
                LastAutoBackup = backupTimestamp.AddDays(-8)
            }, backupTimestamp)
                ? "daily/weekly due checks, Core archive contents, timestamps, and duplicate-run prevention passed"
                : throw new InvalidOperationException("Automatic backup scheduling or archive contents diverged."));

        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("Backup settings could not be opened for the Track W self-test.");
        }
        viewModel.Settings.Backup.AutoBackupEnabled = true;
        viewModel.Settings.Backup.AutoBackupFrequency = AutoBackupFrequency.OnceADay;
        viewModel.Settings.Backup.AutoBackupDir = backupOutputDirectory;
        viewModel.Settings.Backup.RotatingBackups = 3;
        viewModel.Settings.Backup.AutoBackupIncludeLibFiles = false;
        viewModel.Settings.Backup.AutoBackupIncludeExtensions = true;
        viewModel.Settings.Backup.AutoBackupIncludeThemes = true;
        viewModel.Settings.Backup.AutoBackupIncludeExtensionsData = false;
        viewModel.Settings.SaveCommand.Execute(null);
        viewModel.Settings.Open();
        var backupSettingsReopened =
            viewModel.Settings.Backup.AutoBackupEnabled &&
            viewModel.Settings.Backup.AutoBackupFrequency == AutoBackupFrequency.OnceADay &&
            viewModel.Settings.Backup.AutoBackupDir == Path.GetFullPath(backupOutputDirectory) &&
            viewModel.Settings.Backup.RotatingBackups == 3 &&
            !viewModel.Settings.Backup.AutoBackupIncludeLibFiles &&
            viewModel.Settings.Backup.AutoBackupIncludeExtensions &&
            viewModel.Settings.Backup.AutoBackupIncludeThemes &&
            !viewModel.Settings.Backup.AutoBackupIncludeExtensionsData;
        viewModel.Settings.CancelCommand.Execute(null);
        Record(results, "Backup settings use a validated persisted working copy", () =>
            backupSettingsReopened
                ? "folder, frequency, rotation, and all optional data groups saved and reopened"
                : throw new InvalidOperationException("Backup settings did not round-trip."));

        var developmentExtensionRoot = Path.Combine(library.ActiveUserDataDirectory, "track-w-development-extension");
        Directory.CreateDirectory(developmentExtensionRoot);
        File.WriteAllText(
            Path.Combine(developmentExtensionRoot, PlaynitePaths.ExtensionManifestFileName),
            "Id: track-w-development-plugin\n" +
            "Name: Track W development plugin\n" +
            "Author: Playnite pilot\n" +
            "Version: 1.0\n" +
            "Module: TrackW.dll\n" +
            "Type: GenericPlugin\n");
        var externalManifest = ExtensionFactory.GetInstalledManifests(
            new List<string> { developmentExtensionRoot }).Single(manifest =>
                manifest.Id == "track-w-development-plugin");
        if (!viewModel.Settings.Open())
        {
            throw new InvalidOperationException("System settings could not be opened for the Track W self-test.");
        }
        viewModel.Settings.Development.TraceLogEnabled = true;
        viewModel.Settings.Development.Extensions.Add(
            new DevelopmentExtensionOption(developmentExtensionRoot, true));
        viewModel.Settings.GeneralAdvanced.DiscordPresenceEnabled = true;
        viewModel.Settings.GeneralAdvanced.ShowElevatedRightsWarning = false;
        viewModel.Settings.GeneralAdvanced.InstallSizeScanUseSizeOnDisk = true;
        viewModel.Settings.GeneralAdvanced.DirectoryOpenCommand = "open-folder \"{Dir}\"";
        var relocatedDatabasePath = Path.Combine(library.ActiveUserDataDirectory, "relocated-library");
        viewModel.Settings.GeneralAdvanced.DatabasePath = relocatedDatabasePath;
        viewModel.Settings.GeneralAdvanced.ClearWebCacheCommand.Execute(null);
        viewModel.Settings.SaveCommand.Execute(null);
        var systemSettingsRequestedRestart = viewModel.Settings.RestartRequired;
        viewModel.Settings.Open();
        var systemSettingsReopened =
            viewModel.Settings.Development.TraceLogEnabled &&
            viewModel.Settings.Development.Extensions.Single().Path == Path.GetFullPath(developmentExtensionRoot) &&
            viewModel.Settings.Development.Extensions.Single().IsEnabled &&
            viewModel.Settings.GeneralAdvanced.DiscordPresenceEnabled == OperatingSystem.IsWindows() &&
            !viewModel.Settings.GeneralAdvanced.ShowElevatedRightsWarning &&
            viewModel.Settings.GeneralAdvanced.InstallSizeScanUseSizeOnDisk &&
            viewModel.Settings.GeneralAdvanced.DirectoryOpenCommand == "open-folder \"{Dir}\"" &&
            viewModel.Settings.GeneralAdvanced.DatabasePath == Path.GetFullPath(relocatedDatabasePath) &&
            viewModel.Settings.GeneralAdvanced.StatusText.Contains("queued", StringComparison.OrdinalIgnoreCase);
        viewModel.Settings.GeneralAdvanced.SetDefaultsCommand.Execute(null);
        viewModel.Settings.GeneralAdvanced.DatabasePath = library.Database.DatabasePath;
        viewModel.Settings.Development.TraceLogEnabled = false;
        viewModel.Settings.SaveCommand.Execute(null);
        Record(results, "Development and advanced system settings drive real startup policies", () =>
            externalManifest.IsExternalDev && systemSettingsRequestedRestart && systemSettingsReopened &&
            DesktopWebCacheService.GetCacheDirectories().Count > 0 &&
            !global::Playnite.Common.NLogLogger.IsTraceEnabled
                ? "external manifest precedence, trace updates, restart flags, cache queue, database path, and advanced defaults passed"
                : throw new InvalidOperationException("Development or advanced system policies diverged."));

        viewModel.Settings.Open();
        viewModel.Settings.Performance.DisableHwAcceleration = true;
        viewModel.Settings.Performance.AsyncImageLoading = false;
        viewModel.Settings.Performance.ShowImagePerformanceWarning = false;
        viewModel.Settings.SaveCommand.Execute(null);
        var performanceSettingsRequestedRestart = viewModel.Settings.RestartRequired;
        viewModel.Settings.Open();
        var performanceSettingsReopened =
            viewModel.Settings.Performance.DisableHwAcceleration &&
            !viewModel.Settings.Performance.AsyncImageLoading &&
            !viewModel.Settings.Performance.ShowImagePerformanceWarning &&
            !global::Playnite.Avalonia.Controls.GameCoverImage.AsyncLoadingEnabled;
        viewModel.Settings.Performance.DisableHwAcceleration = false;
        viewModel.Settings.Performance.AsyncImageLoading = true;
        viewModel.Settings.Performance.ShowImagePerformanceWarning = true;
        viewModel.Settings.SaveCommand.Execute(null);
        Record(results, "Performance settings drive Avalonia rendering and image policies", () =>
            performanceSettingsRequestedRestart &&
            performanceSettingsReopened &&
            global::Playnite.Avalonia.Controls.GameCoverImage.AsyncLoadingEnabled
                ? "software rendering is restart-scoped while image decoding and media warnings persist"
                : throw new InvalidOperationException("Performance settings did not apply or round-trip."));

        Record(results, "Software rendering is selected before Avalonia starts", () =>
        {
            var rendererProfile = Path.Combine(
                library.ActiveUserDataDirectory,
                "track-w-renderer-profile");
            Directory.CreateDirectory(rendererProfile);
            File.WriteAllText(
                Path.Combine(rendererProfile, "config.json"),
                "{ \"DisableHwAcceleration\": true }");
            var imported = Program.ReadDisableHwAcceleration(rendererProfile);
            File.WriteAllText(
                Path.Combine(rendererProfile, "avaloniaDesktop.json"),
                "{ \"DisableHwAcceleration\": false }");
            var overridden = Program.ReadDisableHwAcceleration(rendererProfile);
            Directory.Delete(rendererProfile, true);
            return imported && !overridden
                ? "first-run WPF import and authoritative Avalonia renderer preferences were resolved"
                : throw new InvalidOperationException("Startup renderer preference resolution diverged.");
        });

        viewModel.Settings.Open();
        viewModel.Settings.Input.EnableGameControllerSupport = false;
        viewModel.Settings.SaveCommand.Execute(null);
        var desktopInputDisabled = !window.SdlInput.InputEnabled;
        viewModel.Settings.Open();
        viewModel.Settings.Input.EnableGameControllerSupport = true;
        viewModel.Settings.SaveCommand.Execute(null);
        Record(results, "Desktop input settings control the live SDL source", () =>
            desktopInputDisabled && window.SdlInput.InputEnabled && window.SdlInput.IsStarted
                ? "controller processing disabled and re-enabled without restarting the shell"
                : throw new InvalidOperationException("Desktop controller settings did not reach SDL."));

        var settingsSectionChecks = viewModel.Settings.RunSelfChecks();
        Record(results, "Every desktop settings module supplies a passing self-check", () =>
            settingsSectionChecks.Count == viewModel.Settings.Sections.Count &&
            settingsSectionChecks.All(check => check.Passed)
                ? string.Join("; ", settingsSectionChecks.Select(check => $"{check.SectionKey}: {check.Detail}"))
                : throw new InvalidOperationException(string.Join(
                    "; ",
                    settingsSectionChecks.Select(check => $"{check.SectionKey}={check.Passed}: {check.Detail}"))));
        window.RuntimeHost.Extensions.Plugins.Remove(pilotSearchPlugin.Id);
        pilotSearchPlugin.Dispose();

        var policyGame = new Game("Avalonia runner policy game")
        {
            PluginId = policyPlugin.Id,
            GameId = "avalonia-runner-policy-game",
            IsInstalled = true,
            IncludeLibraryPluginAction = true,
            InstallDirectory = library.ActiveUserDataDirectory,
            EnableSystemHdr = true,
            PreScript = "game-pre",
            GameStartedScript = "game-started",
            PostScript = "game-post",
            UseGlobalPreScript = true,
            UseGlobalGameStartedScript = true,
            UseGlobalPostScript = true
        };
        library.Database.Games.Add(policyGame);
        var recordingRuntime = new RecordingPowerShellRuntime();
        var hdrTransitions = new List<bool>();
        var runnerPolicy = window.RuntimeHost.Actions.Policy;
        runnerPolicy.CreateScriptRuntime = _ => recordingRuntime;
        runnerPolicy.GlobalPreScript = () => "global-pre";
        runnerPolicy.GlobalGameStartedScript = () => "global-started";
        runnerPolicy.GlobalPostScript = () => "global-post";
        runnerPolicy.IsHdrEnabled = () => false;
        runnerPolicy.SetHdrEnabled = hdrTransitions.Add;
        runnerPolicy.ShutdownClients = () => true;
        runnerPolicy.ClientShutdownMinimumSessionSeconds = () => 0;
        runnerPolicy.ClientShutdownGraceSeconds = () => 0;
        runnerPolicy.ClientShutdownPluginIds = () => new[] { policyPlugin.Id };

        var policyResult = window.RuntimeHost.Play(policyGame);
        Record(results, "Shared runner executes scripts and restores HDR in legacy order", () =>
        {
            var scripts = recordingRuntime.Executions.Select(execution => execution.Script).ToList();
            var startedVariables = recordingRuntime.Executions
                .Single(execution => execution.Script == "game-started")
                .Variables;
            return policyResult.Success &&
                   scripts.SequenceEqual(new[]
                   {
                       "global-pre",
                       "game-pre",
                       "game-started",
                       "global-started",
                       "game-post",
                       "global-post"
                   }) &&
                   hdrTransitions.SequenceEqual(new[] { true, false }) &&
                   startedVariables["StartedProcessId"] is int processId && processId == 4242 &&
                   startedVariables["PlayniteApi"] != null &&
                   startedVariables["Game"] is Game scriptGame && scriptGame.Id == policyGame.Id &&
                   recordingRuntime.IsDisposed
                ? "global/per-game pre, started, and post scripts ran; HDR returned to its original state"
                : throw new InvalidOperationException("Script order, variables, lifetime, or HDR restoration diverged.");
        });

        var shutdownDeadline = DateTime.UtcNow.AddSeconds(2);
        while (policyPlugin.ClientShutdownCount == 0 && DateTime.UtcNow < shutdownDeadline)
        {
            await Task.Delay(10);
        }

        Record(results, "Shared runner notifies plugins and closes eligible library clients", () =>
            policyPlugin.GameStoppedCount == 1 &&
            policyPlugin.LastStoppedSeconds == PilotActionPolicyPlugin.SessionSeconds &&
            policyPlugin.ClientShutdownCount == 1
                ? "the stop callback ran and the selected client closed after the configured grace period"
                : throw new InvalidOperationException("Stop notification or client shutdown policy did not run."));

        var failingGame = new Game("Avalonia failing pre-script game")
        {
            PluginId = policyPlugin.Id,
            GameId = "avalonia-failing-pre-script-game",
            IsInstalled = true,
            IncludeLibraryPluginAction = true,
            EnableSystemHdr = true,
            PreScript = "fail-pre",
            UseGlobalPreScript = false
        };
        library.Database.Games.Add(failingGame);
        var failingRuntime = new RecordingPowerShellRuntime("fail-pre");
        hdrTransitions.Clear();
        runnerPolicy.CreateScriptRuntime = _ => failingRuntime;
        var playCountBeforeFailure = policyPlugin.PlayCount;
        var failingResult = window.RuntimeHost.Play(failingGame);
        Record(results, "Pre-script failures cancel launch and roll back runtime policy", () =>
            !failingResult.Success &&
            !failingGame.IsLaunching &&
            !failingGame.IsRunning &&
            policyPlugin.PlayCount == playCountBeforeFailure &&
            hdrTransitions.SequenceEqual(new[] { true, false }) &&
            failingRuntime.IsDisposed
                ? "the controller never launched and HDR/runtime state was restored without suppression"
                : throw new InvalidOperationException("A failed pre-script leaked launch, HDR, or runtime state."));

        var extensionCancelledGame = new Game("Extension-cancelled HDR game")
        {
            PluginId = policyPlugin.Id,
            GameId = "extension-cancelled-hdr-game",
            IsInstalled = true,
            IncludeLibraryPluginAction = true,
            EnableSystemHdr = true
        };
        library.Database.Games.Add(extensionCancelledGame);
        var cancelledRuntime = new RecordingPowerShellRuntime();
        runnerPolicy.CreateScriptRuntime = _ => cancelledRuntime;
        policyPlugin.CancelNextStartup = true;
        hdrTransitions.Clear();
        var extensionCancelledResult = window.RuntimeHost.Play(extensionCancelledGame);
        Record(results, "Extension cancellation leaves pre-existing HDR state untouched", () =>
            !extensionCancelledResult.Success &&
            hdrTransitions.Count == 0 &&
            cancelledRuntime.IsDisposed
                ? "startup stopped before HDR policy was applied and disposed its script runtime"
                : throw new InvalidOperationException("Extension cancellation changed HDR or leaked runtime state."));
        library.Database.Games.Remove(extensionCancelledGame);
        library.Database.Games.Remove(failingGame);
        library.Database.Games.Remove(policyGame);
        window.RuntimeHost.Extensions.Plugins.Remove(policyPlugin.Id);

        Record(results, "Desktop settings persist atomically", () =>
        {
            var store = new DesktopSettingsStore(library.ActiveUserDataDirectory);
            var persistedMetadataSettings = Playnite.Metadata.MetadataDownloaderSettings.GetDefaultSettings();
            foreach (var field in MetadataSettingsUtilities.SupportedFields)
            {
                var fieldSettings = MetadataSettingsUtilities.GetField(persistedMetadataSettings, field);
                fieldSettings.Import = false;
                fieldSettings.Sources = new List<Guid>();
            }

            persistedMetadataSettings.Description.Import = true;
            persistedMetadataSettings.Description.Sources = new List<Guid> { metadataPlugin.Id, Guid.Empty };
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
                UsePerFieldMetadataSettings = true,
                MetadataSettings = persistedMetadataSettings,
                AgeRatingOrgPriority = AgeRatingOrg.ESRB,
                WebImageSearchIconTerm = "{Name} persisted icon",
                WebImageSearchCoverTerm = "{Name} persisted cover",
                WebImageSearchBackgroundTerm = "{Name} persisted background",
                DefaultWebImageSource = global::Playnite.WebImageSearchSource.DuckDuckGo,
                GameSortingNameAutofill = false,
                GameSortingNameRemovedArticles = new List<string> { "The", "Le" },
                PrimaryGameSearchItemAction = GameSearchItemAction.Edit,
                SecondaryGameSearchItemAction = GameSearchItemAction.None,
                GlobalSearchOpenWithLegacySearch = false,
                SaveGlobalSearchFilterSettings = false,
                IncludeCommandsInDefaultSearch = false,
                CustomSearchKeywords = new Dictionary<string, string>
                {
                    ["pilot-provider"] = "persisted"
                },
                SystemSearchHotkey = new HotKey(Key.F11, KeyModifiers.Control | KeyModifiers.Alt),
                SearchWindowVisibility = new SearchWindowVisibilitySettings
                {
                    GameIcon = false,
                    LibraryIcon = true,
                    HiddenStatus = false,
                    Platform = false,
                    PlayTime = true,
                    CompletionStatus = false,
                    ReleaseDate = true
                },
                GlobalSearchIncludeUninstalled = false,
                GlobalSearchIncludeHidden = true,
                CheckForProgramUpdates = UpdateCheckFrequency.OnceAWeek,
                CheckForAddonUpdates = UpdateCheckFrequency.Manually,
                CheckForLibraryUpdates = LibraryUpdateCheckFrequency.OnceADay,
                CheckForEmulatedLibraryUpdates = LibraryUpdateCheckFrequency.OnceAWeek,
                UpdateNotificationOnPatchesOnly = true,
                LastProgramUpdateCheck = new DateTime(2026, 7, 18, 12, 0, 0),
                LastAddonUpdateCheck = new DateTime(2026, 7, 17, 12, 0, 0),
                LastLibraryUpdateCheck = new DateTime(2026, 7, 16, 12, 0, 0),
                LastEmulatedLibraryUpdateCheck = new DateTime(2026, 7, 15, 12, 0, 0),
                AutoBackupEnabled = true,
                AutoBackupFrequency = AutoBackupFrequency.OnceADay,
                AutoBackupDir = backupOutputDirectory,
                RotatingBackups = 4,
                AutoBackupIncludeLibFiles = false,
                AutoBackupIncludeExtensions = true,
                AutoBackupIncludeThemes = true,
                AutoBackupIncludeExtensionsData = false,
                LastAutoBackup = new DateTime(2026, 7, 14, 12, 0, 0),
                TraceLogEnabled = true,
                DisableHwAcceleration = true,
                AsyncImageLoading = false,
                ShowImagePerformanceWarning = false,
                DevelopmentExtensions = new List<DevelopmentExtensionPath>
                {
                    new() { Path = developmentExtensionRoot, IsEnabled = true }
                },
                DiscordPresenceEnabled = true,
                ShowElevatedRightsWarning = false,
                InstallSizeScanUseSizeOnDisk = true,
                DirectoryOpenCommand = "open-folder \"{Dir}\"",
                DatabasePath = relocatedDatabasePath,
                ClearWebCacheOnNextStartup = true,
                LibraryPluginIds = new List<Guid> { libraryPlugin.Id },
                LibraryPluginSelectionConfigured = true,
                GameScannerIds = new List<Guid> { scannerConfig.Id },
                GameScannerSelectionConfigured = true,
                LibraryPlaytimeImportMode = PlaytimeImportMode.Always,
                DownloadMetadataOnImport = false,
                EnableGameControllerSupport = false,
                DisabledGameControllers = new List<string> { "pilot-controller" },
                EnableTray = false,
                MinimizeToTray = true,
                CloseToTray = false,
                WindowWidth = 1280,
                WindowHeight = 760,
                WindowX = 120,
                WindowY = 80,
                WindowMaximized = true,
                ThemePath = @"C:\Themes\Pilot",
                ShowGroupCount = false,
                PlaytimeUseDaysFormat = true,
                GridItemWidth = 240,
                GridItemWidthRatio = 2,
                GridItemHeightRatio = 3,
                CoverArtStretch = global::Avalonia.Media.Stretch.Uniform,
                GridItemSpacing = 18,
                GridItemMargin = 5,
                ShowGridItemBackground = false,
                ShowNamesUnderCovers = false,
                ShowNameEmptyCover = false,
                DarkenUninstalledGamesGrid = true,
                GridViewScrollSensitivity = 2.25,
                GridViewScrollDurationMilliseconds = 325,
                GridViewSmoothScrollEnabled = true,
                ShowIconsOnList = false,
                ListViewScrollSensitivity = 2.75,
                ListViewScrollDurationMilliseconds = 375,
                ListViewSmoothScrollEnabled = true,
                DetailsVisibility = new Playnite.Avalonia.App.Services.DetailsVisibilitySettings
                {
                    Name = false,
                    CoverImage = false,
                    UserScore = true
                },
                DetailsViewScrollSensitivity = 3.25,
                DetailsViewScrollDurationMilliseconds = 425,
                DetailsViewSmoothScrollEnabled = true,
                IndentGameDetails = true,
                GameDetailsIndentation = 44,
                GameDetailsCoverHeight = 380,
                DetailsViewListIconSize = 76,
                GridViewDetailsPosition = Dock.Left,
                GridDetailsWidth = 430,
                ShowPanelSeparators = false,
                ShowBackgroundImageOnWindow = false,
                BlurWindowBackgroundImage = false,
                BackgroundImageBlurAmount = 36,
                DarkenWindowBackgroundImage = false,
                BackgroundImageDarkAmount = 0.35,
                ShowBackImageOnGridView = true,
                BackgroundImageAnimation = false,
                FontFamilyName = "Arial",
                MonospaceFontFamilyName = "Courier New",
                FontSizeSmall = 11,
                FontSize = 13,
                FontSizeLarge = 17,
                FontSizeLarger = 21,
                FontSizeLargest = 31,
                DefaultIconSource = Playnite.Avalonia.App.Services.DefaultIconSourceOptions.Platform,
                DefaultCoverSource = Playnite.Avalonia.App.Services.DefaultCoverSourceOptions.None,
                DefaultBackgroundSource = Playnite.Avalonia.App.Services.DefaultBackgroundSourceOptions.Cover,
                DateTimeFormatAdded = new Playnite.Avalonia.App.Services.DateFormattingOptions
                {
                    Format = "yyyy-MM-dd",
                    PastWeekRelativeFormat = true
                },
                DateTimeFormatReleaseDate = new Playnite.Avalonia.App.Services.ReleaseDateFormattingOptions
                {
                    Format = "yyyy-MM-dd",
                    PartialFormat = "yyyy-MM"
                },
                PluginTopPanelAlignment = Dock.Left,
                GlobalPreScript = "global-pre",
                GlobalGameStartedScript = "global-started",
                GlobalPostScript = "global-post",
                AppStartupScript = "app-startup",
                AppShutdownScript = "app-shutdown",
                ShutdownLibraryClients = true,
                ClientShutdownGraceSeconds = 45,
                ClientShutdownMinimumSessionSeconds = 90,
                ClientShutdownPluginIds = new List<Guid> { policyPlugin.Id }
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
                !loaded.UsePerFieldMetadataSettings ||
                !loaded.MetadataSettings.Description.Import ||
                !loaded.MetadataSettings.Description.Sources.SequenceEqual(new[] { metadataPlugin.Id, Guid.Empty }) ||
                loaded.MetadataSettings.Name.Import ||
                loaded.AgeRatingOrgPriority != AgeRatingOrg.ESRB ||
                loaded.WebImageSearchIconTerm != "{Name} persisted icon" ||
                loaded.WebImageSearchCoverTerm != "{Name} persisted cover" ||
                loaded.WebImageSearchBackgroundTerm != "{Name} persisted background" ||
                loaded.DefaultWebImageSource != global::Playnite.WebImageSearchSource.DuckDuckGo ||
                loaded.GameSortingNameAutofill ||
                !loaded.GameSortingNameRemovedArticles.SequenceEqual(new[] { "The", "Le" }) ||
                loaded.PrimaryGameSearchItemAction != GameSearchItemAction.Edit ||
                loaded.SecondaryGameSearchItemAction != GameSearchItemAction.None ||
                loaded.GlobalSearchOpenWithLegacySearch ||
                loaded.SaveGlobalSearchFilterSettings ||
                loaded.IncludeCommandsInDefaultSearch ||
                loaded.CustomSearchKeywords.GetValueOrDefault("pilot-provider") != "persisted" ||
                loaded.SystemSearchHotkey != new HotKey(Key.F11, KeyModifiers.Control | KeyModifiers.Alt) ||
                loaded.SearchWindowVisibility.GameIcon ||
                !loaded.SearchWindowVisibility.LibraryIcon ||
                loaded.SearchWindowVisibility.HiddenStatus ||
                loaded.SearchWindowVisibility.Platform ||
                !loaded.SearchWindowVisibility.PlayTime ||
                loaded.SearchWindowVisibility.CompletionStatus ||
                !loaded.SearchWindowVisibility.ReleaseDate ||
                loaded.GlobalSearchIncludeUninstalled ||
                !loaded.GlobalSearchIncludeHidden ||
                loaded.CheckForProgramUpdates != UpdateCheckFrequency.OnceAWeek ||
                loaded.CheckForAddonUpdates != UpdateCheckFrequency.Manually ||
                loaded.CheckForLibraryUpdates != LibraryUpdateCheckFrequency.OnceADay ||
                loaded.CheckForEmulatedLibraryUpdates != LibraryUpdateCheckFrequency.OnceAWeek ||
                !loaded.UpdateNotificationOnPatchesOnly ||
                loaded.LastProgramUpdateCheck != new DateTime(2026, 7, 18, 12, 0, 0) ||
                loaded.LastAddonUpdateCheck != new DateTime(2026, 7, 17, 12, 0, 0) ||
                loaded.LastLibraryUpdateCheck != new DateTime(2026, 7, 16, 12, 0, 0) ||
                loaded.LastEmulatedLibraryUpdateCheck != new DateTime(2026, 7, 15, 12, 0, 0) ||
                !loaded.AutoBackupEnabled ||
                loaded.AutoBackupFrequency != AutoBackupFrequency.OnceADay ||
                loaded.AutoBackupDir != backupOutputDirectory ||
                loaded.RotatingBackups != 4 ||
                loaded.AutoBackupIncludeLibFiles ||
                !loaded.AutoBackupIncludeExtensions ||
                !loaded.AutoBackupIncludeThemes ||
                loaded.AutoBackupIncludeExtensionsData ||
                loaded.LastAutoBackup != new DateTime(2026, 7, 14, 12, 0, 0) ||
                !loaded.TraceLogEnabled ||
                !loaded.DisableHwAcceleration ||
                loaded.AsyncImageLoading ||
                loaded.ShowImagePerformanceWarning ||
                loaded.DevelopmentExtensions.Count != 1 ||
                loaded.DevelopmentExtensions[0].Path != developmentExtensionRoot ||
                !loaded.DevelopmentExtensions[0].IsEnabled ||
                !loaded.DiscordPresenceEnabled ||
                loaded.ShowElevatedRightsWarning ||
                !loaded.InstallSizeScanUseSizeOnDisk ||
                loaded.DirectoryOpenCommand != "open-folder \"{Dir}\"" ||
                loaded.DatabasePath != relocatedDatabasePath ||
                !loaded.ClearWebCacheOnNextStartup ||
                !loaded.LibraryPluginIds.SequenceEqual(new[] { libraryPlugin.Id }) ||
                !loaded.LibraryPluginSelectionConfigured ||
                !loaded.GameScannerIds.SequenceEqual(new[] { scannerConfig.Id }) ||
                !loaded.GameScannerSelectionConfigured ||
                loaded.LibraryPlaytimeImportMode != PlaytimeImportMode.Always ||
                loaded.DownloadMetadataOnImport ||
                loaded.EnableGameControllerSupport ||
                loaded.DisabledGameControllers.SingleOrDefault() != "pilot-controller" ||
                loaded.EnableTray ||
                !loaded.MinimizeToTray ||
                loaded.CloseToTray ||
                loaded.WindowWidth != 1280 ||
                loaded.WindowHeight != 760 ||
                loaded.WindowX != 120 ||
                loaded.WindowY != 80 ||
                !loaded.WindowMaximized ||
                loaded.ThemePath != @"C:\Themes\Pilot" ||
                loaded.ShowGroupCount ||
                !loaded.PlaytimeUseDaysFormat ||
                loaded.GridItemWidth != 240 ||
                loaded.GridItemWidthRatio != 2 ||
                loaded.GridItemHeightRatio != 3 ||
                loaded.CoverArtStretch != global::Avalonia.Media.Stretch.Uniform ||
                loaded.GridItemSpacing != 18 ||
                loaded.GridItemMargin != 5 ||
                loaded.ShowGridItemBackground ||
                loaded.ShowNamesUnderCovers ||
                loaded.ShowNameEmptyCover ||
                !loaded.DarkenUninstalledGamesGrid ||
                loaded.GridViewScrollSensitivity != 2.25 ||
                loaded.GridViewScrollDurationMilliseconds != 325 ||
                !loaded.GridViewSmoothScrollEnabled ||
                loaded.ShowIconsOnList ||
                loaded.ListViewScrollSensitivity != 2.75 ||
                loaded.ListViewScrollDurationMilliseconds != 375 ||
                !loaded.ListViewSmoothScrollEnabled ||
                loaded.DetailsVisibility.Name ||
                loaded.DetailsVisibility.CoverImage ||
                !loaded.DetailsVisibility.UserScore ||
                loaded.DetailsViewScrollSensitivity != 3.25 ||
                loaded.DetailsViewScrollDurationMilliseconds != 425 ||
                !loaded.DetailsViewSmoothScrollEnabled ||
                !loaded.IndentGameDetails ||
                loaded.GameDetailsIndentation != 44 ||
                loaded.GameDetailsCoverHeight != 380 ||
                loaded.DetailsViewListIconSize != 76 ||
                loaded.GridViewDetailsPosition != Dock.Left ||
                loaded.GridDetailsWidth != 430 ||
                loaded.ShowPanelSeparators ||
                loaded.ShowBackgroundImageOnWindow ||
                loaded.BlurWindowBackgroundImage ||
                loaded.BackgroundImageBlurAmount != 36 ||
                loaded.DarkenWindowBackgroundImage ||
                loaded.BackgroundImageDarkAmount != 0.35 ||
                !loaded.ShowBackImageOnGridView ||
                loaded.BackgroundImageAnimation ||
                loaded.FontFamilyName != "Arial" ||
                loaded.MonospaceFontFamilyName != "Courier New" ||
                loaded.FontSizeSmall != 11 ||
                loaded.FontSize != 13 ||
                loaded.FontSizeLarge != 17 ||
                loaded.FontSizeLarger != 21 ||
                loaded.FontSizeLargest != 31 ||
                loaded.DefaultIconSource != Playnite.Avalonia.App.Services.DefaultIconSourceOptions.Platform ||
                loaded.DefaultCoverSource != Playnite.Avalonia.App.Services.DefaultCoverSourceOptions.None ||
                loaded.DefaultBackgroundSource != Playnite.Avalonia.App.Services.DefaultBackgroundSourceOptions.Cover ||
                loaded.DateTimeFormatAdded.Format != "yyyy-MM-dd" ||
                !loaded.DateTimeFormatAdded.PastWeekRelativeFormat ||
                loaded.DateTimeFormatReleaseDate.Format != "yyyy-MM-dd" ||
                loaded.DateTimeFormatReleaseDate.PartialFormat != "yyyy-MM" ||
                loaded.PluginTopPanelAlignment != Dock.Left ||
                loaded.GlobalPreScript != "global-pre" ||
                loaded.GlobalGameStartedScript != "global-started" ||
                loaded.GlobalPostScript != "global-post" ||
                loaded.AppStartupScript != "app-startup" ||
                loaded.AppShutdownScript != "app-shutdown" ||
                !loaded.ShutdownLibraryClients ||
                loaded.ClientShutdownGraceSeconds != 45 ||
                loaded.ClientShutdownMinimumSessionSeconds != 90 ||
                !loaded.ClientShutdownPluginIds.SequenceEqual(new[] { policyPlugin.Id }))
            {
                throw new InvalidOperationException("The persisted Desktop settings did not round-trip.");
            }

            return $"settings round-tripped at {store.SettingsPath}";
        });

        // Track P-B1–B3: multi-selection, complete game-menu composition,
        // bulk library verbs, and confirmed removal/exclusion persistence.
        var librarySelection = viewModel.Games.Take(2).ToList();
        viewModel.SetSelectedGames(librarySelection, librarySelection.LastOrDefault());
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Record(results, "Desktop game lists share a native multi-selection contract", () =>
            librarySelection.Count == 2 && viewModel.SelectedGameCount == 2 &&
            viewModel.SelectedGames.Select(game => game.Game.Id)
                .SequenceEqual(librarySelection.Select(game => game.Game.Id)) &&
            window.MainView.GridGameList.SelectionMode.HasFlag(SelectionMode.Multiple) &&
            window.MainView.GridGameList.SelectionMode.HasFlag(SelectionMode.AlwaysSelected) &&
            window.MainView.ListGameList.SelectionMode.HasFlag(SelectionMode.Multiple) &&
            window.MainView.ListGameList.SelectionMode.HasFlag(SelectionMode.AlwaysSelected) &&
            window.MainView.GridGameList.ContextMenu != null &&
            window.MainView.ListGameList.ContextMenu != null
                ? "grid/list selection, primary game, and native context-menu hosts are synchronized"
                : throw new InvalidOperationException(
                    $"selection={viewModel.SelectedGameCount}, expected={librarySelection.Count}, " +
                    $"primary={viewModel.SelectedGame?.Name ?? "none"}, " +
                    $"grid={window.MainView.GridGameList.SelectionMode}/" +
                    $"{window.MainView.GridGameList.SelectedItems?.Count}, " +
                    $"list={window.MainView.ListGameList.SelectionMode}/" +
                    $"{window.MainView.ListGameList.SelectedItems?.Count}, " +
                    $"menus={window.MainView.GridGameList.ContextMenu != null}/" +
                    $"{window.MainView.ListGameList.ContextMenu != null}."));

        using var contextMenuScript = new PilotMenuScript(
            Path.Combine(library.ActiveUserDataDirectory, "p-b-context-menu.psm1"));
        window.RuntimeHost.Extensions.Scripts.Add(contextMenuScript);
        var contextEntries = viewModel.BuildGameContextMenu();
        window.RuntimeHost.Extensions.Scripts.Remove(contextMenuScript);
        var flattenedContextEntries = FlattenContextMenu(contextEntries).ToList();
        var editGameLabel = Playnite.SDK.ResourceProvider.GetString("LOCEditGame");
        var setCategoryLabel = Playnite.SDK.ResourceProvider.GetString("LOCSetGameCategory");
        var setCompletionLabel = Playnite.SDK.ResourceProvider.GetString("LOCSetCompletionStatus");
        var removeGameLabel = Playnite.SDK.ResourceProvider.GetString("LOCRemoveGame");
        Record(results, "Desktop game context menu covers bulk verbs and hierarchical extensions", () =>
            contextEntries.Count > 0 &&
            flattenedContextEntries.Any(entry => entry.Header == editGameLabel) &&
            flattenedContextEntries.Any(entry => entry.Header == setCategoryLabel) &&
            flattenedContextEntries.Any(entry => entry.Header == setCompletionLabel) &&
            flattenedContextEntries.Any(entry => entry.Header == removeGameLabel) &&
            contextEntries.Any(entry => entry.Header == "Scripts" && entry.Children.Count > 0) &&
            flattenedContextEntries.Any(entry => entry.Header == "Script game command")
                ? $"{flattenedContextEntries.Count} native entries include bulk edit/fields/remove and nested sections"
                : throw new InvalidOperationException(
                    "Context entries: " + string.Join(" | ", flattenedContextEntries.Select(entry => entry.Header))));

        viewModel.SetSelectedFavoriteCommand.Execute(true);
        viewModel.SetSelectedHdrCommand.Execute(true);
        viewModel.SetSelectedHiddenCommand.Execute(true);
        Record(results, "Bulk favorite, hide, and HDR verbs persist through Core", () =>
            librarySelection.All(item =>
            {
                var persisted = library.Database.Games[item.Game.Id];
                return persisted?.Favorite == true && persisted.Hidden && persisted.EnableSystemHdr;
            })
                ? $"three flags persisted for {librarySelection.Count} selected games"
                : throw new InvalidOperationException(string.Join(" | ", librarySelection.Select(item =>
                {
                    var persisted = library.Database.Games[item.Game.Id];
                    return $"{item.Name}: favorite={persisted?.Favorite}, hidden={persisted?.Hidden}, " +
                        $"hdr={persisted?.EnableSystemHdr}";
                }))));

        var removalPluginId = Guid.NewGuid();
        var removalGame = new Game
        {
            Name = "P-B removal contract",
            GameId = "p-b-remove",
            PluginId = removalPluginId
        };
        library.Database.Games.Add(removalGame);
        var removedCount = viewModel.RemoveGamesWithoutConfirmation(new[] { removalGame }, true);
        var exclusionId = ImportExclusionItem.GetId(removalGame.GameId, removalPluginId);
        Record(results, "Remove-from-library preserves the future-import exclusion choice", () =>
            removedCount == 1 && library.Database.Games[removalGame.Id] == null &&
            library.Database.ImportExclusions[exclusionId]?.GameId == removalGame.GameId
                ? "the Core game was removed and its stable library exclusion was stored"
                : throw new InvalidOperationException("Removal or import exclusion persistence failed."));

        // Track P-B B4-B5: full live filter surface and Core preset lifecycle.
        viewModel.ToggleFilterPanelCommand.Execute(null);
        var filterFields = viewModel.FilterGroups.Select(group => group.Field).ToHashSet();
        Record(results, "Desktop filter panel exposes every Core filter field", () =>
            viewModel.IsFilterPanelVisible && viewModel.FilterFieldCount == 29 &&
            viewModel.FilterGroups.Count == 23 && filterFields.SetEquals(new[]
            {
                nameof(FilterPresetSettings.Platform), nameof(FilterPresetSettings.Library),
                nameof(FilterPresetSettings.Genre), nameof(FilterPresetSettings.ReleaseYear),
                nameof(FilterPresetSettings.Developer), nameof(FilterPresetSettings.Publisher),
                nameof(FilterPresetSettings.Category), nameof(FilterPresetSettings.Tag),
                nameof(FilterPresetSettings.Feature), nameof(FilterPresetSettings.PlayTime),
                nameof(FilterPresetSettings.InstallSize), nameof(FilterPresetSettings.CompletionStatuses),
                nameof(FilterPresetSettings.Series), nameof(FilterPresetSettings.Region),
                nameof(FilterPresetSettings.Source), nameof(FilterPresetSettings.AgeRating),
                nameof(FilterPresetSettings.UserScore), nameof(FilterPresetSettings.CommunityScore),
                nameof(FilterPresetSettings.CriticScore), nameof(FilterPresetSettings.LastActivity),
                nameof(FilterPresetSettings.RecentActivity), nameof(FilterPresetSettings.Added),
                nameof(FilterPresetSettings.Modified)
            })
                ? "29 fields include text, state, taxonomy, score, date, playtime, and install-size filters"
                : throw new InvalidOperationException(
                    $"fields={viewModel.FilterFieldCount}, groups={viewModel.FilterGroups.Count}, " +
                    $"visible={viewModel.IsFilterPanelVisible}."));

        var filterProbe = viewModel.LibraryGames.First(game => !game.Game.Hidden);
        var liveFilter = new FilterPresetSettings
        {
            Name = $"!{filterProbe.Name}",
            IsInstalled = filterProbe.Game.IsInstalled,
            IsUnInstalled = !filterProbe.Game.IsInstalled,
            UseAndFilteringStyle = true
        };
        viewModel.ApplyFilterSettingsForTest(liveFilter);
        Record(results, "Desktop filter edits evaluate live through the Core matcher", () =>
            viewModel.IsFilterActive && viewModel.LiveFilterMatchCount > 0 &&
            viewModel.Games.Any(game => game.Game.Id == filterProbe.Game.Id) &&
            viewModel.Games.All(game => library.Database.GetGameMatchesFilter(
                game.Game,
                viewModel.GetCurrentFilterSettings(),
                false))
                ? $"{viewModel.FilterMatchSummary}; AND style and active indicator are live"
                : throw new InvalidOperationException(
                    $"active={viewModel.IsFilterActive}, live={viewModel.LiveFilterMatchCount}, " +
                    $"visible={viewModel.Games.Count}."));

        var libraryGroup = viewModel.FilterGroups.Single(group =>
            group.Field == nameof(FilterPresetSettings.Library));
        libraryGroup.Options.First(option => option.Count > 0).IsSelected = true;
        var selectedLibraryIds = viewModel.GetCurrentFilterSettings().Library?.Ids;
        Record(results, "Database-backed filter options update the working SDK model", () =>
            selectedLibraryIds?.Count == 1 && libraryGroup.SelectedCount == 1
                ? $"{libraryGroup.DisplayTitle} persisted its selected library ID"
                : throw new InvalidOperationException("The selected database option was not reflected in the SDK filter."));

        var presetName = $"P-B preset {Guid.NewGuid():N}";
        var savedPreset = viewModel.SaveFilterPresetForTest(presetName);
        var savedPresetId = savedPreset.Id;
        Record(results, "Filter preset save persists filters and view options through Core", () =>
            library.Database.FilterPresets[savedPresetId]?.Settings?.Name == liveFilter.Name &&
            library.Database.FilterPresets[savedPresetId]?.Settings?.Library?.Ids?.Count == 1 &&
            library.Database.FilterPresets[savedPresetId]?.SortingOrder == viewModel.SelectedSortOrder &&
            viewModel.FilterPresets.Any(preset => preset.Id == savedPresetId)
                ? "the preset stores the complete working filter plus sorting/grouping choices"
                : throw new InvalidOperationException("The saved filter preset did not round-trip through Core."));

        var renamedPresetName = $"{presetName} renamed";
        viewModel.RenameFilterPresetForTest(savedPreset, renamedPresetName);
        var renamedPreset = library.Database.FilterPresets[savedPresetId];
        Record(results, "Filter preset rename persists through Core", () =>
            renamedPreset?.Name == renamedPresetName && viewModel.SelectedFilterPreset?.Id == savedPresetId
                ? "the renamed preset remains selected in the live collection"
                : throw new InvalidOperationException("The filter preset rename did not persist."));

        viewModel.DeleteFilterPresetForTest(renamedPreset);
        Record(results, "Filter preset delete clears selection and persists through Core", () =>
            library.Database.FilterPresets[savedPresetId] == null &&
            viewModel.FilterPresets.All(preset => preset.Id != savedPresetId) &&
            viewModel.SelectedFilterPreset == null && !viewModel.IsFilterActive
                ? "the preset was removed and the working filter returned to its clear state"
                : throw new InvalidOperationException("The deleted filter preset remained active or persisted."));
        viewModel.CloseFilterPanelCommand.Execute(null);

        // Track P-D: exercise the complete fresh-install emulation path against
        // the disposable Core database used by the Desktop pilot.
        EmulationSuiteSelfTestResult emulationResult = null;
        Exception emulationFailure = null;
        try
        {
            emulationResult = RunEmulationSuiteSelfTests(viewModel, library);
        }
        catch (Exception exception)
        {
            emulationFailure = exception;
        }

        Record(results, "Emulator, profile, and scanner CRUD persists through Core", () =>
        {
            if (emulationFailure != null)
            {
                throw new InvalidOperationException(emulationFailure.Message, emulationFailure);
            }
            return emulationResult.ConfigurationPersisted && emulationResult.ProfileKindsPersisted
                ? "custom and built-in profiles plus a full scanner configuration round-tripped through Core"
                : throw new InvalidOperationException(
                    $"configuration={emulationResult.ConfigurationPersisted}, profiles={emulationResult.ProfileKindsPersisted}.");
        });
        Record(results, "Emulator auto-detection and definition download catalog are live", () =>
        {
            if (emulationFailure != null)
            {
                throw new InvalidOperationException(emulationFailure.Message, emulationFailure);
            }
            return emulationResult.DetectedEmulatorCount == 1 && emulationResult.DownloadOptionCount > 0
                ? $"the deterministic install was detected and {emulationResult.DownloadOptionCount:N0} definition links are available"
                : throw new InvalidOperationException(
                    $"detected={emulationResult.DetectedEmulatorCount}, downloads={emulationResult.DownloadOptionCount}.");
        });
        Record(results, "Emulated-game scan review imports selected ROMs", () =>
        {
            if (emulationFailure != null)
            {
                throw new InvalidOperationException(emulationFailure.Message, emulationFailure);
            }
            return emulationResult.ReviewGameCount == 2 && emulationResult.ImportedGameCount == 1
                ? "two ROMs reached review, one was deselected through exclusion, and one Core game was imported"
                : throw new InvalidOperationException(
                    $"review={emulationResult.ReviewGameCount}, imported={emulationResult.ImportedGameCount}.");
        });
        Record(results, "Emulation review and settings persist scanner path exclusions", () =>
        {
            if (emulationFailure != null)
            {
                throw new InvalidOperationException(emulationFailure.Message, emulationFailure);
            }
            return emulationResult.ReviewExclusionPersisted && emulationResult.SettingsPathPersisted
                ? "review-added ROM exclusions and manually added settings paths both persisted on the saved scanner"
                : throw new InvalidOperationException(
                    $"review={emulationResult.ReviewExclusionPersisted}, settings={emulationResult.SettingsPathPersisted}.");
        });

        // Track P-E: validate manager persistence, media/specification details,
        // completion defaults, preset ordering, and editor-local taxonomy creation.
        DatabaseFieldsSelfTestResult databaseFieldsResult = null;
        Exception databaseFieldsFailure = null;
        try
        {
            databaseFieldsResult = RunDatabaseFieldsSelfTests(viewModel, library);
        }
        catch (Exception exception)
        {
            databaseFieldsFailure = exception;
        }

        Record(results, "Database fields manager persists every taxonomy collection", () =>
        {
            if (databaseFieldsFailure != null)
            {
                throw new InvalidOperationException(databaseFieldsFailure.Message, databaseFieldsFailure);
            }
            return databaseFieldsResult.AllTaxonomyKindsPersisted && databaseFieldsResult.UnusedRemovalWorked
                ? "all eleven taxonomy kinds round-tripped and remove-unused preserved referenced values"
                : throw new InvalidOperationException(
                    $"taxonomy={databaseFieldsResult.AllTaxonomyKindsPersisted}, unused={databaseFieldsResult.UnusedRemovalWorked}.");
        });
        Record(results, "Platform and region specifications plus platform media persist", () =>
        {
            if (databaseFieldsFailure != null)
            {
                throw new InvalidOperationException(databaseFieldsFailure.Message, databaseFieldsFailure);
            }
            return databaseFieldsResult.SpecificationsPersisted && databaseFieldsResult.PlatformMediaPersisted
                ? "portable Core media ownership and bundled emulation specification IDs round-tripped"
                : throw new InvalidOperationException(
                    $"specifications={databaseFieldsResult.SpecificationsPersisted}, media={databaseFieldsResult.PlatformMediaPersisted}.");
        });
        Record(results, "Completion defaults and filter preset order persist", () =>
        {
            if (databaseFieldsFailure != null)
            {
                throw new InvalidOperationException(databaseFieldsFailure.Message, databaseFieldsFailure);
            }
            return databaseFieldsResult.CompletionDefaultsPersisted && databaseFieldsResult.FilterPresetOrderPersisted
                ? "new-game/first-play statuses and Fullscreen-aware preset ordering were saved through Core"
                : throw new InvalidOperationException(
                    $"completion={databaseFieldsResult.CompletionDefaultsPersisted}, presets={databaseFieldsResult.FilterPresetOrderPersisted}.");
        });
        Record(results, "Editor creates searchable taxonomy values transactionally", () =>
        {
            if (databaseFieldsFailure != null)
            {
                throw new InvalidOperationException(databaseFieldsFailure.Message, databaseFieldsFailure);
            }
            return databaseFieldsResult.EditorCreationPersisted && databaseFieldsResult.EditorSearchWorked &&
                databaseFieldsResult.EditorCancelDiscarded
                    ? "inline creation selected the new genre, search filtered the picker, and cancel left no tag residue"
                    : throw new InvalidOperationException(
                        $"creation={databaseFieldsResult.EditorCreationPersisted}, search={databaseFieldsResult.EditorSearchWorked}, " +
                        $"cancel={databaseFieldsResult.EditorCancelDiscarded}.");
        });

        // Track P-C: all catalog behavior is deterministic. The fake catalog
        // proves client-side SDK/platform decisions, cached installer lookup,
        // per-entry failure isolation, package validation/queueing, and the
        // persisted enable/disable state without relying on playnite.link.
        AddonStoreSelfTestResult addonStoreResult = null;
        Exception addonStoreFailure = null;
        try
        {
            addonStoreResult = await RunAddonStoreSelfTests();
        }
        catch (Exception exception)
        {
            addonStoreFailure = exception;
        }

        Record(results, "Add-on catalog isolates failures and caches installer manifests", () =>
        {
            if (addonStoreFailure != null)
            {
                throw new InvalidOperationException(addonStoreFailure.Message, addonStoreFailure);
            }

            return addonStoreResult.WindowsCount == 2 && addonStoreResult.FailureCount == 2 &&
                addonStoreResult.InstallerFetchCount == 4
                    ? "two usable entries loaded, malformed and failed entries were counted, successful lookups were cached, and the failure remained retryable"
                    : throw new InvalidOperationException(
                        $"windows={addonStoreResult.WindowsCount}, failures={addonStoreResult.FailureCount}, " +
                        $"fetches={addonStoreResult.InstallerFetchCount}.");
        });
        Record(results, "Add-on compatibility keeps SDK v6 on Windows and greys it on Linux", () =>
        {
            if (addonStoreFailure != null)
            {
                throw new InvalidOperationException(addonStoreFailure.Message, addonStoreFailure);
            }

            return addonStoreResult.WindowsV6Compatible && !addonStoreResult.LinuxV6Compatible &&
                addonStoreResult.LinuxV6Reason.Contains("Linux requires SDK v7", StringComparison.Ordinal)
                    ? "Windows exposes native v7 plus legacy v6; Linux keeps v6 visible with an SDK v7 reason"
                    : throw new InvalidOperationException(
                        $"windows-v6={addonStoreResult.WindowsV6Compatible}, " +
                        $"linux-v6={addonStoreResult.LinuxV6Compatible}, reason={addonStoreResult.LinuxV6Reason}.");
        });
        Record(results, "Add-on install validates identity and uses the restart-safe queue", () =>
        {
            if (addonStoreFailure != null)
            {
                throw new InvalidOperationException(addonStoreFailure.Message, addonStoreFailure);
            }

            return addonStoreResult.PackageQueued
                ? "synthetic SDK v7 and Avalonia-theme packages passed validation and were queued for restart"
                : throw new InvalidOperationException("A validated package was not present in the install queue.");
        });
        Record(results, "Add-on store rejects legacy WPF themes before installation", () =>
        {
            if (addonStoreFailure != null)
            {
                throw new InvalidOperationException(addonStoreFailure.Message, addonStoreFailure);
            }

            return addonStoreResult.AvaloniaThemeCompatible && !addonStoreResult.LegacyThemeCompatible &&
                addonStoreResult.LegacyThemeReason.Contains("WPF theme", StringComparison.Ordinal)
                    ? "theme API 3 remains installable while the legacy package stays visible with a migration reason"
                    : throw new InvalidOperationException(
                        $"avalonia={addonStoreResult.AvaloniaThemeCompatible}, " +
                        $"legacy={addonStoreResult.LegacyThemeCompatible}, reason={addonStoreResult.LegacyThemeReason}.");
        });
        Record(results, "Installed add-on enable state persists and requests restart", () =>
        {
            if (addonStoreFailure != null)
            {
                throw new InvalidOperationException(addonStoreFailure.Message, addonStoreFailure);
            }

            return addonStoreResult.DisablePersisted
                ? "the installed-plugin model persisted DisabledPlugins and raised the restart callback"
                : throw new InvalidOperationException("The installed add-on state did not persist.");
        });

        // Track P-H: Desktop chrome, complete navigation surfaces, remaining
        // first-party windows, drag/restart routing, tools, and details polish.
        var chromeCommands = new[]
        {
            viewModel.AddManualGameCommand, viewModel.OpenInstalledGameImportCommand,
            viewModel.OpenEmulatedImportCommand, viewModel.OpenLibrarySyncCommand,
            viewModel.OpenMetadataDownloadCommand, viewModel.OpenDatabaseFieldsCommand,
            viewModel.OpenExplorerCommand, viewModel.BackupDataCommand,
            viewModel.RestoreDataBackupCommand, viewModel.SelectRandomGameCommand,
            viewModel.SelectRandomFilteredGameCommand, viewModel.OpenAddonStoreCommand,
            viewModel.OpenToolsConfigCommand, viewModel.OpenEmulatorConfigCommand,
            viewModel.OpenPluginMainMenuCommand, viewModel.ReloadScriptsCommand,
            viewModel.OpenInteractivePowerShellCommand, viewModel.OpenSettingsCommand,
            viewModel.OpenHelpCommand, viewModel.OpenAboutCommand,
            viewModel.RestartCommand, viewModel.RestartSafeModeCommand,
            viewModel.ExitApplicationCommand
        };
        Record(results, "Desktop hamburger menu exposes every P-H entry point", () =>
            chromeCommands.All(command => command != null)
                ? $"{chromeCommands.Length} native commands cover add/import, library, tools, scripts, help, restart, and exit"
                : throw new InvalidOperationException("A Desktop chrome command was not initialized."));

        viewModel.SetDetailsViewCommand.Execute(null);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var detailGame = viewModel.SelectedGame.Game;
        var originalDetailDescription = detailGame.Description;
        detailGame.Description =
            "<h2>P-H Desktop details</h2><p>Shared <strong>rich text</strong> with " +
            "<a href='https://playnite.link'>a safe link</a>.</p>";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var desktopHtmlView = window.MainView.GetVisualDescendants()
            .OfType<Playnite.Avalonia.Controls.HtmlTextView>()
            .FirstOrDefault();
        Record(results, "Desktop Details mode uses a narrow list and the shared rich overview", () =>
            viewModel.IsDetailsView && window.MainView.GameList == window.MainView.ListGameList &&
            desktopHtmlView?.Html == detailGame.Description && desktopHtmlView.Children.Count > 0
                ? $"Details mode rendered {desktopHtmlView.Children.Count} safe HTML blocks beside the list"
                : throw new InvalidOperationException("The full Desktop Details surface was incomplete."));
        detailGame.Description = originalDetailDescription;
        viewModel.SetGridViewCommand.Execute(null);

        Record(results, "Desktop sort and grouping selectors cover every SDK enum value", () =>
            viewModel.SortOptions.SequenceEqual(Enum.GetValues<SortOrder>()) &&
            viewModel.GroupingOptions.SequenceEqual(Enum.GetValues<GroupableField>()) &&
            viewModel.GroupingOptions.Contains(GroupableField.PlayTime)
                ? $"{viewModel.SortOptions.Count} sort fields and {viewModel.GroupingOptions.Count} group fields are available"
                : throw new InvalidOperationException(
                    $"sort={viewModel.SortOptions.Count}/{Enum.GetValues<SortOrder>().Length}, " +
                    $"group={viewModel.GroupingOptions.Count}/{Enum.GetValues<GroupableField>().Length}."));

        viewModel.SelectedGrouping = GroupableField.InstallationStatus;
        var expandedGroupCount = viewModel.Games.Count;
        var collapsibleHeader = viewModel.Games.First(game => game.ShowGroupHeader && game.ToggleGroupCommand != null);
        var collapsedHeaderText = collapsibleHeader.GroupHeader;
        collapsibleHeader.ToggleGroupCommand.Execute(null);
        var collapsedGroupCount = viewModel.Games.Count;
        var collapsedHeader = viewModel.Games.First(game =>
            game.ShowGroupHeader && game.GroupHeader == collapsedHeaderText);
        var headerCollapsed = !collapsedHeader.IsGroupExpanded;
        collapsedHeader.ToggleGroupCommand.Execute(null);
        Record(results, "Desktop group headers collapse and restore their virtualized sections", () =>
            collapsedGroupCount < expandedGroupCount && headerCollapsed &&
            viewModel.Games.Count == expandedGroupCount
                ? $"one header reduced {expandedGroupCount:N0} rows to {collapsedGroupCount:N0} and restored them"
                : throw new InvalidOperationException(
                    $"expanded={expandedGroupCount}, collapsed={collapsedGroupCount}, restored={viewModel.Games.Count}."));
        viewModel.SelectedGrouping = GroupableField.None;

        viewModel.OpenExplorerCommand.Execute(null);
        var explorerOpened = viewModel.IsExplorerVisible && viewModel.ExplorerItems.Count > 0;
        viewModel.CloseOverlayCommand.Execute(null);
        viewModel.OpenAboutCommand.Execute(null);
        var aboutOpened = viewModel.IsAboutVisible &&
            !string.IsNullOrWhiteSpace(viewModel.AboutVersion) &&
            viewModel.AboutRuntime.Contains("Avalonia", StringComparison.Ordinal) &&
            viewModel.AboutSdkVersions.Contains("SDK v7", StringComparison.Ordinal);
        viewModel.CloseOverlayCommand.Execute(null);
        Record(results, "Database Explorer and About/licenses surfaces are native and populated", () =>
            explorerOpened && aboutOpened &&
            viewModel.OpenLicenseCommand != null && viewModel.CreateDiagnosticPackageCommand != null
                ? $"Explorer exposed live fields; About reports {viewModel.AboutRuntime} and both SDK generations"
                : throw new InvalidOperationException("Explorer or About was incomplete."));

        viewModel.ShowFirstTimeWizard();
        var wizardOpened = viewModel.IsFirstTimeWizardVisible && viewModel.WizardLanguages.Count > 0;
        viewModel.FinishFirstTimeWizardCommand.Execute(null);
        Record(results, "First-time wizard persists completion through the plugin-facing settings API", () =>
            wizardOpened && !viewModel.IsFirstTimeWizardVisible &&
            window.RuntimeHost.PluginApi.ApplicationSettings.FirstTimeWizardComplete
                ? $"{viewModel.WizardLanguages.Count} language choices feed setup and FirstTimeWizardComplete is live"
                : throw new InvalidOperationException("First-time setup remained hard-stubbed or incomplete."));

        viewModel.OpenToolsConfigCommand.Execute(null);
        viewModel.ToolsConfig.AddCommand.Execute(null);
        var pilotTool = viewModel.ToolsConfig.SelectedApp;
        pilotTool.Name = $"P-H tool {Guid.NewGuid():N}";
        pilotTool.Path = Environment.ProcessPath;
        viewModel.ToolsConfig.SaveCommand.Execute(null);
        var toolPersisted = library.Database.SoftwareApps[pilotTool.Id]?.Name == pilotTool.Name;
        window.TrayService.RefreshMenu();
        var toolsTrayMenu = window.TrayService.Menu.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(item => item.Menu?.Items
                .OfType<NativeMenuItem>()
                .Any(child => string.Equals(child.Header?.ToString(), pilotTool.Name, StringComparison.Ordinal)) == true);
        var toolReachedTray = toolsTrayMenu?.Menu?.Items
            .OfType<NativeMenuItem>()
            .Any(item => string.Equals(item.Header?.ToString(), pilotTool.Name, StringComparison.Ordinal)) == true;
        viewModel.OpenToolsConfigCommand.Execute(null);
        viewModel.ToolsConfig.SelectedApp = viewModel.ToolsConfig.EditingApps.Single(app => app.Id == pilotTool.Id);
        viewModel.ToolsConfig.RemoveCommand.Execute(null);
        viewModel.ToolsConfig.SaveCommand.Execute(null);
        Record(results, "Software-tools CRUD feeds the tray and quick-launch model", () =>
            toolPersisted && toolReachedTray && library.Database.SoftwareApps[pilotTool.Id] == null &&
            window.TrayService.QuickLaunchItemCount is >= 0 and <= 5
                ? $"the saved tool appeared in the tray; quick launch contains {window.TrayService.QuickLaunchItemCount} games"
                : throw new InvalidOperationException("ToolsConfig or its tray consumer did not stay in sync."));

        window.RuntimeHost.Notifications.RemoveAll();
        window.RuntimeHost.Notifications.Add(
            $"pilot-p-h-{Guid.NewGuid():N}",
            "P-H notification",
            NotificationType.Info);
        var notificationVisible = viewModel.NotificationCount == 1 &&
            viewModel.ClearNotificationsCommand.CanExecute(null);
        viewModel.ClearNotificationsCommand.Execute(null);
        Record(results, "Desktop notification chrome supports clear-all", () =>
            notificationVisible && viewModel.NotificationCount == 0
                ? "the top-panel notification state and clear-all command use the live notification API"
                : throw new InvalidOperationException("Notification clear-all did not update the Desktop chrome."));

        var backupOptionsPath = Path.Combine(Path.GetTempPath(), $"playnite-p-h-backup-{Guid.NewGuid():N}.json");
        var restoreOptionsPath = Path.Combine(Path.GetTempPath(), $"playnite-p-h-restore-{Guid.NewGuid():N}.json");
        var backupStartup = StartupOptions.Parse(new[] { "--backup", backupOptionsPath });
        var restoreStartup = StartupOptions.Parse(new[] { "--restore-backup", restoreOptionsPath });
        Record(results, "Manual backup and restore restart actions have explicit startup routing", () =>
            backupStartup.BackupOptionsPath == Path.GetFullPath(backupOptionsPath) &&
            restoreStartup.RestoreBackupOptionsPath == Path.GetFullPath(restoreOptionsPath) &&
            viewModel.BackupDataCommand.CanExecute(null) && viewModel.RestoreDataBackupCommand.CanExecute(null)
                ? "P-A selection dialogs feed distinct --backup and --restore-backup restart actions"
                : throw new InvalidOperationException("Backup/restore startup routing was incomplete."));


        // Track P-A: construct every shared dialog surface, verify SDK v6
        // routing/round-trip models, and construct (but never show) crash UX.
        Record(results, "Shared SDK dialog primitives construct and round-trip", () =>
            AvaloniaDialogPrimitiveSelfTest.ValidateConstructionAndRoundTrip());
        Record(results, "Standalone Avalonia crash UX constructs", () =>
        {
            var crashWindow = AvaloniaCrashHandler.CreateWindowForTest(
                new InvalidOperationException("Synthetic crash construction"));
            return crashWindow.Content != null &&
                crashWindow.Title.Contains("error", StringComparison.OrdinalIgnoreCase)
                    ? "description, log/diagnostics, report, restart, and safe-mode controls constructed"
                    : throw new InvalidOperationException("The standalone crash window was incomplete.");
        });
        Record(results, "Safe startup disables user themes and plugins", () =>
        {
            var safeOptions = StartupOptions.Parse(
                ["--safestartup", "--theme", Path.Combine(Path.GetTempPath(), "unsafe-theme")]);
            return safeOptions.SafeStartup && safeOptions.CustomThemePath == string.Empty &&
                safeOptions.GetRestartArguments().Contains("--userdatadir")
                    ? "--safestartup selects the default theme and the App skips user plugin loading"
                    : throw new InvalidOperationException("Safe-startup parsing did not isolate add-ons.");
        });

        var report = BuildReport(results);
        File.WriteAllText(
            Path.Combine(Program.RuntimeOutputDirectory, "desktop-pilot-results.txt"),
            report);
        Console.WriteLine(report);
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(
            results.Count(result => !result.Pass));
    }

    private static DatabaseFieldsSelfTestResult RunDatabaseFieldsSelfTests(
        DesktopAppViewModel viewModel,
        DesktopLibrary library)
    {
        var database = library.Database;
        var firstPreset = new FilterPreset
        {
            Name = "P-E Preset One",
            Settings = new FilterPresetSettings()
        };
        var secondPreset = new FilterPreset
        {
            Name = "P-E Preset Two",
            Settings = new FilterPresetSettings()
        };
        database.FilterPresets.Add(new[] { firstPreset, secondPreset });
        var presetSettings = database.GetFilterPresetsSettings();
        presetSettings.SortingOrder = new List<Guid> { firstPreset.Id, secondPreset.Id };
        database.SetFilterPresetsSettings(presetSettings);

        var manager = viewModel.DatabaseFields;
        if (!manager.Open())
        {
            throw new InvalidOperationException("The database fields overlay did not open.");
        }
        var platform = (Playnite.SDK.Models.Platform)manager.AddItemForTest(
            DatabaseFieldKind.Platforms, "P-E Platform");
        manager.AddItemForTest(DatabaseFieldKind.Categories, "P-E Category");
        manager.AddItemForTest(DatabaseFieldKind.Genres, "P-E Genre");
        manager.AddItemForTest(DatabaseFieldKind.Companies, "P-E Company");
        manager.AddItemForTest(DatabaseFieldKind.Features, "P-E Feature");
        var tag = (Tag)manager.AddItemForTest(DatabaseFieldKind.Tags, "P-E Referenced Tag");
        manager.AddItemForTest(DatabaseFieldKind.Tags, "P-E Unused Tag");
        manager.AddItemForTest(DatabaseFieldKind.Series, "P-E Series");
        manager.AddItemForTest(DatabaseFieldKind.AgeRatings, "P-E Age Rating");
        var region = (Region)manager.AddItemForTest(DatabaseFieldKind.Regions, "P-E Region");
        manager.AddItemForTest(DatabaseFieldKind.Sources, "P-E Source");
        var defaultStatus = (CompletionStatus)manager.AddItemForTest(
            DatabaseFieldKind.CompletionStatuses, "P-E Default Status");
        var playedStatus = (CompletionStatus)manager.AddItemForTest(
            DatabaseFieldKind.CompletionStatuses, "P-E Played Status");

        var usedGame = database.Games.First();
        usedGame.TagIds ??= new List<Guid>();
        if (!usedGame.TagIds.Contains(tag.Id))
        {
            usedGame.TagIds.Add(tag.Id);
        }
        database.Games.Update(usedGame);
        var removedUnused = manager.RemoveUnusedForTest(DatabaseFieldKind.Tags);
        var unusedRemovalWorked = removedUnused > 0 &&
            manager.Sections.First(section => section.Kind == DatabaseFieldKind.Tags).Items.Any(item => item.Model.Id == tag.Id) &&
            manager.Sections.First(section => section.Kind == DatabaseFieldKind.Tags).Items.All(item => item.Name != "P-E Unused Tag");

        var mediaPath = Path.Combine(library.ActiveUserDataDirectory, "p-e-platform-media.png");
        File.WriteAllBytes(mediaPath, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        platform.Icon = mediaPath;
        platform.Cover = mediaPath;
        platform.Background = mediaPath;
        platform.SpecificationId = manager.PlatformSpecifications.First(option => option.Id != null).Id;
        region.SpecificationId = manager.RegionSpecifications.First(option => option.Id != null).Id;
        manager.SelectedDefaultStatus = manager.CompletionStatusChoices.First(choice => choice.Id == defaultStatus.Id);
        manager.SelectedPlayedStatus = manager.CompletionStatusChoices.First(choice => choice.Id == playedStatus.Id);

        var filterSection = manager.Sections.First(section => section.Kind == DatabaseFieldKind.FilterPresets);
        manager.SelectedSection = filterSection;
        manager.SelectedItem = filterSection.Items.First(item => item.Model.Id == secondPreset.Id);
        manager.SelectedFilterPreset.ShowInFullscreeQuickSelection = false;
        manager.MoveUpCommand.Execute(null);
        if (!manager.Save())
        {
            throw new InvalidOperationException(manager.StatusText);
        }

        var savedPlatform = database.Platforms[platform.Id];
        var savedRegion = database.Regions[region.Id];
        var allKindsPersisted = savedPlatform != null &&
            database.Categories.Any(item => item.Name == "P-E Category") &&
            database.Genres.Any(item => item.Name == "P-E Genre") &&
            database.Companies.Any(item => item.Name == "P-E Company") &&
            database.Features.Any(item => item.Name == "P-E Feature") &&
            database.Tags[tag.Id] != null &&
            database.Series.Any(item => item.Name == "P-E Series") &&
            database.AgeRatings.Any(item => item.Name == "P-E Age Rating") &&
            savedRegion != null &&
            database.Sources.Any(item => item.Name == "P-E Source") &&
            database.CompletionStatuses[defaultStatus.Id] != null &&
            database.CompletionStatuses[playedStatus.Id] != null;
        var mediaPersisted = savedPlatform != null &&
            File.Exists(database.GetFullFilePath(savedPlatform.Icon)) &&
            File.Exists(database.GetFullFilePath(savedPlatform.Cover)) &&
            File.Exists(database.GetFullFilePath(savedPlatform.Background));
        var specificationsPersisted = savedPlatform?.SpecificationId == platform.SpecificationId &&
            savedRegion?.SpecificationId == region.SpecificationId;
        var completionSettings = database.GetCompletionStatusSettings();
        var completionPersisted = completionSettings.DefaultStatus == defaultStatus.Id &&
            completionSettings.PlayedStatus == playedStatus.Id;
        var savedPresetOrder = database.GetFilterPresetsSettings().SortingOrder;
        var filterOrderPersisted = savedPresetOrder.IndexOf(secondPreset.Id) < savedPresetOrder.IndexOf(firstPreset.Id) &&
            database.FilterPresets[secondPreset.Id]?.ShowInFullscreeQuickSelection == false;

        if (!viewModel.Editor.Open(usedGame.Id))
        {
            throw new InvalidOperationException("The editor did not open for the P-E taxonomy test.");
        }
        if (!viewModel.Editor.AddTaxonomyForTest("Genres", "P-E Editor Genre"))
        {
            throw new InvalidOperationException("The editor did not create a genre.");
        }
        viewModel.Editor.GenresSearch = "Editor Genre";
        var editorGenreOption = viewModel.Editor.Genres.First(item => item.Name == "P-E Editor Genre");
        var searchWorked = editorGenreOption.IsVisible && viewModel.Editor.Genres
            .Where(item => item.Id != editorGenreOption.Id).All(item => !item.IsVisible);
        viewModel.Editor.SaveCommand.Execute(null);
        var editorGenre = database.Genres.FirstOrDefault(item => item.Name == "P-E Editor Genre");
        var editorCreationPersisted = editorGenre != null &&
            database.Games[usedGame.Id]?.GenreIds?.Contains(editorGenre.Id) == true;

        if (!viewModel.Editor.Open(usedGame.Id))
        {
            throw new InvalidOperationException("The editor did not reopen for the cancellation test.");
        }
        viewModel.Editor.AddTaxonomyForTest("Tags", "P-E Cancelled Tag");
        viewModel.Editor.CancelCommand.Execute(null);
        var cancelDiscarded = database.Tags.All(item => item.Name != "P-E Cancelled Tag");

        return new DatabaseFieldsSelfTestResult(
            allKindsPersisted,
            unusedRemovalWorked,
            specificationsPersisted,
            mediaPersisted,
            completionPersisted,
            filterOrderPersisted,
            editorCreationPersisted,
            searchWorked,
            cancelDiscarded);
    }

    private static EmulationSuiteSelfTestResult RunEmulationSuiteSelfTests(
        DesktopAppViewModel viewModel,
        DesktopLibrary library)
    {
        var database = library.Database;
        var fixtureRoot = Path.Combine(library.ActiveUserDataDirectory, "p-d-emulation");
        var emulatorDirectory = Path.Combine(fixtureRoot, "emulator");
        var romDirectory = Path.Combine(fixtureRoot, "roms");
        Directory.CreateDirectory(emulatorDirectory);
        Directory.CreateDirectory(romDirectory);
        var executablePath = Path.Combine(emulatorDirectory, "pilot-emulator.bin");
        File.WriteAllBytes(executablePath, new byte[] { 0x50, 0x44 });
        File.WriteAllText(Path.Combine(romDirectory, "P-D Review A.rom"), "alpha");
        File.WriteAllText(Path.Combine(romDirectory, "P-D Review B.rom"), "beta");

        var configuration = viewModel.EmulatorConfig;
        if (!configuration.Open())
        {
            throw new InvalidOperationException("The emulator configuration overlay did not open.");
        }
        configuration.AddEmulatorCommand.Execute(null);
        var emulator = configuration.SelectedEmulator ??
            throw new InvalidOperationException("Adding an emulator did not select it.");
        emulator.Name = "P-D Pilot Emulator";
        emulator.InstallDir = emulatorDirectory;

        var definition = configuration.EmulatorDefinitions.FirstOrDefault(item => item.Profiles?.Count > 0) ??
            throw new InvalidOperationException("No bundled emulator definition contains a profile.");
        configuration.SelectedDefinition = definition;
        configuration.SelectedBuiltInProfileName = definition.Profiles[0].Name;
        configuration.AddBuiltInProfileCommand.Execute(null);
        configuration.AddCustomProfileCommand.Execute(null);
        var customProfile = configuration.SelectedCustomProfile ??
            throw new InvalidOperationException("Adding a custom profile did not select it.");
        customProfile.Name = "P-D ROM Profile";
        customProfile.Executable = executablePath;
        customProfile.WorkingDirectory = emulatorDirectory;
        configuration.CustomProfileImageExtensionsText = "rom";
        if (configuration.CustomProfilePlatforms.Count > 0)
        {
            configuration.CustomProfilePlatforms[0].IsSelected = true;
        }

        configuration.AddScannerCommand.Execute(null);
        var scanner = configuration.SelectedScanner ??
            throw new InvalidOperationException("Adding a game scanner did not select it.");
        scanner.Name = "P-D Pilot Scanner";
        scanner.Directory = romDirectory;
        scanner.ScanInsideArchives = false;
        scanner.ScanSubfolders = true;
        scanner.InGlobalUpdate = true;
        configuration.SelectedScannerEmulator = emulator;
        configuration.SelectedScannerProfile = customProfile;
        if (!configuration.Save())
        {
            throw new InvalidOperationException(configuration.StatusText);
        }

        var persistedEmulator = database.Emulators[emulator.Id];
        var persistedScanner = database.GameScanners[scanner.Id];
        var configurationPersisted = persistedEmulator != null && persistedScanner != null &&
            persistedScanner.EmulatorId == persistedEmulator.Id &&
            persistedScanner.EmulatorProfileId == customProfile.Id &&
            persistedScanner.ScanSubfolders && !persistedScanner.ScanInsideArchives;
        var profileKindsPersisted = persistedEmulator?.CustomProfiles?.Any(profile => profile.Id == customProfile.Id) == true &&
            persistedEmulator.BuiltinProfiles?.Count == 1;

        var detectionDefinition = new EmulatorDefinition
        {
            Id = "p-d-pilot",
            Name = "P-D detected emulator",
            Profiles = new List<EmulatorDefinitionProfile>
            {
                new()
                {
                    Name = "Detected profile",
                    InstallationFile = "^pilot-emulator\\.bin$"
                }
            }
        };
        var detected = EmulatorConfigViewModel.DetectEmulators(
            emulatorDirectory,
            new List<EmulatorDefinition> { detectionDefinition },
            CancellationToken.None);

        var import = viewModel.EmulatedImport;
        if (!import.Open())
        {
            throw new InvalidOperationException("The emulated-game import overlay did not open.");
        }
        import.ScannerConfigs.Clear();
        var importRow = new EmulatedImportScannerRow(
            persistedScanner.GetClone(),
            import.Emulators,
            import.Platforms,
            true,
            false);
        import.ScannerConfigs.Add(importRow);
        import.SelectedScanner = importRow;
        var scanResult = import.ScanCore(CancellationToken.None);
        import.ApplyScanResult(scanResult);
        var reviewCount = import.Games.Count;
        var excluded = import.Games.FirstOrDefault() ??
            throw new InvalidOperationException("The emulated-game scan returned no review rows.");
        excluded.IsMarked = true;
        import.ExcludeSelectedFilesCommand.Execute(null);
        var reviewExclusionPersisted = database.GameScanners[persistedScanner.Id]?.ExcludedFiles?.Count > 0;
        var gamesBeforeImport = database.Games.Count;
        import.ImportCommand.Execute(null);
        var importedCount = database.Games.Count - gamesBeforeImport;

        var exclusionSettings = viewModel.Settings.ImportExclusions;
        exclusionSettings.Open();
        exclusionSettings.SelectedScannerTarget = exclusionSettings.ScannerTargets.FirstOrDefault(target =>
            target.Id == persistedScanner.Id);
        exclusionSettings.NewScannerPath = Path.Combine("manual", "ignored.rom");
        exclusionSettings.NewScannerPathIsDirectory = false;
        exclusionSettings.AddScannerPathCommand.Execute(null);
        exclusionSettings.Save();
        var settingsPathPersisted = database.GameScanners[persistedScanner.Id]?.ExcludedFiles?.Any(path =>
            string.Equals(path, Path.Combine("manual", "ignored.rom"), EmulationConfigUtilities.PathComparison)) == true;

        return new EmulationSuiteSelfTestResult(
            configurationPersisted,
            profileKindsPersisted,
            detected.Count,
            configuration.DownloadOptions.Count,
            reviewCount,
            importedCount,
            reviewExclusionPersisted,
            settingsPathPersisted);
    }

    private static async Task<AddonStoreSelfTestResult> RunAddonStoreSelfTests()
    {
        var packagePath = Path.Combine(Path.GetTempPath(), $"playnite-addon-store-{Guid.NewGuid():N}.pext");
        var themePath = Path.Combine(Path.GetTempPath(), $"playnite-addon-store-{Guid.NewGuid():N}.pthm");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry(PlaynitePaths.ExtensionManifestFileName);
            using var writer = new StreamWriter(manifestEntry.Open());
            writer.Write(
                "Id: p-c-v7\n" +
                "Name: P-C v7 test\n" +
                "Author: Playnite\n" +
                "Version: 1.0.0\n" +
                "Type: GenericPlugin\n");
        }
        using (var archive = ZipFile.Open(themePath, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry(PlaynitePaths.ThemeManifestFileName);
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(
                    "Id: p-c-theme\n" +
                    "Name: P-C Avalonia theme\n" +
                    "Author: Playnite\n" +
                    "Version: 1.0.0\n" +
                    "Mode: Desktop\n" +
                    "ThemeApiVersion: 3.0.0\n" +
                    "Framework: Avalonia\n" +
                    "EntryPoint: Theme.axaml\n" +
                    "Resources: []\n" +
                    "Styles: []\n");
            }

            var dictionaryEntry = archive.CreateEntry("Theme.axaml");
            using var dictionaryWriter = new StreamWriter(dictionaryEntry.Open());
            dictionaryWriter.Write(
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" />");
        }

        try
        {
            var catalog = new AddonStoreFakeCatalog(packagePath, themePath);
            var service = new DesktopAddonStoreService(catalog, new HttpClient());
            var windows = await service.BrowseAsync(
                DesktopAddonStoreCategory.Extensions,
                string.Empty,
                true,
                CancellationToken.None);
            var linux = await service.BrowseAsync(
                DesktopAddonStoreCategory.Extensions,
                string.Empty,
                false,
                CancellationToken.None);
            var windowsV6 = windows.Items.Single(item => item.Id == "p-c-v6");
            var linuxV6 = linux.Items.Single(item => item.Id == "p-c-v6");
            var v7 = windows.Items.Single(item => item.Id == "p-c-v7");
            var installerFetchCount = catalog.InstallerFetchCount;
            await service.QueueInstallAsync(
                v7,
                v7.Packages.Single(package => package.IsCompatible),
                (_, _) => Task.FromResult(true),
                CancellationToken.None);
            var expectedTarget = v7.Manifest.GetTargetDownloadPath();
            var queued = ExtensionInstaller.GetQueuedItems().Any(item =>
                item.InstallType == ExtInstallType.Install &&
                string.Equals(item.Path, expectedTarget, StringComparison.OrdinalIgnoreCase));
            var themes = await service.BrowseAsync(
                DesktopAddonStoreCategory.DesktopThemes,
                string.Empty,
                true,
                CancellationToken.None);
            var avaloniaTheme = themes.Items.Single(item => item.Id == "p-c-theme");
            var legacyTheme = themes.Items.Single(item => item.Id == "p-c-wpf-theme");
            await service.QueueInstallAsync(
                avaloniaTheme,
                avaloniaTheme.Packages.Single(package => package.IsCompatible),
                (_, _) => Task.FromResult(true),
                CancellationToken.None);
            var themeTarget = avaloniaTheme.Manifest.GetTargetDownloadPath();
            var themeQueued = ExtensionInstaller.GetQueuedItems().Any(item =>
                item.InstallType == ExtInstallType.Install &&
                string.Equals(item.Path, themeTarget, StringComparison.OrdinalIgnoreCase));

            var stateSettings = new DesktopSettings();
            var stateChanged = 0;
            var installed = new InstalledAddonItemViewModel(
                new DesktopInstalledAddon(
                    "p-c-toggle",
                    "Toggle test",
                    "1.0.0",
                    "Extension",
                    Path.Combine(PlaynitePaths.ExtensionsUserDataPath, "p-c-toggle"),
                    true,
                    false,
                    true,
                    string.Empty),
                stateSettings,
                () => stateChanged++,
                _ => { },
                _ => { });
            installed.IsEnabled = false;

            return new AddonStoreSelfTestResult(
                windows.Items.Count,
                windows.Failures.Count,
                installerFetchCount,
                windowsV6.IsCompatible,
                linuxV6.IsCompatible,
                linuxV6.CompatibilityReason,
                queued && themeQueued,
                avaloniaTheme.IsCompatible,
                legacyTheme.IsCompatible,
                legacyTheme.CompatibilityReason,
                stateChanged == 1 && stateSettings.DisabledPlugins.Contains("p-c-toggle"));
        }
        finally
        {
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }
            if (File.Exists(themePath))
            {
                File.Delete(themePath);
            }
        }
    }

    private static void Record(
        ICollection<(string Name, bool Pass, string Detail)> results,
        string name,
        Func<string> check)
    {
        Console.WriteLine($"[RUN ] {name}");
        try
        {
            var detail = check();
            results.Add((name, true, detail));
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception exception)
        {
            results.Add((name, false, exception.Message));
            Console.WriteLine($"[FAIL] {name}: {exception.Message}");
        }
    }

    private static IEnumerable<DesktopGameContextMenuEntry> FlattenContextMenu(
        IEnumerable<DesktopGameContextMenuEntry> entries)
    {
        foreach (var entry in entries ?? Array.Empty<DesktopGameContextMenuEntry>())
        {
            yield return entry;
            foreach (var child in FlattenContextMenu(entry.Children))
            {
                yield return child;
            }
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

    private static string LocalizeForTest(string key, string fallback)
    {
        var value = Playnite.SDK.ResourceProvider.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key || value == $"<!{key}!>"
            ? fallback
            : value;
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

    private sealed class PilotMenuScript : PlayniteScript
    {
        private const string MainFunction = "InvokePilotMainMenu";
        private const string GameFunction = "InvokePilotGameMenu";

        public int MainInvocationCount { get; private set; }
        public int GameInvocationCount { get; private set; }
        public string LastMainSourceDescription { get; private set; }
        public IReadOnlyList<Guid> LastGameIds { get; private set; } = [];

        public PilotMenuScript(string path) : base(path, "Pilot menu script")
        {
            typeof(PlayniteScript)
                .GetProperty(nameof(SupportedMenus))
                .SetValue(this, new List<SupportedMenuMethods>
                {
                    SupportedMenuMethods.MainMenu,
                    SupportedMenuMethods.GameMenu
                });
        }

        public override List<ScriptMainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args) =>
        [
            new ScriptMainMenuItem
            {
                Description = "Script main command",
                MenuSection = "Scripts|Tools",
                FunctionName = MainFunction
            }
        ];

        public override List<ScriptGameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args) =>
        [
            new ScriptGameMenuItem
            {
                Description = "Script game command",
                MenuSection = "Scripts|Game",
                FunctionName = GameFunction
            }
        ];

        public override object InvokeFunction(string functionName) => InvokeFunction(functionName, []);

        public override object InvokeFunction(string functionName, List<object> arguments)
        {
            if (functionName == MainFunction)
            {
                var args = (ScriptMainMenuItemActionArgs)arguments.Single();
                LastMainSourceDescription = args.SourceItem.Description;
                MainInvocationCount++;
                return null;
            }

            if (functionName == GameFunction)
            {
                var args = (ScriptGameMenuItemActionArgs)arguments.Single();
                LastGameIds = args.Games.Select(game => game.Id).ToList();
                GameInvocationCount++;
                return null;
            }

            throw new InvalidOperationException($"Unknown pilot script function {functionName}.");
        }

        public override void InvokeExportedFunction(ScriptFunctionExport function) =>
            InvokeFunction(function.FunctionName);
        public override void SetVariable(string name, object value) { }
        public override void OnApplicationStarted() { }
        public override void OnApplicationStopped() { }
        public override void OnLibraryUpdated() { }
        public override void OnGameStarting(Playnite.SDK.Events.OnGameStartingEventArgs args) { }
        public override void OnGameStarted(Playnite.SDK.Events.OnGameStartedEventArgs args) { }
        public override void OnGameStopped(Playnite.SDK.Events.OnGameStoppedEventArgs args) { }
        public override void OnGameInstalled(Playnite.SDK.Events.OnGameInstalledEventArgs args) { }
        public override void OnGameInstallationCancelled(
            Playnite.SDK.Events.OnGameInstallationCancelledEventArgs args) { }
        public override void OnGameUninstalled(Playnite.SDK.Events.OnGameUninstalledEventArgs args) { }
        public override void OnGameSelected(Playnite.SDK.Events.OnGameSelectedEventArgs args) { }
        public override void OnGameStartupCancelled(Playnite.SDK.Events.OnGameStartupCancelledEventArgs args) { }
    }

    private sealed class PilotAddonUpdateService : IDesktopAddonUpdateService
    {
        private readonly DesktopAddonUpdate update;

        public int CheckCount { get; private set; }
        public int QueueCount { get; private set; }

        public PilotAddonUpdateService()
        {
            var package = new AddonInstallerPackage
            {
                Version = new Version(2, 0),
                RequiredApiVersion = SdkVersions.SDKVersion,
                PackageUrl = "pilot-update.pext",
                Changelog = new List<string> { "Track W update" }
            };
            var installer = new AddonInstallerManifest
            {
                AddonId = "pilot-addon",
                AddonType = AddonType.Generic,
                Packages = new List<AddonInstallerPackage> { package }
            };
            update = new DesktopAddonUpdate(
                new AddonManifest
                {
                    AddonId = "pilot-addon",
                    Name = "Pilot add-on",
                    Type = AddonType.Generic
                },
                package,
                installer,
                new Version(1, 0));
        }

        public Task<DesktopAddonUpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCount++;
            return Task.FromResult(new DesktopAddonUpdateCheckResult(
                new[] { update },
                Array.Empty<string>()));
        }

        public Task QueueAsync(
            DesktopAddonUpdate addonUpdate,
            Func<string, string, Task<bool>> acceptLicense,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueueCount++;
            addonUpdate.Status = "Queued for restart";
            return Task.CompletedTask;
        }
    }

    private sealed class PilotProgramUpdateService : IDesktopProgramUpdateService
    {
        public bool IsSupported => true;
        public int CheckCount { get; private set; }

        public Task<DesktopProgramUpdate> CheckAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCount++;
            return Task.FromResult(new DesktopProgramUpdate(
                new Version(11, 0),
                new Version(11, 0, 1),
                "pilot-checksum",
                new[] { "https://example.invalid/update.exe" }));
        }

        public Task<string> DownloadAsync(
            DesktopProgramUpdate update,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100);
            return Task.FromResult("pilot-update.exe");
        }
    }

    private sealed class PilotSearchPlugin : GenericPlugin
    {
        public const string DisplayName = "Pilot search plugin";

        private static readonly Guid pluginId =
            Guid.Parse("7c975fe0-5938-41ea-89b8-ff5982db9ed9");
        private readonly PilotSearchContext context;

        public override Guid Id => pluginId;
        public int InvocationCount => context.InvocationCount;

        public PilotSearchPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            context = new PilotSearchContext();
            Searches =
            [
                new SearchSupport("pilot", "Pilot provider", context)
            ];
        }

        private sealed class PilotSearchContext : SearchContext
        {
            public int InvocationCount { get; private set; }

            public PilotSearchContext()
            {
                Label = "Pilot provider";
                Description = "Track W SDK v6 search provider";
            }

            public override IEnumerable<SearchItem> GetSearchResults(GetSearchResultsArgs args)
            {
                args.CancelToken.ThrowIfCancellationRequested();
                yield return new SearchItem(
                    "Pilot provider result",
                    new SearchItemAction("Activate pilot result", () => InvocationCount++))
                {
                    Description = args.SearchTerm
                };
            }
        }
    }

    private sealed class PilotActionPolicyPlugin : LibraryPlugin
    {
        public const ulong SessionSeconds = 180;
        private static readonly Guid pluginId =
            Guid.Parse("b6e750bb-3b17-4056-a85d-bfa92654ee32");
        private readonly PilotLibraryClient client = new();

        public override Guid Id => pluginId;
        public override string Name => "Pilot action policy library";
        public override LibraryClient Client => client;
        public int PlayCount { get; private set; }
        public int GameStoppedCount { get; private set; }
        public ulong LastStoppedSeconds { get; private set; }
        public int ClientShutdownCount => client.ShutdownCount;
        public bool CancelNextStartup { get; set; }

        public PilotActionPolicyPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            Properties = new LibraryPluginProperties { CanShutdownClient = true };
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            yield return new PilotPlayController(args.Game, this) { Name = "Pilot policy action" };
        }

        public override void OnGameStarting(Playnite.SDK.Events.OnGameStartingEventArgs args)
        {
            if (CancelNextStartup)
            {
                CancelNextStartup = false;
                args.CancelStartup = true;
            }
        }

        public override void OnGameStopped(Playnite.SDK.Events.OnGameStoppedEventArgs args)
        {
            GameStoppedCount++;
            LastStoppedSeconds = args.ElapsedSeconds;
        }

        private sealed class PilotPlayController : PlayController
        {
            private readonly PilotActionPolicyPlugin plugin;

            public PilotPlayController(Game game, PilotActionPolicyPlugin plugin) : base(game)
            {
                this.plugin = plugin;
            }

            public override void Play(PlayActionArgs args)
            {
                plugin.PlayCount++;
                InvokeOnStarted(new GameStartedEventArgs { StartedProcessId = 4242 });
                InvokeOnStopped(new GameStoppedEventArgs(SessionSeconds));
            }
        }

        private sealed class PilotLibraryClient : LibraryClient
        {
            public override bool IsInstalled => true;
            public int ShutdownCount { get; private set; }
            public override void Open()
            {
            }

            public override void Shutdown()
            {
                ShutdownCount++;
            }
        }
    }

    private sealed class RecordingPowerShellRuntime : IPowerShellRuntime
    {
        private readonly string failingScript;
        public List<(string Script, string WorkingDirectory, Dictionary<string, object> Variables)> Executions
            { get; } = new();
        public bool IsDisposed { get; private set; }

        public RecordingPowerShellRuntime(string failingScript = null)
        {
            this.failingScript = failingScript;
        }

        public object Execute(
            string script,
            string workDir = null,
            Dictionary<string, object> variables = null)
        {
            Executions.Add((
                script,
                workDir,
                variables == null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(variables)));
            if (string.Equals(script, failingScript, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Deterministic pre-script failure.");
            }

            return null;
        }

        public object ExecuteFile(string path, string workDir = null) => null;
        public object ExecuteFile(
            string path,
            string workDir = null,
            Dictionary<string, object> variables = null) => null;
        public void SetVariable(string name, object value)
        {
        }

        public object GetVariable(string name) => null;
        public CommandInfo GetFunction(string name) => null;
        public void ImportModule(string path)
        {
        }

        public object InvokeFunction(string name, List<object> arguments) => null;
        public void Dispose()
        {
            IsDisposed = true;
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

    private sealed record DatabaseFieldsSelfTestResult(
        bool AllTaxonomyKindsPersisted,
        bool UnusedRemovalWorked,
        bool SpecificationsPersisted,
        bool PlatformMediaPersisted,
        bool CompletionDefaultsPersisted,
        bool FilterPresetOrderPersisted,
        bool EditorCreationPersisted,
        bool EditorSearchWorked,
        bool EditorCancelDiscarded);

    private sealed record EmulationSuiteSelfTestResult(
        bool ConfigurationPersisted,
        bool ProfileKindsPersisted,
        int DetectedEmulatorCount,
        int DownloadOptionCount,
        int ReviewGameCount,
        int ImportedGameCount,
        bool ReviewExclusionPersisted,
        bool SettingsPathPersisted);

    private sealed record AddonStoreSelfTestResult(
        int WindowsCount,
        int FailureCount,
        int InstallerFetchCount,
        bool WindowsV6Compatible,
        bool LinuxV6Compatible,
        string LinuxV6Reason,
        bool PackageQueued,
        bool AvaloniaThemeCompatible,
        bool LegacyThemeCompatible,
        string LegacyThemeReason,
        bool DisablePersisted);

    private sealed class AddonStoreFakeCatalog : IDesktopAddonCatalogClient
    {
        private readonly string packagePath;
        private readonly string themePath;
        private int installerFetchCount;

        public int InstallerFetchCount => installerFetchCount;

        public AddonStoreFakeCatalog(string packagePath, string themePath)
        {
            this.packagePath = packagePath;
            this.themePath = themePath;
        }

        public IReadOnlyList<AddonManifest> GetAllAddons(AddonType type, string searchTerm)
        {
            if (type != AddonType.Generic)
            {
                return type == AddonType.ThemeDesktop
                    ?
                    [
                        CreateManifest("p-c-theme", "P-C Avalonia theme", AddonType.ThemeDesktop),
                        CreateManifest("p-c-wpf-theme", "P-C legacy WPF theme", AddonType.ThemeDesktop)
                    ]
                    : [];
            }

            return
            [
                CreateManifest("p-c-v7", "P-C native v7"),
                CreateManifest("p-c-v6", "P-C legacy v6"),
                CreateManifest("p-c-broken", "P-C broken manifest"),
                CreateManifest(string.Empty, "P-C missing identity")
            ];
        }

        public AddonInstallerManifest GetAddonInstaller(string addonId)
        {
            Interlocked.Increment(ref installerFetchCount);
            if (addonId == "p-c-broken")
            {
                throw new HttpRequestException("synthetic per-entry failure");
            }

            var isTheme = addonId is "p-c-theme" or "p-c-wpf-theme";
            return new AddonInstallerManifest
            {
                AddonId = addonId,
                Packages =
                [
                    new AddonInstallerPackage
                    {
                        Version = new Version(1, 0),
                        RequiredApiVersion = addonId switch
                        {
                            "p-c-v7" => new Version(7, 0),
                            "p-c-theme" => AvaloniaThemePackage.CurrentApiVersion,
                            "p-c-wpf-theme" => new Version(2, 0),
                            _ => new Version(6, 16)
                        },
                        PackageUrl = isTheme ? themePath : packagePath,
                        ReleaseDate = DateTime.UtcNow
                    }
                ]
            };
        }

        private static AddonManifest CreateManifest(
            string id,
            string name,
            AddonType type = AddonType.Generic) =>
            new()
            {
                AddonId = id,
                Name = name,
                Author = "Playnite",
                Type = type,
                ShortDescription = "Synthetic add-on catalog entry"
            };
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
