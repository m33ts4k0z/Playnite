using Playnite.Database;
using Playnite.SDK.Models;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class PlayniteLibrary : IDisposable
{
    private readonly string userDataDirectory;
    private readonly string configuredLibraryPath;
    private string temporaryRoot;
    private string activeUserDataDirectory;
    private GameDatabase database;

    public IReadOnlyList<GameItemViewModel> Games { get; private set; } = Array.Empty<GameItemViewModel>();
    public GameDatabase Database => database;
    public string UserDataDirectory => userDataDirectory;
    public string ActiveUserDataDirectory => activeUserDataDirectory ?? userDataDirectory;
    public string ActiveLibraryPath => database?.DatabasePath ?? configuredLibraryPath;
    public bool IsOpen => database?.IsOpen == true;
    public bool IsTemporary => temporaryRoot != null;

    public PlayniteLibrary(string userDataDirectory, string libraryPath)
    {
        this.userDataDirectory = userDataDirectory;
        configuredLibraryPath = libraryPath;
    }

    public void OpenExistingLibrary()
    {
        activeUserDataDirectory = userDataDirectory;
        PlaynitePaths.UpdateUserDataDir(userDataDirectory);
        var settingsPath = Path.Combine(configuredLibraryPath, "database.json");
        if (!File.Exists(settingsPath))
        {
            throw new DirectoryNotFoundException(
                $"No Playnite library was found at '{configuredLibraryPath}'. " +
                "Use --library-path or --userdatadir to select an existing library.");
        }

        Open(configuredLibraryPath);
    }

    public void OpenTemporaryPilotLibrary(int gameCount)
    {
        temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "Playnite-Avalonia-Pilot",
            Guid.NewGuid().ToString("N"));
        var libraryPath = Path.Combine(temporaryRoot, "library");
        activeUserDataDirectory = temporaryRoot;
        PlaynitePaths.UpdateUserDataDir(temporaryRoot);
        Open(libraryPath);

        var games = Enumerable.Range(1, gameCount)
            .Select(index => new Game($"Pilot Game {index:N0}")
            {
                IsInstalled = index % 3 != 0,
                Favorite = index % 11 == 0,
                Playtime = (ulong)(index * 137),
                LastActivity = DateTime.Now.AddDays(-(index % 90))
            })
            .ToList();
        database.Games.Add(games);
        LoadViewModels();
    }

    public void Dispose()
    {
        database?.Dispose();
        database = null;

        if (temporaryRoot != null)
        {
            try
            {
                Directory.Delete(temporaryRoot, true);
            }
            catch
            {
                // A failed test cleanup must not hide the pilot result.
            }

            temporaryRoot = null;
        }
    }

    private void Open(string path)
    {
        database = new GameDatabase(path);
        database.OpenDatabase();
        LoadViewModels();
    }

    private void LoadViewModels()
    {
        Games = database.Games
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(game => new GameItemViewModel(game, database))
            .ToList();
    }
}
