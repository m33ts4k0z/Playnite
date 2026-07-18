using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Controls;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace TestPluginV7;

public sealed class TestPlugin : LibraryPlugin
{
    private readonly TestSettings settings;
    private readonly TestLibraryClient client;
    private bool databaseEventsSubscribed;
    private string EventPath => Path.Combine(GetPluginUserDataPath(), "events.txt");
    private string WebViewProbePath => Path.Combine(GetPluginUserDataPath(), "web-view-probe-url.txt");
    private string ApiParityProbePath => Path.Combine(GetPluginUserDataPath(), "api-parity-probe.txt");

    public override Guid Id { get; } = Guid.Parse("8134f4eb-556e-4e01-936f-1bf5a808cb10");
    public override string Name => "Test SDK v7 library";
    public override string LibraryIcon => "library-icon.png";
    public override string LibraryBackground => "library-background.png";
    public override LibraryClient Client => client;

    public TestPlugin(IPlayniteAPI api)
        : base(api)
    {
        settings = LoadPluginSettings<TestSettings>() ?? new TestSettings();
        settings.Plugin = this;
        settings.EventPath = EventPath;
        client = new TestLibraryClient(EventPath);
        Properties = new LibraryPluginProperties
        {
            HasSettings = true,
            CanShutdownClient = true,
            HasCustomizedGameImport = false
        };
        AddCustomElementSupport(new AddCustomElementSupportArgs
        {
            SourceName = "TestSdkV7",
            ElementList = ["GameStatus"]
        });
        AddConvertersSupport(new AddConvertersSupportArgs
        {
            SourceName = "TestSdkV7",
            Converters = [new TestPrefixConverter()]
        });
        api.UriHandler.RegisterSource("sdk-v7-probe", HandleUri);
        LogManager.GetLogger().Info("SDK v7 fixture logger initialized");
        File.AppendAllLines(EventPath, ["constructed:" + api.ApplicationInfo.Mode]);
        api.Notifications.Add("test-v7-loaded", "SDK v7 plugin constructed", NotificationType.Info);
    }

    public override ISettings GetSettings(bool firstRunSettings) => settings;

    public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
    {
        yield return new MainMenuItem
        {
            Description = "SDK v7 main command",
            MenuSection = "SDK v7|Tools",
            Icon = "main-menu-icon.png",
            Action = actionArgs => File.AppendAllLines(EventPath,
            [
                $"menu-main:{actionArgs.SourceItem.Description}:{args.IsGlobalSearchRequest}"
            ])
        };
        if (File.Exists(WebViewProbePath))
        {
            yield return new MainMenuItem
            {
                Description = "SDK v7 web-view probe",
                MenuSection = "SDK v7|Tests",
                Action = _ => RunWebViewProbe()
            };
        }
        if (File.Exists(ApiParityProbePath))
        {
            yield return new MainMenuItem
            {
                Description = "SDK v7 API parity probe",
                MenuSection = "SDK v7|Tests",
                Action = _ => RunApiParityProbe()
            };
        }
    }

    private void RunWebViewProbe()
    {
        var address = new Uri(File.ReadAllText(WebViewProbePath).Trim());
        using var view = PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings
        {
            UserAgent = "Playnite SDK v7 fixture",
            WindowWidth = 960,
            WindowHeight = 540
        });
        var loading = new List<bool>();
        view.LoadingChanged += (_, eventArgs) => loading.Add(eventArgs.IsLoading);
        view.NavigateAsync(address).GetAwaiter().GetResult();
        var text = view.GetPageTextAsync().GetAwaiter().GetResult();
        var source = view.GetPageSourceAsync().GetAwaiter().GetResult();
        var evaluation = view.EvaluateScriptAsync("window.playniteSdkV7").GetAwaiter().GetResult();
        var cookies = view.GetCookiesAsync().GetAwaiter().GetResult();
        view.SetCookieAsync(address, new HttpCookie
        {
            Name = "sdk-v7-probe",
            Value = "written",
            Domain = address.Host,
            Path = "/",
            SameSite = CookieSameSite.Unspecified,
            Priority = CookiePriority.Medium
        }).GetAwaiter().GetResult();
        view.DeleteCookiesAsync(address, "sdk-v7-probe").GetAwaiter().GetResult();
        File.AppendAllLines(EventPath,
        [
            $"web-loading:{string.Join(',', loading)}",
            $"web-address:{view.Address}",
            $"web-text:{text}",
            $"web-source:{source.Contains("SDK v7 page source", StringComparison.Ordinal)}",
            $"web-script:{evaluation.Success}:{evaluation.Result}",
            $"web-cookie:{cookies.Single().Name}:{cookies.Single().Priority}"
        ]);
    }

    private void RunApiParityProbe()
    {
        var main = PlayniteApi.MainView;
        var filtered = main.FilteredGames;
        var selected = main.SelectedGames.ToList();
        var requestedGameId = File.ReadAllText(ApiParityProbePath).Trim();
        var game = filtered.FirstOrDefault(item =>
            string.Equals(item.GameId, requestedGameId, StringComparison.Ordinal))
            ?? filtered.First(item => !string.IsNullOrWhiteSpace(item.GameId));
        var activeDesktopView = main.ActiveDesktopView;
        var activeFullscreenView = main.ActiveFullscreenView;
        var sortOrder = main.SortOrder;
        var sortDirection = main.SortOrderDirection;
        var grouping = main.Grouping;
        var activePreset = main.GetActiveFilterPreset();
        var filterSettings = main.GetCurrentFilterSettings();
        var presets = main.GetSortedFilterPresets();
        var fullscreenPresets = main.GetSortedFilterFullscreenPresets();

        main.ActiveDesktopView = DesktopView.List;
        main.SortOrderDirection = SortOrderDirection.Descending;
        main.Grouping = GroupableField.Genre;
        main.SelectGame(game.Id);
        main.SelectGames([game.Id]);
        main.ApplyFilterPreset(activePreset);
        main.OpenSearch("sdk-v7-search");
        main.OpenSearch(new ProbeSearchContext(EventPath), "custom-term");
        main.SwitchToLibraryView();
        main.ToggleFullscreenView();
        var settingsOpened = main.OpenPluginSettingsAsync(Id).GetAwaiter().GetResult();
        var editResult = main.OpenEditDialogAsync(game.Id).GetAwaiter().GetResult();

        var settings = PlayniteApi.ApplicationSettings;
        var muted = settings.Fullscreen.IsMusicMuted;
        settings.Fullscreen.IsMusicMuted = !muted;
        var settingsValues = string.Join(',', new object[]
        {
            settings.Version,
            settings.GridItemWidthRatio,
            settings.GridItemHeightRatio,
            settings.FirstTimeWizardComplete,
            settings.DisableHwAcceleration,
            settings.AsyncImageLoading,
            settings.DownloadMetadataOnImport,
            settings.StartInFullscreen,
            settings.MinimizeToTray,
            settings.CloseToTray,
            settings.EnableTray,
            settings.UpdateLibStartup,
            settings.StartMinimized,
            settings.StartOnBoot,
            settings.PlaytimeImportMode,
            settings.DiscordPresenceEnabled,
            settings.AgeRatingOrgPriority,
            settings.SidebarVisible,
            settings.SidebarPosition,
            settings.Fullscreen.SwapConfirmCancelButtons,
            settings.Fullscreen.SwapStartDetailsAction,
            settings.Fullscreen.GuideButtonFocus
        });
        var expanded = PlayniteApi.ExpandGameVariables(game, "game={Name};emu={EmulatorDir}", "C:\\Emulator");
        var expandedAction = PlayniteApi.ExpandGameVariables(game, new GameAction
        {
            Type = GameActionType.File,
            Path = "{InstallDir}\\{Name}.exe"
        });
        var controllers = PlayniteApi.GetConnectedControllers();
        var excluded = settings.GetGameExcludedFromImport(game.GameId, Id);
        var choice = PlayniteApi.Dialogs.ShowChoiceAsync(
            "SDK v7 choice",
            "SDK v7 dialogs",
            ["First", "Second"],
            1,
            0).GetAwaiter().GetResult();
        var selectedFile = PlayniteApi.Dialogs.SelectFileAsync("Text files|*.txt").GetAwaiter().GetResult();
        var selectedFiles = PlayniteApi.Dialogs.SelectFilesAsync("Text files|*.txt").GetAwaiter().GetResult();
        var selectedFolder = PlayniteApi.Dialogs.SelectFolderAsync().GetAwaiter().GetResult();
        var currentWindow = PlayniteApi.Dialogs.GetCurrentAppWindow();
        var resource = PlayniteApi.Resources.GetString("SDKv7ProbeString");
        var staticResource = Playnite.SDK.ResourceProvider.GetString("SDKv7ProbeString");
        var resourceType = PlayniteApi.Resources.GetResource("SDKv7ProbeBrush")?.GetType().Name;
        var addons = PlayniteApi.Addons.Addons;
        var disabledAddons = PlayniteApi.Addons.DisabledAddons;
        var loadedPlugins = PlayniteApi.Addons.Plugins;
        var platforms = PlayniteApi.Emulation.Platforms;
        var regions = PlayniteApi.Emulation.Regions;
        var emulators = PlayniteApi.Emulation.Emulators;
        var platform = platforms.FirstOrDefault();
        var region = regions.FirstOrDefault();
        var emulator = emulators.FirstOrDefault();
        var platformRoundTrip = platform != null &&
            PlayniteApi.Emulation.GetPlatform(platform.Id)?.Id == platform.Id;
        var regionRoundTrip = region != null &&
            PlayniteApi.Emulation.GetRegion(region.Id)?.Id == region.Id;
        var emulatorRoundTrip = emulator != null &&
            PlayniteApi.Emulation.GetEmulator(emulator.Id)?.Id == emulator.Id;
        PlayniteApi.UriHandler.RemoveSource("sdk-v7-probe");
        PlayniteApi.UriHandler.RegisterSource("sdk-v7-probe", HandleUri);
        var sqlitePath = Path.Combine(GetPluginUserDataPath(), "sdk-v7-probe.sqlite");
        var sqlite = SQLite.OpenDatabase(
            sqlitePath,
            SqliteOpenFlags.ReadWrite | SqliteOpenFlags.Create);
        var sqliteRow = sqlite.Query<SqliteProbeRow>(
            "SELECT 7 AS Number, 'bridge' AS Text").Single();
        var secondSqliteRow = sqlite.Query<SqliteProbeRow>(
            "SELECT 8 AS Number, 'second' AS Text").Single();
        sqlite.Dispose();
        var sqliteDisposed = false;
        try
        {
            sqlite.Query<SqliteProbeRow>("SELECT 9 AS Number, 'closed' AS Text");
        }
        catch (ObjectDisposedException)
        {
            sqliteDisposed = true;
        }

        var unsupported = new List<string>();
        try
        {
            main.SelectGames(filtered.Take(2).Select(item => item.Id));
        }
        catch (NotSupportedException)
        {
            unsupported.Add("multi-select");
        }
        File.AppendAllLines(EventPath,
        [
            $"api-main:{activeDesktopView}:{activeFullscreenView}:{sortOrder}:{sortDirection}:{grouping}:" +
            $"{selected.Count}:{filtered.Count}:{filterSettings != null}:{presets.Count}:{fullscreenPresets.Count}",
            $"api-actions:{settingsOpened}:{editResult}:{activePreset}",
            $"api-settings:{settings.DatabasePath}:{settings.Language}:{settings.DesktopTheme}:" +
            $"{settings.FullscreenTheme}:{settings.FontFamilyName}:{settingsValues}",
            $"api-completion:{settings.CompletionStatus.DefaultStatus}:{settings.CompletionStatus.PlayedStatus}:{excluded}",
            $"api-expanded:{expanded}:{expandedAction.Path}",
            $"api-controllers:{controllers.Count}",
            $"api-dialogs:{choice}:{selectedFile}:{selectedFiles.Count}:{selectedFolder}:" +
            $"{currentWindow != null}",
            $"api-resources:{resource}:{staticResource}:{resourceType}",
            $"api-addons:{addons.Count}:{disabledAddons.Count}:{loadedPlugins.Count}:" +
            $"{loadedPlugins.Single().Id}:{loadedPlugins.Single() is LibraryPlugin}:" +
            $"{ReferenceEquals(loadedPlugins.Single(), this)}",
            $"api-emulation:{platforms.Count > 0}:{regions.Count > 0}:{emulators.Count > 0}:" +
            $"{platformRoundTrip}:{regionRoundTrip}:{emulatorRoundTrip}",
            $"api-sqlite:{sqliteRow.Number}:{sqliteRow.Text}:" +
            $"{secondSqliteRow.Number}:{secondSqliteRow.Text}:{sqliteDisposed}",
            $"api-unsupported:{string.Join(',', unsupported)}"
        ]);
    }

    private void HandleUri(PlayniteUriEventArgs args) => File.AppendAllLines(
        EventPath,
        [$"uri:{string.Join(',', args.Arguments ?? [])}"]);

    public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
    {
        yield return new GameMenuItem
        {
            Description = "SDK v7 game command",
            MenuSection = "SDK v7|Game",
            Icon = "game-menu-icon.png",
            Action = actionArgs => File.AppendAllLines(EventPath,
            [
                $"menu-game:{string.Join(",", actionArgs.Games.Select(game => game.Name))}:" +
                args.IsGlobalSearchRequest
            ])
        };
    }

    public override Control GetGameViewControl(GetGameViewControlArgs args)
    {
        if (args.Name != "GameStatus")
        {
            return null!;
        }

        File.AppendAllLines(EventPath, [$"element-created:{args.Mode}"]);
        return new TestGameView();
    }

    public override IEnumerable<SidebarItem> GetSidebarItems()
    {
        var quickAction = new SidebarItem
        {
            Type = SiderbarItemType.Button,
            Title = "SDK v7 quick action",
            Icon = "quick-action-icon",
            ProgressValue = 25,
            ProgressMaximum = 100
        };
        quickAction.Activated = () =>
        {
            quickAction.ProgressValue = 50;
            File.AppendAllLines(EventPath, ["sidebar-activated"]);
        };
        yield return quickAction;
        yield return new SidebarItem
        {
            Type = SiderbarItemType.View,
            Title = "SDK v7 sidebar view",
            Icon = "sidebar-view-icon",
            Opened = () =>
            {
                File.AppendAllLines(EventPath, ["sidebar-opened"]);
                return new TextBlock { Text = "SDK v7 sidebar content" };
            },
            Closed = () => File.AppendAllLines(EventPath, ["sidebar-closed"])
        };
    }

    public override IEnumerable<TopPanelItem> GetTopPanelItems()
    {
        var item = new TopPanelItem
        {
            Title = "SDK v7 top action",
            Icon = "top-action-icon"
        };
        item.Activated = () =>
        {
            item.Title = "SDK v7 top action used";
            File.AppendAllLines(EventPath, ["top-panel-activated"]);
        };
        yield return item;
    }

    public override Control GetSettingsView(bool firstRunView)
    {
        var count = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999,
            Width = 180
        };
        count.Bind(NumericUpDown.ValueProperty, new Binding(nameof(TestSettings.LaunchCount))
        {
            Source = settings,
            Mode = BindingMode.TwoWay
        });
        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "SDK v7 launch count" },
                count
            }
        };
    }

    public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
    {
        args.CancelToken.ThrowIfCancellationRequested();
        File.AppendAllLines(EventPath, ["library-get-games"]);
        yield return new GameMetadata
        {
            Name = "SDK v7 library game",
            GameId = "sdk-v7-library-game",
            IsInstalled = true,
            InstallDirectory = "C:\\SDKv7Library",
            Playtime = 120,
            Genres = [new MetadataNameProperty("SDK v7 library genre")]
        };
    }

    public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
    {
        args.CancelToken.ThrowIfCancellationRequested();
        File.AppendAllLines(EventPath, ["library-import-games"]);
        yield return new Game("SDK v7 customized import game")
        {
            PluginId = Id,
            GameId = "sdk-v7-customized-game"
        };
    }

    public override LibraryMetadataProvider GetMetadataDownloader() =>
        new TestLibraryMetadataProvider(EventPath);

    public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) =>
        File.AppendAllLines(EventPath, ["event-library-updated"]);

    public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
    {
        yield return new TestPlayController(args.Game, EventPath) { Name = "SDK v7 play action" };
    }

    public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
    {
        yield return new TestInstallController(args.Game, EventPath) { Name = "SDK v7 install action" };
    }

    public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
    {
        yield return new TestUninstallController(args.Game, EventPath) { Name = "SDK v7 uninstall action" };
    }

    public override void OnGameStarting(OnGameStartingEventArgs args)
    {
        File.AppendAllLines(EventPath, ["event-starting:" + args.Game.Name]);
        if (args.Game.Name.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            args.CancelStartup = true;
        }
    }

    public override void OnGameStarted(OnGameStartedEventArgs args) =>
        File.AppendAllLines(EventPath, [$"event-started:{args.StartedProcessId}"]);

    public override void OnGameStopped(OnGameStoppedEventArgs args) =>
        File.AppendAllLines(EventPath, [$"event-stopped:{args.ElapsedSeconds}"]);

    public override void OnGameInstalled(OnGameInstalledEventArgs args) =>
        File.AppendAllLines(EventPath, ["event-installed:" + args.Game.Name]);

    public override void OnGameInstallationCancelled(OnGameInstallationCancelledEventArgs args) =>
        File.AppendAllLines(EventPath, ["event-install-cancelled:" + args.Game.Name]);

    public override void OnGameUninstalled(OnGameUninstalledEventArgs args) =>
        File.AppendAllLines(EventPath, ["event-uninstalled:" + args.Game.Name]);

    public override void OnGameStartupCancelled(OnGameStartupCancelledEventArgs args) =>
        File.AppendAllLines(EventPath, ["event-startup-cancelled:" + args.Game.Name]);

    public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
    {
        File.AppendAllLines(EventPath, ["started"]);
        if (!PlayniteApi.Database.IsOpen)
        {
            return;
        }

        PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;
        databaseEventsSubscribed = true;
        var game = PlayniteApi.Database.Games.FirstOrDefault(item => item.Name == "SDK v7 bridge game");
        if (game == null)
        {
            throw new InvalidOperationException("SDK v7 database transport did not return the seeded game.");
        }

        File.AppendAllLines(EventPath, ["database-read:" + game.Name]);
        game.Name += " updated";
        using (PlayniteApi.Database.Games.BufferedUpdate())
        {
            PlayniteApi.Database.Games.Update(game);
        }
        var genre = PlayniteApi.Database.Genres.Add("SDK v7 bridge genre");
        File.AppendAllLines(EventPath, ["database-write:" + genre.Name]);

        Tag tag;
        using (PlayniteApi.Database.BufferedUpdate())
        {
            tag = PlayniteApi.Database.Tags.Add(new MetadataNameProperty("SDK v7 bridge tag"));
        }
        var filteredCount = PlayniteApi.Database.GetFilteredGames(new FilterPresetSettings()).Count();
        var imported = PlayniteApi.Database.ImportGame(new GameMetadata
        {
            Name = "SDK v7 imported game",
            Genres = [new MetadataNameProperty("SDK v7 imported genre")]
        });

        var sourcePath = Path.Combine(GetPluginUserDataPath(), "database-source.txt");
        var exportedPath = Path.Combine(GetPluginUserDataPath(), "database-export.txt");
        File.WriteAllText(sourcePath, "SDK v7 file transport");
        var databaseFile = PlayniteApi.Database.AddFile(sourcePath, game.Id);
        var storedPath = PlayniteApi.Database.GetFullFilePath(databaseFile);
        PlayniteApi.Database.SaveFile(databaseFile, exportedPath);
        if (!File.Exists(storedPath) || File.ReadAllText(exportedPath) != "SDK v7 file transport")
        {
            throw new InvalidOperationException("SDK v7 database file transport failed.");
        }
        PlayniteApi.Database.RemoveFile(databaseFile);
        File.AppendAllLines(EventPath,
        [
            $"database-extra:{tag.Name}:{filteredCount}:{imported.Name}:{!File.Exists(storedPath)}"
        ]);
    }

    public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
    {
        if (databaseEventsSubscribed)
        {
            PlayniteApi.Database.Games.ItemUpdated -= Games_ItemUpdated;
            databaseEventsSubscribed = false;
        }
        File.AppendAllLines(EventPath, ["stopped"]);
    }

    private void Games_ItemUpdated(object? sender, ItemUpdatedEventArgs<Game> args)
    {
        foreach (var update in args.UpdatedItems)
        {
            File.AppendAllLines(EventPath, ["database-event:" + update.NewData.Name]);
        }
    }

    private sealed class TestPlayController : PlayController
    {
        private readonly string eventPath;

        public TestPlayController(Game game, string eventPath) : base(game) => this.eventPath = eventPath;

        public override void Play(PlayActionArgs args)
        {
            File.AppendAllLines(eventPath, ["controller-play"]);
            InvokeOnStarted(new GameStartedEventArgs { StartedProcessId = 4242 });
            InvokeOnStopped(new GameStoppedEventArgs(9));
        }
    }

    private sealed class TestInstallController : InstallController
    {
        private readonly string eventPath;

        public TestInstallController(Game game, string eventPath) : base(game) => this.eventPath = eventPath;

        public override void Install(InstallActionArgs args)
        {
            File.AppendAllLines(eventPath, ["controller-install"]);
            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
            {
                InstallDirectory = "C:\\SDKv7Installed"
            }));
        }
    }

    private sealed class TestUninstallController : UninstallController
    {
        private readonly string eventPath;

        public TestUninstallController(Game game, string eventPath) : base(game) => this.eventPath = eventPath;

        public override void Uninstall(UninstallActionArgs args)
        {
            File.AppendAllLines(eventPath, ["controller-uninstall"]);
            InvokeOnUninstalled();
        }
    }

    private sealed class TestLibraryClient : LibraryClient
    {
        private readonly string eventPath;

        public override bool IsInstalled => true;
        public override string Icon => "client-icon.png";

        public TestLibraryClient(string eventPath) => this.eventPath = eventPath;
        public override void Open() => File.AppendAllLines(eventPath, ["library-client-open"]);
        public override void Shutdown() => File.AppendAllLines(eventPath, ["library-client-shutdown"]);
    }

    public sealed class SqliteProbeRow
    {
        public int Number { get; set; }
        public string? Text { get; set; }
    }

    private sealed class ProbeSearchContext : SearchContext
    {
        private readonly string eventPath;

        public ProbeSearchContext(string eventPath)
        {
            this.eventPath = eventPath;
            Label = "SDK v7 custom search";
            Description = "Search results supplied across the SDK v7 isolation boundary";
            Hint = "Type a fixture term";
            Delay = 25;
        }

        public override IEnumerable<SearchItem> GetSearchResults(GetSearchResultsArgs args)
        {
            yield return new SearchItem(
                "SDK v7 result " + args.SearchTerm,
                new SearchItemAction(
                    "Run",
                    () => File.AppendAllLines(eventPath, ["search-primary:" + args.SearchTerm])))
            {
                Description = $"filters:{args.GameFilterSettings.Uninstalled}:{args.GameFilterSettings.Hidden}",
                SecondaryAction = new SearchItemAction(
                    "Inspect",
                    () => File.AppendAllLines(eventPath, ["search-secondary:" + args.SearchTerm]))
                {
                    CloseSearch = false
                },
                MenuAction = new ContextSwitchSearchItemAction(
                    "Details",
                    new ProbeNestedSearchContext(eventPath))
            };
        }
    }

    private sealed class ProbeNestedSearchContext : SearchContext
    {
        private readonly string eventPath;

        public ProbeNestedSearchContext(string eventPath)
        {
            this.eventPath = eventPath;
            Label = "SDK v7 nested search";
            UseAutoSearch = true;
        }

        public override IEnumerable<SearchItem> GetSearchResults(GetSearchResultsArgs args)
        {
            yield return new SearchItem(
                "Nested SDK v7 result",
                "Open",
                () => File.AppendAllLines(eventPath, ["search-nested"]));
        }
    }

    private sealed class TestGameView : PluginUserControl
    {
        public TestGameView()
        {
            var text = new TextBlock();
            text.Bind(TextBlock.TextProperty, new Binding("GameContext.Name")
            {
                Source = this
            });
            Content = text;
        }
    }

    public sealed class TestPrefixConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            $"sdk-v7:{value}";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value?.ToString()?.Replace("sdk-v7:", string.Empty, StringComparison.Ordinal);
    }

    private sealed class TestLibraryMetadataProvider : LibraryMetadataProvider
    {
        private readonly string eventPath;

        public TestLibraryMetadataProvider(string eventPath) => this.eventPath = eventPath;

        public override GameMetadata GetMetadata(Game game)
        {
            File.AppendAllLines(eventPath, ["library-metadata:" + game.GameId]);
            return new GameMetadata
            {
                Name = "SDK v7 official metadata",
                Genres = [new MetadataNameProperty("SDK v7 official genre")],
                Description = "Metadata supplied by the SDK v7 library provider"
            };
        }

        public override void Dispose() =>
            File.AppendAllLines(eventPath, ["library-metadata-disposed"]);
    }

    public sealed class TestSettings : ObservableObject, ISettings
    {
        private int launchCount;
        private TestSettings? editClone;

        [DontSerialize]
        public TestPlugin? Plugin { get; set; }

        [DontSerialize]
        public string? EventPath { get; set; }

        public int LaunchCount
        {
            get => launchCount;
            set => SetValue(ref launchCount, value);
        }

        public void BeginEdit()
        {
            editClone = Serialization.GetClone(this);
            AppendEvent("settings-begin");
        }
        public void CancelEdit()
        {
            if (editClone != null)
            {
                LaunchCount = editClone.LaunchCount;
            }
            AppendEvent("settings-cancel");
        }
        public void EndEdit()
        {
            Plugin?.SavePluginSettings(this);
            AppendEvent("settings-end");
        }
        public bool VerifySettings(out List<string> errors)
        {
            AppendEvent("settings-verify");
            errors = [];
            return true;
        }

        private void AppendEvent(string value)
        {
            if (!string.IsNullOrWhiteSpace(EventPath))
            {
                File.AppendAllLines(EventPath, [value]);
            }
        }
    }
}
