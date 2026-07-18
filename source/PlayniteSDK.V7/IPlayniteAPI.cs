using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IPlayniteAPI
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IMainViewAPI MainView { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IGameDatabaseAPI Database { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IDialogsFactory Dialogs { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IPlaynitePathsAPI Paths { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    INotificationsAPI Notifications { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IPlayniteInfoAPI ApplicationInfo { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IWebViewFactory WebViews { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IResourceProvider Resources { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IUriHandlerAPI UriHandler { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IPlayniteSettingsAPI ApplicationSettings { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IAddons Addons { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IEmulationAPI Emulation { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string ExpandGameVariables(Game game, string inputString);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string ExpandGameVariables(Game game, string inputString, string emulatorDir);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    GameAction ExpandGameVariables(Game game, GameAction action);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task StartGameAsync(Guid gameId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task InstallGameAsync(Guid gameId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task UninstallGameAsync(Guid gameId);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    List<GamepadController> GetConnectedControllers();
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public static class API
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public static IPlayniteAPI Instance { get; internal set; }
}
