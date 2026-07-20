#if !WINDOWS
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Playnite.Plugins;

namespace Playnite.Avalonia.App.Services;

internal static class PluginCompatibilityHost
{
    public static IPluginCompatibilityHost Create() => new PortablePluginCompatibilityHost();
}

internal sealed class PortablePluginCompatibilityHost : IPluginCompatibilityHost
{
    public void Initialize()
    {
    }

    public Playnite.SDK.IResourceProvider InstallResourceProvider(
        Playnite.SDK.IResourceProvider provider) => null;

    public void RestoreResourceProvider(Playnite.SDK.IResourceProvider provider)
    {
    }

    public void LoadPluginResources(string extensionDirectory)
    {
    }

    public void LoadLegacyPlugins(
        ExtensionFactory extensions,
        List<string> ignoreList,
        List<string> externalExtensionDirectories)
    {
        // SDK v7 manifests were already claimed by V7PluginHost and are in the
        // ignore list. Passing the remaining compiled extensions through Core's
        // version gate records its precise SDK incompatibility failure for v6
        // plugins without constructing any plugin types.
        extensions.LoadPlugins(ignoreList, false, externalExtensionDirectories);
    }

    public IValueConverter ResolveConverter(
        ExtensionFactory extensions,
        string pluginSource,
        string converterName) => null;

    public Control CreateElement(
        ExtensionFactory extensions,
        Playnite.SDK.ApplicationMode mode,
        string pluginSource,
        string elementName,
        object gameContext) => null;

    public void Shutdown()
    {
    }
}
#endif
