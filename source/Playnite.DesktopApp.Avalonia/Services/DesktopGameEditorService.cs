using Avalonia.Threading;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopGameEditorService
{
    private readonly DesktopAppViewModel viewModel;

    public DesktopGameEditorService(DesktopAppViewModel viewModel)
    {
        this.viewModel = viewModel;
    }

    public bool? Show(IReadOnlyList<Guid> gameIds)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() => Show(gameIds));
        }

        if (gameIds?.Count != 1)
        {
            viewModel.SetStatusMessage("Avalonia bulk metadata editing is not implemented yet.");
            return null;
        }

        bool? result = null;
        var frame = new DispatcherFrame();
        if (!viewModel.OpenGameEditor(gameIds[0], selected =>
            {
                result = selected;
                frame.Continue = false;
            }))
        {
            return null;
        }

        Dispatcher.UIThread.PushFrame(frame);
        return result;
    }
}
