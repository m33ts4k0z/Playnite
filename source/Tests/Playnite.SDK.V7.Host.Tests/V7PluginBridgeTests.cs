using Newtonsoft.Json;
using NUnit.Framework;
using Playnite.SDK.V7.Host;
using TestPluginV7;
using TestV7MetadataPlugin = TestMetadataPluginV7.TestMetadataPlugin;

namespace Playnite.SDK.V7.Host.Tests;

[TestFixture]
public class V7PluginBridgeTests
{
    private string testRoot;
    private string extensionsDataPath;
    private List<(string Operation, string Payload)> calls;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(Path.GetTempPath(), $"PlayniteSdkV7HostTests_{Guid.NewGuid():N}");
        extensionsDataPath = Path.Combine(testRoot, "ExtensionsData");
        Directory.CreateDirectory(extensionsDataPath);
        calls = [];
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, true);
        }
    }

    [Test]
    public void LoadsPluginAndRunsApplicationLifecycle()
    {
        var loaded = V7PluginBridge.LoadAll(typeof(TestPlugin).Assembly.Location, HostCall);
        Assert.That(loaded, Has.Length.EqualTo(1));
        var plugin = (V7PluginInstance)loaded[0];

        Assert.That(plugin.Id, Is.EqualTo(Guid.Parse("8134f4eb-556e-4e01-936f-1bf5a808cb10")));
        Assert.That(plugin.Kind, Is.EqualTo("LibraryPlugin"));
        Assert.That(plugin.HasSettings, Is.True);
        Assert.That(plugin.CanShutdownLibraryClient, Is.True);
        Assert.That(plugin.HasCustomizedGameImport, Is.False);
        Assert.That(plugin.LibraryIcon, Is.EqualTo("library-icon.png"));
        Assert.That(plugin.LibraryBackground, Is.EqualTo("library-background.png"));
        Assert.That(plugin.HasLibraryClient, Is.True);
        Assert.That(plugin.IsLibraryClientInstalled, Is.True);
        Assert.That(plugin.LibraryClientIcon, Is.EqualTo("client-icon.png"));
        Assert.That(calls.Any(call => call.Operation == "NotificationAdd"), Is.True);

        plugin.InvokeApplicationStarted();
        plugin.InvokeApplicationStarted();
        plugin.Dispose();

        var eventPath = Path.Combine(
            extensionsDataPath,
            plugin.Id.ToString(),
            "events.txt");
        var events = File.ReadAllLines(eventPath);
        Assert.That(events, Is.EqualTo(new[] { "constructed:Desktop", "started", "stopped" }));
    }

    [Test]
    public void BridgesLibraryImportContractsAndCancellation()
    {
        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(typeof(TestPlugin).Assembly.Location, HostCall)
            .Single();

        var games = Newtonsoft.Json.Linq.JArray.Parse(
            plugin.GetLibraryGames(CancellationToken.None));
        Assert.That(games, Has.Count.EqualTo(1));
        Assert.That(games[0]["Name"]?.ToObject<string>(), Is.EqualTo("SDK v7 library game"));
        Assert.That(games[0]["Genres"]?[0]?["Kind"]?.ToObject<string>(), Is.EqualTo("Name"));

        var imported = Newtonsoft.Json.Linq.JArray.Parse(
            plugin.ImportLibraryGames(CancellationToken.None));
        Assert.That(imported, Has.Count.EqualTo(1));
        Assert.That(imported[0]["GameId"]?.ToObject<string>(), Is.EqualTo("sdk-v7-customized-game"));

        var libraryMetadata = (V7LibraryMetadataProviderInstance)plugin.CreateLibraryMetadataProvider();
        var metadata = Newtonsoft.Json.Linq.JObject.Parse(libraryMetadata.GetMetadata(
            JsonConvert.SerializeObject(new Playnite.SDK.Models.Game("Library game")
            {
                GameId = "library-game"
            })));
        Assert.That(metadata["Name"]?.ToObject<string>(), Is.EqualTo("SDK v7 official metadata"));
        libraryMetadata.Dispose();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            plugin.GetLibraryGames(cancellation.Token));
        plugin.Dispose();
    }

    [Test]
    public void BridgesOnDemandMetadataFieldsAndCancellation()
    {
        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(typeof(TestV7MetadataPlugin).Assembly.Location, HostCall)
            .Single();

        Assert.That(plugin.Kind, Is.EqualTo("MetadataPlugin"));
        Assert.That(plugin.Name, Is.EqualTo("Test SDK v7 metadata provider"));
        Assert.That(plugin.SupportedMetadataFields, Is.EquivalentTo(
            Enum.GetNames<Playnite.SDK.Plugins.MetadataField>()));
        var provider = (V7MetadataProviderInstance)plugin.CreateMetadataProvider(
            JsonConvert.SerializeObject(new Playnite.SDK.Models.Game("Metadata bridge game")),
            true);
        Assert.That(provider.AvailableFields, Is.EquivalentTo(plugin.SupportedMetadataFields));
        Assert.That(
            JsonConvert.DeserializeObject<string>(provider.GetField("Name", CancellationToken.None)),
            Is.EqualTo("SDK v7 metadata name"));
        var genres = Newtonsoft.Json.Linq.JArray.Parse(
            provider.GetField("Genres", CancellationToken.None));
        Assert.That(genres[0]["Kind"]?.ToObject<string>(), Is.EqualTo("Name"));
        var image = Newtonsoft.Json.Linq.JObject.Parse(
            provider.GetField("CoverImage", CancellationToken.None));
        Assert.That(image["FileName"]?.ToObject<string>(), Is.EqualTo("v7-cover.png"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            provider.GetField("Name", cancellation.Token));
        provider.Dispose();
        plugin.Dispose();

        var eventPath = Path.Combine(
            extensionsDataPath,
            plugin.Id.ToString(),
            "metadata-events.txt");
        Assert.That(File.ReadAllLines(eventPath), Is.EqualTo(new[]
        {
            "provider-created:Metadata bridge game:True",
            "provider-disposed"
        }));
    }

    [Test]
    public void InitializesSdkSevenSerializationForPluginSettings()
    {
        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(typeof(TestPlugin).Assembly.Location, HostCall)
            .Single();
        var settings = (TestPlugin.TestSettings)plugin.GetSettings();
        settings.LaunchCount = 7;

        var settingsPath = Path.Combine(extensionsDataPath, plugin.Id.ToString(), "config.json");
        File.WriteAllText(settingsPath, Playnite.SDK.Data.Serialization.ToJson(settings, true));
        var loaded = Playnite.SDK.Data.Serialization.FromJsonFile<TestPlugin.TestSettings>(settingsPath);

        Assert.That(loaded.LaunchCount, Is.EqualTo(7));
        plugin.Dispose();
    }

    [Test]
    public void RunsSdkSevenSettingsEditLifecycleWithAvaloniaView()
    {
        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(typeof(TestPlugin).Assembly.Location, HostCall)
            .Single();

        var firstView = plugin.BeginSettingsEdit();
        Assert.That(firstView, Is.TypeOf<Avalonia.Controls.StackPanel>());
        var firstValidation = Newtonsoft.Json.Linq.JObject.Parse(plugin.VerifySettings());
        Assert.That(firstValidation.Value<bool>("Valid"), Is.True);
        Assert.That(firstValidation["Errors"], Is.Empty);
        plugin.CancelSettingsEdit();

        Assert.That(plugin.BeginSettingsEdit(), Is.Not.Null);
        Assert.That(Newtonsoft.Json.Linq.JObject.Parse(plugin.VerifySettings()).Value<bool>("Valid"), Is.True);
        plugin.EndSettingsEdit();
        plugin.Dispose();

        var eventPath = Path.Combine(extensionsDataPath, plugin.Id.ToString(), "events.txt");
        Assert.That(File.ReadAllLines(eventPath), Is.EqualTo(new[]
        {
            "constructed:Desktop",
            "settings-begin",
            "settings-verify",
            "settings-cancel",
            "settings-begin",
            "settings-verify",
            "settings-end"
        }));
        Assert.That(
            File.Exists(Path.Combine(extensionsDataPath, plugin.Id.ToString(), "config.json")),
            Is.True);
    }

    [Test]
    public void BridgesSdkSevenMainAndGameMenuActions()
    {
        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(typeof(TestPlugin).Assembly.Location, HostCall)
            .Single();

        var main = (V7MenuItemInstance)plugin.GetMenuItems("Main", "[]", true).Single();
        Assert.That(main.Description, Is.EqualTo("SDK v7 main command"));
        Assert.That(main.MenuSection, Is.EqualTo("SDK v7|Tools"));
        Assert.That(main.Icon, Is.EqualTo("main-menu-icon.png"));
        main.Invoke();

        var gamesJson = JsonConvert.SerializeObject(new[]
        {
            new Playnite.SDK.Models.Game("First game"),
            new Playnite.SDK.Models.Game("Second game")
        });
        var game = (V7MenuItemInstance)plugin.GetMenuItems("Game", gamesJson, false).Single();
        Assert.That(game.Description, Is.EqualTo("SDK v7 game command"));
        Assert.That(game.MenuSection, Is.EqualTo("SDK v7|Game"));
        Assert.That(game.Icon, Is.EqualTo("game-menu-icon.png"));
        game.Invoke();
        plugin.Dispose();

        var eventPath = Path.Combine(extensionsDataPath, plugin.Id.ToString(), "events.txt");
        Assert.That(File.ReadAllLines(eventPath), Is.EqualTo(new[]
        {
            "constructed:Desktop",
            "menu-main:SDK v7 main command:True",
            "menu-game:First game,Second game:False"
        }));
    }

    private string HostCall(string operation, string payload)
    {
        calls.Add((operation, payload));
        return operation switch
        {
            "ApplicationMode" => "Desktop",
            "ApplicationVersion" => "10.0.0",
            "ApplicationPath" => testRoot,
            "ConfigurationPath" => testRoot,
            "ExtensionsDataPath" => extensionsDataPath,
            "DatabasePath" => Path.Combine(testRoot, "library"),
            "Language" => "en_US",
            "DisabledAddons" or "Addons" => JsonConvert.SerializeObject(Array.Empty<string>()),
            "IsPortable" or "InOfflineMode" or "IsDebugBuild" or "ThrowAllErrors" => "false",
            _ => string.Empty
        };
    }
}
