using System.Net;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class GameItemViewModel : INotifyPropertyChanged
{
    private static readonly Regex htmlTags = new("<[^>]+>", RegexOptions.Compiled);

    public Game Game { get; }
    public event PropertyChangedEventHandler PropertyChanged;
    public string Name => Game.Name ?? string.Empty;
    public bool Favorite => Game.Favorite;
    public bool IsInstalled => Game.IsInstalled;
    public string InstallationText => IsInstalled ? "Installed" : "Not installed";
    public string ActivityText => Game.IsLaunching
        ? "Launching"
        : Game.IsRunning
            ? "Running"
            : Game.IsInstalling
                ? "Installing"
                : Game.IsUninstalling
                    ? "Uninstalling"
                    : InstallationText;
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
    public bool ShowTitle { get; private set; }
    public bool DarkenUninstalled { get; private set; }
    public double TileOpacity => !IsInstalled && DarkenUninstalled ? 0.45 : 1;

    public GameItemViewModel(Game game, GameDatabase database)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        MetadataLine = BuildMetadataLine(game, database);
        DescriptionText = ToPlainText(game.Description);
        CoverPath = ResolveMediaPath(game.CoverImage, database);
        BackgroundPath = ResolveMediaPath(game.BackgroundImage, database);
        Game.PropertyChanged += Game_PropertyChanged;
    }

    private void Game_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        OnPropertyChanged(nameof(InstallationText));
        OnPropertyChanged(nameof(ActivityText));
        OnPropertyChanged(nameof(PlaytimeText));
        OnPropertyChanged(nameof(LastPlayedText));
    }

    internal void Refresh()
    {
        OnPropertyChanged(string.Empty);
        OnPropertyChanged(nameof(InstallationText));
        OnPropertyChanged(nameof(ActivityText));
        OnPropertyChanged(nameof(PlaytimeText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(TileOpacity));
    }

    internal void ApplyVisualSettings(bool showTitle, bool darkenUninstalled)
    {
        ShowTitle = showTitle;
        DarkenUninstalled = darkenUninstalled;
        OnPropertyChanged(nameof(ShowTitle));
        OnPropertyChanged(nameof(TileOpacity));
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
