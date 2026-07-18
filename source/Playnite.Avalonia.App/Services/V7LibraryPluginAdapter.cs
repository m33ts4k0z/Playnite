using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.Avalonia.App.Services;

internal sealed class V7LibraryPluginAdapter : LibraryPlugin
{
    private readonly V7LoadedPlugin plugin;

    public override Guid Id => plugin.Id;
    public override string Name => plugin.Name;
    public override string LibraryIcon => plugin.LibraryIcon;
    public override string LibraryBackground => plugin.LibraryBackground;
    public override LibraryClient Client { get; }

    public V7LibraryPluginAdapter(IPlayniteAPI playniteApi, V7LoadedPlugin plugin)
        : base(playniteApi)
    {
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Properties = new LibraryPluginProperties
        {
            CanShutdownClient = plugin.CanShutdownLibraryClient,
            HasCustomizedGameImport = plugin.HasCustomizedGameImport,
            HasSettings = plugin.HasSettings
        };
        if (plugin.HasLibraryClient)
        {
            Client = new V7LibraryClientAdapter(plugin);
        }
    }

    public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args) =>
        plugin.GetLibraryGames(args?.CancelToken ?? CancellationToken.None);

    public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args) =>
        plugin.ImportLibraryGames(args?.CancelToken ?? CancellationToken.None);
}

internal sealed class V7LibraryClientAdapter : LibraryClient
{
    private readonly V7LoadedPlugin plugin;

    public override bool IsInstalled => plugin.IsLibraryClientInstalled;
    public override string Icon => plugin.LibraryClientIcon;

    public V7LibraryClientAdapter(V7LoadedPlugin plugin) =>
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

    public override void Open() => plugin.OpenLibraryClient();
    public override void Shutdown() => plugin.ShutdownLibraryClient();
}
