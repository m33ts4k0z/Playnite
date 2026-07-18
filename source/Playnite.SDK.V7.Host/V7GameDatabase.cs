using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections;

namespace Playnite.SDK.V7.Host;

internal sealed class V7MetadataPropertyConverter : JsonConverter
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

internal static class V7RpcJson
{
    private static readonly JsonSerializerSettings settings = new()
    {
        ContractResolver = V7JsonResolver.Instance,
        Converters = { new V7MetadataPropertyConverter() },
        MaxDepth = 128
    };

    public static string Serialize(object value) => JsonConvert.SerializeObject(value, settings);
    public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, settings);
}

internal interface IV7HostCollection
{
    void Publish(string eventName, string payload);
}

internal sealed class HostItemCollection<TItem> : IItemCollection<TItem>, IV7HostCollection
    where TItem : DatabaseObject
{
    private sealed class BufferScope : IDisposable
    {
        private HostItemCollection<TItem> collection;

        public BufferScope(HostItemCollection<TItem> collection)
        {
            this.collection = collection;
            collection.BeginBufferUpdate();
        }

        public void Dispose()
        {
            collection?.EndBufferUpdate();
            collection = null;
        }
    }

    private sealed class CollectionChangedPayload
    {
        public List<TItem> AddedItems { get; set; }
        public List<TItem> RemovedItems { get; set; }
    }

    private sealed class UpdatedPayload
    {
        public List<UpdatePayload> UpdatedItems { get; set; }
    }

    private sealed class UpdatePayload
    {
        public TItem OldData { get; set; }
        public TItem NewData { get; set; }
    }

    private readonly Func<string, string, string> hostCall;
    public GameDatabaseCollection CollectionType { get; }
    public int Count => CallRequired<int>("Count");
    public bool IsReadOnly => false;

    public event EventHandler<ItemCollectionChangedEventArgs<TItem>> ItemCollectionChanged;
    public event EventHandler<ItemUpdatedEventArgs<TItem>> ItemUpdated;

    public TItem this[Guid id]
    {
        get => Get(id);
        set => Update(value);
    }

    public HostItemCollection(
        GameDatabaseCollection collectionType,
        Func<string, string, string> hostCall)
    {
        CollectionType = collectionType;
        this.hostCall = hostCall;
    }

    public bool ContainsItem(Guid id) => CallRequired<bool>("Contains", new { Id = id });
    public TItem Get(Guid id) => CallOptional<TItem>("Get", new { Id = id });
    public List<TItem> Get(IList<Guid> ids) => CallRequired<List<TItem>>("GetMany", new { Ids = ids });
    public TItem Add(string itemName) => CallRequired<TItem>("AddName", new { Name = itemName });

    public TItem Add(string itemName, Func<TItem, string, bool> existingComparer)
    {
        ArgumentNullException.ThrowIfNull(existingComparer);
        return this.FirstOrDefault(item => existingComparer(item, itemName)) ?? Add(itemName);
    }

    public IEnumerable<TItem> Add(List<string> items) =>
        CallRequired<List<TItem>>("AddNames", new { Names = items });

    public IEnumerable<TItem> Add(List<string> items, Func<TItem, string, bool> existingComparer)
    {
        ArgumentNullException.ThrowIfNull(existingComparer);
        return items.Select(item => Add(item, existingComparer)).ToList();
    }

    public TItem Add(MetadataProperty property) =>
        CallRequired<List<TItem>>("AddMetadata", new { Properties = new[] { property } }).FirstOrDefault()
        ?? throw new InvalidDataException(
            $"Avalonia host returned no added {CollectionType} item for metadata property {property}.");

    public IEnumerable<TItem> Add(IEnumerable<MetadataProperty> properties) =>
        CallRequired<List<TItem>>("AddMetadata", new { Properties = properties.ToList() });

    public void Add(IEnumerable<TItem> items) =>
        CallVoid("AddItems", new { Items = items.ToList() });

    public void Add(TItem item) => Add(new[] { item });
    public bool Remove(Guid id) => CallRequired<bool>("Remove", new { Ids = new[] { id } });
    public bool Remove(IEnumerable<TItem> items) =>
        CallRequired<bool>("Remove", new { Ids = items.Select(item => item.Id).ToList() });
    public bool Remove(TItem item) => item != null && Remove(item.Id);
    public void Update(TItem item) => Update(new[] { item });
    public void Update(IEnumerable<TItem> items) =>
        CallVoid("Update", new { Items = items.ToList() });
    public void Clear() => CallVoid("Clear");
    public bool Contains(TItem item) => item != null && ContainsItem(item.Id);
    public void CopyTo(TItem[] array, int arrayIndex) => this.ToList().CopyTo(array, arrayIndex);
    public IEnumerator<TItem> GetEnumerator() => CallRequired<List<TItem>>("All").GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerable<TItem> GetClone() => CallRequired<List<TItem>>("All");
    public IDisposable BufferedUpdate() => new BufferScope(this);
    public void BeginBufferUpdate() => CallVoid("BeginBufferUpdate");
    public void EndBufferUpdate() => CallVoid("EndBufferUpdate");
    public void Dispose() { }

    public void Publish(string eventName, string payload)
    {
        if (eventName == "Changed")
        {
            var data = V7RpcJson.Deserialize<CollectionChangedPayload>(payload);
            ItemCollectionChanged?.Invoke(this, new ItemCollectionChangedEventArgs<TItem>(
                data?.AddedItems ?? [],
                data?.RemovedItems ?? []));
        }
        else if (eventName == "Updated")
        {
            var data = V7RpcJson.Deserialize<UpdatedPayload>(payload);
            ItemUpdated?.Invoke(this, new ItemUpdatedEventArgs<TItem>(
                data?.UpdatedItems?.Select(update =>
                    new ItemUpdateEvent<TItem>(update.OldData, update.NewData)) ?? []));
        }
    }

    private void CallVoid(string action, object data = null) =>
        hostCall("Database", CreateRequest(action, data));

    private TResult CallRequired<TResult>(string action, object data = null)
    {
        var response = hostCall("Database", CreateRequest(action, data));
        if (string.IsNullOrWhiteSpace(response) || response == "null")
        {
            throw new InvalidDataException(
                $"Avalonia host returned no {CollectionType} result for database action {action}.");
        }
        return V7RpcJson.Deserialize<TResult>(response)
            ?? throw new InvalidDataException(
                $"Avalonia host returned an invalid {CollectionType} result for database action {action}.");
    }

    private TResult CallOptional<TResult>(string action, object data = null)
    {
        var response = hostCall("Database", CreateRequest(action, data));
        return string.IsNullOrWhiteSpace(response) || response == "null"
            ? default
            : V7RpcJson.Deserialize<TResult>(response);
    }

    private string CreateRequest(string action, object data) => V7RpcJson.Serialize(new
    {
        Action = action,
        Collection = CollectionType.ToString(),
        Data = data
    });
}

internal sealed class HostGameDatabase : IGameDatabaseAPI
{
    private sealed class BufferScope : IDisposable
    {
        private HostGameDatabase database;

        public BufferScope(HostGameDatabase database)
        {
            this.database = database;
            database.BeginBufferUpdate();
        }

        public void Dispose()
        {
            database?.EndBufferUpdate();
            database = null;
        }
    }

    private readonly Func<string, string, string> hostCall;
    private readonly Dictionary<string, IV7HostCollection> collections;

    public IItemCollection<Game> Games { get; }
    public IItemCollection<Platform> Platforms { get; }
    public IItemCollection<Emulator> Emulators { get; }
    public IItemCollection<Genre> Genres { get; }
    public IItemCollection<Company> Companies { get; }
    public IItemCollection<Tag> Tags { get; }
    public IItemCollection<Category> Categories { get; }
    public IItemCollection<Series> Series { get; }
    public IItemCollection<AgeRating> AgeRatings { get; }
    public IItemCollection<Region> Regions { get; }
    public IItemCollection<GameSource> Sources { get; }
    public IItemCollection<GameFeature> Features { get; }
    public IItemCollection<GameScannerConfig> GameScanners { get; }
    public IItemCollection<CompletionStatus> CompletionStatuses { get; }
    public IItemCollection<ImportExclusionItem> ImportExclusions { get; }
    public IItemCollection<FilterPreset> FilterPresets { get; }
    public string DatabasePath => CallRequired<string>("Path");
    public bool IsOpen => CallRequired<bool>("IsOpen");

    public event EventHandler DatabaseOpened;

    public HostGameDatabase(Func<string, string, string> hostCall)
    {
        this.hostCall = hostCall;
        Games = Create<Game>(GameDatabaseCollection.Games);
        Platforms = Create<Platform>(GameDatabaseCollection.Platforms);
        Emulators = Create<Emulator>(GameDatabaseCollection.Emulators);
        Genres = Create<Genre>(GameDatabaseCollection.Genres);
        Companies = Create<Company>(GameDatabaseCollection.Companies);
        Tags = Create<Tag>(GameDatabaseCollection.Tags);
        Categories = Create<Category>(GameDatabaseCollection.Categories);
        Series = Create<Series>(GameDatabaseCollection.Series);
        AgeRatings = Create<AgeRating>(GameDatabaseCollection.AgeRatings);
        Regions = Create<Region>(GameDatabaseCollection.Regions);
        Sources = Create<GameSource>(GameDatabaseCollection.Sources);
        Features = Create<GameFeature>(GameDatabaseCollection.Features);
        GameScanners = Create<GameScannerConfig>(GameDatabaseCollection.GameScanners);
        CompletionStatuses = Create<CompletionStatus>(GameDatabaseCollection.CompletionStatuses);
        ImportExclusions = Create<ImportExclusionItem>(GameDatabaseCollection.ImportExclusions);
        FilterPresets = Create<FilterPreset>(GameDatabaseCollection.FilterPresets);
        collections = new Dictionary<string, IV7HostCollection>(StringComparer.Ordinal)
        {
            [GameDatabaseCollection.Games.ToString()] = (IV7HostCollection)Games,
            [GameDatabaseCollection.Platforms.ToString()] = (IV7HostCollection)Platforms,
            [GameDatabaseCollection.Emulators.ToString()] = (IV7HostCollection)Emulators,
            [GameDatabaseCollection.Genres.ToString()] = (IV7HostCollection)Genres,
            [GameDatabaseCollection.Companies.ToString()] = (IV7HostCollection)Companies,
            [GameDatabaseCollection.Tags.ToString()] = (IV7HostCollection)Tags,
            [GameDatabaseCollection.Categories.ToString()] = (IV7HostCollection)Categories,
            [GameDatabaseCollection.Series.ToString()] = (IV7HostCollection)Series,
            [GameDatabaseCollection.AgeRatings.ToString()] = (IV7HostCollection)AgeRatings,
            [GameDatabaseCollection.Regions.ToString()] = (IV7HostCollection)Regions,
            [GameDatabaseCollection.Sources.ToString()] = (IV7HostCollection)Sources,
            [GameDatabaseCollection.Features.ToString()] = (IV7HostCollection)Features,
            [GameDatabaseCollection.GameScanners.ToString()] = (IV7HostCollection)GameScanners,
            [GameDatabaseCollection.CompletionStatuses.ToString()] = (IV7HostCollection)CompletionStatuses,
            [GameDatabaseCollection.ImportExclusions.ToString()] = (IV7HostCollection)ImportExclusions,
            [GameDatabaseCollection.FilterPresets.ToString()] = (IV7HostCollection)FilterPresets
        };
    }

    public string AddFile(string path, Guid parentId) =>
        CallOptional<string>("AddFile", new { Path = path, ParentId = parentId });
    public void SaveFile(string id, string path) => CallVoid("SaveFile", new { Id = id, Path = path });
    public void RemoveFile(string id) => CallVoid("RemoveFile", new { Id = id });
    public string GetFileStoragePath(Guid parentId) =>
        CallRequired<string>("GetFileStoragePath", new { ParentId = parentId });
    public string GetFullFilePath(string databasePath) =>
        CallRequired<string>("GetFullFilePath", new { Path = databasePath });
    public IDisposable BufferedUpdate() => new BufferScope(this);
    public void BeginBufferUpdate() => CallVoid("BeginBufferUpdate");
    public void EndBufferUpdate() => CallVoid("EndBufferUpdate");
    public Game ImportGame(GameMetadata game) => CallRequired<Game>("ImportGame", new { Game = game });
    public Game ImportGame(GameMetadata game, LibraryPlugin sourcePlugin) =>
        CallRequired<Game>("ImportGame", new { Game = game, SourcePluginId = sourcePlugin?.Id });
    public bool GetGameMatchesFilter(Game game, FilterPresetSettings filterSettings) =>
        GetGameMatchesFilter(game, filterSettings, false);
    public bool GetGameMatchesFilter(Game game, FilterPresetSettings filterSettings, bool useFuzzyNameMatch) =>
        CallRequired<bool>("MatchesFilter", new { Game = game, Filter = filterSettings, Fuzzy = useFuzzyNameMatch });
    public IEnumerable<Game> GetFilteredGames(FilterPresetSettings filterSettings) =>
        GetFilteredGames(filterSettings, false);
    public IEnumerable<Game> GetFilteredGames(FilterPresetSettings filterSettings, bool useFuzzyNameMatch) =>
        CallRequired<List<Game>>("FilteredGames", new { Filter = filterSettings, Fuzzy = useFuzzyNameMatch });

    public void Publish(string collection, string eventName, string payload)
    {
        if (collection == "Database" && eventName == "Opened")
        {
            DatabaseOpened?.Invoke(this, EventArgs.Empty);
        }
        else if (collections.TryGetValue(collection, out var target))
        {
            target.Publish(eventName, payload);
        }
    }

    private HostItemCollection<TItem> Create<TItem>(GameDatabaseCollection type)
        where TItem : DatabaseObject => new(type, hostCall);

    private void CallVoid(string action, object data = null) =>
        hostCall("Database", CreateRequest(action, data));

    private TResult CallRequired<TResult>(string action, object data = null)
    {
        var response = hostCall("Database", CreateRequest(action, data));
        if (string.IsNullOrWhiteSpace(response) || response == "null")
        {
            throw new InvalidDataException(
                $"Avalonia host returned no database result for action {action}.");
        }
        return V7RpcJson.Deserialize<TResult>(response)
            ?? throw new InvalidDataException(
                $"Avalonia host returned an invalid database result for action {action}.");
    }

    private TResult CallOptional<TResult>(string action, object data = null)
    {
        var response = hostCall("Database", CreateRequest(action, data));
        return string.IsNullOrWhiteSpace(response) || response == "null"
            ? default
            : V7RpcJson.Deserialize<TResult>(response);
    }

    private static string CreateRequest(string action, object data) =>
        V7RpcJson.Serialize(new { Action = action, Data = data });
}
