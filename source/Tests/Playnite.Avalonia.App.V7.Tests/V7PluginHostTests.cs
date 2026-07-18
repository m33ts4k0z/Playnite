using NUnit.Framework;
using Playnite.API;
using Playnite.Avalonia.App.Services;
using Playnite.Database;
using Playnite.SDK;
using Playnite.SDK.Models;
using System.IO;

namespace Playnite.Avalonia.App.V7.Tests;

[TestFixture]
public class V7PluginHostTests
{
    private sealed class TestSettings : IAvaloniaHostSettings
    {
        public int Version => 1;
        public string Language => "en_US";
        public string DesktopTheme => string.Empty;
        public string FullscreenTheme => string.Empty;
        public List<string> DisabledPlugins { get; } = [];
        public bool IsMusicMuted { get; set; }
        public bool SwapConfirmCancelButtons => false;
        public bool SwapStartDetailsAction => false;
        public bool GuideButtonFocus => false;
        public string GlobalPreScript => string.Empty;
        public string GlobalGameStartedScript => string.Empty;
        public string GlobalPostScript => string.Empty;
        public bool ShutdownLibraryClients => false;
        public uint ClientShutdownGraceSeconds => 0;
        public uint ClientShutdownMinimumSessionSeconds => 0;
        public List<Guid> ClientShutdownPluginIds { get; } = [];
    }

    private sealed class TestDialogs : IAvaloniaDialogService
    {
        public string ShowMessage(
            string message,
            string caption,
            IReadOnlyList<string> options,
            int defaultIndex = 0,
            int cancelIndex = -1) => options.ElementAtOrDefault(defaultIndex) ?? string.Empty;
    }

    private string testRoot;
    private string previousUserDataPath;
    private SynchronizationContext previousContext;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(Path.GetTempPath(), $"PlayniteAvaloniaV7Host_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        previousUserDataPath = PlaynitePaths.ConfigRootPath;
        PlaynitePaths.UpdateUserDataDir(testRoot);
        previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
    }

    [TearDown]
    public void TearDown()
    {
        SynchronizationContext.SetSynchronizationContext(previousContext);
        PlaynitePaths.UpdateUserDataDir(previousUserDataPath);
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, true);
        }
    }

    [Test]
    public void DiscoversAndLoadsSdkSevenPluginWithoutLoadingItAsSdkSix()
    {
        var fixturePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "V7Fixture");
        var pluginPath = Path.Combine(fixturePath, "TestPluginV7.dll");
        Assert.That(V7PluginHost.IsSdkV7Assembly(pluginPath), Is.True);
        Assert.That(V7PluginHost.IsSdkV7Assembly(typeof(IPlayniteAPI).Assembly.Location), Is.False);

        using var database = new GameDatabase(Path.Combine(testRoot, "library"));
        database.OpenDatabase();
        var seededGame = new Game("SDK v7 bridge game") { IsInstalled = true };
        database.Games.Add(seededGame);
        var notifications = new NotificationsAPI();
        var status = string.Empty;
        var callbacks = new AvaloniaHostCallbacks
        {
            Mode = ApplicationMode.Desktop,
            Settings = new TestSettings(),
            Dialogs = new TestDialogs(),
            SetStatus = value => status = value
        };
        using var host = new V7PluginHost(
            database,
            callbacks,
            notifications,
            () => null,
            () => [],
            Path.Combine(fixturePath, "Host"));
        var manifest = ExtensionManifest.FromFile(Path.Combine(fixturePath, "extension.yaml"));

        var claimed = host.Load([manifest], []);

        Assert.That(claimed, Is.EqualTo(new[] { manifest.Id }));
        Assert.That(
            host.FailedPlugins,
            Is.Empty,
            host.FailedPlugins.FirstOrDefault()?.Exception.ToString() ?? status);
        Assert.That(host.Plugins, Has.Count.EqualTo(1));
        Assert.That(host.Plugins[0].Id, Is.EqualTo(Guid.Parse("8134f4eb-556e-4e01-936f-1bf5a808cb10")));
        Assert.That(host.Plugins[0].Kind, Is.EqualTo("GenericPlugin"));
        Assert.That(host.Plugins[0].HasSettings, Is.True);
        Assert.That(notifications.Messages.Select(message => message.Id), Does.Contain("test-v7-loaded"));
        Assert.That(database.Games[seededGame.Id].Name, Is.EqualTo("SDK v7 bridge game updated"));
        Assert.That(database.Genres.Any(genre => genre.Name == "SDK v7 bridge genre"), Is.True);
        Assert.That(database.Tags.Any(tag => tag.Name == "SDK v7 bridge tag"), Is.True);
        var imported = database.Games.FirstOrDefault(game => game.Name == "SDK v7 imported game");
        Assert.That(imported, Is.Not.Null);
        Assert.That(
            imported.GenreIds.Select(id => database.Genres[id]?.Name),
            Does.Contain("SDK v7 imported genre"));

        var eventPath = Path.Combine(
            PlaynitePaths.ExtensionsDataPath,
            host.Plugins[0].Id.ToString(),
            "events.txt");
        Assert.That(File.ReadAllLines(eventPath), Is.EqualTo(new[]
        {
            "constructed:Desktop",
            "started",
            "database-read:SDK v7 bridge game",
            "database-event:SDK v7 bridge game updated",
            "database-write:SDK v7 bridge genre",
            "database-extra:SDK v7 bridge tag:1:SDK v7 imported game:True"
        }));

        var externallyUpdated = database.Games[seededGame.Id].GetCopy();
        externallyUpdated.Name = "SDK v7 external update";
        database.Games.Update(externallyUpdated);
        Assert.That(File.ReadAllLines(eventPath).Last(), Is.EqualTo("database-event:SDK v7 external update"));

        host.Dispose();
        Assert.That(
            File.ReadAllLines(eventPath).Last(),
            Is.EqualTo("stopped"));
    }

    [Test]
    public void ClaimsButDoesNotConstructDisabledSdkSevenPlugin()
    {
        var fixturePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "V7Fixture");
        var manifest = ExtensionManifest.FromFile(Path.Combine(fixturePath, "extension.yaml"));
        using var database = new GameDatabase(Path.Combine(testRoot, "library"));
        using var host = new V7PluginHost(
            database,
            new AvaloniaHostCallbacks
            {
                Mode = ApplicationMode.Desktop,
                Settings = new TestSettings(),
                Dialogs = new TestDialogs()
            },
            new NotificationsAPI(),
            () => null,
            () => [],
            Path.Combine(fixturePath, "Host"));

        var claimed = host.Load([manifest], [manifest.Id]);

        Assert.That(claimed, Is.EqualTo(new[] { manifest.Id }));
        Assert.That(host.Plugins, Is.Empty);
        Assert.That(host.FailedPlugins, Is.Empty);
    }
}
