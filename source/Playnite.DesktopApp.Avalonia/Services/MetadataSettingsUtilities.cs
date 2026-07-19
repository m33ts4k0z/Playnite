using Playnite.Metadata;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.Services;

internal static class MetadataSettingsUtilities
{
    public static IReadOnlyList<MetadataField> SupportedFields { get; } = Enum.GetValues<MetadataField>();

    public static MetadataDownloaderSettings Clone(MetadataDownloaderSettings source)
    {
        source = EnsureInitialized(source);
        var clone = new MetadataDownloaderSettings
        {
            GamesSource = source.GamesSource,
            SkipExistingValues = source.SkipExistingValues
        };
        foreach (var field in SupportedFields)
        {
            var sourceField = GetField(source, field);
            var targetField = GetField(clone, field);
            targetField.Import = sourceField.Import;
            targetField.Sources = sourceField.Sources?.ToList() ?? new List<Guid>();
        }

        return clone;
    }

    public static MetadataDownloaderSettings EnsureInitialized(MetadataDownloaderSettings settings)
    {
        settings ??= MetadataDownloaderSettings.GetDefaultSettings();
        settings.Name ??= new MetadataFieldSettings();
        settings.Genre ??= new MetadataFieldSettings();
        settings.ReleaseDate ??= new MetadataFieldSettings();
        settings.Developer ??= new MetadataFieldSettings();
        settings.Publisher ??= new MetadataFieldSettings();
        settings.Tag ??= new MetadataFieldSettings();
        settings.Description ??= new MetadataFieldSettings();
        settings.Links ??= new MetadataFieldSettings();
        settings.CriticScore ??= new MetadataFieldSettings();
        settings.CommunityScore ??= new MetadataFieldSettings();
        settings.Icon ??= new MetadataFieldSettings();
        settings.CoverImage ??= new MetadataFieldSettings();
        settings.BackgroundImage ??= new MetadataFieldSettings();
        settings.Feature ??= new MetadataFieldSettings();
        settings.AgeRating ??= new MetadataFieldSettings();
        settings.Series ??= new MetadataFieldSettings();
        settings.Region ??= new MetadataFieldSettings();
        settings.Platform ??= new MetadataFieldSettings();
        settings.InstallSize ??= new MetadataFieldSettings();
        var defaults = MetadataDownloaderSettings.GetDefaultSettings();
        foreach (var field in SupportedFields)
        {
            var current = GetField(settings, field);
            var fallback = GetField(defaults, field);
            current.Sources ??= fallback.Sources?.ToList() ?? new List<Guid>();
        }

        return settings;
    }

    public static MetadataFieldSettings GetField(MetadataDownloaderSettings settings, MetadataField field) =>
        field switch
        {
            MetadataField.Name => settings.Name,
            MetadataField.Genres => settings.Genre,
            MetadataField.ReleaseDate => settings.ReleaseDate,
            MetadataField.Developers => settings.Developer,
            MetadataField.Publishers => settings.Publisher,
            MetadataField.Tags => settings.Tag,
            MetadataField.Description => settings.Description,
            MetadataField.Links => settings.Links,
            MetadataField.CriticScore => settings.CriticScore,
            MetadataField.CommunityScore => settings.CommunityScore,
            MetadataField.Icon => settings.Icon,
            MetadataField.CoverImage => settings.CoverImage,
            MetadataField.BackgroundImage => settings.BackgroundImage,
            MetadataField.Features => settings.Feature,
            MetadataField.AgeRating => settings.AgeRating,
            MetadataField.Series => settings.Series,
            MetadataField.Region => settings.Region,
            MetadataField.Platform => settings.Platform,
            MetadataField.InstallSize => settings.InstallSize,
            _ => throw new NotSupportedException($"Unsupported metadata field {field}.")
        };
}
