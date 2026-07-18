using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopGameItemViewModel : INotifyPropertyChanged
{
    private static readonly Regex htmlTags = new("<[^>]+>", RegexOptions.Compiled);
    private readonly GameDatabase database;
    private string groupHeader;
    private bool showGroupHeader;

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
    public string PlaytimeText => Game.Playtime == 0
        ? "Not played"
        : $"{TimeSpan.FromSeconds(Game.Playtime).TotalHours:0.#} hours played";
    public string LastPlayedText => Game.LastActivity.HasValue
        ? $"Last played {Game.LastActivity.Value:d}"
        : "Never played";
    public string AddedText => Game.Added.HasValue ? Game.Added.Value.ToString("d") : "Unknown";
    public string ReleaseYearText => Game.ReleaseYear?.ToString() ?? "Unknown";
    public string SourceName => database.Sources[Game.SourceId]?.Name ?? "No source";
    public string PlatformName => Game.PlatformIds?.Select(id => database.Platforms[id]?.Name)
        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "No platform";
    public string CompletionStatusName => database.CompletionStatuses[Game.CompletionStatusId]?.Name ?? "No status";
    public string UserScoreText => Game.UserScore.HasValue ? $"User score {Game.UserScore}/100" : "Not rated";
    public string GenresText => FormatNames("Genres", Game.GenreIds, id => database.Genres[id]?.Name);
    public string PlatformsText => FormatNames("Platforms", Game.PlatformIds, id => database.Platforms[id]?.Name);
    public string CategoriesText => FormatNames("Categories", Game.CategoryIds, id => database.Categories[id]?.Name);
    public string TagsText => FormatNames("Tags", Game.TagIds, id => database.Tags[id]?.Name);
    public string DevelopersText => FormatNames("Developers", Game.DeveloperIds, id => database.Companies[id]?.Name);
    public string PublishersText => FormatNames("Publishers", Game.PublisherIds, id => database.Companies[id]?.Name);
    public string LinksText => FormatLinks(Game.Links);
    public string GameActionsText => FormatGameActions(Game);
    public string RomsText => FormatRoms(Game.Roms);
    public string InstallationDetailsText => FormatInstallation(Game);
    public string ScriptsText => FormatScripts(Game);
    public string MetadataLine => BuildMetadataLine(Game, database);
    public string DescriptionText => ToPlainText(Game.Description);
    public string CoverPath => ResolveMediaPath(Game.CoverImage, database);
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

    private void Game_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(PlaytimeText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(MetadataLine));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(CompletionStatusName));
        OnPropertyChanged(nameof(ReleaseYearText));
        OnPropertyChanged(nameof(UserScoreText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(GenresText));
        OnPropertyChanged(nameof(PlatformsText));
        OnPropertyChanged(nameof(CategoriesText));
        OnPropertyChanged(nameof(TagsText));
        OnPropertyChanged(nameof(DevelopersText));
        OnPropertyChanged(nameof(PublishersText));
        OnPropertyChanged(nameof(LinksText));
        OnPropertyChanged(nameof(GameActionsText));
        OnPropertyChanged(nameof(RomsText));
        OnPropertyChanged(nameof(InstallationDetailsText));
        OnPropertyChanged(nameof(ScriptsText));
        OnPropertyChanged(nameof(CoverPath));
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
