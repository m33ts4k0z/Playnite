using Playnite.Common;
using Playnite.Database;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Playnite
{
    /// <summary>
    /// Calculates installed-game and emulated-ROM sizes without depending on a UI framework.
    /// </summary>
    public sealed class GameInstallSizeCalculator
    {
        private readonly GameDatabase database;

        public GameInstallSizeCalculator(GameDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public ulong? Calculate(Game game, bool useSizeOnDisk)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (!game.IsInstalled)
            {
                return null;
            }

            var expandedGame = ExpandGame(game);
            if (expandedGame.Roms != null && expandedGame.Roms.Count > 0)
            {
                return CalculateRomSize(expandedGame, useSizeOnDisk);
            }

            if (string.IsNullOrWhiteSpace(expandedGame.InstallDirectory) ||
                !FileSystem.DirectoryExists(expandedGame.InstallDirectory))
            {
                return null;
            }

            var size = FileSystem.GetDirectorySize(expandedGame.InstallDirectory, useSizeOnDisk);
            return checked((ulong)size);
        }

        private Game ExpandGame(Game game)
        {
            var emulatorAction = game.GameActions == null
                ? null
                : game.GameActions.FirstOrDefault(action => action.Type == GameActionType.Emulator);
            var emulator = emulatorAction == null ? null : database.Emulators[emulatorAction.EmulatorId];
            return emulator == null
                ? game.ExpandGame(true)
                : game.ExpandGame(true, emulator.InstallDir);
        }

        private static ulong? CalculateRomSize(Game game, bool useSizeOnDisk)
        {
            var comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var paths = new HashSet<string>(comparer);
            foreach (var rom in game.Roms)
            {
                if (rom == null || string.IsNullOrWhiteSpace(rom.Path) || !FileSystem.FileExists(rom.Path))
                {
                    continue;
                }

                if (rom.Path.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
                {
                    AddPlaylistPaths(rom.Path, CueSheet.GetFileEntries(rom.Path).Select(entry => entry.Path), paths);
                }
                else if (rom.Path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase))
                {
                    AddPlaylistPaths(rom.Path, M3U.GetEntries(rom.Path).Select(entry => entry.Path), paths);
                }
                else if (rom.Path.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase))
                {
                    AddPlaylistPaths(rom.Path, GdiFile.GetEntries(rom.Path).Select(entry => entry.Path), paths);
                }
                else
                {
                    paths.Add(rom.Path);
                }
            }

            if (paths.Count == 0)
            {
                return null;
            }

            long size = 0;
            foreach (var path in paths)
            {
                size = checked(size + (useSizeOnDisk
                    ? FileSystem.GetFileSizeOnDisk(path)
                    : FileSystem.GetFileSize(path)));
            }

            return checked((ulong)size);
        }

        private static void AddPlaylistPaths(
            string playlistPath,
            IEnumerable<string> entries,
            ISet<string> paths)
        {
            var root = Path.GetDirectoryName(playlistPath) ?? string.Empty;
            foreach (var entry in entries ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var path = Path.IsPathRooted(entry) ? entry : Path.Combine(root, entry);
                if (FileSystem.FileExists(path))
                {
                    paths.Add(path);
                }
            }
        }
    }
}
