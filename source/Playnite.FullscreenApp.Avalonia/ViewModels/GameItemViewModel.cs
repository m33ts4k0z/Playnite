using System.Net;
using System.Text.RegularExpressions;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class GameItemViewModel
{
    private static readonly Regex htmlTags = new("<[^>]+>", RegexOptions.Compiled);

    public Game Game { get; }
    public string Name => Game.Name ?? string.Empty;
    public bool Favorite => Game.Favorite;
    public bool IsInstalled => Game.IsInstalled;
    public string InstallationText => IsInstalled ? "Installed" : "Not installed";
    public string PlaytimeText => Game.Playtime == 0
        ? "Not played"
        : $"{TimeSpan.FromSeconds(Game.Playtime).TotalHours:0.#} hours played";
    public string LastPlayedText => Game.LastActivity.HasValue
        ? $"Last played {Game.LastActivity.Value:d}"
        : "Never played";
    public string MetadataLine { get; }
    public string DescriptionText { get; }
    public string CoverPath { get; }
    public string BackgroundPath { get; }

    public GameItemViewModel(Game game, GameDatabase database)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        MetadataLine = BuildMetadataLine(game, database);
        DescriptionText = ToPlainText(game.Description);
        CoverPath = ResolveMediaPath(game.CoverImage, database);
        BackgroundPath = ResolveMediaPath(game.BackgroundImage, database);
    }

    private static string BuildMetadataLine(Game game, GameDatabase database)
    {
        var parts = new List<string>();
        if (game.ReleaseYear.HasValue)
        {
            parts.Add(game.ReleaseYear.Value.ToString());
        }

        if (game.PlatformIds?.Count > 0)
        {
            var platforms = game.PlatformIds
                .Select(id => database.Platforms[id]?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name));
            parts.AddRange(platforms.Take(2));
        }

        if (game.GenreIds?.Count > 0)
        {
            var genres = game.GenreIds
                .Select(id => database.Genres[id]?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name));
            parts.AddRange(genres.Take(2));
        }

        return parts.Count == 0 ? "Playnite library" : string.Join("  •  ", parts);
    }

    private static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "No description is available for this game.";
        }

        var withoutTags = htmlTags.Replace(html, " ");
        return WebUtility.HtmlDecode(Regex.Replace(withoutTags, @"\s+", " ")).Trim();
    }

    private static string ResolveMediaPath(string path, GameDatabase database)
    {
        if (string.IsNullOrWhiteSpace(path) || Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsHttpUrl())
        {
            return path;
        }

        return Path.IsPathFullyQualified(path) ? path : database.GetFullFilePath(path);
    }
}

internal static class UriExtensions
{
    public static bool IsHttpUrl(this Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
