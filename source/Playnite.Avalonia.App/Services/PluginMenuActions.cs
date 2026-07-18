using Playnite.Scripting;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

public sealed class PluginMenuAction
{
    private readonly Action action;

    public Guid PluginId { get; }
    public string PluginName { get; }
    public string Description { get; }
    public string MenuSection { get; }
    public string Icon { get; }
    public string DisplayName
    {
        get
        {
            var section = MenuSection?.TrimStart('@').Replace("|", " > ");
            return string.IsNullOrWhiteSpace(section) ? Description : $"{section} > {Description}";
        }
    }

    internal PluginMenuAction(
        Guid pluginId,
        string pluginName,
        string description,
        string menuSection,
        string icon,
        Action action)
    {
        PluginId = pluginId;
        PluginName = pluginName;
        Description = description;
        MenuSection = menuSection;
        Icon = icon;
        this.action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public void Invoke() => action();
}

internal sealed class V7RemoteMenuItem
{
    private readonly object instance;
    private readonly MethodInfo invoke;

    public string Description { get; }
    public string MenuSection { get; }
    public string Icon { get; }

    public V7RemoteMenuItem(object instance)
    {
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        var type = instance.GetType();
        Description = Read<string>(type, nameof(Description));
        MenuSection = Read<string>(type, nameof(MenuSection));
        Icon = Read<string>(type, nameof(Icon));
        invoke = V7Reflection.GetRequiredMethod(type, nameof(Invoke));
    }

    public void Invoke() => V7Reflection.Invoke(instance, invoke);

    private T Read<T>(Type type, string name) =>
        (T)(type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance)
            ?? default(T));
}

public sealed partial class AvaloniaRuntimeHost
{
    public IReadOnlyList<PluginMenuAction> GetMainMenuActions(bool globalSearchRequest = false)
    {
        var result = new List<PluginMenuAction>();
        foreach (var loaded in extensions.Plugins.Values)
        {
            try
            {
                foreach (var item in loaded.Plugin.GetMainMenuItems(new GetMainMenuItemsArgs
                         {
                             IsGlobalSearchRequest = globalSearchRequest
                         }) ?? [])
                {
                    Add(result, loaded.Plugin.Id, loaded.Description.Name, item, () =>
                        item.Action?.Invoke(new MainMenuItemActionArgs { SourceItem = item }));
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"Plugin {loaded.Description.Name} main-menu discovery failed: {exception.Message}");
            }
        }

        foreach (var script in extensions.Scripts.Where(script =>
                     script.SupportedMenus.Contains(SupportedMenuMethods.MainMenu)))
        {
            try
            {
                foreach (var item in script.GetMainMenuItems(new GetMainMenuItemsArgs
                         {
                             IsGlobalSearchRequest = globalSearchRequest
                         }) ?? [])
                {
                    Add(result, Guid.Empty, script.Name, item, () => script.InvokeFunction(
                        item.FunctionName,
                        [new ScriptMainMenuItemActionArgs { SourceItem = item }]));
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus($"Script {script.Name} main-menu discovery failed: {exception.Message}");
            }
        }

        AddV7MenuActions(result, "Main", [], globalSearchRequest);
        return result;
    }

    public IReadOnlyList<PluginMenuAction> GetGameMenuActions(
        IReadOnlyList<Game> games,
        bool globalSearchRequest = false)
    {
        var selectedGames = games?.Where(game => game != null).ToList() ?? [];
        var result = new List<PluginMenuAction>();
        var args = new GetGameMenuItemsArgs
        {
            Games = selectedGames,
            IsGlobalSearchRequest = globalSearchRequest
        };
        foreach (var loaded in extensions.Plugins.Values)
        {
            try
            {
                foreach (var item in loaded.Plugin.GetGameMenuItems(args) ?? [])
                {
                    Add(result, loaded.Plugin.Id, loaded.Description.Name, item, () =>
                        item.Action?.Invoke(new GameMenuItemActionArgs
                        {
                            Games = selectedGames,
                            SourceItem = item
                        }));
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"Plugin {loaded.Description.Name} game-menu discovery failed: {exception.Message}");
            }
        }

        foreach (var script in extensions.Scripts.Where(script =>
                     script.SupportedMenus.Contains(SupportedMenuMethods.GameMenu)))
        {
            try
            {
                foreach (var item in script.GetGameMenuItems(args) ?? [])
                {
                    Add(result, Guid.Empty, script.Name, item, () => script.InvokeFunction(
                        item.FunctionName,
                        [new ScriptGameMenuItemActionArgs
                        {
                            Games = selectedGames,
                            SourceItem = item
                        }]));
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus($"Script {script.Name} game-menu discovery failed: {exception.Message}");
            }
        }

        AddV7MenuActions(result, "Game", selectedGames, globalSearchRequest);
        return result;
    }

    private void AddV7MenuActions(
        List<PluginMenuAction> result,
        string kind,
        IReadOnlyList<Game> games,
        bool globalSearchRequest)
    {
        var gamesJson = V7DatabaseTransport.Serialize(games);
        foreach (var plugin in v7Plugins.Plugins)
        {
            try
            {
                foreach (var instance in plugin.GetMenuItems(kind, gamesJson, globalSearchRequest))
                {
                    var item = new V7RemoteMenuItem(instance);
                    Add(result, plugin.Id, plugin.Name, item.Description, item.MenuSection, item.Icon, item.Invoke);
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"SDK v7 plugin {plugin.Name} {kind.ToLowerInvariant()}-menu discovery failed: " +
                    exception.Message);
            }
        }
    }

    private static void Add(
        List<PluginMenuAction> result,
        Guid pluginId,
        string pluginName,
        PluginMenuItem item,
        Action action) => Add(
            result,
            pluginId,
            pluginName,
            item.Description,
            item.MenuSection,
            item.Icon,
            action);

    private static void Add(
        List<PluginMenuAction> result,
        Guid pluginId,
        string pluginName,
        string description,
        string menuSection,
        string icon,
        Action action)
    {
        if (string.IsNullOrWhiteSpace(description) || description == "-")
        {
            return;
        }

        result.Add(new PluginMenuAction(
            pluginId,
            string.IsNullOrWhiteSpace(pluginName) ? "Plugin" : pluginName,
            description,
            menuSection,
            icon,
            action));
    }
}
