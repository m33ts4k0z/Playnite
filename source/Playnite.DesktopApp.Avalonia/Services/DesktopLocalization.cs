using System.Text.RegularExpressions;
using Avalonia;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.Services;

internal static partial class DesktopLocalization
{
    [GeneratedRegex("<!([A-Za-z0-9_.-]+)!>", RegexOptions.CultureInvariant)]
    private static partial Regex LocalizationTokenPattern();

    [GeneratedRegex("^LOC[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalizationKeyPattern();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundaryPattern();

    public static string Resolve(string key, string fallback)
        => ResolveCore(key, fallback, new HashSet<string>(StringComparer.Ordinal));

    public static string ResolveStored(string value)
        => ResolveStoredCore(value, new HashSet<string>(StringComparer.Ordinal));

    public static bool ContainsUnresolvedToken(string value) =>
        !string.IsNullOrEmpty(value) && LocalizationTokenPattern().IsMatch(value);

    private static string ResolveCore(string key, string fallback, HashSet<string> resolvingKeys)
    {
        var normalizedKey = UnwrapKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return fallback ?? string.Empty;
        }

        if (!resolvingKeys.Add(normalizedKey))
        {
            return fallback ?? HumanizeKey(normalizedKey);
        }

        try
        {
            if (TryGetApplicationString(normalizedKey, out var applicationValue) &&
                !IsUnresolved(applicationValue, normalizedKey))
            {
                return ResolveStoredCore(applicationValue, resolvingKeys);
            }

            var sdkValue = ResourceProvider.GetString(normalizedKey);
            return IsUnresolved(sdkValue, normalizedKey)
                ? fallback ?? HumanizeKey(normalizedKey)
                : ResolveStoredCore(sdkValue, resolvingKeys);
        }
        finally
        {
            resolvingKeys.Remove(normalizedKey);
        }
    }

    private static string ResolveStoredCore(string value, HashSet<string> resolvingKeys)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (LocalizationKeyPattern().IsMatch(value))
        {
            return ResolveCore(value, HumanizeKey(value), resolvingKeys);
        }

        return LocalizationTokenPattern().Replace(value, match =>
        {
            var key = match.Groups[1].Value;
            return ResolveCore(key, HumanizeKey(key), resolvingKeys);
        });
    }

    private static bool TryGetApplicationString(string key, out string value)
    {
        if (Application.Current?.TryGetResource(key, null, out var resource) == true &&
            resource is string text)
        {
            value = text;
            return true;
        }

        value = null;
        return false;
    }

    private static string UnwrapKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        var match = LocalizationTokenPattern().Match(key);
        return match.Success && match.Length == key.Length
            ? match.Groups[1].Value
            : key;
    }

    private static bool IsUnresolved(string value, string key) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, key, StringComparison.Ordinal) ||
        string.Equals(value, $"<!{key}!>", StringComparison.Ordinal);

    private static string HumanizeKey(string key)
    {
        var name = key?.StartsWith("LOC", StringComparison.Ordinal) == true
            ? key[3..]
            : key;
        return string.IsNullOrWhiteSpace(name)
            ? key ?? string.Empty
            : WordBoundaryPattern().Replace(name, " ");
    }
}
