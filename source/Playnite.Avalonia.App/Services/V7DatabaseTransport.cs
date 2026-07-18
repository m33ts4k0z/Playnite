using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Playnite.Database;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

internal sealed class V7TransportContractResolver : DefaultContractResolver
{
    public static V7TransportContractResolver Instance { get; } = new();

    protected override List<MemberInfo> GetSerializableMembers(Type objectType) => objectType
        .GetMembers(BindingFlags.Public | BindingFlags.Instance)
        .Where(member => member is PropertyInfo or FieldInfo)
        .Where(member => !member.IsDefined(typeof(DontSerializeAttribute)))
        .Where(member => !member.IsDefined(typeof(JsonIgnoreAttribute)))
        .ToList();
}

internal sealed class V7TransportMetadataPropertyConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => typeof(MetadataProperty).IsAssignableFrom(objectType);

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var data = value switch
        {
            MetadataNameProperty property => new JObject
            {
                ["Kind"] = "Name",
                ["Value"] = property.Name
            },
            MetadataIdProperty property => new JObject
            {
                ["Kind"] = "Id",
                ["Value"] = property.Id
            },
            MetadataSpecProperty property => new JObject
            {
                ["Kind"] = "Spec",
                ["Value"] = property.Id
            },
            null => null,
            _ => throw new JsonSerializationException(
                $"Metadata property type {value.GetType().FullName} is not supported by the SDK v7 bridge.")
        };

        if (data == null)
        {
            writer.WriteNull();
        }
        else
        {
            data.WriteTo(writer);
        }
    }

    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object existingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var data = JObject.Load(reader);
        var value = data.Value<string>("Value");
        return data.Value<string>("Kind") switch
        {
            "Name" => new MetadataNameProperty(value),
            "Id" => new MetadataIdProperty(Guid.Parse(value)),
            "Spec" => new MetadataSpecProperty(value),
            var kind => throw new JsonSerializationException($"Unknown metadata property kind {kind}.")
        };
    }
}

internal sealed class V7DatabaseTransport
{
    private sealed class Request
    {
        public string Action { get; set; }
        public string Collection { get; set; }
        public JObject Data { get; set; }
    }

    private static readonly JsonSerializerSettings jsonSettings = new()
    {
        ContractResolver = V7TransportContractResolver.Instance,
        Converters = { new V7TransportMetadataPropertyConverter() },
        MaxDepth = 128
    };

    private readonly GameDatabase database;

    public V7DatabaseTransport(GameDatabase database)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public string Handle(string payload)
    {
        var request = Deserialize<Request>(payload)
            ?? throw new InvalidDataException("SDK v7 database request is empty.");
        object result = request.Action switch
        {
            "Path" => database.DatabasePath,
            "IsOpen" => database.IsOpen,
            "AddFile" => database.AddFile(
                request.Data.Value<string>("Path"),
                GetGuid(request.Data, "ParentId"),
                false,
                CancellationToken.None),
            "SaveFile" => Run(() => database.CopyFile(
                request.Data.Value<string>("Id"),
                request.Data.Value<string>("Path"))),
            "RemoveFile" => Run(() => database.RemoveFile(request.Data.Value<string>("Id"))),
            "GetFileStoragePath" => database.GetFileStoragePath(GetGuid(request.Data, "ParentId")),
            "GetFullFilePath" => database.GetFullFilePath(request.Data.Value<string>("Path")),
            "BeginBufferUpdate" => Run(database.BeginBufferUpdate),
            "EndBufferUpdate" => Run(database.EndBufferUpdate),
            "ImportGame" => ImportGame(request.Data),
            "MatchesFilter" => database.GetGameMatchesFilter(
                Convert<Game>(request.Data["Game"]),
                Convert<FilterPresetSettings>(request.Data["Filter"]),
                request.Data.Value<bool>("Fuzzy")),
            "FilteredGames" => database.GetFilteredGames(
                Convert<FilterPresetSettings>(request.Data["Filter"]),
                request.Data.Value<bool>("Fuzzy")).ToList(),
            _ => HandleCollection(request)
        };
        return Serialize(result);
    }

    public static string Serialize(object value) => JsonConvert.SerializeObject(value, jsonSettings);

    private object HandleCollection(Request request)
    {
        if (!Enum.TryParse<GameDatabaseCollection>(request.Collection, out var collection))
        {
            throw new InvalidDataException($"Unknown SDK v7 database collection {request.Collection}.");
        }

        return collection switch
        {
            GameDatabaseCollection.Games => HandleCollection(database.Games, request),
            GameDatabaseCollection.Platforms => HandleCollection(database.Platforms, request),
            GameDatabaseCollection.Emulators => HandleCollection(database.Emulators, request),
            GameDatabaseCollection.Genres => HandleCollection(database.Genres, request),
            GameDatabaseCollection.Companies => HandleCollection(database.Companies, request),
            GameDatabaseCollection.Tags => HandleCollection(database.Tags, request),
            GameDatabaseCollection.Categories => HandleCollection(database.Categories, request),
            GameDatabaseCollection.Series => HandleCollection(database.Series, request),
            GameDatabaseCollection.AgeRatings => HandleCollection(database.AgeRatings, request),
            GameDatabaseCollection.Regions => HandleCollection(database.Regions, request),
            GameDatabaseCollection.Sources => HandleCollection(database.Sources, request),
            GameDatabaseCollection.Features => HandleCollection(database.Features, request),
            GameDatabaseCollection.GameScanners => HandleCollection(database.GameScanners, request),
            GameDatabaseCollection.CompletionStatuses => HandleCollection(database.CompletionStatuses, request),
            GameDatabaseCollection.ImportExclusions => HandleCollection(database.ImportExclusions, request),
            GameDatabaseCollection.FilterPresets => HandleCollection(database.FilterPresets, request),
            _ => throw new NotSupportedException(
                $"SDK v7 database collection {request.Collection} is not available from IGameDatabaseAPI.")
        };
    }

    private static object HandleCollection<TItem>(IItemCollection<TItem> collection, Request request)
        where TItem : DatabaseObject
    {
        return request.Action switch
        {
            "Count" => collection.Count,
            "Contains" => collection.ContainsItem(GetGuid(request.Data, "Id")),
            "Get" => collection.Get(GetGuid(request.Data, "Id")),
            "GetMany" => collection.Get(request.Data["Ids"].ToObject<List<Guid>>()),
            "All" => collection.ToList(),
            "AddName" => collection.Add(request.Data.Value<string>("Name")),
            "AddNames" => collection.Add(request.Data["Names"].ToObject<List<string>>()).ToList(),
            "AddMetadata" => AddMetadata(collection, request.Data["Properties"]),
            "AddItems" => Run(() => collection.Add(Convert<List<TItem>>(request.Data["Items"]))),
            "Remove" => Remove(collection, request.Data["Ids"].ToObject<List<Guid>>()),
            "Update" => Run(() => collection.Update(Convert<List<TItem>>(request.Data["Items"]))),
            "Clear" => Run(collection.Clear),
            "BeginBufferUpdate" => Run(collection.BeginBufferUpdate),
            "EndBufferUpdate" => Run(collection.EndBufferUpdate),
            _ => throw new InvalidDataException(
                $"Unknown SDK v7 database collection action {request.Action}.")
        };
    }

    private Game ImportGame(JObject data)
    {
        var metadata = Convert<GameMetadata>(data["Game"]);
        var sourceId = data["SourcePluginId"]?.Type == JTokenType.Null
            ? null
            : data["SourcePluginId"]?.ToObject<Guid?>();
        return sourceId.HasValue ? database.ImportGame(metadata, sourceId.Value) : database.ImportGame(metadata);
    }

    private static List<TItem> AddMetadata<TItem>(IItemCollection<TItem> collection, JToken token)
        where TItem : DatabaseObject =>
        collection.Add(Convert<List<MetadataProperty>>(token)).ToList();

    private static bool Remove<TItem>(IItemCollection<TItem> collection, IEnumerable<Guid> ids)
        where TItem : DatabaseObject
    {
        var removedAll = true;
        foreach (var id in ids)
        {
            removedAll &= collection.Remove(id);
        }
        return removedAll;
    }

    private static object Run(Action action)
    {
        action();
        return null;
    }

    private static T Convert<T>(JToken token) =>
        token == null ? default : token.ToObject<T>(JsonSerializer.Create(jsonSettings));

    private static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, jsonSettings);

    private static Guid GetGuid(JObject data, string propertyName) =>
        data[propertyName]?.ToObject<Guid>() ?? Guid.Empty;
}
