using Playnite.SDK.Models;

namespace Playnite.SDK.Plugins;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class LibraryGetGamesArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public CancellationToken CancelToken { get; internal set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class LibraryImportGamesArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public CancellationToken CancelToken { get; internal set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class LibraryPluginProperties : PluginProperties
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool CanShutdownClient { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool HasCustomizedGameImport { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public abstract class LibraryPlugin : Plugin
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public LibraryPluginProperties Properties { get; protected set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public abstract string Name { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual string LibraryIcon => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual string LibraryBackground => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual LibraryClient Client => null;

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    protected LibraryPlugin(IPlayniteAPI playniteAPI)
        : base(playniteAPI)
    {
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args) => [];
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<Game> ImportGames(LibraryImportGamesArgs args) => [];
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual LibraryMetadataProvider GetMetadataDownloader() => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public override string ToString() => Name;
}
