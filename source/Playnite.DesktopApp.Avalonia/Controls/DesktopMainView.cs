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
    private Control detailsPanel;
    private DesktopAppViewModel observedViewModel;

    public int TemplateAppliedCount { get; private set; }
    public ListBox GameList => gridGameList?.IsVisible == true ? gridGameList : listGameList;
    public ListBox GridGameList => gridGameList;
    public ListBox ListGameList => listGameList;
    public TextBox SearchBox => searchBox ??= FindVisualPart<TextBox>("PART_SearchBox");
    public TextBox PluginSearchBox => pluginSearchBox ??= FindVisualPart<TextBox>("PART_PluginSearchBox");
    public UniformGridVirtualizingPanel TilePanel =>
        gridGameList?.GetVisualDescendants().OfType<UniformGridVirtualizingPanel>().FirstOrDefault();
    public ScrollViewer GridScrollViewer =>
        gridGameList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    public ScrollViewer ListScrollViewer =>
        listGameList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    public ScrollViewer DetailsScrollViewer =>
        detailsPanel?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    public Control DetailsPanel => detailsPanel;
    public TextBlock DetailsName => FindVisualPart<TextBlock>("PART_DetailsName");
    public Border DetailsCover => FindVisualPart<Border>("PART_DetailsCover");

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;
        gridGameList = e.NameScope.Find<ListBox>("PART_GridGameList");
        listGameList = e.NameScope.Find<ListBox>("PART_ListGameList");
        searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        pluginSearchBox = e.NameScope.Find<TextBox>("PART_PluginSearchBox");
        detailsPanel = e.NameScope.Find<Control>("PART_DetailsPanel");
        ObserveViewModel();
        Dispatcher.UIThread.Post(() =>
        {
            searchBox ??= FindVisualPart<TextBox>("PART_SearchBox");
            pluginSearchBox ??= FindVisualPart<TextBox>("PART_PluginSearchBox");
            ApplyScrollSettings();
            FocusSelectedGame();
        }, DispatcherPriority.Loaded);
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
            var container = gameList.ContainerFromIndex(gameList.SelectedIndex) as Control;
            if (container?.Focus() != true)
            {
                container?.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(control => control.Focusable && control.IsEffectivelyEnabled && control.IsVisible)
                    ?.Focus();
            }
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
        if (e.PropertyName is nameof(DesktopAppViewModel.GridViewScrollSensitivity) or
            nameof(DesktopAppViewModel.GridViewScrollDuration) or
            nameof(DesktopAppViewModel.GridViewSmoothScrollEnabled) or
            nameof(DesktopAppViewModel.ListViewScrollSensitivity) or
            nameof(DesktopAppViewModel.ListViewScrollDuration) or
            nameof(DesktopAppViewModel.ListViewSmoothScrollEnabled) or
            nameof(DesktopAppViewModel.DetailsViewScrollSensitivity) or
            nameof(DesktopAppViewModel.DetailsViewScrollDuration) or
            nameof(DesktopAppViewModel.DetailsViewSmoothScrollEnabled))
        {
            ApplyScrollSettings();
        }

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

    private T FindVisualPart<T>(string name) where T : Control =>
        this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);

    private void ApplyScrollSettings()
    {
        if (observedViewModel == null)
        {
            return;
        }

        ConfigureScrollViewer(
            gridGameList,
            observedViewModel.GridViewScrollSensitivity,
            observedViewModel.GridViewSmoothScrollEnabled,
            observedViewModel.GridViewScrollDuration);
        ConfigureScrollViewer(
            listGameList,
            observedViewModel.ListViewScrollSensitivity,
            observedViewModel.ListViewSmoothScrollEnabled,
            observedViewModel.ListViewScrollDuration);
        ConfigureScrollViewer(
            DetailsScrollViewer,
            observedViewModel.DetailsViewScrollSensitivity,
            observedViewModel.DetailsViewSmoothScrollEnabled,
            observedViewModel.DetailsViewScrollDuration);
    }

    private static void ConfigureScrollViewer(
        ListBox listBox,
        double sensitivity,
        bool smoothScrolling,
        TimeSpan duration)
    {
        var viewer = listBox?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (viewer == null)
        {
            return;
        }

        ScrollBehavior.SetWheelSensitivity(viewer, sensitivity);
        ScrollBehavior.SetSmoothScrollingEnabled(viewer, smoothScrolling);
        ScrollBehavior.SetSmoothScrollDuration(viewer, duration);
        ScrollBehavior.SetIsEnabled(viewer, true);
    }

    private static void ConfigureScrollViewer(
        ScrollViewer viewer,
        double sensitivity,
        bool smoothScrolling,
        TimeSpan duration)
    {
        if (viewer == null)
        {
            return;
        }

        ScrollBehavior.SetWheelSensitivity(viewer, sensitivity);
        ScrollBehavior.SetSmoothScrollingEnabled(viewer, smoothScrolling);
        ScrollBehavior.SetSmoothScrollDuration(viewer, duration);
        ScrollBehavior.SetIsEnabled(viewer, true);
    }
}
