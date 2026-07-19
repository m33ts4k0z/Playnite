using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK.Models;
using Playnite.Avalonia.App.Services;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopGameItemViewModel : INotifyPropertyChanged
{
    private static readonly Regex htmlTags = new("<[^>]+>", RegexOptions.Compiled);
    private readonly GameDatabase database;
    private DesktopSettings appearanceSettings = new();
    private string groupHeader;
    private bool showGroupHeader;
    private string libraryIconPath;
    private string libraryBackgroundPath;

    public event PropertyChangedEventHandler PropertyChanged;
    public Game Game { get; }
    public string Name => Game.Name ?? string.Empty;
    public bool Favorite => Game.Favorite;
    public bool IsInstalled => Game.IsInstalled;
    public string StateText => Game.IsLaunching
        ? "Launching"
        : Game.IsRunning
            ? "Running"
            : Game.IsInstalling
                ? "Installing"
                : Game.IsUninstalling
                    ? "Uninstalling"
                    : IsInstalled ? "Installed" : "Not installed";
    public string PlaytimeText
    {
        get
        {
            if (Game.Playtime == 0)
            {
                return "Not played";
            }

            var playtime = TimeSpan.FromSeconds(Game.Playtime);
            return appearanceSettings.PlaytimeUseDaysFormat && playtime.TotalHours >= 24
                ? $"{playtime.TotalDays:0.#} days played"
                : $"{playtime.TotalHours:0.#} hours played";
        }
    }
    public string LastPlayedText => Game.LastActivity.HasValue
        ? $"Last played {DateFormattingService.Format(Game.LastActivity.Value, appearanceSettings.DateTimeFormatLastPlayed)}"
        : "Never played";
    public string AddedText => Game.Added.HasValue
        ? $"Added {DateFormattingService.Format(Game.Added.Value, appearanceSettings.DateTimeFormatAdded)}"
        : "Added: Unknown";
    public string ModifiedText => Game.Modified.HasValue
        ? $"Modified {DateFormattingService.Format(Game.Modified.Value, appearanceSettings.DateTimeFormatModified)}"
        : "Modified: Unknown";
    public string RecentActivityText => Game.RecentActivity.HasValue
        ? $"Recent activity {DateFormattingService.Format(Game.RecentActivity.Value, appearanceSettings.DateTimeFormatRecentActivity)}"
        : "Recent activity: None";
    public string ReleaseDateText => Game.ReleaseDate.HasValue
        ? FormatReleaseDate(Game.ReleaseDate.Value)
        : "Release date: Unknown";
    public string ReleaseYearText => Game.ReleaseYear?.ToString() ?? "Unknown";
    public string LibraryText => $"Library: {Game.PluginId}";
    public string VersionText => string.IsNullOrWhiteSpace(Game.Version) ? "Version: Unknown" : $"Version: {Game.Version}";
    public string InstallSizeText => Game.InstallSize.HasValue
        ? $"Install size: {Game.InstallSize.Value / 1024d / 1024d:0.##} MB"
        : "Install size: Unknown";
    public string InstallDirectoryText => string.IsNullOrWhiteSpace(Game.InstallDirectory)
        ? "Install directory: Unknown"
        : $"Install directory: {Game.InstallDirectory}";
    public string NotesText => string.IsNullOrWhiteSpace(Game.Notes) ? "Notes: None" : $"Notes: {Game.Notes}";
    public string SourceName => database.Sources[Game.SourceId]?.Name ?? "No source";
    public string PlatformName => Game.PlatformIds?.Select(id => database.Platforms[id]?.Name)
        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "No platform";
    public string CompletionStatusName => database.CompletionStatuses[Game.CompletionStatusId]?.Name ?? "No status";
    public string UserScoreText => Game.UserScore.HasValue ? $"User score {Game.UserScore}/100" : "Not rated";
    public string CriticScoreText => Game.CriticScore.HasValue ? $"Critic score {Game.CriticScore}/100" : "No critic score";
    public string CommunityScoreText => Game.CommunityScore.HasValue
        ? $"Community score {Game.CommunityScore}/100"
        : "No community score";
    public string GenresText => FormatNames("Genres", Game.GenreIds, id => database.Genres[id]?.Name);
    public string PlatformsText => FormatNames("Platforms", Game.PlatformIds, id => database.Platforms[id]?.Name);
    public string CategoriesText => FormatNames("Categories", Game.CategoryIds, id => database.Categories[id]?.Name);
    public string TagsText => FormatNames("Tags", Game.TagIds, id => database.Tags[id]?.Name);
    public string DevelopersText => FormatNames("Developers", Game.DeveloperIds, id => database.Companies[id]?.Name);
    public string PublishersText => FormatNames("Publishers", Game.PublisherIds, id => database.Companies[id]?.Name);
    public string FeaturesText => FormatNames("Features", Game.FeatureIds, id => database.Features[id]?.Name);
    public string SeriesText => FormatNames("Series", Game.SeriesIds, id => database.Series[id]?.Name);
    public string AgeRatingsText => FormatNames("Age ratings", Game.AgeRatingIds, id => database.AgeRatings[id]?.Name);
    public string RegionsText => FormatNames("Regions", Game.RegionIds, id => database.Regions[id]?.Name);
    public string LinksText => FormatLinks(Game.Links);
    public string GameActionsText => FormatGameActions(Game);
    public string RomsText => FormatRoms(Game.Roms);
    public string InstallationDetailsText => FormatInstallation(Game);
    public string ScriptsText => FormatScripts(Game);
    public string MetadataLine => BuildMetadataLine(Game, database);
    public string DescriptionText => ToPlainText(Game.Description);
    public string CoverPath => ResolveCoverPath();
    public string IconPath => ResolveIconPath();
    public string BackgroundPath => ResolveBackgroundPath();
    public double WindowBackgroundDarkOpacity => string.IsNullOrWhiteSpace(BackgroundPath) ||
        !appearanceSettings.DarkenWindowBackgroundImage
            ? 0
            : appearanceSettings.BackgroundImageDarkAmount;
    public Stretch CoverArtStretch => appearanceSettings.CoverArtStretch;
    public Thickness GridItemMargin => new(appearanceSettings.GridItemMargin);
    public bool ShowGridItemBackground => appearanceSettings.ShowGridItemBackground;
    public bool ShowNamesUnderCovers => appearanceSettings.ShowNamesUnderCovers;
    public bool ShowEmptyCoverName => appearanceSettings.ShowNameEmptyCover && string.IsNullOrWhiteSpace(CoverPath);
    public double GridItemOpacity => appearanceSettings.DarkenUninstalledGamesGrid && !IsInstalled ? 0.5 : 1;
    public bool ShowListIcon => appearanceSettings.ShowIconsOnList;
    public double ListIconHeight => appearanceSettings.DetailsViewListIconSize;
    public double ListIconWidth => appearanceSettings.DetailsViewListIconSize * 0.75;
    public string GroupHeader => groupHeader;
    public bool ShowGroupHeader => showGroupHeader;

    public DesktopGameItemViewModel(Game game, GameDatabase database)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        this.database = database ?? throw new ArgumentNullException(nameof(database));
        Game.PropertyChanged += Game_PropertyChanged;
    }

    internal void SetGroup(string header, bool showHeader)
    {
        if (groupHeader != header)
        {
            groupHeader = header;
            OnPropertyChanged(nameof(GroupHeader));
        }

        if (showGroupHeader != showHeader)
        {
            showGroupHeader = showHeader;
            OnPropertyChanged(nameof(ShowGroupHeader));
        }
    }

    internal void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }

    internal void ApplyAppearance(DesktopSettings settings)
    {
        appearanceSettings = settings ?? new DesktopSettings();
        OnPropertyChanged(nameof(PlaytimeText));
        OnPropertyChanged(nameof(CoverArtStretch));
        OnPropertyChanged(nameof(GridItemMargin));
        OnPropertyChanged(nameof(ShowGridItemBackground));
        OnPropertyChanged(nameof(ShowNamesUnderCovers));
        OnPropertyChanged(nameof(ShowEmptyCoverName));
        OnPropertyChanged(nameof(GridItemOpacity));
        OnPropertyChanged(nameof(ShowListIcon));
        OnPropertyChanged(nameof(ListIconHeight));
        OnPropertyChanged(nameof(ListIconWidth));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(AddedText));
        OnPropertyChanged(nameof(ModifiedText));
        OnPropertyChanged(nameof(RecentActivityText));
        OnPropertyChanged(nameof(ReleaseDateText));
        OnPropertyChanged(nameof(CoverPath));
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(BackgroundPath));
        OnPropertyChanged(nameof(WindowBackgroundDarkOpacity));
    }

    internal void ConfigureLibraryMedia(string iconPath, string backgroundPath)
    {
        libraryIconPath = iconPath;
        libraryBackgroundPath = backgroundPath;
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(BackgroundPath));
        OnPropertyChanged(nameof(WindowBackgroundDarkOpacity));
    }

    private void Game_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RaiseGamePropertiesChanged(e.PropertyName));
            return;
        }

        RaiseGamePropertiesChanged(e.PropertyName);
    }

    private void RaiseGamePropertiesChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(PlaytimeText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(AddedText));
        OnPropertyChanged(nameof(ModifiedText));
        OnPropertyChanged(nameof(RecentActivityText));
        OnPropertyChanged(nameof(ReleaseDateText));
        OnPropertyChanged(nameof(MetadataLine));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(CompletionStatusName));
        OnPropertyChanged(nameof(ReleaseYearText));
        OnPropertyChanged(nameof(LibraryText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(InstallSizeText));
        OnPropertyChanged(nameof(InstallDirectoryText));
        OnPropertyChanged(nameof(NotesText));
        OnPropertyChanged(nameof(UserScoreText));
        OnPropertyChanged(nameof(CriticScoreText));
        OnPropertyChanged(nameof(CommunityScoreText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(GenresText));
        OnPropertyChanged(nameof(PlatformsText));
        OnPropertyChanged(nameof(CategoriesText));
        OnPropertyChanged(nameof(TagsText));
        OnPropertyChanged(nameof(DevelopersText));
        OnPropertyChanged(nameof(PublishersText));
        OnPropertyChanged(nameof(FeaturesText));
        OnPropertyChanged(nameof(SeriesText));
        OnPropertyChanged(nameof(AgeRatingsText));
        OnPropertyChanged(nameof(RegionsText));
        OnPropertyChanged(nameof(LinksText));
        OnPropertyChanged(nameof(GameActionsText));
        OnPropertyChanged(nameof(RomsText));
        OnPropertyChanged(nameof(InstallationDetailsText));
        OnPropertyChanged(nameof(ScriptsText));
        OnPropertyChanged(nameof(CoverPath));
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(BackgroundPath));
        OnPropertyChanged(nameof(WindowBackgroundDarkOpacity));
        OnPropertyChanged(nameof(ShowEmptyCoverName));
        OnPropertyChanged(nameof(GridItemOpacity));
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string BuildMetadataLine(Game game, GameDatabase database)
    {
        var parts = new List<string>();
        if (game.ReleaseYear.HasValue)
        {
            parts.Add(game.ReleaseYear.Value.ToString());
        }

        if (game.PlatformIds?.Count > 0)
        {
            parts.AddRange(game.PlatformIds
                .Select(id => database.Platforms[id]?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(2));
        }

        return parts.Count == 0 ? "Playnite library" : string.Join("  •  ", parts);
    }

    private static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "No description is available for this game.";
        }

        return WebUtility.HtmlDecode(Regex.Replace(htmlTags.Replace(html, " "), @"\s+", " ")).Trim();
    }

    private static string ResolveMediaPath(string path, GameDatabase database)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return path;
        }

        return Path.IsPathFullyQualified(path) ? path : database.GetFullFilePath(path);
    }

    private string ResolveIconPath()
    {
        if (!string.IsNullOrWhiteSpace(Game.Icon))
        {
            return ResolveMediaPath(Game.Icon, database);
        }

        var fallback = appearanceSettings.DefaultIconSource switch
        {
            DefaultIconSourceOptions.Library => libraryIconPath,
            DefaultIconSourceOptions.Platform => FirstPlatformMedia(platform => platform.Icon),
            DefaultIconSourceOptions.General => Path.Combine(AppContext.BaseDirectory, "Assets", "applogo.png"),
            _ => null
        };
        return ResolveMediaPath(fallback, database);
    }

    private string ResolveCoverPath()
    {
        if (!string.IsNullOrWhiteSpace(Game.CoverImage))
        {
            return ResolveMediaPath(Game.CoverImage, database);
        }

        var fallback = appearanceSettings.DefaultCoverSource switch
        {
            DefaultCoverSourceOptions.Platform => FirstPlatformMedia(platform => platform.Cover),
            DefaultCoverSourceOptions.General => Path.Combine(AppContext.BaseDirectory, "Assets", "custom_cover_background.png"),
            _ => null
        };
        return ResolveMediaPath(fallback, database);
    }

    private string ResolveBackgroundPath()
    {
        if (!string.IsNullOrWhiteSpace(Game.BackgroundImage))
        {
            return ResolveMediaPath(Game.BackgroundImage, database);
        }

        var fallback = appearanceSettings.DefaultBackgroundSource switch
        {
            DefaultBackgroundSourceOptions.Library => libraryBackgroundPath,
            DefaultBackgroundSourceOptions.Platform => FirstPlatformMedia(platform => platform.Background),
            DefaultBackgroundSourceOptions.Cover => Game.CoverImage,
            _ => null
        };
        return ResolveMediaPath(fallback, database);
    }

    private string FirstPlatformMedia(Func<Platform, string> selector) =>
        (Game.PlatformIds ?? new List<Guid>())
            .Select(id => database.Platforms[id])
            .Where(platform => platform != null)
            .Select(selector)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

    private string FormatReleaseDate(ReleaseDate releaseDate) =>
        "Released " + DateFormattingService.FormatReleaseDate(
            releaseDate.Date,
            releaseDate.Month.HasValue,
            releaseDate.Day.HasValue,
            appearanceSettings.DateTimeFormatReleaseDate);

    private static string FormatNames(
        string label,
        IEnumerable<Guid> ids,
        Func<Guid, string> resolveName)
    {
        var names = (ids ?? Array.Empty<Guid>())
            .Select(resolveName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return names.Count == 0 ? $"{label}: None" : $"{label}: {string.Join(", ", names)}";
    }

    private static string FormatLinks(IEnumerable<Link> links)
    {
        var names = (links ?? Array.Empty<Link>())
            .Select(link => link?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return names.Count == 0 ? "Links: None" : $"Links: {string.Join(", ", names)}";
    }

    private static string FormatGameActions(Game game)
    {
        var customCount = game.GameActions?.Count ?? 0;
        if (game.IncludeLibraryPluginAction)
        {
            return customCount == 0
                ? "Actions: Library plugin"
                : $"Actions: Library plugin + {customCount:N0} custom";
        }

        return customCount == 0 ? "Actions: None" : $"Actions: {customCount:N0} custom";
    }

    private static string FormatRoms(IEnumerable<GameRom> roms)
    {
        var names = (roms ?? Array.Empty<GameRom>())
            .Where(rom => rom != null)
            .Select(rom => string.IsNullOrWhiteSpace(rom.Name) ? GetFileName(rom.Path) : rom.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return names.Count == 0 ? "ROMs: None" : $"ROMs: {string.Join(", ", names)}";
    }

    private static string GetFileName(string path)
    {
        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return path;
        }
    }

    private static string FormatInstallation(Game game)
    {
        var parts = new List<string> { game.IsInstalled ? "Installed" : "Not installed" };
        if (!string.IsNullOrWhiteSpace(game.Version))
        {
            parts.Add($"Version {game.Version}");
        }

        if (game.InstallSize.HasValue)
        {
            parts.Add($"{game.InstallSize.Value / 1024d / 1024d:0.##} MB");
        }

        if (!string.IsNullOrWhiteSpace(game.InstallDirectory))
        {
            parts.Add(game.InstallDirectory);
        }

        return $"Installation: {string.Join(" • ", parts)}";
    }

    private static string FormatScripts(Game game)
    {
        var scripts = new List<string>();
        if (!string.IsNullOrWhiteSpace(game.PreScript)) scripts.Add("pre");
        if (!string.IsNullOrWhiteSpace(game.GameStartedScript)) scripts.Add("started");
        if (!string.IsNullOrWhiteSpace(game.PostScript)) scripts.Add("post");
        var scriptText = scripts.Count == 0 ? "none" : string.Join(", ", scripts);
        return game.EnableSystemHdr
            ? $"Scripts: {scriptText} • System HDR"
            : $"Scripts: {scriptText}";
    }
}
