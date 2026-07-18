using Avalonia.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace _namespace_;

public sealed class _name_ : GenericPlugin
{
    private readonly _name_SettingsViewModel settings;

    public override Guid Id { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public _name_(IPlayniteAPI api)
        : base(api)
    {
        settings = new _name_SettingsViewModel(this);
        Properties = new GenericPluginProperties
        {
            HasSettings = true
        };
    }

    public override ISettings GetSettings(bool firstRunSettings) => settings;

    public override Control GetSettingsView(bool firstRunView) => new _name_SettingsView();

    public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
    {
        // Initialize services after Playnite has started.
    }
}
