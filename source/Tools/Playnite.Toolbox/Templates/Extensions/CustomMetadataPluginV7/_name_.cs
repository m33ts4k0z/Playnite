using Avalonia.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace _namespace_;

public sealed class _name_ : MetadataPlugin
{
    private readonly _name_SettingsViewModel settings;

    public override Guid Id { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public override string Name => "_name_";
    public override List<MetadataField> SupportedFields { get; } = [MetadataField.Description];

    public _name_(IPlayniteAPI api)
        : base(api)
    {
        settings = new _name_SettingsViewModel(this);
        Properties = new MetadataPluginProperties
        {
            HasSettings = true
        };
    }

    public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options) =>
        new _name_Provider(options);

    public override ISettings GetSettings(bool firstRunSettings) => settings;

    public override Control GetSettingsView(bool firstRunView) => new _name_SettingsView();
}
