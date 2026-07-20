using Avalonia.Controls;
using Avalonia.Threading;
using Playnite.Avalonia.App.Services;
using Playnite.FullscreenApp.Avalonia.ViewModels;
using Playnite.SDK;
using LegacyWindow = System.Windows.Window;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenDialogService : IAvaloniaDialogService
{
    private readonly FullscreenAppViewModel viewModel;
    private readonly Func<Window> currentWindow;
    private readonly AvaloniaDialogHost dialogHost;
    private readonly LegacyWpfWindowBridge legacyWindows;

    public FullscreenDialogService(FullscreenAppViewModel viewModel, Func<Window> currentWindow)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.currentWindow = currentWindow ?? throw new ArgumentNullException(nameof(currentWindow));
        dialogHost = new AvaloniaDialogHost(
            currentWindow,
            dialogWindow => (currentWindow() as global::Playnite.FullscreenApp.Avalonia.MainWindow)?
                .GamepadBridge.RedirectTo(dialogWindow));
        legacyWindows = new LegacyWpfWindowBridge(currentWindow);
    }

    public string ShowMessage(
        string message,
        string caption,
        IReadOnlyList<string> options,
        int defaultIndex = 0,
        int cancelIndex = -1)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() =>
                ShowMessage(message, caption, options, defaultIndex, cancelIndex));
        }

        var choices = options?.Where(option => !string.IsNullOrWhiteSpace(option)).ToList() ?? new List<string>();
        if (choices.Count == 0)
        {
            choices.Add("OK");
        }

        defaultIndex = Math.Clamp(defaultIndex, 0, choices.Count - 1);
        cancelIndex = cancelIndex < 0 ? defaultIndex : Math.Clamp(cancelIndex, 0, choices.Count - 1);

        string result = null;
        var frame = new DispatcherFrame();
        viewModel.OpenDialog(
            string.IsNullOrWhiteSpace(caption) ? "Playnite" : caption,
            message ?? string.Empty,
            choices,
            defaultIndex,
            cancelIndex,
            selected =>
            {
                result = selected;
                frame.Continue = false;
            });
        Dispatcher.UIThread.PushFrame(frame);
        return result ?? choices[cancelIndex];
    }

    public IReadOnlyList<string> SelectFiles(
        string filter,
        bool allowMultiple,
        string initialDirectory = null) =>
        AvaloniaStorageDialog.SelectFiles(GetCurrentWindow(), filter, allowMultiple, initialDirectory);

    public string SelectFolder(string initialDirectory = null) =>
        AvaloniaStorageDialog.SelectFolder(GetCurrentWindow(), initialDirectory);

    public string SaveFile(string filter, bool promptOverwrite = true, string initialDirectory = null) =>
        AvaloniaStorageDialog.SaveFile(GetCurrentWindow(), filter, promptOverwrite, initialDirectory);

    public StringSelectionDialogResult ShowInput(
        string message,
        string caption,
        string defaultInput,
        IReadOnlyList<MessageBoxToggle> toggleOptions = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() =>
                ShowInput(message, caption, defaultInput, toggleOptions));
        }

        var confirmed = false;
        var selected = defaultInput ?? string.Empty;
        var frame = new DispatcherFrame();
        viewModel.OpenTextInput(
            caption,
            message,
            selected,
            toggleOptions,
            (result, value) =>
            {
                confirmed = result;
                selected = value;
                frame.Continue = false;
            });
        Dispatcher.UIThread.PushFrame(frame);
        return new StringSelectionDialogResult(confirmed, selected);
    }

    public void ShowSelectableString(string message, string caption, string value) =>
        dialogHost.ShowSelectableString(message, caption, value);

    public GenericItemOption ChooseItemWithSearch(
        IReadOnlyList<GenericItemOption> items,
        Func<string, List<GenericItemOption>> searchFunction,
        string defaultSearch = null,
        string caption = null) =>
        dialogHost.ChooseItemWithSearch(items, searchFunction, defaultSearch, caption);

    public ImageFileOption ChooseImageFile(
        IReadOnlyList<ImageFileOption> files,
        string caption = null,
        double itemWidth = 240,
        double itemHeight = 180) =>
        dialogHost.ChooseImageFile(files, caption, itemWidth, itemHeight);

    public GlobalProgressResult ActivateGlobalProgress(
        Action<GlobalProgressActionArgs> progressAction,
        GlobalProgressOptions options) =>
        dialogHost.ActivateGlobalProgress(progressAction, options);

    public GlobalProgressResult ActivateGlobalProgress(
        Func<GlobalProgressActionArgs, Task> progressAction,
        GlobalProgressOptions options) =>
        dialogHost.ActivateGlobalProgress(progressAction, options);

    public AvaloniaSelectionResult<T> SelectSingle<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items) =>
        dialogHost.SelectSingle(caption, message, items);

    public AvaloniaSelectionResult<T> SelectMultiple<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items) =>
        dialogHost.SelectMultiple(caption, message, items);

    public LegacyWindow CreateLegacyWindow(WindowCreationOptions options) =>
        legacyWindows.CreateWindow(options);

    public LegacyWindow GetCurrentLegacyWindow() => legacyWindows.GetCurrentWindow();

    public Window GetCurrentWindow() => currentWindow() ?? throw new NotSupportedException(
        "The Avalonia Fullscreen window is not available for a storage dialog.");
}
