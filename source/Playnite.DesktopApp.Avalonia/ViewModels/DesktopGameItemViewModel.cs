using System.Net;
using System.Text.RegularExpressions;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopGameItemViewModel
{
    private static readonly Regex htmlTags = new("<[^>]+>", RegexOptions.Compiled);

    public Game Game { get; }
    public string Name => Game.Name ?? string.Empty;
    public bool Favorite => Game.Favorite;
    public bool IsInstalled => Game.IsInstalled;
    public string StateText => IsInstalled ? "Installed" : "Not installed";
    public string PlaytimeText => Game.Playtime == 0
        ? "Not played"
        : $"{TimeSpan.FromSeconds(Game.Playtime).TotalHours:0.#} hours played";
    public string LastPlayedText => Game.LastActivity.HasValue
        ? $"Last played {Game.LastActivity.Value:d}"
        : "Never played";
    public string MetadataLine { get; }
    public string DescriptionText { get; }
    public string CoverPath { get; }

    public DesktopGameItemViewModel(Game game, GameDatabase database)
    {
        Game = game;
        MetadataLine = BuildMetadataLine(game, database);
        DescriptionText = ToPlainText(game.Description);
        CoverPath = ResolveMediaPath(game.CoverImage, database);
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
}
