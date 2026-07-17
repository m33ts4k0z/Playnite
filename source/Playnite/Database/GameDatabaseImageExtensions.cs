using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Playnite.Common;

namespace Playnite.Database
{
    // WPF image loading for database files, split out of GameDatabase so the
    // database itself can live in the UI-free Playnite.Core assembly. Uses the
    // database's internal file locks (InternalsVisibleTo) to keep the original
    // concurrency behavior.
    public static class GameDatabaseImageExtensions
    {
        public static BitmapSource GetFileAsImage(this IGameDatabaseMain database, string dbPath, BitmapLoadProperties loadProperties = null)
        {
            if (database is GameDatabase gameDb)
            {
                gameDb.CheckDbState();
                var filePath = gameDb.GetFullFilePath(dbPath);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                try
                {
                    lock (gameDb.GetFileLock(dbPath))
                    {
                        using (var fStream = FileSystem.OpenReadFileStreamSafe(filePath))
                        {
                            return BitmapExtensions.BitmapFromStream(fStream, loadProperties);
                        }
                    }
                }
                finally
                {
                    gameDb.ReleaseFileLock(dbPath);
                }
            }

            var path = database.GetFullFilePath(dbPath);
            if (!File.Exists(path))
            {
                return null;
            }

            using (var stream = FileSystem.OpenReadFileStreamSafe(path))
            {
                return BitmapExtensions.BitmapFromStream(stream, loadProperties);
            }
        }
    }
}
