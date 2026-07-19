using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.Database;

namespace Playnite.Avalonia.App.Services;

// Resolves the same user profile the WPF applications use so one
// configuration carries across the shell switch.
public static class CanonicalProfile
{
    // Portable-aware: Playnite.Core computes the config root from the portable
    // marker next to the executable, falling back to %AppData%\Playnite.
    public static string ResolveDefaultUserDataDirectory() => PlaynitePaths.ConfigRootPath;

    public static string ResolveDefaultLibraryPath(string userDataDirectory)
    {
        // The WPF applications store the library location as DatabasePath in
        // config.json, with {PlayniteDir} and %AppData% placeholders.
        try
        {
            var configPath = Path.Combine(userDataDirectory, "config.json");
            if (File.Exists(configPath))
            {
                var databasePath = JObject.Parse(File.ReadAllText(configPath)).Value<string>("DatabasePath");
                var resolved = GameDatabase.GetFullDbPath(databasePath);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // An unreadable config must not block startup; the shell falls back
            // to the profile-local library like before.
        }

        return Path.Combine(userDataDirectory, "library");
    }
}
