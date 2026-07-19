#if !WINDOWS
using Playnite.Common;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Playnite.Emulators
{
    public class EmulationDatabase
    {
        public interface IEmulationDatabaseReader : IDisposable
        {
            string DatabaseName { get; }
            IEnumerable<DatGame> GetByCrc(string checksum);
            IEnumerable<DatGame> GetBySerial(string serial);
            IEnumerable<DatGame> GetByRomName(string romName);
            IEnumerable<DatGame> GetByRomNamePartial(string romNamePart);
            void ClearStatementCache();
        }

        public class EmulationDatabaseReader : IEmulationDatabaseReader
        {
            private readonly Sqlite database;
            private readonly bool tableExists;

            public string DatabaseName { get; }

            public EmulationDatabaseReader(string dbPath)
            {
                DatabaseName = Path.GetFileNameWithoutExtension(dbPath);
                database = new Sqlite(dbPath, SqliteOpenFlags.ReadOnly);
                tableExists = database.Query<TableCount>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'DatGame'")
                    .Single().Value > 0;
            }

            public void ClearStatementCache()
            {
                // Commands are disposed after every query and are never cached by
                // this implementation, so there is no retained cache to clear.
            }

            public IEnumerable<DatGame> GetByCrc(string checksum)
            {
                return QueryExact(nameof(DatGame.RomCrc), checksum);
            }

            public IEnumerable<DatGame> GetBySerial(string serial)
            {
                return QueryExact(nameof(DatGame.Serial), serial);
            }

            public IEnumerable<DatGame> GetByRomName(string romName)
            {
                return QueryExact(nameof(DatGame.RomName), romName);
            }

            public IEnumerable<DatGame> GetByRomNamePartial(string romNamePart)
            {
                if (!tableExists)
                {
                    return Array.Empty<DatGame>();
                }

                return database.Query<DatGame>(
                    "SELECT Id, Name, Region, ReleaseYear, Serial, RomCrc, RomName FROM DatGame " +
                    "WHERE INSTR(UPPER(RomName), UPPER($p0)) > 0",
                    romNamePart);
            }

            private IEnumerable<DatGame> QueryExact(string columnName, string value)
            {
                if (!tableExists)
                {
                    return Array.Empty<DatGame>();
                }

                var allowedColumns = new[] { nameof(DatGame.RomCrc), nameof(DatGame.Serial), nameof(DatGame.RomName) };
                if (!allowedColumns.Contains(columnName, StringComparer.Ordinal))
                {
                    throw new ArgumentOutOfRangeException(nameof(columnName));
                }

                return database.Query<DatGame>(
                    "SELECT Id, Name, Region, ReleaseYear, Serial, RomCrc, RomName FROM DatGame " +
                    $"WHERE UPPER({columnName}) = UPPER($p0)",
                    value);
            }

            public void Dispose()
            {
                database.Dispose();
            }

            public class TableCount
            {
                public long Value { get; set; }
            }
        }

        public static EmulationDatabaseReader GetDatabase(string databaseName, string databaseDir)
        {
            var dbFile = Path.Combine(databaseDir, databaseName + ".db");
            return File.Exists(dbFile) ? new EmulationDatabaseReader(dbFile) : null;
        }
    }
}
#endif
