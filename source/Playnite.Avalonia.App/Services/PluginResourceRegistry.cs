namespace Playnite.Avalonia.App.Services;

internal static class PluginResourceRegistry
{
    private static readonly object sync = new();
    private static Dictionary<string, string> resources = new(StringComparer.Ordinal);

    public static bool TryGet(string key, out string value)
    {
        lock (sync)
        {
            return resources.TryGetValue(key, out value);
        }
    }

    public static void Replace(IEnumerable<KeyValuePair<string, string>> values)
    {
        lock (sync)
        {
            resources = values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal);
        }
    }

    public static void Clear() => Replace([]);
}
