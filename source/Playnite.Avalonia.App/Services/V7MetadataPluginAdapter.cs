using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Playnite.Avalonia.App.Services;

internal static class V7Reflection
{
    public static MethodInfo GetRequiredMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new MissingMethodException(type.FullName, name);

    public static object Invoke(object instance, MethodInfo method, params object[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    public static T Read<T>(object instance, string name)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var type = instance.GetType();
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(type.FullName, name);
        var value = property.GetValue(instance);
        if (value == null)
        {
            if (typeof(T).IsValueType)
            {
                throw new InvalidDataException(
                    $"SDK v7 bridge property {type.FullName}.{name} returned no {typeof(T).FullName} value.");
            }
            return default;
        }
        return value is T typed
            ? typed
            : throw new InvalidDataException(
                $"SDK v7 bridge property {type.FullName}.{name} returned {value.GetType().FullName} " +
                $"instead of {typeof(T).FullName}.");
    }
}

internal sealed class V7MetadataPluginAdapter : MetadataPlugin
{
    private readonly V7LoadedPlugin plugin;

    public override Guid Id => plugin.Id;
    public override string Name => plugin.Name;
    public override List<MetadataField> SupportedFields { get; }

    public V7MetadataPluginAdapter(IPlayniteAPI playniteApi, V7LoadedPlugin plugin)
        : base(playniteApi)
    {
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Properties = new MetadataPluginProperties { HasSettings = plugin.HasSettings };
        SupportedFields = plugin.SupportedMetadataFields
            .Select(field => Enum.Parse<MetadataField>(field))
            .ToList();
    }

    public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var provider = plugin.CreateMetadataProvider(
            V7DatabaseTransport.Serialize(options.GameData),
            options.IsBackgroundDownload);
        return provider == null ? null : new V7OnDemandMetadataProviderAdapter(provider);
    }
}

internal sealed class V7OnDemandMetadataProviderAdapter : OnDemandMetadataProvider
{
    private readonly object instance;
    private readonly MethodInfo getField;
    private readonly MethodInfo dispose;

    public override List<MetadataField> AvailableFields { get; }

    public V7OnDemandMetadataProviderAdapter(object instance)
    {
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        var type = instance.GetType();
        getField = V7Reflection.GetRequiredMethod(type, "GetField");
        dispose = V7Reflection.GetRequiredMethod(type, nameof(IDisposable.Dispose));
        var fields = (string[])(type.GetProperty("AvailableFields")?.GetValue(instance)
            ?? throw new MissingMemberException(type.FullName, "AvailableFields"));
        AvailableFields = fields.Select(field => Enum.Parse<MetadataField>(field)).ToList();
    }

    private T Read<T>(MetadataField field, GetMetadataFieldArgs args)
    {
        var json = (string)V7Reflection.Invoke(
            instance,
            getField,
            field.ToString(),
            args?.CancelToken ?? CancellationToken.None);
        return V7DatabaseTransport.Deserialize<T>(json);
    }

    public override string GetName(GetMetadataFieldArgs args) => Read<string>(MetadataField.Name, args);
    public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Genres, args);
    public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args) =>
        Read<ReleaseDate?>(MetadataField.ReleaseDate, args);
    public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Developers, args);
    public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Publishers, args);
    public override IEnumerable<MetadataProperty> GetTags(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Tags, args);
    public override string GetDescription(GetMetadataFieldArgs args) =>
        Read<string>(MetadataField.Description, args);
    public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args) =>
        Read<List<Link>>(MetadataField.Links, args);
    public override int? GetCriticScore(GetMetadataFieldArgs args) =>
        Read<int?>(MetadataField.CriticScore, args);
    public override int? GetCommunityScore(GetMetadataFieldArgs args) =>
        Read<int?>(MetadataField.CommunityScore, args);
    public override MetadataFile GetIcon(GetMetadataFieldArgs args) =>
        Read<MetadataFile>(MetadataField.Icon, args);
    public override MetadataFile GetCoverImage(GetMetadataFieldArgs args) =>
        Read<MetadataFile>(MetadataField.CoverImage, args);
    public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args) =>
        Read<MetadataFile>(MetadataField.BackgroundImage, args);
    public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Features, args);
    public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.AgeRating, args);
    public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Series, args);
    public override IEnumerable<MetadataProperty> GetRegions(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Region, args);
    public override IEnumerable<MetadataProperty> GetPlatforms(GetMetadataFieldArgs args) =>
        Read<List<MetadataProperty>>(MetadataField.Platform, args);
    public override ulong? GetInstallSize(GetMetadataFieldArgs args) =>
        Read<ulong?>(MetadataField.InstallSize, args);
    public override void Dispose() => V7Reflection.Invoke(instance, dispose);
}
