using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Playnite.Avalonia.App.Services;

// Reads the WPF applications' persisted configuration so the Avalonia shells can
// carry a user's language, main-window placement and fullscreen monitor across
// on first run — before the shell has written its own settings file. Every read
// is best-effort: an absent, unreadable or malformed source leaves the matching
// value null and the shell keeps its own default. Lives beside CanonicalProfile
// because both bridge the WPF profile into the Avalonia shells.
public static class WpfProfileImport
{
    // The WPF desktop main window persists under this key in windowPositions.json
    // (WindowBase.savePositionName for Playnite.DesktopApp MainWindow).
    private const string DesktopMainWindowKey = "Main_V2";

    public sealed class WindowPlacement
    {
        public int? X { get; init; }
        public int? Y { get; init; }
        public double? Width { get; init; }
        public double? Height { get; init; }
        public bool Maximized { get; init; }
    }

    public sealed class Defaults
    {
        public string Language { get; init; }
        public int? FullscreenMonitor { get; init; }
        public WindowPlacement MainWindow { get; init; }
    }

    public static Defaults Read(string userDataDirectory)
    {
        return new Defaults
        {
            Language = ReadLanguage(userDataDirectory),
            FullscreenMonitor = ReadFullscreenMonitor(userDataDirectory),
            MainWindow = ReadMainWindowPlacement(userDataDirectory)
        };
    }

    private static string ReadLanguage(string userDataDirectory) =>
        ReadJson<string>(Path.Combine(userDataDirectory, "config.json"), root =>
        {
            var language = root.Value<string>("Language");
            return string.IsNullOrWhiteSpace(language) ? null : language;
        });

    private static int? ReadFullscreenMonitor(string userDataDirectory) =>
        ReadJson<int?>(Path.Combine(userDataDirectory, "fullscreenConfig.json"), root =>
        {
            var token = root["Monitor"];
            return token != null && token.Type == JTokenType.Integer && token.Value<int>() >= 0
                ? token.Value<int>()
                : null;
        });

    private static WindowPlacement ReadMainWindowPlacement(string userDataDirectory) =>
        ReadJson<WindowPlacement>(Path.Combine(userDataDirectory, "windowPositions.json"), root =>
        {
            var entry = root["Positions"]?[DesktopMainWindowKey];
            if (entry == null)
            {
                return null;
            }

            var position = entry["Position"];
            var size = entry["Size"];
            return new WindowPlacement
            {
                X = ReadInt(position?["X"]),
                Y = ReadInt(position?["Y"]),
                Width = ReadDouble(size?["X"]),
                Height = ReadDouble(size?["Y"]),
                Maximized = IsMaximized(entry["State"])
            };
        });

    private static int? ReadInt(JToken token) =>
        token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            ? (int)Math.Round(token.Value<double>())
            : null;

    private static double? ReadDouble(JToken token) =>
        token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            ? token.Value<double>()
            : null;

    private static bool IsMaximized(JToken state)
    {
        if (state == null)
        {
            return false;
        }

        // System.Windows.WindowState is persisted as Normal=0, Minimized=1,
        // Maximized=2, and may serialize as the integer or the enum name.
        if (state.Type == JTokenType.Integer)
        {
            return state.Value<int>() == 2;
        }

        return string.Equals(state.Value<string>(), "Maximized", StringComparison.OrdinalIgnoreCase);
    }

    private static T ReadJson<T>(string path, Func<JObject, T> read)
    {
        try
        {
            if (File.Exists(path))
            {
                return read(JObject.Parse(File.ReadAllText(path)));
            }
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or locked WPF settings file must never block startup;
            // the shell falls back to its own defaults.
        }

        return default;
    }
}
