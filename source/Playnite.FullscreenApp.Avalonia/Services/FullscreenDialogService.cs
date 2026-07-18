using Avalonia.Threading;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenDialogService
{
    private readonly FullscreenAppViewModel viewModel;

    public FullscreenDialogService(FullscreenAppViewModel viewModel)
    {
        this.viewModel = viewModel;
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
}
