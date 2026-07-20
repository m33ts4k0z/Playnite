using Avalonia.Controls;
using Playnite.SDK;
#if WINDOWS
using LegacyWindow = System.Windows.Window;
#endif

namespace Playnite.Avalonia.App.Services;

public interface IAvaloniaDialogService
{
    string ShowMessage(
        string message,
        string caption,
        IReadOnlyList<string> options,
        int defaultIndex = 0,
        int cancelIndex = -1);

    IReadOnlyList<string> SelectFiles(
        string filter,
        bool allowMultiple,
        string initialDirectory = null);

    string SelectFolder(string initialDirectory = null);

    string SaveFile(
        string filter,
        bool promptOverwrite = true,
        string initialDirectory = null);

    StringSelectionDialogResult ShowInput(
        string message,
        string caption,
        string defaultInput,
        IReadOnlyList<MessageBoxToggle> toggleOptions = null);

    void ShowSelectableString(string message, string caption, string value);

    GenericItemOption ChooseItemWithSearch(
        IReadOnlyList<GenericItemOption> items,
        Func<string, List<GenericItemOption>> searchFunction,
        string defaultSearch = null,
        string caption = null);

    ImageFileOption ChooseImageFile(
        IReadOnlyList<ImageFileOption> files,
        string caption = null,
        double itemWidth = 240,
        double itemHeight = 180);

    GlobalProgressResult ActivateGlobalProgress(
        Action<GlobalProgressActionArgs> progressAction,
        GlobalProgressOptions options);

    GlobalProgressResult ActivateGlobalProgress(
        Func<GlobalProgressActionArgs, Task> progressAction,
        GlobalProgressOptions options);

    AvaloniaSelectionResult<T> SelectSingle<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items);

    AvaloniaSelectionResult<T> SelectMultiple<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items);

#if WINDOWS
    LegacyWindow CreateLegacyWindow(WindowCreationOptions options);

    LegacyWindow GetCurrentLegacyWindow();
#else
    Window CreateWindow(WindowCreationOptions options);
#endif

    Window GetCurrentWindow();
}
