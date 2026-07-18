using Newtonsoft.Json;
using NUnit.Framework;
using Playnite.SDK.V7.Host;
using TestPluginV7;

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

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            plugin.GetLibraryGames(cancellation.Token));
        plugin.Dispose();
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
