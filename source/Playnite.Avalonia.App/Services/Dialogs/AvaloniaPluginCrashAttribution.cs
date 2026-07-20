using Playnite.Avalonia.Services;
using Playnite.Plugins;
using System.Diagnostics;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

public static class AvaloniaPluginCrashAttribution
{
    public static AvaloniaCrashAttribution Resolve(
        Exception exception,
        AvaloniaRuntimeHost host) => Resolve(
            exception,
            host?.Extensions,
            host?.V7Plugins);

    public static AvaloniaCrashAttribution Resolve(
        Exception exception,
        ExtensionFactory extensions,
        IReadOnlyList<V7LoadedPlugin> v7Plugins)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var legacyInfo = global::Playnite.Exceptions.GetExceptionInfo(exception, extensions);
        if (legacyInfo.CrashExtension != null)
        {
            return new AvaloniaCrashAttribution
            {
                IsAddonRelated = true,
                PluginId = legacyInfo.CrashExtension.Id,
                PluginName = legacyInfo.CrashExtension.Name
            };
        }

        var modules = GetExceptionModules(exception);
        foreach (var plugin in v7Plugins ?? Array.Empty<V7LoadedPlugin>())
        {
            var manifest = plugin?.Manifest;
            if (manifest == null)
            {
                continue;
            }

            var manifestModule = manifest.Module ?? string.Empty;
            var manifestDirectory = NormalizePath(manifest.DirectoryPath);
            if (modules.Any(module =>
                    string.Equals(module.Name, manifestModule, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(manifestDirectory) &&
                     string.Equals(
                         NormalizePath(Path.GetDirectoryName(module.Assembly.Location)),
                         manifestDirectory,
                         PathComparison))))
            {
                return new AvaloniaCrashAttribution
                {
                    IsAddonRelated = true,
                    PluginId = manifest.Id,
                    PluginName = manifest.Name
                };
            }
        }

        return new AvaloniaCrashAttribution
        {
            IsAddonRelated = legacyInfo.IsExtensionCrash
        };
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static IReadOnlyList<Module> GetExceptionModules(Exception exception)
    {
        var modules = new List<Module>();
        for (var current = exception; current != null; current = current.InnerException)
        {
            foreach (var frame in new StackTrace(current, false).GetFrames() ?? Array.Empty<StackFrame>())
            {
                var module = frame.GetMethod()?.Module;
                if (module != null && !modules.Contains(module))
                {
                    modules.Add(module);
                }
            }
        }
        return modules;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
