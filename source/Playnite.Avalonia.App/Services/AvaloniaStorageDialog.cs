using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Playnite.Avalonia.App.Services;

public static class AvaloniaStorageDialog
{
    public static IReadOnlyList<string> SelectFiles(
        Window owner,
        string filter,
        bool allowMultiple,
        string initialDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var startLocation = ResolveStartLocation(owner, initialDirectory);
        var files = RunAsync(() => owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = allowMultiple,
            FileTypeFilter = ParseFileTypes(filter),
            SuggestedStartLocation = startLocation
        }));
        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }

    public static string SelectFolder(Window owner, string initialDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var startLocation = ResolveStartLocation(owner, initialDirectory);
        var folders = RunAsync(() => owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = startLocation
        }));
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public static string SaveFile(
        Window owner,
        string filter,
        bool promptOverwrite,
        string initialDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var types = ParseFileTypes(filter);
        var file = RunAsync(() => owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            FileTypeChoices = types,
            DefaultExtension = GetDefaultExtension(types),
            ShowOverwritePrompt = promptOverwrite,
            SuggestedStartLocation = ResolveStartLocation(owner, initialDirectory)
        }));
        return file?.TryGetLocalPath();
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

    private static IStorageFolder ResolveStartLocation(Window owner, string initialDirectory)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(initialDirectory);
        if (File.Exists(fullPath))
        {
            fullPath = Path.GetDirectoryName(fullPath);
        }

        return Directory.Exists(fullPath)
            ? RunAsync(() => owner.StorageProvider.TryGetFolderFromPathAsync(fullPath))
            : null;
    }

    private static string GetDefaultExtension(IReadOnlyList<FilePickerFileType> types)
    {
        var pattern = types?.FirstOrDefault()?.Patterns?.FirstOrDefault(candidate =>
            candidate.StartsWith("*.", StringComparison.Ordinal) &&
            candidate.IndexOfAny(new[] { '*', '?' }, 1) < 0);
        return pattern?[2..];
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
