using Nett;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Playnite.SDK.Data;
using System.Reflection;
using YamlDotNet.Serialization;

namespace Playnite.SDK.V7.Host;

internal sealed class V7JsonResolver : DefaultContractResolver
{
    public static V7JsonResolver Instance { get; } = new();

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        if (member.GetCustomAttribute<SerializationPropertyNameAttribute>() is { } attribute)
        {
            property.PropertyName = attribute.PropertyName;
        }

        return property;
    }

    protected override List<MemberInfo> GetSerializableMembers(Type objectType) => objectType
        .GetMembers(BindingFlags.Public | BindingFlags.Instance)
        .Where(member => member is PropertyInfo or FieldInfo)
        .Where(member => !member.IsDefined(typeof(DontSerializeAttribute)))
        .Where(member => !member.IsDefined(typeof(JsonIgnoreAttribute)))
        .ToList();
}

internal sealed class V7DataSerializer : IDataSerializer
{
    private static readonly JsonSerializerSettings readSettings = new()
    {
        ContractResolver = V7JsonResolver.Instance,
        MaxDepth = 128
    };

    public string ToYaml(object obj) => new SerializerBuilder().Build().Serialize(obj);
    public T FromYaml<T>(string yaml) where T : class =>
        new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<T>(yaml);
    public T FromYamlFile<T>(string filePath) where T : class => FromYaml<T>(File.ReadAllText(filePath));

    public bool TryFromYaml<T>(string yaml, out T content) where T : class =>
        Try(() => FromYaml<T>(yaml), out content, out _);
    public bool TryFromYaml<T>(string yaml, out T content, out Exception error) where T : class =>
        Try(() => FromYaml<T>(yaml), out content, out error);
    public bool TryFromYamlFile<T>(string filePath, out T content) where T : class =>
        Try(() => FromYamlFile<T>(filePath), out content, out _);
    public bool TryFromYamlFile<T>(string filePath, out T content, out Exception error) where T : class =>
        Try(() => FromYamlFile<T>(filePath), out content, out error);

    public string ToJson(object obj, bool formatted = false) => JsonConvert.SerializeObject(
        obj,
        new JsonSerializerSettings
        {
            Formatting = formatted ? Formatting.Indented : Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = V7JsonResolver.Instance,
            MaxDepth = 128
        });

    public void ToJsonStream(object obj, Stream stream, bool formatted = false)
    {
        using var streamWriter = new StreamWriter(stream, leaveOpen: true);
        using var jsonWriter = new JsonTextWriter(streamWriter);
        JsonSerializer.Create(new JsonSerializerSettings
        {
            Formatting = formatted ? Formatting.Indented : Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = V7JsonResolver.Instance,
            MaxDepth = 128
        }).Serialize(jsonWriter, obj);
    }

    public T FromJson<T>(string json) where T : class => JsonConvert.DeserializeObject<T>(json, readSettings);

    public T FromJsonStream<T>(Stream stream) where T : class
    {
        using var streamReader = new StreamReader(stream, leaveOpen: true);
        using var jsonReader = new JsonTextReader(streamReader);
        return JsonSerializer.Create(readSettings).Deserialize<T>(jsonReader);
    }

    public T FromJsonFile<T>(string filePath) where T : class
    {
        using var stream = File.OpenRead(filePath);
        return FromJsonStream<T>(stream);
    }

    public bool TryFromJson<T>(string json, out T content) where T : class =>
        Try(() => FromJson<T>(json), out content, out _);
    public bool TryFromJson<T>(string json, out T content, out Exception error) where T : class =>
        Try(() => FromJson<T>(json), out content, out error);
    public bool TryFromJsonStream<T>(Stream stream, out T content) where T : class =>
        Try(() => FromJsonStream<T>(stream), out content, out _);
    public bool TryFromJsonStream<T>(Stream stream, out T content, out Exception error) where T : class =>
        Try(() => FromJsonStream<T>(stream), out content, out error);
    public bool TryFromJsonFile<T>(string filePath, out T content) where T : class =>
        Try(() => FromJsonFile<T>(filePath), out content, out _);
    public bool TryFromJsonFile<T>(string filePath, out T content, out Exception error) where T : class =>
        Try(() => FromJsonFile<T>(filePath), out content, out error);

    public T FromToml<T>(string toml) where T : class => Toml.ReadString<T>(toml);
    public T FromTomlFile<T>(string filePath) where T : class => FromToml<T>(File.ReadAllText(filePath));
    public bool TryFromToml<T>(string toml, out T content) where T : class =>
        Try(() => FromToml<T>(toml), out content, out _);
    public bool TryFromToml<T>(string toml, out T content, out Exception error) where T : class =>
        Try(() => FromToml<T>(toml), out content, out error);
    public bool TryFromTomlFile<T>(string filePath, out T content) where T : class =>
        Try(() => FromTomlFile<T>(filePath), out content, out _);
    public bool TryFromTomlFile<T>(string filePath, out T content, out Exception error) where T : class =>
        Try(() => FromTomlFile<T>(filePath), out content, out error);

    public T GetClone<T>(T source) where T : class => FromJson<T>(ToJson(source));
    public U GetClone<T, U>(T source) where T : class where U : class => FromJson<U>(ToJson(source));
    public bool AreObjectsEqual(object object1, object object2) =>
        string.Equals(ToJson(object1), ToJson(object2), StringComparison.Ordinal);

    private static bool Try<T>(Func<T> action, out T content, out Exception error) where T : class
    {
        try
        {
            content = action();
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            content = null;
            error = exception;
            return false;
        }
    }
}
