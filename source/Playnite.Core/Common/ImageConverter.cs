using System;
using System.IO;
using Playnite.Common.Media.Icons;
using Playnite.SDK;

namespace Playnite.Common
{
    // Image normalization for database storage, split from Images.cs (WPF side)
    // so GameDatabase can live in Playnite.Core. TGA decoding is supplied by the
    // host (WPF BitmapExtensions today, a portable decoder later).
    public static class ImageConverter
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static Func<string, byte[]> TgaToPng { get; set; }

        public static string ConvertToCompatibleFormat(string imagePath, string outFileRoot)
        {
            if (imagePath.IsNullOrEmpty() || !File.Exists(imagePath))
            {
                return null;
            }

            FileSystem.CreateDirectory(Path.GetDirectoryName(outFileRoot));
            if (imagePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var icoPath = outFileRoot + ".ico";
                if (IconExtractor.ExtractMainIconFromFile(imagePath, icoPath))
                {
                    return icoPath;
                }
                else
                {
                    return null;
                }
            }
            else if (imagePath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
            {
                var pngPath = outFileRoot + ".png";
                try
                {
                    if (TgaToPng == null)
                    {
                        logger.Error("No TGA converter installed, can't convert image.");
                        return null;
                    }

                    File.WriteAllBytes(pngPath, TgaToPng(imagePath));
                    return pngPath;
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to covert {imagePath} to png.");
                    return null;
                }
            }

            return imagePath;
        }
    }
}
