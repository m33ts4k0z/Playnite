using Avalonia;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IResourceProvider
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    string GetString(string key);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    object GetResource(string key);
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public sealed class ResourceProvider : IResourceProvider
{
    private static IResourceProvider staticProvider;

    string IResourceProvider.GetString(string key) => GetString(key);
    object IResourceProvider.GetResource(string key) => GetResource(key);

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public static string GetString(string key)
    {
        var resource = GetResource(key);
        return resource as string ?? $"<!{key}!>";
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public static object GetResource(string key)
    {
        if (staticProvider != null)
        {
            return staticProvider.GetResource(key);
        }

        return Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true
            ? value
            : null;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public static T GetResource<T>(string key) => (T)GetResource(key);

    internal static IResourceProvider SetGlobalProvider(IResourceProvider provider)
    {
        var previous = staticProvider;
        staticProvider = provider;
        return previous;
    }
}
