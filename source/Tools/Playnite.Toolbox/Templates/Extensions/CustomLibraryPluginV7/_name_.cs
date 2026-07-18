using Avalonia.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace _namespace_;

public sealed class _name_ : LibraryPlugin
{
    private readonly _name_SettingsViewModel settings;

    public override Guid Id { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public override string Name => "_name_";
    public override LibraryClient Client { get; } = new _name_Client();

    public _name_(IPlayniteAPI api)
        : base(api)
    {
        settings = new _name_SettingsViewModel(this);
        Properties = new LibraryPluginProperties
        {
            HasSettings = true,
            CanShutdownClient = true
        };
    }

    public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
    {
        args.CancelToken.ThrowIfCancellationRequested();
        return [];
    }

    public override ISettings GetSettings(bool firstRunSettings) => settings;

    public override Control GetSettingsView(bool firstRunView) => new _name_SettingsView();
}
