using Playnite.Database;
using Playnite.DesktopApp.Avalonia.ViewModels;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopLibrary : IDisposable
{
    private readonly string userDataDirectory;
    private readonly string libraryPath;
    private string temporaryRoot;

    public GameDatabase Database { get; private set; }
    public IReadOnlyList<DesktopGameItemViewModel> Games { get; private set; } =
        Array.Empty<DesktopGameItemViewModel>();
    public bool IsOpen => Database?.IsOpen == true;
    public string ActiveUserDataDirectory => temporaryRoot ?? userDataDirectory;

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
            "Playnite-Avalonia-Desktop-Pilot",
            Guid.NewGuid().ToString("N"));
        PlaynitePaths.UpdateUserDataDir(temporaryRoot);
        Open(Path.Combine(temporaryRoot, "library"));
        Database.Games.Add(Enumerable.Range(1, gameCount).Select(index => new Game($"Desktop Pilot {index:N0}")
        {
            IsInstalled = index % 4 != 0,
            Favorite = index % 13 == 0,
            Hidden = index % 37 == 0,
            Playtime = (ulong)(index * 91),
            LastActivity = DateTime.Now.AddDays(-(index % 120)),
            Description = "A real Playnite.Core game record displayed by the side-by-side Avalonia Desktop pilot."
        }).ToList());
        LoadGames();
    }

    public void Dispose()
    {
        Database?.Dispose();
        Database = null;
        if (temporaryRoot != null)
        {
            try
            {
                Directory.Delete(temporaryRoot, true);
            }
            catch
            {
            }

            temporaryRoot = null;
        }
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
            .Select(game => new DesktopGameItemViewModel(game, Database))
            .ToList();
    }
}
