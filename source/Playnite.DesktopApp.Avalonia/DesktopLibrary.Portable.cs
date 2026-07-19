using Playnite.Database;
using Playnite.SDK.Models;
using System.Diagnostics;

namespace Playnite.DesktopApp.Avalonia;

internal sealed class DesktopLibrary : IDisposable
{
    private readonly string userDataDirectory;
    private readonly string libraryPath;
    private string temporaryRoot;

    public GameDatabase Database { get; private set; }
    public IReadOnlyList<Game> Games { get; private set; } = Array.Empty<Game>();
    public bool IsOpen => Database?.IsOpen == true;

    public DesktopLibrary(string userDataDirectory, string libraryPath)
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
            "Playnite-Avalonia-Linux-SelfTest",
            Guid.NewGuid().ToString("N"));
        PlaynitePaths.UpdateUserDataDir(temporaryRoot);
        Open(Path.Combine(temporaryRoot, "library"));
        Database.Games.Add(Enumerable.Range(1, gameCount).Select(index => new Game($"Linux Game {index:N0}")
        {
            IsInstalled = index % 4 != 0,
            Favorite = index % 13 == 0,
            Playtime = (ulong)(index * 73),
            LastActivity = DateTime.Now.AddDays(-(index % 120)),
            Description = "A portable Playnite.Core record rendered by the native Avalonia shell."
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
            Trace.TraceError("Failed to remove portable Desktop self-test data '{0}': {1}", temporaryRoot, exception);
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
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
