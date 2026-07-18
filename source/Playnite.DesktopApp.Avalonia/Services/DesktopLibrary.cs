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
    public string SelfTestMediaPath { get; private set; }

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
        SelfTestMediaPath = Path.Combine(temporaryRoot, "pilot-media.png");
        File.WriteAllBytes(
            SelfTestMediaPath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nQAAAABJRU5ErkJggg=="));
        var actionGenre = new Genre("Action");
        var strategyGenre = new Genre("Strategy");
        var windowsPlatform = new Platform("Windows");
        var linuxPlatform = new Platform("Linux");
        var backlogCategory = new Category("Backlog");
        var showcaseCategory = new Category("Showcase");
        var controllerTag = new Tag("Controller support");
        var cooperativeTag = new Tag("Co-op");
        var pilotDeveloper = new Company("Pilot Studio");
        var samplePublisher = new Company("Sample Publishing");
        Database.Genres.Add(new List<Genre> { actionGenre, strategyGenre });
        Database.Platforms.Add(new List<Platform> { windowsPlatform, linuxPlatform });
        Database.Categories.Add(new List<Category> { backlogCategory, showcaseCategory });
        Database.Tags.Add(new List<Tag> { controllerTag, cooperativeTag });
        Database.Companies.Add(new List<Company> { pilotDeveloper, samplePublisher });
        Database.Games.Add(Enumerable.Range(1, gameCount).Select(index => new Game($"Desktop Pilot {index:N0}")
        {
            IsInstalled = index % 4 != 0,
            Favorite = index % 13 == 0,
            Hidden = index % 37 == 0,
            Playtime = (ulong)(index * 91),
            LastActivity = DateTime.Now.AddDays(-(index % 120)),
            Description = "A real Playnite.Core game record displayed by the side-by-side Avalonia Desktop pilot.",
            GenreIds = new List<Guid> { index % 2 == 0 ? actionGenre.Id : strategyGenre.Id },
            PlatformIds = new List<Guid> { index % 3 == 0 ? linuxPlatform.Id : windowsPlatform.Id },
            CategoryIds = new List<Guid> { index % 5 == 0 ? showcaseCategory.Id : backlogCategory.Id },
            TagIds = new List<Guid>
            {
                controllerTag.Id,
                index % 7 == 0 ? cooperativeTag.Id : controllerTag.Id
            }.Distinct().ToList(),
            DeveloperIds = new List<Guid> { pilotDeveloper.Id },
            PublisherIds = new List<Guid> { samplePublisher.Id }
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
            SelfTestMediaPath = null;
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
