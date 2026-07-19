#if !WINDOWS
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Playnite.Common
{
    public partial class Programs
    {
        private static readonly string[] portableExecutableExtensions =
        {
            ".AppImage", ".appimage", ".sh", ".desktop", ".exe", ".bat"
        };

        public static Task<List<Program>> GetExecutablesFromFolder(string path, SearchOption searchOption, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                var executables = new List<Program>();
                var files = new SafeFileEnumerator(path, "*.*", searchOption);
                foreach (var file in files)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (file.Attributes.HasFlag(FileAttributes.Directory) ||
                        IsFileScanExcluded(file.Name) ||
                        !IsPortableExecutable(file))
                    {
                        continue;
                    }

                    executables.Add(GetProgramData(file.FullName));
                }

                return executables;
            });
        }

        private static bool IsPortableExecutable(FileSystemInfo file)
        {
            if (portableExecutableExtensions.Any(extension =>
                file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
            {
                return false;
            }

            try
            {
                var mode = File.GetUnixFileMode(file.FullName);
                const UnixFileMode executableBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                return (mode & executableBits) != 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static Program GetProgramData(string filePath)
        {
            var file = new FileInfo(filePath);
            if (file.Extension.Equals(".desktop", StringComparison.OrdinalIgnoreCase))
            {
                return ReadDesktopEntry(file.FullName);
            }

            if (!IsPortableExecutable(file))
            {
                throw new NotSupportedException("The file is not an executable or desktop entry.");
            }

            return new Program
            {
                Path = file.FullName,
                Icon = file.FullName,
                WorkDir = file.DirectoryName,
                Name = Path.GetFileNameWithoutExtension(file.Name),
                AppId = filePath.MD5()
            };
        }

        public static void CreateShortcut(string executablePath, string arguments, string iconPath, string shortcutPath)
        {
            FileSystem.PrepareSaveFile(shortcutPath);
            var builder = new StringBuilder();
            builder.AppendLine("[Desktop Entry]");
            builder.AppendLine("Type=Application");
            builder.AppendLine("Name=" + Path.GetFileNameWithoutExtension(shortcutPath));
            builder.Append("Exec=").Append(QuoteDesktopArgument(executablePath));
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                builder.Append(' ').Append(arguments);
            }

            builder.AppendLine();
            var workingDirectory = Path.GetDirectoryName(executablePath);
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                builder.AppendLine("Path=" + workingDirectory);
            }
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                builder.AppendLine("Icon=" + iconPath);
            }

            builder.AppendLine("Terminal=false");
            File.WriteAllText(shortcutPath, builder.ToString());
        }

        private static string QuoteDesktopArgument(string value)
        {
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("`", "\\`")
                .Replace("$", "\\$") + "\"";
        }

        public static Program GetLnkShortcutData(string shortcutPath)
        {
            if (!Path.GetExtension(shortcutPath).Equals(".desktop", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException("Windows .lnk shortcuts cannot be read on this platform.");
            }

            return ReadDesktopEntry(shortcutPath);
        }

        private static Program ReadDesktopEntry(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var inDesktopEntry = false;
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    inDesktopEntry = trimmed.Equals("[Desktop Entry]", StringComparison.Ordinal);
                    continue;
                }

                if (!inDesktopEntry || trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator > 0)
                {
                    values[trimmed.Substring(0, separator)] = trimmed.Substring(separator + 1);
                }
            }

            if ((values.TryGetValue("Type", out var entryType) && !entryType.Equals("Application", StringComparison.Ordinal)) ||
                (values.TryGetValue("Hidden", out var hidden) && hidden.Equals("true", StringComparison.OrdinalIgnoreCase)) ||
                (values.TryGetValue("NoDisplay", out var noDisplay) && noDisplay.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("The desktop entry is not a visible application.");
            }

            values.TryGetValue("Name", out var defaultName);
            var name = GetLocalizedDesktopValue(values, "Name") ?? defaultName;
            values.TryGetValue("Icon", out var icon);
            values.TryGetValue("Exec", out var exec);
            var command = ExpandDesktopCommand(SplitDesktopCommand(exec), name, icon, path);
            if (command.Count == 0)
            {
                throw new InvalidDataException("The desktop entry has no executable command.");
            }

            values.TryGetValue("Path", out var workDir);
            var appId = GetDesktopApplicationId(values, path);
            return new Program
            {
                Path = command.FirstOrDefault(),
                Arguments = string.Join(" ", command.Skip(1).Select(QuoteProcessArgumentIfNeeded)),
                Icon = icon,
                WorkDir = workDir,
                Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name,
                AppId = appId
            };
        }

        private static string GetLocalizedDesktopValue(IReadOnlyDictionary<string, string> values, string key)
        {
            var culture = CultureInfo.CurrentUICulture;
            while (!string.IsNullOrEmpty(culture.Name))
            {
                var locale = culture.Name.Replace('-', '_');
                if (values.TryGetValue($"{key}[{locale}]", out var localized))
                {
                    return localized;
                }

                culture = culture.Parent;
            }

            return values.TryGetValue(key, out var fallback) ? fallback : null;
        }

        private static string GetDesktopApplicationId(IReadOnlyDictionary<string, string> values, string path)
        {
            if (values.TryGetValue("X-Flatpak", out var flatpakId) && !string.IsNullOrWhiteSpace(flatpakId))
            {
                return "flatpak:" + flatpakId.Trim();
            }

            if (values.TryGetValue("X-SnapInstanceName", out var snapInstance) &&
                !string.IsNullOrWhiteSpace(snapInstance))
            {
                values.TryGetValue("X-SnapAppName", out var snapApplication);
                return "snap:" + snapInstance.Trim() +
                    (string.IsNullOrWhiteSpace(snapApplication) ? string.Empty : "." + snapApplication.Trim());
            }

            if (values.TryGetValue("Exec", out var exec))
            {
                const string steamLaunchMarker = "steam://rungameid/";
                var markerIndex = exec.IndexOf(steamLaunchMarker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    var appId = new string(exec
                        .Skip(markerIndex + steamLaunchMarker.Length)
                        .TakeWhile(character => character >= '0' && character <= '9')
                        .ToArray());
                    if (appId.Length > 0)
                    {
                        return "steam:" + appId;
                    }
                }
            }

            return path.MD5();
        }

        private static List<string> ExpandDesktopCommand(
            IReadOnlyList<string> command,
            string applicationName,
            string icon,
            string desktopFile)
        {
            var expanded = new List<string>();
            foreach (var argument in command)
            {
                if (argument == "%i")
                {
                    if (!string.IsNullOrWhiteSpace(icon))
                    {
                        expanded.Add("--icon");
                        expanded.Add(icon);
                    }

                    continue;
                }

                var value = new StringBuilder();
                for (var index = 0; index < argument.Length; index++)
                {
                    if (argument[index] != '%' || index + 1 >= argument.Length)
                    {
                        value.Append(argument[index]);
                        continue;
                    }

                    var code = argument[++index];
                    switch (code)
                    {
                        case '%':
                            value.Append('%');
                            break;
                        case 'c':
                            value.Append(applicationName);
                            break;
                        case 'k':
                            value.Append(desktopFile);
                            break;
                        case 'f':
                        case 'F':
                        case 'u':
                        case 'U':
                            break;
                        default:
                            throw new InvalidDataException($"The desktop entry contains unsupported field code %{code}.");
                    }
                }

                if (value.Length > 0)
                {
                    expanded.Add(value.ToString());
                }
            }

            return expanded;
        }

        private static List<string> SplitDesktopCommand(string command)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(command))
            {
                return result;
            }

            var current = new StringBuilder();
            var quoted = false;
            var escaped = false;
            foreach (var character in command)
            {
                if (escaped)
                {
                    current.Append(character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    quoted = !quoted;
                }
                else if (char.IsWhiteSpace(character) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(character);
                }
            }

            if (escaped)
            {
                current.Append('\\');
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        private static string QuoteProcessArgumentIfNeeded(string argument)
        {
            if (!argument.Any(char.IsWhiteSpace) && argument.IndexOf('"') < 0)
            {
                return argument;
            }

            return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public static Task<List<Program>> GetShortcutProgramsFromFolder(string path, CancellationTokenSource cancelToken = null)
        {
            return Task.Run(() =>
            {
                var applications = new List<Program>();
                if (!Directory.Exists(path))
                {
                    return applications;
                }

                var shortcuts = new SafeFileEnumerator(path, "*.desktop", SearchOption.AllDirectories);
                foreach (var shortcut in shortcuts)
                {
                    if (cancelToken?.IsCancellationRequested == true)
                    {
                        return null;
                    }

                    if (shortcut.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        continue;
                    }

                    try
                    {
                        var application = ReadDesktopEntry(shortcut.FullName);
                        if (!string.IsNullOrEmpty(application.Path) &&
                            applications.All(existing => !string.Equals(
                                GetProgramIdentity(existing),
                                GetProgramIdentity(application),
                                StringComparison.Ordinal)))
                        {
                            applications.Add(application);
                        }
                    }
                    catch (InvalidDataException exception)
                    {
                        logger.Debug(exception, $"Skipped unsupported desktop entry {shortcut.FullName}.");
                    }
                    catch (IOException exception)
                    {
                        logger.Warn(exception, $"Failed to read desktop entry {shortcut.FullName}.");
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        logger.Warn(exception, $"Failed to read desktop entry {shortcut.FullName}.");
                    }
                }

                return applications;
            });
        }

        public static async Task<List<Program>> GetInstalledPrograms(CancellationToken cancelToken)
        {
            var applications = new List<Program>();
            var roots = GetLinuxApplicationRoots();

            using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
            {
                foreach (var root in roots)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    var discovered = await GetShortcutProgramsFromFolder(root, cancellationSource).ConfigureAwait(false);
                    if (discovered == null)
                    {
                        return null;
                    }

                    applications.AddRange(discovered.Where(application => applications.All(existing =>
                        !string.Equals(
                            GetProgramIdentity(existing),
                            GetProgramIdentity(application),
                            StringComparison.Ordinal))));
                }
            }

            if (OperatingSystem.IsLinux())
            {
                foreach (var steamGame in LinuxSteam.GetInstalledGames())
                {
                    var application = steamGame.ToProgram();
                    if (applications.All(existing => !string.Equals(
                        GetProgramIdentity(existing),
                        GetProgramIdentity(application),
                        StringComparison.Ordinal)))
                    {
                        applications.Add(application);
                    }
                }
            }

            return applications;
        }

        private static IReadOnlyList<string> GetLinuxApplicationRoots()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome))
            {
                dataHome = Path.Combine(userProfile, ".local", "share");
            }

            var dataDirectories = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            if (string.IsNullOrWhiteSpace(dataDirectories))
            {
                dataDirectories = "/usr/local/share:/usr/share";
            }

            var roots = new List<string>
            {
                Path.Combine(dataHome, "applications"),
                Path.Combine(dataHome, "flatpak", "exports", "share", "applications"),
                "/var/lib/flatpak/exports/share/applications",
                "/var/lib/snapd/desktop/applications"
            };
            roots.AddRange(dataDirectories
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => Path.Combine(directory, "applications")));
            return roots.Distinct(StringComparer.Ordinal).ToList();
        }

        private static string GetProgramIdentity(Program program)
        {
            if (!string.IsNullOrWhiteSpace(program.AppId))
            {
                return program.AppId;
            }

            return (program.Path ?? string.Empty) + "\0" + (program.Arguments ?? string.Empty);
        }

        public static List<Program> GetUWPApps()
        {
            return new List<Program>();
        }
    }
}
#endif
