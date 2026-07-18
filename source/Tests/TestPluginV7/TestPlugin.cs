using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace TestPluginV7;

public sealed class TestPlugin : LibraryPlugin
{
    private readonly TestSettings settings;
    private readonly TestLibraryClient client;
    private bool databaseEventsSubscribed;
    private string EventPath => Path.Combine(GetPluginUserDataPath(), "events.txt");

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
        client = new TestLibraryClient(EventPath);
        Properties = new LibraryPluginProperties
        {
            HasSettings = true,
            CanShutdownClient = true,
            HasCustomizedGameImport = false
        };
        File.AppendAllLines(EventPath, ["constructed:" + api.ApplicationInfo.Mode]);
        api.Notifications.Add("test-v7-loaded", "SDK v7 plugin constructed", NotificationType.Info);
    }

    public override ISettings GetSettings(bool firstRunSettings) => settings;

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

    public sealed class TestSettings : ObservableObject, ISettings
    {
        private int launchCount;
        private TestSettings? editClone;

        [DontSerialize]
        public TestPlugin? Plugin { get; set; }

        public int LaunchCount
        {
            get => launchCount;
            set => SetValue(ref launchCount, value);
        }

        public void BeginEdit() => editClone = Serialization.GetClone(this);
        public void CancelEdit()
        {
            if (editClone != null)
            {
                LaunchCount = editClone.LaunchCount;
            }
        }
        public void EndEdit() => Plugin?.SavePluginSettings(this);
        public bool VerifySettings(out List<string> errors)
        {
            errors = [];
            return true;
        }
    }
}
