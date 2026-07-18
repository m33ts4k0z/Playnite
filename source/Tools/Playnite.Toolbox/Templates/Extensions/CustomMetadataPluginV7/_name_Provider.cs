using Playnite.SDK.Plugins;

namespace _namespace_;

public sealed class _name_Provider : OnDemandMetadataProvider
{
    private readonly MetadataRequestOptions options;

    public override List<MetadataField> AvailableFields { get; } = [MetadataField.Description];

    public _name_Provider(MetadataRequestOptions options)
    {
        this.options = options;
    }

    public override string GetDescription(GetMetadataFieldArgs args)
    {
        args.CancelToken.ThrowIfCancellationRequested();
        return $"Metadata for {options.GameData.Name}";
    }
}
