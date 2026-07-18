using Avalonia.Controls;
using Avalonia.Threading;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopDialogService : IAvaloniaDialogService
{
    private readonly DesktopAppViewModel viewModel;
    private readonly Func<Window> currentWindow;

    public DesktopDialogService(DesktopAppViewModel viewModel, Func<Window> currentWindow)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.currentWindow = currentWindow ?? throw new ArgumentNullException(nameof(currentWindow));
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

    public IReadOnlyList<string> SelectFiles(string filter, bool allowMultiple) =>
        AvaloniaStorageDialog.SelectFiles(GetCurrentWindow(), filter, allowMultiple);

    public string SelectFolder() => AvaloniaStorageDialog.SelectFolder(GetCurrentWindow());

    private Window GetCurrentWindow() => currentWindow() ?? throw new NotSupportedException(
        "The Avalonia Desktop window is not available for a storage dialog.");
}
