using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Playnite.Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia.Controls;

public sealed class DesktopMainView : TemplatedControl
{
    private ListBox gridGameList;
    private ListBox listGameList;
    private TextBox searchBox;
    private TextBox pluginSearchBox;
    private DesktopAppViewModel observedViewModel;

    public int TemplateAppliedCount { get; private set; }
    public ListBox GameList => gridGameList?.IsVisible == true ? gridGameList : listGameList;
    public ListBox GridGameList => gridGameList;
    public ListBox ListGameList => listGameList;
    public TextBox SearchBox => searchBox;
    public TextBox PluginSearchBox => pluginSearchBox;
    public UniformGridVirtualizingPanel TilePanel =>
        gridGameList?.GetVisualDescendants().OfType<UniformGridVirtualizingPanel>().FirstOrDefault();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;
        gridGameList = e.NameScope.Find<ListBox>("PART_GridGameList");
        listGameList = e.NameScope.Find<ListBox>("PART_ListGameList");
        searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        pluginSearchBox = e.NameScope.Find<TextBox>("PART_PluginSearchBox");
        ObserveViewModel();
        Dispatcher.UIThread.Post(FocusSelectedGame, DispatcherPriority.Loaded);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ObserveViewModel();
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

    private void ObserveViewModel()
    {
        if (ReferenceEquals(observedViewModel, DataContext))
        {
            return;
        }

        if (observedViewModel != null)
        {
            observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        observedViewModel = DataContext as DesktopAppViewModel;
        if (observedViewModel != null)
        {
            observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DesktopAppViewModel.IsPluginSearchVisible) ||
            observedViewModel?.IsPluginSearchVisible != true || pluginSearchBox == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            pluginSearchBox.Focus();
            pluginSearchBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
