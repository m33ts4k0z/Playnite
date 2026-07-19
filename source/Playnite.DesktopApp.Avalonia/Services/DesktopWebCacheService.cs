namespace Playnite.DesktopApp.Avalonia.Services;

public static class DesktopWebCacheService
{
    public static IReadOnlyList<string> GetCacheDirectories()
    {
        var directories = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            var executableName = Path.GetFileName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(executableName))
            {
                directories.Add(Path.Combine(AppContext.BaseDirectory, $"{executableName}.WebView2"));
            }
        }
        else if (!string.IsNullOrWhiteSpace(global::Playnite.PlaynitePaths.BrowserCachePath))
        {
            directories.Add(global::Playnite.PlaynitePaths.BrowserCachePath);
        }
        return directories;
    }

    public static void Clear()
    {
        foreach (var directory in GetCacheDirectories())
        {
            global::Playnite.Common.FileSystem.DeleteDirectory(directory);
        }
    }
}
