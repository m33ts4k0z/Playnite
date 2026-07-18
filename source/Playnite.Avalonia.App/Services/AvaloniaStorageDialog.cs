using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Playnite.Avalonia.App.Services;

public static class AvaloniaStorageDialog
{
    public static IReadOnlyList<string> SelectFiles(
        Window owner,
        string filter,
        bool allowMultiple)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var files = RunAsync(() => owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            FileTypeFilter = ParseFileTypes(filter)
        }));
        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }

    public static string SelectFolder(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var folders = RunAsync(() => owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        }));
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private static IReadOnlyList<FilePickerFileType> ParseFileTypes(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var parts = filter.Split('|');
        if (parts.Length % 2 != 0)
        {
            throw new ArgumentException(
                "File filters must contain description/pattern pairs separated by '|'.",
                nameof(filter));
        }

        var types = new List<FilePickerFileType>();
        for (var index = 0; index < parts.Length; index += 2)
        {
            var patterns = parts[index + 1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (patterns.Length == 0)
            {
                throw new ArgumentException(
                    $"File filter '{parts[index]}' has no patterns.",
                    nameof(filter));
            }

            types.Add(new FilePickerFileType(parts[index]) { Patterns = patterns });
        }

        return types;
    }

    private static T RunAsync<T>(Func<Task<T>> operation)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() => RunAsync(operation));
        }

        var task = operation();
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(
                _ => Dispatcher.UIThread.Post(() => frame.Continue = false),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(frame);
        }

        return task.GetAwaiter().GetResult();
    }
}
