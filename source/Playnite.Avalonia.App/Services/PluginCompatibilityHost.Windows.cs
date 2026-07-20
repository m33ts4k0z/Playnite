#if WINDOWS
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Playnite.Plugins;
using Playnite.WpfPluginSupport;

namespace Playnite.Avalonia.App.Services;

internal static class PluginCompatibilityHost
{
    public static IPluginCompatibilityHost Create() => new WindowsPluginCompatibilityHost();
}

internal sealed class WindowsPluginCompatibilityHost : IPluginCompatibilityHost
{
    public void Initialize() => WpfPluginSupportRuntime.EnsureApplication();

    public Playnite.SDK.IResourceProvider InstallResourceProvider(
        Playnite.SDK.IResourceProvider provider) =>
        Playnite.SDK.ResourceProvider.SetGlobalProvider(provider);

    public void RestoreResourceProvider(Playnite.SDK.IResourceProvider provider) =>
        Playnite.SDK.ResourceProvider.SetGlobalProvider(provider);

    public void LoadPluginResources(string extensionDirectory) =>
        WpfPluginSupportRuntime.LoadPluginResources(extensionDirectory);

    public void LoadLegacyPlugins(
        ExtensionFactory extensions,
        List<string> ignoreList,
        List<string> externalExtensionDirectories) =>
        extensions.LoadPlugins(ignoreList, false, externalExtensionDirectories);

    public IValueConverter ResolveConverter(
        ExtensionFactory extensions,
        string pluginSource,
        string converterName) =>
        WpfPluginSupportRuntime.ResolveConverter(extensions, pluginSource, converterName);

    public Control CreateElement(
        ExtensionFactory extensions,
        Playnite.SDK.ApplicationMode mode,
        string pluginSource,
        string elementName,
        object gameContext) =>
        WpfPluginElementFactory.Create(
            extensions,
            mode,
            pluginSource,
            elementName,
            gameContext);

    public void Shutdown() => WpfPluginSupportRuntime.Shutdown();
}
#endif
