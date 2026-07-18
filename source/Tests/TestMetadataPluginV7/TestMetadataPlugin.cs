using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace TestMetadataPluginV7;

public sealed class TestMetadataPlugin : MetadataPlugin
{
    private string EventPath => Path.Combine(GetPluginUserDataPath(), "metadata-events.txt");

    public override Guid Id { get; } = Guid.Parse("d8d4d9b5-3fa1-44c8-b851-5f87198fc110");
    public override string Name => "Test SDK v7 metadata provider";
    public override List<MetadataField> SupportedFields { get; } =
        Enum.GetValues<MetadataField>().ToList();

    public TestMetadataPlugin(IPlayniteAPI api)
        : base(api)
    {
        Properties = new MetadataPluginProperties();
    }

    public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
    {
        File.AppendAllLines(EventPath,
        [
            $"provider-created:{options.GameData.Name}:{options.IsBackgroundDownload}"
        ]);
        return new TestProvider(EventPath, SupportedFields);
    }

    private sealed class TestProvider : OnDemandMetadataProvider
    {
        private static readonly byte[] imageBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        private readonly string eventPath;

        public override List<MetadataField> AvailableFields { get; }

        public TestProvider(string eventPath, List<MetadataField> availableFields)
        {
            this.eventPath = eventPath;
            AvailableFields = availableFields.ToList();
        }

        private static void Check(GetMetadataFieldArgs args) =>
            args.CancelToken.ThrowIfCancellationRequested();

        public override string GetName(GetMetadataFieldArgs args)
        {
            Check(args);
            return "SDK v7 metadata name";
        }

        public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 metadata genre")];
        }

        public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
        {
            Check(args);
            return new ReleaseDate(2024, 7, 18);
        }

        public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 developer")];
        }

        public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 publisher")];
        }

        public override IEnumerable<MetadataProperty> GetTags(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 tag")];
        }

        public override string GetDescription(GetMetadataFieldArgs args)
        {
            Check(args);
            return "SDK v7 metadata description";
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new Link("SDK v7 link", "https://playnite.link/sdk-v7")];
        }

        public override int? GetCriticScore(GetMetadataFieldArgs args)
        {
            Check(args);
            return 91;
        }

        public override int? GetCommunityScore(GetMetadataFieldArgs args)
        {
            Check(args);
            return 87;
        }

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            Check(args);
            return new MetadataFile("v7-icon.png", imageBytes);
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            Check(args);
            return new MetadataFile("v7-cover.png", imageBytes);
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            Check(args);
            return new MetadataFile("v7-background.png", imageBytes);
        }

        public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 feature")];
        }

        public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 age rating")];
        }

        public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 series")];
        }

        public override IEnumerable<MetadataProperty> GetRegions(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 region")];
        }

        public override IEnumerable<MetadataProperty> GetPlatforms(GetMetadataFieldArgs args)
        {
            Check(args);
            return [new MetadataNameProperty("SDK v7 platform")];
        }

        public override ulong? GetInstallSize(GetMetadataFieldArgs args)
        {
            Check(args);
            return 123456789;
        }

        public override void Dispose() =>
            File.AppendAllLines(eventPath, ["provider-disposed"]);
    }
}
