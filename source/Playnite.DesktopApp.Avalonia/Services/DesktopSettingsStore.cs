using System.Text.Json;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopSettingsStore
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly string settingsPath;

    public string SettingsPath => settingsPath;

    public DesktopSettingsStore(string userDataDirectory)
    {
        settingsPath = Path.Combine(userDataDirectory, "avaloniaDesktop.json");
    }

    public DesktopSettings Load()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var settings = JsonSerializer.Deserialize<DesktopSettings>(
                    File.ReadAllText(settingsPath),
                    serializerOptions) ?? new DesktopSettings();
                settings.Language ??= "english";
                settings.ViewMode = settings.ViewMode is "Grid" or "List" ? settings.ViewMode : "Grid";
                settings.DisabledPlugins ??= new List<string>();
                settings.MetadataSourceIds ??= new List<Guid>();
                settings.MetadataFields ??= DesktopSettings.GetDefaultMetadataFields();
                settings.LibraryPluginIds ??= new List<Guid>();
                settings.GameScannerIds ??= new List<Guid>();
                if (!Enum.IsDefined(settings.MetadataGamesSource))
                {
                    settings.MetadataGamesSource = Playnite.Metadata.MetadataGamesSource.Selected;
                }
                if (!Enum.IsDefined(settings.LibraryPlaytimeImportMode))
                {
                    settings.LibraryPlaytimeImportMode = Playnite.SDK.PlaytimeImportMode.NewImportsOnly;
                }
                return settings;
            }
        }
        catch
        {
        }

        return new DesktopSettings();
    }

    public void Save(DesktopSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
        var temporaryPath = settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, serializerOptions));
        File.Move(temporaryPath, settingsPath, true);
    }
}
