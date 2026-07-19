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

    // True once the shell has written its own settings file. When false the shell
    // is running against a profile for the first time and imports WPF defaults.
    public bool Exists => File.Exists(settingsPath);

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
                settings.DisabledGameControllers ??= new List<string>();
                settings.DetailsVisibility ??= new Playnite.Avalonia.App.Services.DetailsVisibilitySettings();
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
                if (!Enum.IsDefined(settings.AfterLaunch))
                {
                    settings.AfterLaunch = Playnite.Avalonia.App.Services.AfterLaunchOption.Minimize;
                }
                if (!Enum.IsDefined(settings.AfterGameClose))
                {
                    settings.AfterGameClose = Playnite.Avalonia.App.Services.AfterGameCloseOption.Restore;
                }
                if (!Enum.IsDefined(settings.TrayIcon))
                {
                    settings.TrayIcon = Playnite.Avalonia.App.Services.TrayIconOption.Default;
                }
                if (!Enum.IsDefined(settings.CoverArtStretch))
                {
                    settings.CoverArtStretch = global::Avalonia.Media.Stretch.UniformToFill;
                }
                settings.GridItemWidth = Math.Clamp(settings.GridItemWidth, 80, 600);
                settings.GridItemWidthRatio = Math.Clamp(settings.GridItemWidthRatio, 1, 20);
                settings.GridItemHeightRatio = Math.Clamp(settings.GridItemHeightRatio, 1, 20);
                settings.GridItemSpacing = Math.Clamp(settings.GridItemSpacing, 0, 80);
                settings.GridItemMargin = Math.Clamp(settings.GridItemMargin, 0, 40);
                settings.GridViewScrollSensitivity = Math.Clamp(settings.GridViewScrollSensitivity, 0.1, 10);
                settings.ListViewScrollSensitivity = Math.Clamp(settings.ListViewScrollSensitivity, 0.1, 10);
                settings.GridViewScrollDurationMilliseconds =
                    Math.Clamp(settings.GridViewScrollDurationMilliseconds, 16, 5000);
                settings.ListViewScrollDurationMilliseconds =
                    Math.Clamp(settings.ListViewScrollDurationMilliseconds, 16, 5000);
                settings.DetailsViewScrollSensitivity = Math.Clamp(settings.DetailsViewScrollSensitivity, 0.1, 10);
                settings.DetailsViewScrollDurationMilliseconds =
                    Math.Clamp(settings.DetailsViewScrollDurationMilliseconds, 16, 5000);
                settings.GameDetailsIndentation = Math.Clamp(settings.GameDetailsIndentation, 0, 200);
                settings.GameDetailsCoverHeight = Math.Clamp(settings.GameDetailsCoverHeight, 100, 800);
                settings.DetailsViewListIconSize = Math.Clamp(settings.DetailsViewListIconSize, 20, 160);
                settings.GridDetailsWidth = Math.Clamp(settings.GridDetailsWidth, 240, 800);
                if (settings.GridViewDetailsPosition is not global::Avalonia.Controls.Dock.Left and
                    not global::Avalonia.Controls.Dock.Right)
                {
                    settings.GridViewDetailsPosition = global::Avalonia.Controls.Dock.Right;
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
