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
        public bool? DisableHwAcceleration { get; init; }
        public bool? AsyncImageLoading { get; init; }
        public bool? ShowImagePerformanceWarning { get; init; }
        public int? FullscreenMonitor { get; init; }
        public int? FullscreenInterfaceVolume { get; init; }
        public int? FullscreenBackgroundVolume { get; init; }
        public bool? FullscreenMuteInBackground { get; init; }
        public bool? FullscreenUsePrimaryDisplay { get; init; }
        public bool? FullscreenShowClock { get; init; }
        public bool? FullscreenShowBattery { get; init; }
        public bool? FullscreenShowBatteryPercentage { get; init; }
        public bool? FullscreenMinimizeAfterGameStartup { get; init; }
        public string FullscreenTheme { get; init; }
        public int? FullscreenRows { get; init; }
        public int? FullscreenColumns { get; init; }
        public bool? FullscreenHorizontalLayout { get; init; }
        public int? FullscreenItemSpacing { get; init; }
        public bool? FullscreenSmoothScrolling { get; init; }
        public bool? FullscreenDarkenUninstalledGamesGrid { get; init; }
        public bool? FullscreenEnableMainBackgroundImage { get; init; }
        public int? FullscreenMainBackgroundImageBlurAmount { get; init; }
        public double? FullscreenMainBackgroundImageDarkAmount { get; init; }
        public bool? FullscreenShowGameTitles { get; init; }
        public double? FullscreenFontSize { get; init; }
        public double? FullscreenFontSizeSmall { get; init; }
        public int? FullscreenButtonPrompts { get; init; }
        public bool? FullscreenMainMenuShowRestart { get; init; }
        public bool? FullscreenMainMenuShowShutdown { get; init; }
        public bool? FullscreenMainMenuShowSuspend { get; init; }
        public bool? FullscreenMainMenuShowHibernate { get; init; }
        public bool? FullscreenMainMenuShowMinimize { get; init; }
        public bool? FullscreenMainMenuShowLogout { get; init; }
        public bool? FullscreenMainMenuShowLock { get; init; }
        public bool? FullscreenMainMenuShowTools { get; init; }
        public bool? FullscreenMainMenuShowExtensions { get; init; }
        public bool? FullscreenMainMenuShowClients { get; init; }
        public WindowPlacement MainWindow { get; init; }
    }

    public static Defaults Read(string userDataDirectory)
    {
        return new Defaults
        {
            Language = ReadLanguage(userDataDirectory),
            DisableHwAcceleration = ReadDesktopBoolean(userDataDirectory, "DisableHwAcceleration"),
            AsyncImageLoading = ReadDesktopBoolean(userDataDirectory, "AsyncImageLoading"),
            ShowImagePerformanceWarning = ReadDesktopBoolean(userDataDirectory, "ShowImagePerformanceWarning"),
            FullscreenMonitor = ReadFullscreenInt(userDataDirectory, "Monitor", nonNegative: true),
            FullscreenInterfaceVolume = ReadFullscreenVolume(userDataDirectory, "InterfaceVolume"),
            FullscreenBackgroundVolume = ReadFullscreenVolume(userDataDirectory, "BackgroundVolume"),
            FullscreenMuteInBackground = ReadFullscreenBoolean(userDataDirectory, "MuteInBackground"),
            FullscreenUsePrimaryDisplay = ReadFullscreenBoolean(userDataDirectory, "UsePrimaryDisplay"),
            FullscreenShowClock = ReadFullscreenBoolean(userDataDirectory, "ShowClock"),
            FullscreenShowBattery = ReadFullscreenBoolean(userDataDirectory, "ShowBattery"),
            FullscreenShowBatteryPercentage = ReadFullscreenBoolean(userDataDirectory, "ShowBatteryPercentage"),
            FullscreenMinimizeAfterGameStartup = ReadFullscreenBoolean(userDataDirectory, "MinimizeAfterGameStartup"),
            FullscreenTheme = ReadFullscreenString(userDataDirectory, "Theme"),
            FullscreenRows = ReadFullscreenInt(userDataDirectory, "Rows"),
            FullscreenColumns = ReadFullscreenInt(userDataDirectory, "Columns"),
            FullscreenHorizontalLayout = ReadFullscreenBoolean(userDataDirectory, "HorizontalLayout"),
            FullscreenItemSpacing = ReadDesktopInt(userDataDirectory, "FullscreenItemSpacing"),
            FullscreenSmoothScrolling = ReadFullscreenBoolean(userDataDirectory, "SmoothScrolling"),
            FullscreenDarkenUninstalledGamesGrid = ReadFullscreenBoolean(userDataDirectory, "DarkenUninstalledGamesGrid"),
            FullscreenEnableMainBackgroundImage = ReadFullscreenBoolean(userDataDirectory, "EnableMainBackgroundImage"),
            FullscreenMainBackgroundImageBlurAmount = ReadFullscreenInt(userDataDirectory, "MainBackgroundImageBlurAmount"),
            FullscreenMainBackgroundImageDarkAmount = ReadFullscreenDouble(userDataDirectory, "MainBackgroundImageDarkAmount"),
            FullscreenShowGameTitles = ReadFullscreenBoolean(userDataDirectory, "ShowGameTitles"),
            FullscreenFontSize = ReadFullscreenDouble(userDataDirectory, "FontSize"),
            FullscreenFontSizeSmall = ReadFullscreenDouble(userDataDirectory, "FontSizeSmall"),
            FullscreenButtonPrompts = ReadFullscreenInt(userDataDirectory, "ButtonPrompts"),
            FullscreenMainMenuShowRestart = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowRestart"),
            FullscreenMainMenuShowShutdown = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowShutdown"),
            FullscreenMainMenuShowSuspend = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowSuspend"),
            FullscreenMainMenuShowHibernate = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowHibernate"),
            FullscreenMainMenuShowMinimize = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowMinimize"),
            FullscreenMainMenuShowLogout = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowLogout"),
            FullscreenMainMenuShowLock = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowLock"),
            FullscreenMainMenuShowTools = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowTools"),
            FullscreenMainMenuShowExtensions = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowExtensions"),
            FullscreenMainMenuShowClients = ReadFullscreenBoolean(userDataDirectory, "MainMenuShowClients"),
            MainWindow = ReadMainWindowPlacement(userDataDirectory)
        };
    }

    private static string ReadLanguage(string userDataDirectory) =>
        ReadJson<string>(Path.Combine(userDataDirectory, "config.json"), root =>
        {
            var language = root.Value<string>("Language");
            return string.IsNullOrWhiteSpace(language) ? null : language;
        });

    private static bool? ReadDesktopBoolean(string userDataDirectory, string propertyName) =>
        ReadJson<bool?>(Path.Combine(userDataDirectory, "config.json"), root =>
        {
            var token = root[propertyName];
            return token?.Type == JTokenType.Boolean ? token.Value<bool>() : null;
        });

    private static int? ReadDesktopInt(string userDataDirectory, string propertyName) =>
        ReadJson<int?>(Path.Combine(userDataDirectory, "config.json"), root =>
        {
            var token = root[propertyName];
            return token?.Type == JTokenType.Integer ? token.Value<int>() : null;
        });

    private static int? ReadFullscreenInt(string userDataDirectory, string propertyName, bool nonNegative = false) =>
        ReadJson<int?>(Path.Combine(userDataDirectory, "fullscreenConfig.json"), root =>
        {
            var token = root[propertyName];
            if (token == null || token.Type != JTokenType.Integer)
            {
                return null;
            }

            var value = token.Value<int>();
            return nonNegative && value < 0 ? null : value;
        });

    private static bool? ReadFullscreenBoolean(string userDataDirectory, string propertyName) =>
        ReadJson<bool?>(Path.Combine(userDataDirectory, "fullscreenConfig.json"), root =>
        {
            var token = root[propertyName];
            return token?.Type == JTokenType.Boolean ? token.Value<bool>() : null;
        });

    private static string ReadFullscreenString(string userDataDirectory, string propertyName) =>
        ReadJson<string>(Path.Combine(userDataDirectory, "fullscreenConfig.json"), root =>
        {
            var value = root.Value<string>(propertyName);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        });

    private static double? ReadFullscreenDouble(string userDataDirectory, string propertyName) =>
        ReadJson<double?>(Path.Combine(userDataDirectory, "fullscreenConfig.json"), root =>
        {
            var token = root[propertyName];
            return token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                ? token.Value<double>()
                : null;
        });

    private static int? ReadFullscreenVolume(string userDataDirectory, string propertyName) =>
        ReadJson<int?>(Path.Combine(userDataDirectory, "fullscreenConfig.json"), root =>
        {
            var token = root[propertyName];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                return null;
            }

            var value = token.Value<double>();
            // WPF persists these values as 0..1 floats. Also accept an integer
            // percentage so profiles written by preview builds remain usable.
            return Math.Clamp((int)Math.Round(value <= 1 ? value * 100 : value), 0, 100);
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
