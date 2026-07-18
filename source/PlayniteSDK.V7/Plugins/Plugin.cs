using Avalonia.Controls;
using Avalonia.Data.Converters;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;

namespace Playnite.SDK.Plugins;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class GetPlayActionsArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Game Game { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class GetInstallActionsArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Game Game { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class GetUninstallActionsArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Game Game { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LoadPluginAttribute : Attribute
{
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class IgnorePluginAttribute : Attribute
{
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class GetGameViewControlArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Name { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public ApplicationMode Mode { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class AddCustomElementSupportArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public List<string> ElementList { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string SourceName { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class AddSettingsSupportArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string SourceName { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string SettingsRoot { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class AddConvertersSupportArgs
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public List<IValueConverter> Converters { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string SourceName { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public abstract class PluginProperties
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool HasSettings { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class GenericPluginProperties : PluginProperties
{
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public abstract class GenericPlugin : Plugin
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public GenericPluginProperties Properties { get; protected set; }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    protected GenericPlugin(IPlayniteAPI playniteAPI)
        : base(playniteAPI)
    {
    }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public abstract class Plugin : IDisposable, IIdentifiable
{
    private const string PluginSettingsFileName = "config.json";

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public List<SearchSupport> Searches { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public readonly IPlayniteAPI PlayniteApi;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public abstract Guid Id { get; }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    protected Plugin(IPlayniteAPI playniteAPI)
    {
        PlayniteApi = playniteAPI ?? throw new ArgumentNullException(nameof(playniteAPI));
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual ISettings GetSettings(bool firstRunSettings) => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual Control GetSettingsView(bool firstRunView) => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameStarting(OnGameStartingEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameStarted(OnGameStartedEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameStopped(OnGameStoppedEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameStartupCancelled(OnGameStartupCancelledEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameInstalled(OnGameInstalledEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameInstallationCancelled(OnGameInstallationCancelledEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameUninstalled(OnGameUninstalledEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnGameSelected(OnGameSelectedEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnApplicationStarted(OnApplicationStartedEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnApplicationStopped(OnApplicationStoppedEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnDesktopControllerButtonStateChanged(OnControllerButtonStateChangedArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnControllerConnected(OnControllerConnectedArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnControllerDisconnected(OnControllerDisconnectedArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual void OnFullscreenViewChanged(OnFullscreenViewChangedArgs args) { }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args) => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args) => null;

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string GetPluginUserDataPath()
    {
        var path = Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, Id.ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public TConfig GetPluginConfiguration<TConfig>() where TConfig : class
    {
        var pluginDir = Path.GetDirectoryName(GetType().Assembly.Location);
        var pluginConfig = Path.Combine(pluginDir, "plugin.cfg");
        return File.Exists(pluginConfig) ? Serialization.FromJsonFile<TConfig>(pluginConfig) : null;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public TSettings LoadPluginSettings<TSettings>() where TSettings : class
    {
        var settingsFile = Path.Combine(GetPluginUserDataPath(), PluginSettingsFileName);
        return File.Exists(settingsFile) ? Serialization.FromJsonFile<TSettings>(settingsFile) : null;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void SavePluginSettings<TSettings>(TSettings settings) where TSettings : class
    {
        var settingsFile = Path.Combine(GetPluginUserDataPath(), PluginSettingsFileName);
        File.WriteAllText(settingsFile, Serialization.ToJson(settings, true));
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Task<bool> OpenSettingsViewAsync()
    {
        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
        {
            return Task.FromResult(false);
        }

        return PlayniteApi.MainView.OpenPluginSettingsAsync(Id);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
    {
        yield break;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
    {
        yield break;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
    {
        yield break;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual Control GetGameViewControl(GetGameViewControlArgs args) => null;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void AddCustomElementSupport(AddCustomElementSupportArgs args) => PlayniteApi.AddCustomElementSupport(this, args);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void AddSettingsSupport(AddSettingsSupportArgs args) => PlayniteApi.AddSettingsSupport(this, args);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void AddConvertersSupport(AddConvertersSupportArgs args) => PlayniteApi.AddConvertersSupport(this, args);

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<SidebarItem> GetSidebarItems()
    {
        yield break;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<TopPanelItem> GetTopPanelItems()
    {
        yield break;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public virtual IEnumerable<SearchItem> GetSearchGlobalCommands()
    {
        yield break;
    }
}
