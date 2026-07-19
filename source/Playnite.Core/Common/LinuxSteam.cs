#if !WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Playnite.SDK;

namespace Playnite.Common
{
    public enum LinuxSteamDistribution
    {
        Native,
        Flatpak,
        Snap
    }

    public sealed class LinuxSteamGame
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string InstallDirectory { get; set; }
        public string ManifestPath { get; set; }
        public string LibraryPath { get; set; }
        public string CompatDataPath { get; set; }
        public LinuxSteamDistribution Distribution { get; set; }
        public bool UsesProton => Directory.Exists(CompatDataPath);

        public Program ToProgram()
        {
            var program = new Program
            {
                Name = Name,
                AppId = "steam:" + AppId,
                WorkDir = InstallDirectory
            };
            switch (Distribution)
            {
                case LinuxSteamDistribution.Flatpak:
                    program.Path = "flatpak";
                    program.Arguments = $"run com.valvesoftware.Steam steam://rungameid/{AppId}";
                    break;
                case LinuxSteamDistribution.Snap:
                    program.Path = "snap";
                    program.Arguments = $"run steam steam://rungameid/{AppId}";
                    break;
                default:
                    program.Path = "steam";
                    program.Arguments = $"steam://rungameid/{AppId}";
                    break;
            }

            return program;
        }
    }

    public static class LinuxSteam
    {
        public static IReadOnlyList<string> GetSteamRoots()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new List<string>();
            var configuredRoot = Environment.GetEnvironmentVariable("STEAM_DIR");
            if (!string.IsNullOrWhiteSpace(configuredRoot))
            {
                roots.Add(configuredRoot);
            }

            roots.Add(Path.Combine(home, ".local", "share", "Steam"));
            roots.Add(Path.Combine(home, ".steam", "steam"));
            roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"));
            roots.Add(Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam"));
            return roots
                .Select(Path.GetFullPath)
                .Where(Directory.Exists)
                .Distinct(GetPathComparer())
                .ToList();
        }

        public static IReadOnlyList<string> GetLibraryPaths(string steamRoot)
        {
            if (string.IsNullOrWhiteSpace(steamRoot))
            {
                throw new ArgumentException("A Steam root is required.", nameof(steamRoot));
            }

            var normalizedRoot = Path.GetFullPath(steamRoot);
            var paths = new List<string> { normalizedRoot };
            foreach (var libraryFile in new[]
            {
                Path.Combine(normalizedRoot, "steamapps", "libraryfolders.vdf"),
                Path.Combine(normalizedRoot, "config", "libraryfolders.vdf")
            })
            {
                if (!File.Exists(libraryFile))
                {
                    continue;
                }

                var document = VdfDocument.Parse(File.ReadAllText(libraryFile));
                foreach (var path in document.FindValues("path"))
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(Path.GetFullPath(path));
                    }
                }

                foreach (var pair in document.FindObject("libraryfolders"))
                {
                    if (IsNumericKey(pair.Key) && pair.Value.Value != null)
                    {
                        paths.Add(Path.GetFullPath(pair.Value.Value));
                    }
                }
            }

            return paths
                .Where(path => Directory.Exists(Path.Combine(path, "steamapps")))
                .Distinct(GetPathComparer())
                .ToList();
        }

        public static IReadOnlyList<LinuxSteamGame> GetInstalledGames()
        {
            return GetSteamRoots().SelectMany(GetInstalledGames).GroupBy(
                game => game.AppId,
                StringComparer.Ordinal).Select(group => group.First()).ToList();
        }

        public static IReadOnlyList<LinuxSteamGame> GetInstalledGames(string steamRoot)
        {
            var distribution = GetDistribution(steamRoot);
            var games = new List<LinuxSteamGame>();
            foreach (var libraryPath in GetLibraryPaths(steamRoot))
            {
                var steamAppsPath = Path.Combine(libraryPath, "steamapps");
                foreach (var manifestPath in Directory.EnumerateFiles(
                    steamAppsPath,
                    "appmanifest_*.acf",
                    SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var document = VdfDocument.Parse(File.ReadAllText(manifestPath));
                        var appId = document.FindValue("appid");
                        var name = document.FindValue("name");
                        var installDirectoryName = document.FindValue("installdir");
                        if (string.IsNullOrWhiteSpace(appId) ||
                            string.IsNullOrWhiteSpace(name) ||
                            string.IsNullOrWhiteSpace(installDirectoryName))
                        {
                            continue;
                        }

                        games.Add(new LinuxSteamGame
                        {
                            AppId = appId,
                            Name = name,
                            LibraryPath = libraryPath,
                            ManifestPath = manifestPath,
                            InstallDirectory = Path.Combine(steamAppsPath, "common", installDirectoryName),
                            CompatDataPath = Path.Combine(steamAppsPath, "compatdata", appId),
                            Distribution = distribution
                        });
                    }
                    catch (InvalidDataException exception)
                    {
                        LogManager.GetLogger().Warn(exception, $"Skipped invalid Steam manifest {manifestPath}.");
                    }
                    catch (IOException exception)
                    {
                        LogManager.GetLogger().Warn(exception, $"Failed to read Steam manifest {manifestPath}.");
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        LogManager.GetLogger().Warn(exception, $"Failed to read Steam manifest {manifestPath}.");
                    }
                }
            }

            return games.GroupBy(game => game.AppId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static LinuxSteamDistribution GetDistribution(string steamRoot)
        {
            var normalized = Path.GetFullPath(steamRoot);
            if (normalized.IndexOf(
                Path.Combine(".var", "app", "com.valvesoftware.Steam"),
                StringComparison.Ordinal) >= 0)
            {
                return LinuxSteamDistribution.Flatpak;
            }

            if (normalized.IndexOf(Path.Combine("snap", "steam"), StringComparison.Ordinal) >= 0)
            {
                return LinuxSteamDistribution.Snap;
            }

            return LinuxSteamDistribution.Native;
        }

        private static bool IsNumericKey(string value)
        {
            return value.All(character => character >= '0' && character <= '9');
        }

        private static StringComparer GetPathComparer()
        {
            return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        }

        private sealed class VdfDocument
        {
            public Dictionary<string, VdfValue> Root { get; } = new Dictionary<string, VdfValue>(StringComparer.OrdinalIgnoreCase);

            public static VdfDocument Parse(string content)
            {
                var tokens = Tokenize(content);
                var index = 0;
                var document = new VdfDocument();
                ParseObject(tokens, ref index, document.Root, false);
                if (index != tokens.Count)
                {
                    throw new InvalidDataException("The VDF document has unexpected trailing data.");
                }

                return document;
            }

            public string FindValue(string key)
            {
                return FindValues(key).FirstOrDefault();
            }

            public IEnumerable<string> FindValues(string key)
            {
                return FindValues(Root, key);
            }

            public IReadOnlyDictionary<string, VdfValue> FindObject(string key)
            {
                return Root.TryGetValue(key, out var value) && value.Children != null
                    ? value.Children
                    : new Dictionary<string, VdfValue>();
            }

            private static IEnumerable<string> FindValues(
                IReadOnlyDictionary<string, VdfValue> values,
                string key)
            {
                foreach (var pair in values)
                {
                    if (pair.Value.Value != null && pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return pair.Value.Value;
                    }

                    if (pair.Value.Children != null)
                    {
                        foreach (var nested in FindValues(pair.Value.Children, key))
                        {
                            yield return nested;
                        }
                    }
                }
            }

            private static void ParseObject(
                IReadOnlyList<string> tokens,
                ref int index,
                IDictionary<string, VdfValue> target,
                bool expectClosingBrace)
            {
                while (index < tokens.Count)
                {
                    if (tokens[index] == "}")
                    {
                        if (!expectClosingBrace)
                        {
                            throw new InvalidDataException("The VDF document contains an unmatched closing brace.");
                        }

                        index++;
                        return;
                    }

                    var key = tokens[index++];
                    if (index >= tokens.Count)
                    {
                        throw new InvalidDataException($"The VDF key '{key}' has no value.");
                    }

                    if (tokens[index] == "{")
                    {
                        index++;
                        var children = new Dictionary<string, VdfValue>(StringComparer.OrdinalIgnoreCase);
                        ParseObject(tokens, ref index, children, true);
                        target[key] = new VdfValue { Children = children };
                    }
                    else if (tokens[index] == "}")
                    {
                        throw new InvalidDataException($"The VDF key '{key}' has no value.");
                    }
                    else
                    {
                        target[key] = new VdfValue { Value = tokens[index++] };
                    }
                }

                if (expectClosingBrace)
                {
                    throw new InvalidDataException("The VDF document has an unterminated object.");
                }
            }

            private static List<string> Tokenize(string content)
            {
                var tokens = new List<string>();
                var index = 0;
                while (index < content.Length)
                {
                    while (index < content.Length && char.IsWhiteSpace(content[index]))
                    {
                        index++;
                    }

                    if (index >= content.Length)
                    {
                        break;
                    }

                    if (content[index] == '/' && index + 1 < content.Length && content[index + 1] == '/')
                    {
                        index += 2;
                        while (index < content.Length && content[index] != '\n')
                        {
                            index++;
                        }

                        continue;
                    }

                    if (content[index] == '{' || content[index] == '}')
                    {
                        tokens.Add(content[index++].ToString());
                        continue;
                    }

                    if (content[index] != '"')
                    {
                        throw new InvalidDataException($"Unexpected VDF character '{content[index]}'.");
                    }

                    index++;
                    var value = new StringBuilder();
                    var closed = false;
                    while (index < content.Length)
                    {
                        var character = content[index++];
                        if (character == '"')
                        {
                            closed = true;
                            break;
                        }

                        if (character == '\\' && index < content.Length)
                        {
                            var escaped = content[index++];
                            value.Append(escaped == 'n' ? '\n' : escaped == 't' ? '\t' : escaped);
                        }
                        else
                        {
                            value.Append(character);
                        }
                    }

                    if (!closed)
                    {
                        throw new InvalidDataException("The VDF document has an unterminated string.");
                    }

                    tokens.Add(value.ToString());
                }

                return tokens;
            }
        }

        private sealed class VdfValue
        {
            public string Value { get; set; }
            public Dictionary<string, VdfValue> Children { get; set; }
        }
    }
}
#endif
