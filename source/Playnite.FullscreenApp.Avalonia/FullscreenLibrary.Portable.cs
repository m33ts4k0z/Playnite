using System.Diagnostics;
using Playnite.Database;
using Playnite.SDK.Models;

namespace Playnite.FullscreenApp.Avalonia;

internal sealed class FullscreenLibrary : IDisposable
{
    private readonly string userDataDirectory;
    private readonly string libraryPath;
    private string temporaryRoot;

    public GameDatabase Database { get; private set; }
    public IReadOnlyList<Game> Games { get; private set; } = Array.Empty<Game>();
    public bool IsOpen => Database?.IsOpen == true;

    public FullscreenLibrary(string userDataDirectory, string libraryPath)
    {
        this.userDataDirectory = userDataDirectory;
        this.libraryPath = libraryPath;
    }

    public void OpenExistingLibrary()
    {
        PlaynitePaths.UpdateUserDataDir(userDataDirectory);
        if (!File.Exists(Path.Combine(libraryPath, "database.json")))
        {
            throw new DirectoryNotFoundException(
                $"No Playnite library was found at '{libraryPath}'. Use --library-path or --userdatadir.");
        }

        Open(libraryPath);
    }

    public void OpenTemporaryLibrary(int gameCount)
    {
        temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "Playnite-Avalonia-Linux-Fullscreen-SelfTest",
            Guid.NewGuid().ToString("N"));
        PlaynitePaths.UpdateUserDataDir(temporaryRoot);
        Open(Path.Combine(temporaryRoot, "library"));
        Database.Games.Add(Enumerable.Range(1, gameCount).Select(index => new Game($"Couch Game {index:N0}")
        {
            IsInstalled = index % 5 != 0,
            Favorite = index % 11 == 0,
            Playtime = (ulong)(index * 91),
            LastActivity = DateTime.Now.AddDays(-(index % 90)),
            Description = "A portable Playnite.Core record rendered by the native Fullscreen shell."
        }).ToList());
        LoadGames();
    }

    public void Dispose()
    {
        Database?.Dispose();
        Database = null;
        if (temporaryRoot == null)
        {
            return;
        }

        try
        {
            Directory.Delete(temporaryRoot, true);
        }
        catch (Exception exception)
        {
            Trace.TraceError("Failed to remove portable Fullscreen self-test data '{0}': {1}", temporaryRoot, exception);
        }

        temporaryRoot = null;
    }

    private void Open(string path)
    {
        Database = new GameDatabase(path);
        Database.OpenDatabase();
        LoadGames();
    }

    private void LoadGames()
    {
        Games = Database.Games
            .OrderByDescending(game => game.Favorite)
            .ThenByDescending(game => game.LastActivity)
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
