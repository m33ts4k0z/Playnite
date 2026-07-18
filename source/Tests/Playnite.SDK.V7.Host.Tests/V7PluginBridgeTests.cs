using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        var notificationCall = calls.Single(call => call.Operation == "NotificationAdd");
        var notificationPayload = JObject.Parse(notificationCall.Payload);
        Assert.That(Guid.Parse(notificationPayload.Value<string>("OwnerToken")), Is.Not.EqualTo(Guid.Empty));
        var notificationActionToken = Guid.Parse(notificationPayload.Value<string>("ActionToken"));
        Assert.That(notificationActionToken, Is.Not.EqualTo(Guid.Empty));
        var logCall = calls.Single(call => call.Operation == "Log");
        var logPayload = JObject.Parse(logCall.Payload);
        Assert.That(logPayload.Value<string>("Level"), Is.EqualTo("Info"));
        Assert.That(logPayload.Value<string>("LoggerName"), Does.Contain("TestPluginV7"));
        Assert.That(logPayload.Value<string>("Message"), Is.EqualTo("SDK v7 fixture logger initialized"));

        plugin.InvokeApplicationStarted();
        plugin.InvokeApplicationStarted();
        plugin.InvokeNotificationAction(notificationActionToken);
        Assert.Throws<InvalidOperationException>(() =>
            plugin.InvokeNotificationAction(notificationActionToken));
        plugin.Dispose();

        var eventPath = Path.Combine(
            extensionsDataPath,
            plugin.Id.ToString(),
            "events.txt");
        var events = File.ReadAllLines(eventPath);
        Assert.That(events, Is.EqualTo(new[]
        {
            "constructed:Desktop",
            "started",
            "notification-activated",
            "stopped"
        }));
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

        var mainItems = plugin.GetMenuItems("Main", "[]", true).Cast<V7MenuItemInstance>().ToList();
        var main = mainItems.Single(item => item.Description == "SDK v7 main command");
        Assert.That(main.Description, Is.EqualTo("SDK v7 main command"));
        Assert.That(main.MenuSection, Is.EqualTo("SDK v7|Tools"));
        Assert.That(main.Icon, Is.EqualTo("main-menu-icon.png"));
        main.Invoke();
        mainItems.Single(item => item.Description == "SDK v7 clear own notifications").Invoke();
        var removeAllPayload = JObject.Parse(calls.Last(call => call.Operation == "NotificationRemoveAll").Payload);
        Assert.That(Guid.Parse(removeAllPayload.Value<string>("OwnerToken")), Is.Not.EqualTo(Guid.Empty));

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

    [Test]
    public async Task BridgesSdkSevenWebViewContractsAndCancellation()
    {
        var fake = new FakeWebViewHost();
        JObject creation = null;
        var factory = new HostWebViewFactory((operation, payload) =>
        {
            Assert.That(operation, Is.EqualTo("CreateWebView"));
            creation = JObject.Parse(payload);
            return fake;
        });
        var settings = new Playnite.SDK.WebViewSettings
        {
            UserAgent = "Playnite SDK v7 test",
            WindowWidth = 1280,
            WindowHeight = 720,
            WindowBackground = Avalonia.Media.Color.FromArgb(128, 12, 34, 56)
        };

        using var view = factory.CreateView(settings);
        Assert.That(creation.Value<bool>("Offscreen"), Is.False);
        Assert.That(creation["Settings"]?.Value<string>("UserAgent"), Is.EqualTo(settings.UserAgent));
        Assert.That(creation["Settings"]?.Value<int>("WindowWidth"), Is.EqualTo(1280));
        Assert.That(creation["Settings"]?.Value<int>("WindowHeight"), Is.EqualTo(720));
        Assert.That(creation["Settings"]?.Value<int>("BackgroundA"), Is.EqualTo(128));
        Assert.That(creation["Settings"]?.Value<int>("BackgroundR"), Is.EqualTo(12));
        Assert.That(creation["Settings"]?.Value<int>("BackgroundG"), Is.EqualTo(34));
        Assert.That(creation["Settings"]?.Value<int>("BackgroundB"), Is.EqualTo(56));
        Assert.That(view.CanExecuteJavascriptInMainFrame, Is.True);
        Assert.That(view.View, Is.SameAs(fake.View));
        Assert.That(view.WindowHost, Is.Null);

        var loadingStates = new List<bool>();
        view.LoadingChanged += (_, args) => loadingStates.Add(args.IsLoading);
        var address = new Uri("https://example.test/sdk-v7");
        await view.OpenAsync(cancellationToken: CancellationToken.None);
        await view.NavigateAsync(address);
        Assert.That(view.Address, Is.EqualTo(address));
        Assert.That(loadingStates, Is.EqualTo(new[] { true, false }));
        Assert.That(await view.GetPageTextAsync(), Is.EqualTo("SDK v7 page text"));
        Assert.That(await view.GetPageSourceAsync(), Does.Contain("SDK v7 page source"));

        var evaluation = await view.EvaluateScriptAsync("window.playniteSdkV7");
        Assert.That(evaluation.Success, Is.True);
        Assert.That(evaluation.Message, Is.EqualTo("evaluated"));
        var result = (Dictionary<string, object>)evaluation.Result;
        Assert.That(result["number"], Is.EqualTo(7L));
        Assert.That((List<object>)result["items"], Is.EqualTo(new object[] { true, "web" }));

        var cookies = await view.GetCookiesAsync();
        Assert.That(cookies, Has.Count.EqualTo(1));
        Assert.That(cookies[0].Name, Is.EqualTo("playnite-v7"));
        Assert.That(cookies[0].SameSite, Is.EqualTo(Playnite.SDK.CookieSameSite.LaxMode));
        Assert.That(cookies[0].Priority, Is.EqualTo(Playnite.SDK.CookiePriority.High));

        await view.SetCookieAsync(address, new Playnite.SDK.HttpCookie
        {
            Name = "new-cookie",
            Value = "value",
            Domain = "example.test",
            Path = "/",
            SameSite = Playnite.SDK.CookieSameSite.StrictMode,
            Priority = Playnite.SDK.CookiePriority.High
        });
        Assert.That(fake.SetCookieAddress, Is.EqualTo(address));
        Assert.That(fake.SetCookiePayload.Value<string>("SameSite"), Is.EqualTo("StrictMode"));
        Assert.That(fake.SetCookiePayload.Value<string>("Priority"), Is.EqualTo("High"));

        await view.DeleteCookiesAsync(address, "new-cookie");
        Assert.That(fake.DeletedCookieAddress, Is.EqualTo(address));
        Assert.That(fake.DeletedCookieName, Is.EqualTo("new-cookie"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await view.NavigateAsync(address, cancellation.Token));
        Assert.Throws<NotSupportedException>(() =>
        {
            view.ResourceLoaded += (_, _) => { };
        });

        view.Close();
        Assert.That(fake.Closed, Is.True);
        view.Dispose();
        Assert.That(fake.Disposed, Is.True);

        Assert.Throws<NotSupportedException>(() => factory.CreateView(new Playnite.SDK.WebViewSettings
        {
            JavaScriptEnabled = false
        }));
        Assert.Throws<NotSupportedException>(() => factory.CreateView(new Playnite.SDK.WebViewSettings
        {
            CaptureResponseContent = true
        }));
        Assert.Throws<NotSupportedException>(() => factory.CreateView(new Playnite.SDK.WebViewSettings
        {
            ShouldCaptureResponseContent = (_, _) => true
        }));

        var offscreenFake = new FakeWebViewHost();
        JObject offscreenCreation = null;
        var offscreenFactory = new HostWebViewFactory((_, payload) =>
        {
            offscreenCreation = JObject.Parse(payload);
            return offscreenFake;
        });
        using var offscreenView = offscreenFactory.CreateOffscreenView();
        Assert.That(offscreenCreation.Value<bool>("Offscreen"), Is.True);
        Assert.That(offscreenView.View, Is.SameAs(offscreenFake.View));
        Assert.That(offscreenView.WindowHost, Is.Null);
    }

    [Test]
    public void SuppliesWebViewsThroughTheIsolatedPluginApi()
    {
        var pluginDataPath = Path.Combine(
            extensionsDataPath,
            "8134f4eb-556e-4e01-936f-1bf5a808cb10");
        Directory.CreateDirectory(pluginDataPath);
        File.WriteAllText(
            Path.Combine(pluginDataPath, "web-view-probe-url.txt"),
            "https://example.test/sdk-v7");
        var fake = new FakeWebViewHost();
        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(
                typeof(TestPlugin).Assembly.Location,
                HostCall,
                (operation, payload) =>
                {
                    Assert.That(operation, Is.EqualTo("CreateWebView"));
                    Assert.That(JObject.Parse(payload).Value<bool>("Offscreen"), Is.True);
                    return fake;
                })
            .Single();

        var probe = plugin.GetMenuItems("Main", "[]", false)
            .Cast<V7MenuItemInstance>()
            .Single(item => item.Description == "SDK v7 web-view probe");
        probe.Invoke();
        plugin.Dispose();

        var events = File.ReadAllLines(Path.Combine(pluginDataPath, "events.txt"));
        Assert.That(events, Does.Contain("web-loading:True,False"));
        Assert.That(events, Does.Contain("web-address:https://example.test/sdk-v7"));
        Assert.That(events, Does.Contain("web-text:SDK v7 page text"));
        Assert.That(events, Does.Contain("web-source:True"));
        Assert.That(events, Does.Contain(
            "web-script:True:System.Collections.Generic.Dictionary`2[System.String,System.Object]"));
        Assert.That(events, Does.Contain("web-cookie:playnite-v7:High"));
        Assert.That(fake.SetCookiePayload.Value<string>("SameSite"), Is.EqualTo("Unspecified"));
        Assert.That(fake.SetCookiePayload.Value<string>("Priority"), Is.EqualTo("Medium"));
        Assert.That(fake.DeletedCookieName, Is.EqualTo("sdk-v7-probe"));
        Assert.That(fake.Disposed, Is.True);
    }

    [Test]
    public void RejectsMissingRequiredHostValuesWithoutCoercingDefaults()
    {
        string StrictHostCall(string operation, string payload)
        {
            if (operation == "MainView.SelectedGames" || operation == "ConnectedControllers")
            {
                return "null";
            }
            if (operation == "OpenPluginSettings")
            {
                return "not-a-boolean";
            }
            if (operation == "Database")
            {
                var request = JObject.Parse(payload);
                return request.Value<string>("Action") == "Get" ? "null" : string.Empty;
            }
            return HostCall(operation, payload);
        }

        var plugin = (V7PluginInstance)V7PluginBridge
            .LoadAll(typeof(TestPlugin).Assembly.Location, StrictHostCall)
            .Single();
        var api = Playnite.SDK.API.Instance;

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidDataException>(() => api.MainView.SelectedGames.ToList());
            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await api.MainView.OpenPluginSettingsAsync(plugin.Id));
            Assert.Throws<InvalidDataException>(() => api.GetConnectedControllers());
            Assert.Throws<InvalidDataException>(() => _ = api.Database.Games.Count);
            Assert.That(api.Database.Games[Guid.NewGuid()], Is.Null);
        });
        plugin.Dispose();

        var invalidWebHost = new FakeWebViewHost { View = null };
        using var view = new HostWebViewFactory((_, _) => invalidWebHost).CreateView();
        Assert.Throws<InvalidDataException>(() => _ = view.View);
        Assert.That(view.WindowHost, Is.Null);
        Assert.That(view.Address, Is.Null);
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
            "Database" => JObject.Parse(payload).Value<string>("Action") == "IsOpen"
                ? "false"
                : string.Empty,
            "DisabledAddons" or "Addons" => JsonConvert.SerializeObject(Array.Empty<string>()),
            "LoadedPlugins" => JsonConvert.SerializeObject(new[]
            {
                new
                {
                    Id = "8134f4eb-556e-4e01-936f-1bf5a808cb10",
                    Kind = "LibraryPlugin",
                    Name = "Test SDK v7 library"
                }
            }),
            "IsPortable" or "InOfflineMode" or "IsDebugBuild" or "ThrowAllErrors" => "false",
            _ => string.Empty
        };
    }

    private sealed class FakeWebViewHost
    {
        private Action<bool> loadingChanged;

        public bool CanExecuteJavascriptInMainFrame => true;
        public Avalonia.Controls.Control View { get; set; } = new Avalonia.Controls.Border();
        public Avalonia.Controls.Window WindowHost => null;
        public Uri Address { get; private set; }
        public Uri SetCookieAddress { get; private set; }
        public JObject SetCookiePayload { get; private set; }
        public Uri DeletedCookieAddress { get; private set; }
        public string DeletedCookieName { get; private set; }
        public bool Closed { get; private set; }
        public bool Disposed { get; private set; }

        public void SubscribeLoading(Action<bool> handler) => loadingChanged = handler;

        public Task OpenAsync(bool modal, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task NavigateAsync(Uri address, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Address = address;
            loadingChanged?.Invoke(true);
            loadingChanged?.Invoke(false);
            return Task.CompletedTask;
        }

        public Task<string> GetPageTextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("SDK v7 page text");
        }

        public Task<string> GetPageSourceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("<html>SDK v7 page source</html>");
        }

        public Task<string> EvaluateScriptAsync(string script, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.That(script, Is.EqualTo("window.playniteSdkV7"));
            return Task.FromResult(JsonConvert.SerializeObject(new
            {
                Success = true,
                Message = "evaluated",
                Result = new
                {
                    number = 7,
                    items = new object[] { true, "web" }
                }
            }));
        }

        public Task<string> GetCookiesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(JsonConvert.SerializeObject(new[]
            {
                new
                {
                    Name = "playnite-v7",
                    Value = "cookie-value",
                    Domain = "example.test",
                    Path = "/",
                    Expires = (DateTime?)null,
                    Creation = DateTime.UnixEpoch,
                    Secure = true,
                    HttpOnly = true,
                    LastAccess = DateTime.UnixEpoch,
                    SameSite = "LaxMode",
                    Priority = "High"
                }
            }));
        }

        public Task SetCookieAsync(
            Uri address,
            string cookieJson,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCookieAddress = address;
            SetCookiePayload = JObject.Parse(cookieJson);
            return Task.CompletedTask;
        }

        public Task DeleteCookiesAsync(
            Uri address,
            string name,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletedCookieAddress = address;
            DeletedCookieName = name;
            return Task.CompletedTask;
        }

        public void Close() => Closed = true;
        public void Dispose() => Disposed = true;
    }
}
