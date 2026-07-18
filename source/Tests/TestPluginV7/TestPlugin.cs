using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace TestPluginV7;

public sealed class TestPlugin : GenericPlugin
{
    private readonly TestSettings settings;
    private bool databaseEventsSubscribed;
    private string EventPath => Path.Combine(GetPluginUserDataPath(), "events.txt");

    public override Guid Id { get; } = Guid.Parse("8134f4eb-556e-4e01-936f-1bf5a808cb10");

    public TestPlugin(IPlayniteAPI api)
        : base(api)
    {
        settings = LoadPluginSettings<TestSettings>() ?? new TestSettings();
        settings.Plugin = this;
        Properties = new GenericPluginProperties { HasSettings = true };
        File.AppendAllLines(EventPath, ["constructed:" + api.ApplicationInfo.Mode]);
        api.Notifications.Add("test-v7-loaded", "SDK v7 plugin constructed", NotificationType.Info);
    }

    public override ISettings GetSettings(bool firstRunSettings) => settings;

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
