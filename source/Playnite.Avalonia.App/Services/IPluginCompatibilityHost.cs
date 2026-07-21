using Avalonia.Controls;
using Avalonia.Data.Converters;
using Playnite.Plugins;

namespace Playnite.Avalonia.App.Services;

/// <summary>
/// Isolates the optional SDK v6/WPF compatibility layer from the portable host.
/// </summary>
internal interface IPluginCompatibilityHost
{
    void Initialize();

    Playnite.SDK.IResourceProvider InstallResourceProvider(
        Playnite.SDK.IResourceProvider provider);

    void RestoreResourceProvider(Playnite.SDK.IResourceProvider provider);

    void LoadPluginResources(string extensionDirectory, string language);

    void LoadLegacyPlugins(
        ExtensionFactory extensions,
        List<string> ignoreList,
        List<string> externalExtensionDirectories);

    IValueConverter ResolveConverter(
        ExtensionFactory extensions,
        string pluginSource,
        string converterName);

    Control CreateElement(
        ExtensionFactory extensions,
        Playnite.SDK.ApplicationMode mode,
        string pluginSource,
        string elementName,
        object gameContext);

    void Shutdown();
}
