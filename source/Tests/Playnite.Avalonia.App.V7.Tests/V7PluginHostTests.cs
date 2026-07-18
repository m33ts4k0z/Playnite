using NUnit.Framework;
using Playnite.API;
using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.Controls;
using Playnite.Avalonia.Markup;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Metadata;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Avalonia.Controls;
using System.Globalization;
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

    private sealed class TestMetadataSettings : IMetadataDownloadSettings
    {
        public bool DownloadBackgroundsImmediately => true;
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
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, true);
        }
    }

    [Test]
    public void DiscoversAndLoadsSdkSevenPluginWithoutLoadingItAsSdkSix()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "V7Fixture");
        var fixturePath = Path.Combine(fixtureRoot, "Library");
        var pluginPath = Path.Combine(fixturePath, "TestPluginV7.dll");
        Assert.That(V7PluginHost.IsSdkV7Assembly(pluginPath), Is.True);
        Assert.That(V7PluginHost.IsSdkV7Assembly(typeof(IPlayniteAPI).Assembly.Location), Is.False);

        using var database = new GameDatabase(Path.Combine(testRoot, "library"));
        database.OpenDatabase();
        using var controllers = new GameControllerFactory(database);
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
            controllers,
            callbacks,
            notifications,
            () => null,
            () => [],
            Path.Combine(fixtureRoot, "Host"));
        var manifest = ExtensionManifest.FromFile(Path.Combine(fixturePath, "extension.yaml"));

        var claimed = host.Load([manifest], []);

        Assert.That(claimed, Is.EqualTo(new[] { manifest.Id }));
        Assert.That(
            host.FailedPlugins,
            Is.Empty,
            host.FailedPlugins.FirstOrDefault()?.Exception.ToString() ?? status);
        Assert.That(host.Plugins, Has.Count.EqualTo(1));
        Assert.That(host.Plugins[0].Id, Is.EqualTo(Guid.Parse("8134f4eb-556e-4e01-936f-1bf5a808cb10")));
        Assert.That(host.Plugins[0].Kind, Is.EqualTo("LibraryPlugin"));
        Assert.That(host.Plugins[0].HasSettings, Is.True);
        Assert.That(host.LibraryPlugins, Has.Count.EqualTo(1));
        Assert.That(host.LibraryPlugins[0].Name, Is.EqualTo("Test SDK v7 library"));
        Assert.That(host.LibraryPlugins[0].Properties.CanShutdownClient, Is.True);
        Assert.That(host.LibraryPlugins[0].Client.IsInstalled, Is.True);
        Assert.That(host.LibraryPlugins[0].Client.Icon, Is.EqualTo("client-icon.png"));
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

        var settingsView = host.Plugins[0].BeginSettingsEdit();
        Assert.That(settingsView, Is.TypeOf<global::Avalonia.Controls.StackPanel>());
        Assert.That(host.Plugins[0].VerifySettings().IsValid, Is.True);
        host.Plugins[0].CancelSettingsEdit();
        Assert.That(host.Plugins[0].BeginSettingsEdit(), Is.Not.Null);
        Assert.That(host.Plugins[0].VerifySettings().Errors, Is.Empty);
        host.Plugins[0].EndSettingsEdit();

        var libraryGames = database.ImportGames(
            host.LibraryPlugins[0],
            CancellationToken.None,
            PlaytimeImportMode.Always);
        Assert.That(libraryGames, Has.Count.EqualTo(1));
        Assert.That(libraryGames[0].Name, Is.EqualTo("SDK v7 library game"));
        Assert.That(libraryGames[0].PluginId, Is.EqualTo(host.Plugins[0].Id));
        Assert.That(libraryGames[0].Playtime, Is.EqualTo(120));
        Assert.That(
            libraryGames[0].GenreIds.Select(id => database.Genres[id]?.Name),
            Does.Contain("SDK v7 library genre"));
        var customizedGames = host.LibraryPlugins[0]
            .ImportGames(new LibraryImportGamesArgs { })
            .ToList();
        Assert.That(customizedGames.Single().GameId, Is.EqualTo("sdk-v7-customized-game"));
        host.NotifyLibraryUpdated();
        host.LibraryPlugins[0].Client.Open();
        host.LibraryPlugins[0].Client.Shutdown();
        var libraryEvents = File.ReadAllLines(eventPath);
        Assert.That(libraryEvents, Does.Contain("library-get-games"));
        Assert.That(libraryEvents, Does.Contain("library-import-games"));
        Assert.That(libraryEvents, Does.Contain("event-library-updated"));
        Assert.That(libraryEvents, Does.Contain("library-client-open"));
        Assert.That(libraryEvents, Does.Contain("library-client-shutdown"));

        var externallyUpdated = database.Games[seededGame.Id].GetCopy();
        externallyUpdated.Name = "SDK v7 external update";
        database.Games.Update(externallyUpdated);
        Assert.That(File.ReadAllLines(eventPath).Last(), Is.EqualTo("database-event:SDK v7 external update"));

        var play = host.GetPlayControllers(externallyUpdated).Single();
        Assert.That(play.Name, Is.EqualTo("SDK v7 play action"));
        var started = 0;
        var stopped = 0;
        controllers.Started += (_, args) => started = args.StartedProcessId;
        controllers.Stopped += (_, args) => stopped = (int)args.SessionLength;
        controllers.AddController(play);
        play.Play(new PlayActionArgs());
        Assert.That(started, Is.EqualTo(4242));
        Assert.That(stopped, Is.EqualTo(9));
        controllers.RemoveController(play);

        var install = host.GetInstallControllers(externallyUpdated).Single();
        var installDirectory = string.Empty;
        controllers.Installed += (_, args) => installDirectory = args.InstalledInfo.InstallDirectory;
        controllers.AddController(install);
        install.Install(new InstallActionArgs());
        Assert.That(installDirectory, Is.EqualTo("C:\\SDKv7Installed"));
        controllers.RemoveController(install);

        var uninstall = host.GetUninstallControllers(externallyUpdated).Single();
        var uninstalled = false;
        controllers.Uninstalled += (_, _) => uninstalled = true;
        controllers.AddController(uninstall);
        uninstall.Uninstall(new UninstallActionArgs());
        Assert.That(uninstalled, Is.True);
        controllers.RemoveController(uninstall);

        var actionEvents = File.ReadAllLines(eventPath);
        Assert.That(actionEvents, Does.Contain("controller-play"));
        Assert.That(actionEvents, Does.Contain("event-started:4242"));
        Assert.That(actionEvents, Does.Contain("event-stopped:9"));
        Assert.That(actionEvents, Does.Contain("controller-install"));
        Assert.That(actionEvents, Does.Contain("event-installed:SDK v7 external update"));
        Assert.That(actionEvents, Does.Contain("controller-uninstall"));
        Assert.That(actionEvents, Does.Contain("event-uninstalled:SDK v7 external update"));

        host.Dispose();
        Assert.That(
            File.ReadAllLines(eventPath).Last(),
            Is.EqualTo("stopped"));
    }

    [Test]
    public void ClaimsButDoesNotConstructDisabledSdkSevenPlugin()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "V7Fixture");
        var fixturePath = Path.Combine(fixtureRoot, "Library");
        var manifest = ExtensionManifest.FromFile(Path.Combine(fixturePath, "extension.yaml"));
        using var database = new GameDatabase(Path.Combine(testRoot, "library"));
        using var controllers = new GameControllerFactory(database);
        using var host = new V7PluginHost(
            database,
            controllers,
            new AvaloniaHostCallbacks
            {
                Mode = ApplicationMode.Desktop,
                Settings = new TestSettings(),
                Dialogs = new TestDialogs()
            },
            new NotificationsAPI(),
            () => null,
            () => [],
            Path.Combine(fixtureRoot, "Host"));

        var claimed = host.Load([manifest], [manifest.Id]);

        Assert.That(claimed, Is.EqualTo(new[] { manifest.Id }));
        Assert.That(host.Plugins, Is.Empty);
        Assert.That(host.FailedPlugins, Is.Empty);
    }

    [Test]
    public void SharedActionRunnerExecutesSdkSevenControllersAndCancellation()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "V7Fixture");
        var fixturePath = Path.Combine(fixtureRoot, "Library");
        var extensionPath = Path.Combine(PlaynitePaths.ExtensionsUserDataPath, "TestPluginV7");
        Directory.CreateDirectory(extensionPath);
        var fixtureAssembly = Path.Combine(fixturePath, "TestPluginV7.dll");
        var manifestText = File.ReadAllText(Path.Combine(fixturePath, "extension.yaml"))
            .Replace("Module: TestPluginV7.dll", $"Module: '{fixtureAssembly}'", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(extensionPath, "extension.yaml"), manifestText);

        using var database = new GameDatabase(Path.Combine(testRoot, "library"));
        database.OpenDatabase();
        var game = new Game("SDK v7 bridge game") { IsInstalled = true };
        var cancelledGame = new Game("SDK v7 cancel game") { IsInstalled = true };
        database.Games.Add(new[] { game, cancelledGame });
        var callbacks = new AvaloniaHostCallbacks
        {
            Mode = ApplicationMode.Desktop,
            Settings = new TestSettings(),
            Dialogs = new TestDialogs()
        };

        using var runtime = new AvaloniaRuntimeHost(database, callbacks);
        runtime.InitializePlugins(true);

        Assert.That(runtime.V7Plugins, Has.Count.EqualTo(1));
        Assert.That(runtime.V7PluginFailures, Is.Empty);
        Assert.That(runtime.LibraryPlugins, Has.Count.EqualTo(1));
        var mainMenu = runtime.GetMainMenuActions();
        Assert.That(mainMenu.Single().DisplayName, Is.EqualTo("SDK v7 > Tools > SDK v7 main command"));
        Assert.That(mainMenu.Single().PluginName, Is.EqualTo("Test SDK v7 library"));
        mainMenu.Single().Invoke();
        var gameMenu = runtime.GetGameMenuActions([database.Games[game.Id]]);
        Assert.That(gameMenu.Single().DisplayName, Is.EqualTo("SDK v7 > Game > SDK v7 game command"));
        gameMenu.Single().Invoke();
        var pluginElement = PluginElementRuntime.Resolver(
            "TestSdkV7",
            "GameStatus",
            database.Games[game.Id]);
        Assert.That(pluginElement, Is.TypeOf<V7RemotePluginElementHost>());
        var pluginControl = (ContentControl)((ContentControl)pluginElement).Content;
        var pluginLabel = (TextBlock)pluginControl.Content;
        Assert.That(pluginLabel.Text, Is.EqualTo("SDK v7 bridge game updated"));
        ((IPluginElementContextSink)pluginElement).GameContext = database.Games[cancelledGame.Id];
        Assert.That(pluginLabel.Text, Is.EqualTo("SDK v7 cancel game"));
        var converter = new PluginConverterProvider("TestSdkV7", "TestPrefixConverter");
        Assert.That(
            converter.Convert("value", typeof(string), null, CultureInfo.InvariantCulture),
            Is.EqualTo("sdk-v7:value"));
        var sidebarItems = runtime.PluginSidebarItems;
        Assert.That(sidebarItems, Has.Count.EqualTo(2));
        var sidebarButton = sidebarItems.Single(item => !item.IsView);
        Assert.That(sidebarButton.ProgressValue, Is.EqualTo(25));
        var sidebarChanged = false;
        sidebarButton.PropertyChanged += (_, args) =>
            sidebarChanged |= args.PropertyName == nameof(sidebarButton.ProgressValue);
        sidebarButton.Activate();
        Assert.That(sidebarButton.ProgressValue, Is.EqualTo(50));
        Assert.That(sidebarChanged, Is.True);
        var sidebarView = sidebarItems.Single(item => item.IsView);
        Assert.That(((TextBlock)sidebarView.Open()).Text, Is.EqualTo("SDK v7 sidebar content"));
        sidebarView.Close();
        var topPanelItem = runtime.PluginTopPanelItems.Single();
        Assert.That(topPanelItem.Title, Is.EqualTo("SDK v7 top action"));
        var topPanelChanged = false;
        topPanelItem.PropertyChanged += (_, args) =>
            topPanelChanged |= args.PropertyName == nameof(topPanelItem.Title);
        topPanelItem.Activate();
        Assert.That(topPanelItem.Title, Is.EqualTo("SDK v7 top action used"));
        Assert.That(topPanelChanged, Is.True);
        var importedLibraryGames = database.ImportGames(
            runtime.LibraryPlugins[0],
            CancellationToken.None,
            PlaytimeImportMode.Always);
        Assert.That(importedLibraryGames.Single().Name, Is.EqualTo("SDK v7 library game"));
        runtime.NotifyLibraryUpdated();
        var playResult = runtime.Play(database.Games[game.Id]);
        Assert.That(playResult.Success, Is.True, playResult.Message);
        Assert.That(database.Games[game.Id].PlayCount, Is.EqualTo(1));
        Assert.That(database.Games[game.Id].Playtime, Is.EqualTo(9));
        Assert.That(database.Games[game.Id].IsRunning, Is.False);

        var installResult = runtime.Install(database.Games[game.Id]);
        Assert.That(installResult.Success, Is.True, installResult.Message);
        Assert.That(database.Games[game.Id].InstallDirectory, Is.EqualTo("C:\\SDKv7Installed"));
        Assert.That(database.Games[game.Id].IsInstalled, Is.True);

        var uninstallResult = runtime.Uninstall(database.Games[game.Id]);
        Assert.That(uninstallResult.Success, Is.True, uninstallResult.Message);
        Assert.That(database.Games[game.Id].IsInstalled, Is.False);

        var cancelledResult = runtime.Play(database.Games[cancelledGame.Id]);
        Assert.That(cancelledResult.Success, Is.False);
        Assert.That(cancelledResult.Message, Does.Contain("cancelled"));
        Assert.That(database.Games[cancelledGame.Id].IsLaunching, Is.False);

        var eventPath = Path.Combine(
            PlaynitePaths.ExtensionsDataPath,
            runtime.V7Plugins[0].Id.ToString(),
            "events.txt");
        var events = File.ReadAllLines(eventPath);
        Assert.That(events, Does.Contain("event-starting:SDK v7 bridge game updated"));
        Assert.That(events, Does.Contain("event-started:4242"));
        Assert.That(events, Does.Contain("event-stopped:9"));
        Assert.That(events, Does.Contain("event-installed:SDK v7 bridge game updated"));
        Assert.That(events, Does.Contain("event-uninstalled:SDK v7 bridge game updated"));
        Assert.That(events, Does.Contain("event-starting:SDK v7 cancel game"));
        Assert.That(events, Does.Contain("event-startup-cancelled:SDK v7 cancel game"));
        Assert.That(events, Does.Contain("event-library-updated"));
        Assert.That(events, Does.Contain("menu-main:SDK v7 main command:False"));
        Assert.That(events, Does.Contain("menu-game:SDK v7 bridge game updated:False"));
        Assert.That(events, Does.Contain("element-created:Desktop"));
        Assert.That(events, Does.Contain("sidebar-activated"));
        Assert.That(events, Does.Contain("sidebar-opened"));
        Assert.That(events, Does.Contain("sidebar-closed"));
        Assert.That(events, Does.Contain("top-panel-activated"));
    }

    [Test]
    public async Task SharedMetadataDownloaderUsesSdkSevenLibraryAndMetadataProviders()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "V7Fixture");
        InstallFixture(Path.Combine(fixtureRoot, "Library"), "TestPluginV7");
        InstallFixture(Path.Combine(fixtureRoot, "Metadata"), "TestMetadataPluginV7");

        using var database = new GameDatabase(Path.Combine(testRoot, "library"));
        database.OpenDatabase();
        database.Games.Add(new Game("SDK v7 bridge game") { IsInstalled = true });
        var callbacks = new AvaloniaHostCallbacks
        {
            Mode = ApplicationMode.Desktop,
            Settings = new TestSettings(),
            Dialogs = new TestDialogs()
        };

        using var runtime = new AvaloniaRuntimeHost(database, callbacks);
        runtime.InitializePlugins(true);

        Assert.That(runtime.V7Plugins, Has.Count.EqualTo(2));
        Assert.That(runtime.V7PluginFailures, Is.Empty);
        Assert.That(runtime.LibraryPlugins, Has.Count.EqualTo(1));
        Assert.That(runtime.MetadataPlugins, Has.Count.EqualTo(1));
        Assert.That(runtime.MetadataPlugins[0].SupportedFields, Is.EquivalentTo(Enum.GetValues<MetadataField>()));

        var imported = database.ImportGames(
            runtime.LibraryPlugins[0],
            CancellationToken.None,
            PlaytimeImportMode.Always).Single();
        var officialSettings = new MetadataDownloaderSettings { SkipExistingValues = false };
        officialSettings.ConfigureFields([Guid.Empty], false);
        officialSettings.Name.Import = true;
        officialSettings.Genre.Import = true;
        officialSettings.Description.Import = true;
        using (var downloader = new MetadataDownloader(
                   database,
                   runtime.MetadataPlugins.ToList(),
                   runtime.LibraryPlugins.ToList()))
        {
            await downloader.DownloadMetadataAsync(
                [imported],
                officialSettings,
                new TestMetadataSettings(),
                null,
                CancellationToken.None);
        }

        var officiallyUpdated = database.Games[imported.Id];
        Assert.That(officiallyUpdated.Name, Is.EqualTo("SDK v7 official metadata"));
        Assert.That(officiallyUpdated.Description, Does.Contain("library provider"));
        Assert.That(
            officiallyUpdated.GenreIds.Select(id => database.Genres[id]?.Name),
            Does.Contain("SDK v7 official genre"));

        var metadataPlugin = runtime.MetadataPlugins[0];
        var pluginSettings = new MetadataDownloaderSettings { SkipExistingValues = false };
        pluginSettings.ConfigureFields([metadataPlugin.Id], true);
        using (var downloader = new MetadataDownloader(
                   database,
                   runtime.MetadataPlugins.ToList(),
                   runtime.LibraryPlugins.ToList()))
        {
            await downloader.DownloadMetadataAsync(
                [officiallyUpdated],
                pluginSettings,
                new TestMetadataSettings(),
                null,
                CancellationToken.None);
        }

        var updated = database.Games[imported.Id];
        Assert.That(updated.Name, Is.EqualTo("SDK v7 metadata name"));
        Assert.That(updated.Description, Is.EqualTo("SDK v7 metadata description"));
        Assert.That(updated.ReleaseDate?.Year, Is.EqualTo(2024));
        Assert.That(updated.ReleaseDate?.Month, Is.EqualTo(7));
        Assert.That(updated.ReleaseDate?.Day, Is.EqualTo(18));
        Assert.That(updated.CriticScore, Is.EqualTo(91));
        Assert.That(updated.CommunityScore, Is.EqualTo(87));
        Assert.That(updated.InstallSize, Is.EqualTo(123456789));
        Assert.That(updated.Links.Single().Url, Is.EqualTo("https://playnite.link/sdk-v7"));
        Assert.That(database.GetFullFilePath(updated.Icon), Is.Not.Null.And.Not.Empty);
        Assert.That(File.Exists(database.GetFullFilePath(updated.Icon)), Is.True);
        Assert.That(File.Exists(database.GetFullFilePath(updated.CoverImage)), Is.True);
        Assert.That(File.Exists(database.GetFullFilePath(updated.BackgroundImage)), Is.True);
        AssertMetadataName(database.Genres, updated.GenreIds, "SDK v7 metadata genre");
        AssertMetadataName(database.Companies, updated.DeveloperIds, "SDK v7 developer");
        AssertMetadataName(database.Companies, updated.PublisherIds, "SDK v7 publisher");
        AssertMetadataName(database.Tags, updated.TagIds, "SDK v7 tag");
        AssertMetadataName(database.Features, updated.FeatureIds, "SDK v7 feature");
        AssertMetadataName(database.AgeRatings, updated.AgeRatingIds, "SDK v7 age rating");
        AssertMetadataName(database.Series, updated.SeriesIds, "SDK v7 series");
        AssertMetadataName(database.Regions, updated.RegionIds, "SDK v7 region");
        AssertMetadataName(database.Platforms, updated.PlatformIds, "SDK v7 platform");

        var libraryEventPath = Path.Combine(
            PlaynitePaths.ExtensionsDataPath,
            runtime.LibraryPlugins[0].Id.ToString(),
            "events.txt");
        var metadataEventPath = Path.Combine(
            PlaynitePaths.ExtensionsDataPath,
            metadataPlugin.Id.ToString(),
            "metadata-events.txt");
        Assert.That(File.ReadAllLines(libraryEventPath), Does.Contain("library-metadata:sdk-v7-library-game"));
        Assert.That(File.ReadAllLines(libraryEventPath), Does.Contain("library-metadata-disposed"));
        Assert.That(
            File.ReadAllLines(metadataEventPath),
            Does.Contain("provider-created:SDK v7 official metadata:True"));
        Assert.That(File.ReadAllLines(metadataEventPath), Does.Contain("provider-disposed"));
    }

    private static void InstallFixture(string fixturePath, string extensionName)
    {
        var extensionPath = Path.Combine(PlaynitePaths.ExtensionsUserDataPath, extensionName);
        Directory.CreateDirectory(extensionPath);
        var assemblyName = extensionName + ".dll";
        var fixtureAssembly = Path.Combine(fixturePath, assemblyName);
        var manifestText = File.ReadAllText(Path.Combine(fixturePath, "extension.yaml"))
            .Replace($"Module: {assemblyName}", $"Module: '{fixtureAssembly}'", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(extensionPath, "extension.yaml"), manifestText);
    }

    private static void AssertMetadataName<TItem>(
        IItemCollection<TItem> collection,
        IEnumerable<Guid> ids,
        string expectedName)
        where TItem : DatabaseObject =>
        Assert.That(ids.Select(id => collection[id]?.Name), Does.Contain(expectedName));
}
