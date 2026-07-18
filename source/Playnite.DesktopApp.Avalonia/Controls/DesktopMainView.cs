using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Playnite.Avalonia.Controls;

namespace Playnite.DesktopApp.Avalonia.Controls;

public sealed class DesktopMainView : TemplatedControl
{
    private ListBox gridGameList;
    private ListBox listGameList;
    private TextBox searchBox;

    public int TemplateAppliedCount { get; private set; }
    public ListBox GameList => gridGameList?.IsVisible == true ? gridGameList : listGameList;
    public ListBox GridGameList => gridGameList;
    public ListBox ListGameList => listGameList;
    public TextBox SearchBox => searchBox;
    public UniformGridVirtualizingPanel TilePanel =>
        gridGameList?.GetVisualDescendants().OfType<UniformGridVirtualizingPanel>().FirstOrDefault();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;
        gridGameList = e.NameScope.Find<ListBox>("PART_GridGameList");
        listGameList = e.NameScope.Find<ListBox>("PART_ListGameList");
        searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        Dispatcher.UIThread.Post(FocusSelectedGame, DispatcherPriority.Loaded);
    }

    public void FocusSelectedGame()
    {
        var gameList = GameList;
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
