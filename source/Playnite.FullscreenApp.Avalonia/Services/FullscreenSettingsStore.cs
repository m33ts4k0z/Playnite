using System.Text.Json;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenSettingsStore
{
    private readonly string settingsPath;
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string SettingsPath => settingsPath;

    // True once the shell has written its own settings file. When false the shell
    // is running against a profile for the first time and imports WPF defaults.
    public bool Exists => File.Exists(settingsPath);

    public FullscreenSettingsStore(string userDataDirectory)
    {
        settingsPath = Path.Combine(userDataDirectory, "avaloniaFullscreen.json");
    }

    public FullscreenSettings Load()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var settings = JsonSerializer.Deserialize<FullscreenSettings>(
                    File.ReadAllText(settingsPath),
                    serializerOptions) ?? new FullscreenSettings();
                settings.ActiveFilter ??= "All";
                settings.Language ??= "english";
                settings.DisabledPlugins ??= new List<string>();
                settings.DisabledGameControllers ??= new List<string>();
                settings.InterfaceVolume = Math.Clamp(settings.InterfaceVolume, 0, 100);
                settings.BackgroundVolume = Math.Clamp(settings.BackgroundVolume, 0, 100);
                settings.Rows = Math.Clamp(settings.Rows, 1, 10);
                settings.Columns = Math.Clamp(settings.Columns, 1, 20);
                settings.FullscreenItemSpacing = Math.Clamp(settings.FullscreenItemSpacing, 0, 100);
                settings.MainBackgroundImageBlurAmount = Math.Clamp(settings.MainBackgroundImageBlurAmount, 0, 100);
                settings.MainBackgroundImageDarkAmount = Math.Clamp(settings.MainBackgroundImageDarkAmount, 0, 100);
                settings.FontSize = Math.Clamp(settings.FontSize, 8, 72);
                settings.FontSizeSmall = Math.Clamp(settings.FontSizeSmall, 8, 72);
                if (!Enum.IsDefined(settings.ButtonPrompts))
                {
                    settings.ButtonPrompts = FullscreenButtonPrompts.Xbox;
                }
                if (!Enum.IsDefined(settings.SortingOrder))
                {
                    settings.SortingOrder = Playnite.SDK.Models.SortOrder.Name;
                }
                if (!Enum.IsDefined(settings.SortingDirection))
                {
                    settings.SortingDirection = Playnite.SDK.Models.SortOrderDirection.Ascending;
                }
                return settings;
            }
        }
        catch
        {
            // A corrupt pilot settings file must not prevent library startup.
        }

        return new FullscreenSettings();
    }

    public void Save(FullscreenSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
        var temporaryPath = settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, serializerOptions));
        File.Move(temporaryPath, settingsPath, true);
    }
}
