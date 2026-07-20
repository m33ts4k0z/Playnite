using NUnit.Framework;
using Playnite.Common;
using Playnite.Database;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Playnite.Core.Portable.Tests
{
    [TestFixture]
    public class PortableCoreTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "PlaynitePortableTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void PortableAssemblyDoesNotImportWindowsNativeLibraries()
        {
            var windowsLibraries = new[] { "kernel32", "user32", "shell32", "ntdll", "shlwapi", "advapi32", "wintrust" };
            var imports = typeof(GameDatabase).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .Select(method => method.GetCustomAttribute<DllImportAttribute>())
                .Where(attribute => attribute != null)
                .Select(attribute => attribute.Value)
                .ToList();

            Assert.That(imports.Where(library => windowsLibraries.Any(windowsLibrary =>
                library.IndexOf(windowsLibrary, StringComparison.OrdinalIgnoreCase) >= 0)), Is.Empty);
        }

        [Test]
        public void DesktopShortcutRoundTripsCommandData()
        {
            var shortcutPath = Path.Combine(temporaryDirectory, "sample.desktop");
            Programs.CreateShortcut("/opt/My Game/game", "--profile \"Player One\"", "/opt/My Game/icon.png", shortcutPath);

            var shortcut = Programs.GetLnkShortcutData(shortcutPath);

            Assert.That(shortcut.Path, Is.EqualTo("/opt/My Game/game"));
            Assert.That(shortcut.Arguments, Is.EqualTo("--profile \"Player One\""));
            Assert.That(shortcut.Icon, Is.EqualTo("/opt/My Game/icon.png"));
            Assert.That(shortcut.WorkDir, Is.EqualTo(Path.GetDirectoryName("/opt/My Game/game")));
        }

        [Test]
        public void DesktopDiscoverySkipsHiddenEntries()
        {
            File.WriteAllText(Path.Combine(temporaryDirectory, "visible.desktop"),
                "[Desktop Entry]\nType=Application\nName=Visible\nExec=/opt/visible\n");
            File.WriteAllText(Path.Combine(temporaryDirectory, "hidden.desktop"),
                "[Desktop Entry]\nType=Application\nName=Hidden\nExec=/opt/hidden\nNoDisplay=true\n");

            var programs = Programs.GetShortcutProgramsFromFolder(temporaryDirectory).GetAwaiter().GetResult();

            Assert.That(programs.Select(program => program.Name), Is.EqualTo(new[] { "Visible" }));
        }

        [Test]
        public void DesktopEntriesPreserveFlatpakAndSnapIdentity()
        {
            File.WriteAllText(Path.Combine(temporaryDirectory, "flatpak-one.desktop"),
                "[Desktop Entry]\nType=Application\nName=Flatpak One\n" +
                "Exec=/usr/bin/flatpak run com.example.One %U\nX-Flatpak=com.example.One\n");
            File.WriteAllText(Path.Combine(temporaryDirectory, "flatpak-two.desktop"),
                "[Desktop Entry]\nType=Application\nName=Flatpak Two\n" +
                "Exec=/usr/bin/flatpak run com.example.Two %U\nX-Flatpak=com.example.Two\n");
            File.WriteAllText(Path.Combine(temporaryDirectory, "snap.desktop"),
                "[Desktop Entry]\nType=Application\nName=Snap Game\n" +
                "Exec=/snap/bin/example.game %U\nX-SnapInstanceName=example\nX-SnapAppName=game\n");

            var programs = Programs.GetShortcutProgramsFromFolder(temporaryDirectory).GetAwaiter().GetResult();

            Assert.That(programs.Select(program => program.AppId), Is.EquivalentTo(new[]
            {
                "flatpak:com.example.One",
                "flatpak:com.example.Two",
                "snap:example.game"
            }));
            Assert.That(programs.Single(program => program.Name == "Flatpak One").Arguments,
                Is.EqualTo("run com.example.One"));
        }

        [Test]
        public void DesktopEntryExpandsStandardFieldCodes()
        {
            var shortcutPath = Path.Combine(temporaryDirectory, "field-codes.desktop");
            File.WriteAllText(shortcutPath,
                "[Desktop Entry]\nType=Application\nName=Field Code Game\nIcon=game-icon\n" +
                "Exec=/opt/game --title=%c --desktop=%k %i %% %f\n");

            var program = Programs.GetLnkShortcutData(shortcutPath);

            Assert.That(program.Arguments, Does.Contain("\"--title=Field Code Game\""));
            Assert.That(program.Arguments, Does.Contain("--desktop="));
            Assert.That(program.Arguments, Does.Contain("--icon game-icon"));
            Assert.That(program.Arguments, Does.EndWith("%"));
            Assert.That(program.Arguments, Does.Not.Contain("%f"));
        }

        [Test]
        public void LinuxInstalledDiscoveryHonorsXdgAndPackageExports()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Ignore("Linux-specific discovery roots.");
            }

            var previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var previousDataDirectories = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            try
            {
                var dataHome = Path.Combine(temporaryDirectory, "data-home");
                var dataDirectory = Path.Combine(temporaryDirectory, "data-dir");
                var flatpakDirectory = Path.Combine(dataHome, "flatpak", "exports", "share", "applications");
                var xdgDirectory = Path.Combine(dataDirectory, "applications");
                Directory.CreateDirectory(flatpakDirectory);
                Directory.CreateDirectory(xdgDirectory);
                File.WriteAllText(Path.Combine(flatpakDirectory, "flatpak.desktop"),
                    "[Desktop Entry]\nType=Application\nName=Flatpak Fixture\n" +
                    "Exec=/usr/bin/flatpak run com.example.Fixture\nX-Flatpak=com.example.Fixture\n");
                File.WriteAllText(Path.Combine(xdgDirectory, "xdg.desktop"),
                    "[Desktop Entry]\nType=Application\nName=XDG Fixture\nExec=/opt/xdg-fixture\n");
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataHome);
                Environment.SetEnvironmentVariable("XDG_DATA_DIRS", dataDirectory);

                var programs = Programs.GetInstalledPrograms(CancellationToken.None).GetAwaiter().GetResult();

                Assert.That(programs.Any(program => program.Name == "Flatpak Fixture"), Is.True);
                Assert.That(programs.Any(program => program.Name == "XDG Fixture"), Is.True);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousDataHome);
                Environment.SetEnvironmentVariable("XDG_DATA_DIRS", previousDataDirectories);
            }
        }

        [Test]
        public void LinuxSteamDiscoversLibrariesManifestsAndProtonData()
        {
            var steamRoot = Path.Combine(temporaryDirectory, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
            var secondLibrary = Path.Combine(temporaryDirectory, "Steam Library");
            Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
            Directory.CreateDirectory(Path.Combine(secondLibrary, "steamapps", "common", "Proton Game"));
            Directory.CreateDirectory(Path.Combine(secondLibrary, "steamapps", "compatdata", "12345"));
            File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
                "\"libraryfolders\"\n{\n  \"0\" { \"path\" \"" + EscapeVdf(steamRoot) + "\" }\n" +
                "  \"1\" { \"path\" \"" + EscapeVdf(secondLibrary) + "\" }\n}\n");
            File.WriteAllText(Path.Combine(secondLibrary, "steamapps", "appmanifest_12345.acf"),
                "\"AppState\"\n{\n  \"appid\" \"12345\"\n  \"name\" \"Proton Game\"\n" +
                "  \"installdir\" \"Proton Game\"\n}\n");

            var games = LinuxSteam.GetInstalledGames(steamRoot);

            Assert.That(games, Has.Count.EqualTo(1));
            var game = games.Single();
            Assert.That(game.Name, Is.EqualTo("Proton Game"));
            Assert.That(game.InstallDirectory, Is.EqualTo(Path.Combine(secondLibrary, "steamapps", "common", "Proton Game")));
            Assert.That(game.UsesProton, Is.True);
            Assert.That(game.Distribution, Is.EqualTo(LinuxSteamDistribution.Flatpak));
            Assert.That(game.ToProgram().Path, Is.EqualTo("flatpak"));
            Assert.That(game.ToProgram().Arguments,
                Is.EqualTo("run com.valvesoftware.Steam steam://rungameid/12345"));
        }

        [Test]
        public void SteamDesktopEntryUsesManifestIdentity()
        {
            var shortcutPath = Path.Combine(temporaryDirectory, "steam-12345.desktop");
            File.WriteAllText(shortcutPath,
                "[Desktop Entry]\nType=Application\nName=Steam Game\n" +
                "Exec=steam steam://rungameid/12345\n");

            var program = Programs.GetLnkShortcutData(shortcutPath);

            Assert.That(program.AppId, Is.EqualTo("steam:12345"));
        }

        [Test]
        public void PortablePatternMatchingSupportsMultipleGlobs()
        {
            Assert.That(Paths.MathcesFilePattern("game.CUE", "*.iso;*.cue"), Is.True);
            Assert.That(Paths.MathcesFilePattern("game.zip", "*.iso;*.cue"), Is.False);
            Assert.That(Paths.MathcesFilePattern("disc1.bin", "disc?.bin"), Is.True);
        }

        [Test]
        public void EmulatorScannerExclusionsFollowHostPathCaseRules()
        {
            var root = Path.Combine(temporaryDirectory, "roms");
            var configured = Path.Combine("System", "Game.rom");
            var differentlyCased = Path.Combine(root, "system", "game.rom");
            var exclusions = GameScanner.ParseExclusions(root, new System.Collections.Generic.List<string>
            {
                configured
            });

            var matches = GameScanner.GetFileExclusionMatches(
                new System.Collections.Generic.List<string> { differentlyCased },
                exclusions);

            Assert.That(matches.Count, Is.EqualTo(OperatingSystem.IsWindows() ? 1 : 0));
        }

        [Test]
        public void ScannedGameImportPathOnlyUsesSelectedRoms()
        {
            var selectedDirectory = Path.Combine(temporaryDirectory, "selected");
            var ignoredDirectory = Path.Combine(temporaryDirectory, "ignored");
            var selectedPath = Path.Combine(selectedDirectory, "game.rom");
            var ignoredPath = Path.Combine(ignoredDirectory, "bonus.rom");
            var scanned = new ScannedGame
            {
                Name = "Selected ROM game",
                SourceConfig = new GameScannerConfig
                {
                    EmulatorId = Guid.NewGuid(),
                    EmulatorProfileId = "custom-profile",
                    ImportWithRelativePaths = false
                },
                Roms = new System.Collections.ObjectModel.ObservableCollection<ScannedRom>
                {
                    new ScannedRom(selectedPath),
                    new ScannedRom(ignoredPath) { Import = false }
                }
            };

            var imported = scanned.ToGame();

            Assert.That(imported.InstallDirectory, Is.EqualTo(selectedDirectory.EndWithDirSeparator()));
            Assert.That(imported.Roms.Select(rom => rom.Path),
                Is.EqualTo(new[] { ExpandableVariables.InstallationDirectory.EndWithDirSeparator() + "game.rom" }));
        }

        [Test]
        public void CoreDatabasePersistsOnPortableRuntime()
        {
            var databasePath = Path.Combine(temporaryDirectory, "library");
            var gameId = Guid.NewGuid();
            using (var database = new GameDatabase(databasePath))
            {
                database.OpenDatabase();
                database.Games.Add(new Game("Linux Test") { Id = gameId });
            }

            using (var database = new GameDatabase(databasePath))
            {
                database.OpenDatabase();
                Assert.That(database.Games.Get(gameId)?.Name, Is.EqualTo("Linux Test"));
            }
        }

        [Test]
        public void DatabaseMetadataImportPreservesLocalSourceUnderTempDirectory()
        {
            var sourcePath = Path.Combine(temporaryDirectory, "source.txt");
            File.WriteAllText(sourcePath, "local metadata source");
            using (var database = new GameDatabase(Path.Combine(temporaryDirectory, "library-files")))
            {
                database.OpenDatabase();
                var storedPath = database.AddFile(
                    new MetadataFile(sourcePath),
                    Guid.NewGuid(),
                    false,
                    CancellationToken.None);

                Assert.That(storedPath, Is.Not.Null.And.Not.Empty);
                Assert.That(File.Exists(database.GetFullFilePath(storedPath)), Is.True);
                Assert.That(File.Exists(sourcePath), Is.True);
            }
        }

        [Test]
        public void ImportedRomPathsPreserveHostCaseRules()
        {
            var emulatorDirectory = Path.Combine(temporaryDirectory, "EmulatorRoot");
            var relativeRomPath = Path.Combine("MixedCase", "Pilot.rom");
            var storedRomPath = Path.Combine(ExpandableVariables.EmulatorDirectory, relativeRomPath);
            using (var database = new GameDatabase(Path.Combine(temporaryDirectory, "rom-library")))
            {
                database.OpenDatabase();
                database.Games.Add(new Game("Case-sensitive ROM")
                {
                    Roms = new System.Collections.ObjectModel.ObservableCollection<GameRom>
                    {
                        new GameRom { Name = "Pilot", Path = storedRomPath }
                    }
                });

                var imported = database.GetImportedRomFiles(emulatorDirectory);
                var expected = Path.GetFullPath(Path.Combine(emulatorDirectory, relativeRomPath));

                Assert.That(imported.Single(), Is.EqualTo(expected));
                Assert.That(imported.Comparer, Is.SameAs(OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal));
            }
        }

        [Test]
        public void CurrentArchiveProviderReadsZipEntries()
        {
            var archivePath = Path.Combine(temporaryDirectory, "sample.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("folder/data.txt");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("portable archive");
                }
            }

            Assert.That(Archive.GetArchiveFiles(archivePath), Is.EqualTo(new[] { "folder/data.txt" }));
            var streamAndArchive = Archive.GetEntryStream(archivePath, "folder/data.txt");
            using (streamAndArchive.Item2)
            using (streamAndArchive.Item1)
            using (var reader = new StreamReader(streamAndArchive.Item1))
            {
                Assert.That(reader.ReadToEnd(), Is.EqualTo("portable archive"));
            }
        }

        [Test]
        public void SqliteUsesThePlatformNativeLibrary()
        {
            var databasePath = Path.Combine(temporaryDirectory, "portable.db");
            using (var database = new Sqlite(databasePath, SqliteOpenFlags.ReadWrite | SqliteOpenFlags.Create))
            {
                var result = database.Query<ScalarResult>("SELECT ? AS Value", 42);
                Assert.That(result.Single().Value, Is.EqualTo(42));
            }
        }

        [Test]
        public void PortableEmulationDatabaseUsesParameterizedQueries()
        {
            var databasePath = Path.Combine(temporaryDirectory, "Nintendo.db");
            using (var database = new Sqlite(databasePath, SqliteOpenFlags.ReadWrite | SqliteOpenFlags.Create))
            {
                database.Query<ScalarResult>(
                    "CREATE TABLE DatGame (Id INTEGER, Name TEXT, Region TEXT, ReleaseYear TEXT, Serial TEXT, RomCrc TEXT, RomName TEXT)");
                database.Query<ScalarResult>(
                    "INSERT INTO DatGame (Id, Name, Region, ReleaseYear, Serial, RomCrc, RomName) " +
                    "VALUES (1, 'Example', 'World', '2026', $p0, 'ABCD', 'Example (World).rom')",
                    "SER-001");
            }

            using (var database = EmulationDatabase.GetDatabase("Nintendo", temporaryDirectory))
            {
                Assert.That(database.GetBySerial("ser-001").Single().Name, Is.EqualTo("Example"));
                Assert.That(database.GetByRomNamePartial("world").Single().RomCrc, Is.EqualTo("ABCD"));
            }
        }

        [Test]
        public void LinuxProcessInspectionUsesProcfs()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Ignore("Linux-specific runtime contract.");
            }

            using (var process = Process.GetCurrentProcess())
            {
                Assert.That(process.TryGetParentId(out var parentId), Is.True);
                Assert.That(parentId, Is.GreaterThan(0));
                Assert.That(process.GetCommandLine(), Is.Not.Empty);
                Assert.That(process.TryGetMainModuleFileName(out var executable), Is.True);
                Assert.That(executable, Is.Not.Empty);
            }
        }

        [Test]
        public void LinuxPathDefaultsUseNativeLocationsAndSeparators()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Ignore("Linux-specific runtime contract.");
            }

            Assert.That(Paths.FixSeparators(@"games\library\title"), Is.EqualTo("games/library/title"));
            Assert.That(GameDatabase.GetDefaultPath(false, null), Does.Not.Contain("%AppData%"));
            Assert.That(Path.IsPathFullyQualified(GameDatabase.GetDefaultPath(false, null)), Is.True);
            Assert.That(PlaynitePaths.IsPortable, Is.False);
        }

        [Test]
        public void LinuxRegistrationUsesSeparateXdgEntries()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Ignore("Linux-specific runtime contract.");
            }

            var oldDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var oldConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var oldExecutable = CoreRuntime.ApplicationExecutablePath;
            try
            {
                var dataHome = Path.Combine(temporaryDirectory, "data");
                var configHome = Path.Combine(temporaryDirectory, "config");
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataHome);
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configHome);
                CoreRuntime.ApplicationExecutablePath = () => "/opt/playnite/playnite";

                SystemIntegration.RegisterPlayniteUriProtocol();
                SystemIntegration.RegisterFileExtensions();
                SystemIntegration.SetBootupStateRegistration(true, true);

                var applications = Path.Combine(dataHome, "applications");
                var uriEntry = Path.Combine(applications, "playnite-uri.desktop");
                var extensionEntry = Path.Combine(applications, "playnite-extension.desktop");
                var autostartEntry = Path.Combine(configHome, "autostart", "playnite.desktop");
                Assert.That(File.Exists(uriEntry), Is.True);
                Assert.That(File.ReadAllText(uriEntry), Does.Contain(
                    "Exec=\"/opt/playnite/playnite\" --uridata %u"));
                Assert.That(File.ReadAllText(uriEntry), Does.Contain("x-scheme-handler/playnite;"));
                Assert.That(File.Exists(extensionEntry), Is.True);
                Assert.That(File.Exists(autostartEntry), Is.True);
                Assert.That(File.ReadAllText(autostartEntry), Does.Contain("--startclosedtotray"));
                Assert.That(File.Exists(Path.Combine(dataHome, "mime", "packages", "playnite.xml")), Is.True);

                var shortcut = Path.Combine(temporaryDirectory, "Linux Game.desktop");
                Programs.CreateShortcut(
                    "/opt/games/example game",
                    "--profile linux",
                    "/opt/games/icon.png",
                    shortcut);
                var shortcutText = File.ReadAllText(shortcut);
                Assert.That(shortcutText, Does.Contain("Exec=\"/opt/games/example game\" --profile linux"));
                Assert.That(shortcutText, Does.Contain("Icon=/opt/games/icon.png"));

                SystemIntegration.SetBootupStateRegistration(false, false);
                Assert.That(File.Exists(autostartEntry), Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", oldDataHome);
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", oldConfigHome);
                CoreRuntime.ApplicationExecutablePath = oldExecutable;
            }
        }

        [Test]
        public void SingleInstanceCoordinatorForwardsAndQueuesCommands()
        {
            var endpoint = SingleInstanceCoordinator.CreateEndpoint(
                "desktop",
                Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N")));
            using (var primary = new SingleInstanceCoordinator(endpoint))
            {
                Assert.That(primary.IsPrimary, Is.True);
                bool? secondaryWasPrimary = null;
                Exception secondaryError = null;
                var secondaryThread = new Thread(() =>
                {
                    try
                    {
                        using (var secondary = new SingleInstanceCoordinator(endpoint))
                        {
                            secondaryWasPrimary = secondary.IsPrimary;
                            secondary.SendToPrimary(CmdlineCommand.UriRequest, "playnite://test/queued");
                        }
                    }
                    catch (Exception exception)
                    {
                        secondaryError = exception;
                    }
                });
                secondaryThread.Start();
                Assert.That(secondaryThread.Join(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(secondaryError, Is.Null);
                Assert.That(secondaryWasPrimary, Is.False);

                var received = new ManualResetEventSlim();
                CommandExecutedEventArgs command = null;
                primary.SetCommandHandler(args =>
                {
                    command = args;
                    received.Set();
                });

                Assert.That(received.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(command.Command, Is.EqualTo(CmdlineCommand.UriRequest));
                Assert.That(command.Args, Is.EqualTo("playnite://test/queued"));
            }

            using (var replacement = new SingleInstanceCoordinator(endpoint))
            {
                Assert.That(replacement.IsPrimary, Is.True);
            }
        }

        [Test]
        public void FlatpakProcessLaunchesAreDelegatedToTheHost()
        {
            var target = new ProcessStartInfo("/opt/games/Example Game")
            {
                Arguments = "--profile \"Deck User\"",
                WorkingDirectory = "/mnt/games/Example Library",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var wrapped = ProcessStarter.PrepareForFlatpak(target, true);
            Assert.That(wrapped.FileName, Is.EqualTo("flatpak-spawn"));
            Assert.That(wrapped.Arguments, Does.StartWith("--host "));
            Assert.That(wrapped.Arguments, Does.Contain(
                "--directory=\"/mnt/games/Example Library\""));
            Assert.That(wrapped.Arguments, Does.Contain("\"/opt/games/Example Game\""));
            Assert.That(wrapped.Arguments, Does.EndWith("--profile \"Deck User\""));
            Assert.That(wrapped.RedirectStandardOutput, Is.True);
            Assert.That(wrapped.UseShellExecute, Is.False);
        }

        [Test]
        public void PortableShellResolverSupportsSplitPackageLayout()
        {
            var desktopDirectory = Path.Combine(temporaryDirectory, "lib", "playnite", "desktop");
            var fullscreenDirectory = Path.Combine(temporaryDirectory, "lib", "playnite", "fullscreen");
            Directory.CreateDirectory(desktopDirectory);
            Directory.CreateDirectory(fullscreenDirectory);
            var fullscreenExecutable = Path.Combine(
                fullscreenDirectory,
                "Playnite.FullscreenApp.Avalonia");
            File.WriteAllText(fullscreenExecutable, string.Empty);

            Assert.That(
                PlaynitePaths.SelectPortableShellExecutable(
                    desktopDirectory,
                    "Playnite.FullscreenApp"),
                Is.EqualTo(fullscreenExecutable));
        }

        public class ScalarResult
        {
            public int Value { get; set; }
        }

        private static string EscapeVdf(string value) => value.Replace("\\", "\\\\");
    }
}
