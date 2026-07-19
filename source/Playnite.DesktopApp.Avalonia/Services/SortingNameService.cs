using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.Services;

internal static class SortingNameService
{
    public static int FillMissing(
        GameDatabase database,
        IEnumerable<Game> games,
        IEnumerable<string> removedArticles)
    {
        if (database == null)
        {
            return 0;
        }

        var candidates = (games ?? Array.Empty<Game>())
            .Where(game => game != null && string.IsNullOrWhiteSpace(game.SortingName))
            .GroupBy(game => game.Id)
            .Select(group => group.First())
            .ToList();
        var converter = new global::Playnite.SortableNameConverter(
            removedArticles ?? Array.Empty<string>(),
            batchOperation: candidates.Count > 20);
        var updated = 0;
        using (database.BufferedUpdate())
        {
            foreach (var game in candidates)
            {
                var sortingName = converter.Convert(game.Name);
                if (string.Equals(game.Name, sortingName, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(sortingName))
                {
                    continue;
                }

                game.SortingName = sortingName;
                database.Games.Update(game);
                updated++;
            }
        }

        return updated;
    }
}
