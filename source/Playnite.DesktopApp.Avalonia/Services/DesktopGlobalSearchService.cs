using Playnite.Avalonia.App.Services;
using Playnite.Common;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.Services;

internal sealed class DesktopGlobalSearchService
{
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private readonly Func<IReadOnlyList<LoadedPlugin>> plugins;
    private readonly Func<IReadOnlyList<V7LoadedPlugin>> v7Plugins;
    private readonly Func<IReadOnlyList<DesktopSearchCommand>> commands;
    private readonly Action<Guid, GameSearchItemAction> invokeGameAction;
    private readonly Func<Game, string> resolveGameIcon;
    private readonly Func<Guid, string> resolveLibraryIcon;

    public DesktopGlobalSearchService(
        GameDatabase database,
        DesktopSettings settings,
        Func<IReadOnlyList<LoadedPlugin>> plugins,
        Func<IReadOnlyList<V7LoadedPlugin>> v7Plugins,
        Func<IReadOnlyList<DesktopSearchCommand>> commands,
        Action<Guid, GameSearchItemAction> invokeGameAction,
        Func<Game, string> resolveGameIcon,
        Func<Guid, string> resolveLibraryIcon)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.plugins = plugins ?? (() => Array.Empty<LoadedPlugin>());
        this.v7Plugins = v7Plugins ?? (() => Array.Empty<V7LoadedPlugin>());
        this.commands = commands ?? (() => Array.Empty<DesktopSearchCommand>());
        this.invokeGameAction = invokeGameAction ?? throw new ArgumentNullException(nameof(invokeGameAction));
        this.resolveGameIcon = resolveGameIcon ?? (_ => null);
        this.resolveLibraryIcon = resolveLibraryIcon ?? (_ => null);
    }

    public AvaloniaSearchContext CreateContext() => new(
        "Search games, Playnite commands, and extension providers.",
        "Type a game or command, # for commands, or / for providers",
        "Global search",
        100,
        false,
        false,
        Search);

    private AvaloniaSearchBatch Search(AvaloniaSearchRequest request)
    {
        var term = request.SearchTerm ?? string.Empty;
        if (term.StartsWith('#'))
        {
            return new AvaloniaSearchBatch(SearchCommands(term[1..].Trim()));
        }

        var providers = GetProviders();
        if (term.EndsWith(' '))
        {
            var providerKeyword = term.Trim().TrimStart('/');
            var provider = providers.FirstOrDefault(candidate =>
                string.Equals(candidate.Keyword, providerKeyword, StringComparison.OrdinalIgnoreCase));
            if (provider != null)
            {
                return new AvaloniaSearchBatch(
                    Array.Empty<AvaloniaSearchItem>(),
                    provider.Context);
            }
        }

        if (term.StartsWith('/'))
        {
            var providerTerm = term[1..].Trim();
            var results = providers
                .Where(provider => Matches(provider.Name, providerTerm) || Matches(provider.Keyword, providerTerm))
                .Select(provider => new AvaloniaSearchItem(
                    provider.Name,
                    $"/{provider.Keyword}",
                    null,
                    new AvaloniaSearchAction(
                        "Activate",
                        false,
                        provider.Context,
                        null),
                    null,
                    null))
                .ToList();
            return new AvaloniaSearchBatch(results);
        }

        var trimmedTerm = term.Trim();
        var items = new List<AvaloniaSearchItem>();
        if (!string.IsNullOrWhiteSpace(trimmedTerm) && settings.IncludeCommandsInDefaultSearch)
        {
            items.AddRange(SearchCommands(trimmedTerm));
        }

        var games = database.Games
            .Where(game => (request.IncludeUninstalled || game.IsInstalled) &&
                (request.IncludeHidden || !game.Hidden) &&
                (string.IsNullOrWhiteSpace(trimmedTerm) || Matches(game.Name, trimmedTerm)))
            .OrderBy(game => string.IsNullOrWhiteSpace(trimmedTerm)
                ? 0
                : game.Name.GetLevenshteinDistanceIgnoreCase(trimmedTerm))
            .ThenByDescending(game => string.IsNullOrWhiteSpace(trimmedTerm) ? game.LastActivity : null)
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(string.IsNullOrWhiteSpace(trimmedTerm) ? 20 : 60);
        items.AddRange(games.Select(CreateGameItem));
        return new AvaloniaSearchBatch(items);
    }

    private IReadOnlyList<AvaloniaSearchItem> SearchCommands(string term) => commands()
        .Where(command => Matches(command.Name, term))
        .Select(command => new AvaloniaSearchItem(
            command.Name,
            command.Description,
            null,
            new AvaloniaSearchAction("Run", true, null, command.Invoke),
            null,
            null))
        .ToList();

    private AvaloniaSearchItem CreateGameItem(Game game)
    {
        var visibility = settings.SearchWindowVisibility ?? new SearchWindowVisibilitySettings();
        var details = new List<string>();
        if (visibility.HiddenStatus && game.Hidden)
        {
            details.Add("Hidden");
        }
        if (visibility.Platform)
        {
            details.AddRange((game.PlatformIds ?? [])
                .Select(id => database.Platforms[id]?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)));
        }
        if (visibility.PlayTime)
        {
            details.Add(FormatPlaytime(game.Playtime));
        }
        if (visibility.CompletionStatus && game.CompletionStatusId != Guid.Empty)
        {
            var completionStatus = database.CompletionStatuses[game.CompletionStatusId]?.Name;
            if (!string.IsNullOrWhiteSpace(completionStatus))
            {
                details.Add(completionStatus);
            }
        }
        if (visibility.ReleaseDate && game.ReleaseDate != null)
        {
            details.Add(game.ReleaseDate.Value.Year.ToString());
        }

        var iconPath = visibility.GameIcon ? resolveGameIcon(game) : null;
        if (string.IsNullOrWhiteSpace(iconPath) && visibility.LibraryIcon)
        {
            iconPath = resolveLibraryIcon(game.PluginId);
        }

        return new AvaloniaSearchItem(
            game.Name,
            string.Join(" • ", details.Distinct(StringComparer.CurrentCultureIgnoreCase)),
            null,
            CreateGameAction(game, settings.PrimaryGameSearchItemAction),
            CreateGameAction(game, settings.SecondaryGameSearchItemAction),
            CreateGameAction(game, GameSearchItemAction.OpenMenu),
            iconPath);
    }

    private AvaloniaSearchAction CreateGameAction(Game game, GameSearchItemAction action)
    {
        var name = action switch
        {
            GameSearchItemAction.Play => game.IsInstalled ? "Play" : "Install",
            GameSearchItemAction.SwitchTo => "Switch to game",
            GameSearchItemAction.OpenMenu => "Open game menu",
            GameSearchItemAction.Edit => "Edit",
            GameSearchItemAction.None => null,
            _ => throw new NotSupportedException($"Unsupported game search action {action}.")
        };
        return name == null
            ? null
            : new AvaloniaSearchAction(name, true, null, () => invokeGameAction(game.Id, action));
    }

    private IReadOnlyList<SearchProvider> GetProviders()
    {
        var providers = new List<SearchProvider>();
        foreach (var plugin in plugins().Where(plugin => plugin?.Plugin != null))
        {
            foreach (var support in plugin.Plugin.Searches ?? [])
            {
                if (support?.Context == null || string.IsNullOrWhiteSpace(support.DefaultKeyword))
                {
                    continue;
                }

                var id = $"{plugin.Description.Id}{support.DefaultKeyword}";
                var keyword = settings.CustomSearchKeywords.TryGetValue(id, out var customKeyword) &&
                    !string.IsNullOrWhiteSpace(customKeyword)
                    ? customKeyword.Trim()
                    : support.DefaultKeyword;
                providers.Add(new SearchProvider(
                    support.Name,
                    keyword,
                    AvaloniaSearchContext.FromSdk(support.Context)));
            }
        }

        foreach (var plugin in v7Plugins())
        {
            foreach (var support in plugin.GetSearches())
            {
                var id = $"{plugin.Manifest.Id}{support.DefaultKeyword}";
                var keyword = settings.CustomSearchKeywords.TryGetValue(id, out var customKeyword) &&
                    !string.IsNullOrWhiteSpace(customKeyword)
                    ? customKeyword.Trim()
                    : support.DefaultKeyword;
                providers.Add(new SearchProvider(
                    support.Name,
                    keyword,
                    AvaloniaSearchContext.FromSdkV7(support.Context)));
            }
        }

        return providers;
    }

    private static bool Matches(string value, string term)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(term) || value.Contains(term, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }
        if (term.GetJaroWinklerSimilarityIgnoreCase(value) >= 0.90)
        {
            return true;
        }
        return term.Length <= value.Length && term.IsStartOfStringAcronym(value);
    }

    private static string FormatPlaytime(ulong seconds)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        return duration.TotalHours >= 1
            ? $"{duration.TotalHours:0.#} hours"
            : $"{duration.TotalMinutes:0} minutes";
    }

    private sealed record SearchProvider(string Name, string Keyword, AvaloniaSearchContext Context);
}

internal sealed record DesktopSearchCommand(string Name, string Description, Action Invoke);
