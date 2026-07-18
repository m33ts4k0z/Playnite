using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Playnite.Avalonia.Controls;
using Playnite.FullscreenApp.Avalonia.ViewModels;

namespace Playnite.FullscreenApp.Avalonia.Controls;

public sealed class FullscreenMainView : TemplatedControl
{
    private ListBox gameList;

    public int TemplateAppliedCount { get; private set; }
    public ListBox GameList => gameList;
    public UniformGridVirtualizingPanel TilePanel =>
        gameList?.GetVisualDescendants().OfType<UniformGridVirtualizingPanel>().FirstOrDefault();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;
        gameList = e.NameScope.Find<ListBox>("PART_GameList");
        Dispatcher.UIThread.Post(FocusSelectedGame, DispatcherPriority.Loaded);
    }

    public void FocusSelectedGame()
    {
        if (gameList == null)
        {
            return;
        }

        if (gameList.SelectedIndex < 0 && gameList.ItemCount > 0)
        {
            gameList.SelectedIndex = 0;
        }

        if (gameList.SelectedIndex >= 0)
        {
            gameList.ScrollIntoView(gameList.SelectedIndex);
            (gameList.ContainerFromIndex(gameList.SelectedIndex) as Control)?.Focus();
        }
    }
}
