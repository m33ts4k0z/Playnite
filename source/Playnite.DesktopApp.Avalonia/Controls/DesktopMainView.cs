using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
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
    private Button detailsMoreButton;
    private DesktopAppViewModel observedViewModel;
    private bool synchronizingSelection;

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
    public Border DetailsBackground => FindVisualPart<Border>("PART_DetailsBackground");
    public Button DetailsMoreButton => detailsMoreButton ??= FindVisualPart<Button>("PART_DetailsMoreButton");

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;
        DetachGameList(gridGameList);
        DetachGameList(listGameList);
        DetachDetailsMoreButton(detailsMoreButton);
        detailsMoreButton = null;
        gridGameList = e.NameScope.Find<ListBox>("PART_GridGameList");
        listGameList = e.NameScope.Find<ListBox>("PART_ListGameList");
        searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        pluginSearchBox = e.NameScope.Find<TextBox>("PART_PluginSearchBox");
        detailsPanel = e.NameScope.Find<Control>("PART_DetailsPanel");
        AttachGameList(gridGameList);
        AttachGameList(listGameList);
        ObserveViewModel();
        Dispatcher.UIThread.Post(() =>
        {
            searchBox ??= FindVisualPart<TextBox>("PART_SearchBox");
            pluginSearchBox ??= FindVisualPart<TextBox>("PART_PluginSearchBox");
            detailsMoreButton = FindVisualPart<Button>("PART_DetailsMoreButton");
            AttachDetailsMoreButton(detailsMoreButton);
            ApplyScrollSettings();
            SynchronizeGameSelection();
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
            SynchronizeGameSelection();
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

        if (e.PropertyName is nameof(DesktopAppViewModel.SelectedGame) or
            nameof(DesktopAppViewModel.SelectedGames) or
            nameof(DesktopAppViewModel.Games) or
            nameof(DesktopAppViewModel.IsGridView) or
            nameof(DesktopAppViewModel.IsListView))
        {
            Dispatcher.UIThread.Post(() => SynchronizeGameSelection(), DispatcherPriority.Input);
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

    private void AttachGameList(ListBox listBox)
    {
        if (listBox == null)
        {
            return;
        }

        listBox.SelectionChanged += GameList_SelectionChanged;
        listBox.PointerPressed += GameList_PointerPressed;
        var menu = new ContextMenu();
        menu.Opened += (_, _) => PopulateContextMenu(menu);
        listBox.ContextMenu = menu;
    }

    private void DetachGameList(ListBox listBox)
    {
        if (listBox == null)
        {
            return;
        }

        listBox.SelectionChanged -= GameList_SelectionChanged;
        listBox.PointerPressed -= GameList_PointerPressed;
        listBox.ContextMenu = null;
    }

    private void AttachDetailsMoreButton(Button button)
    {
        if (button == null || button.ContextMenu != null)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Opened += (_, _) => PopulateContextMenu(menu);
        button.ContextMenu = menu;
        button.Click += DetailsMoreButton_Click;
    }

    private void DetachDetailsMoreButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.Click -= DetailsMoreButton_Click;
        button.ContextMenu = null;
    }

    private static void DetailsMoreButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.Open(button);
        }
    }

    private void GameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (synchronizingSelection || observedViewModel == null || sender is not ListBox listBox ||
            !listBox.IsVisible)
        {
            return;
        }

        var selected = listBox.SelectedItems?
            .OfType<DesktopGameItemViewModel>()
            .ToList() ?? new List<DesktopGameItemViewModel>();
        observedViewModel.SetSelectedGames(selected, listBox.SelectedItem as DesktopGameItemViewModel);
        SynchronizeGameSelection(listBox);
    }

    private void GameList_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox listBox ||
            e.GetCurrentPoint(listBox).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
        {
            return;
        }

        var source = e.Source as Visual;
        var container = source as ListBoxItem ??
            source?.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        var game = container?.DataContext as DesktopGameItemViewModel;
        if (game == null || listBox.SelectedItems?.Contains(game) == true)
        {
            return;
        }

        synchronizingSelection = true;
        try
        {
            listBox.SelectedItems?.Clear();
            listBox.SelectedItems?.Add(game);
            listBox.SelectedItem = game;
        }
        finally
        {
            synchronizingSelection = false;
        }
        observedViewModel?.SetSelectedGames(new[] { game }, game);
        SynchronizeGameSelection(listBox);
    }

    private void SynchronizeGameSelection(ListBox source = null)
    {
        if (synchronizingSelection || observedViewModel == null)
        {
            return;
        }

        synchronizingSelection = true;
        try
        {
            SynchronizeListSelection(gridGameList, source);
            SynchronizeListSelection(listGameList, source);
        }
        finally
        {
            synchronizingSelection = false;
        }
    }

    private void SynchronizeListSelection(ListBox listBox, ListBox source)
    {
        if (listBox == null || ReferenceEquals(listBox, source))
        {
            return;
        }

        var selectionMode = SelectionMode.Multiple;
        if (observedViewModel.Games.Count > 0)
        {
            selectionMode |= SelectionMode.AlwaysSelected;
        }
        if (listBox.SelectionMode != selectionMode)
        {
            listBox.SelectionMode = selectionMode;
        }

        var expectedSelection = observedViewModel.SelectedGames
            .Where(observedViewModel.Games.Contains)
            .ToList();
        if (ReferenceEquals(listBox.SelectedItem, observedViewModel.SelectedGame) &&
            listBox.SelectedItems?.Count == expectedSelection.Count &&
            expectedSelection.All(game => listBox.SelectedItems.Contains(game)))
        {
            return;
        }

        listBox.SelectedItems?.Clear();
        listBox.SelectedItem = observedViewModel.SelectedGame;
        foreach (var game in expectedSelection)
        {
            if (listBox.SelectedItems?.Contains(game) != true)
            {
                listBox.SelectedItems?.Add(game);
            }
        }
    }

    private void PopulateContextMenu(ContextMenu menu)
    {
        var entries = observedViewModel?.BuildGameContextMenu() ??
            Array.Empty<DesktopGameContextMenuEntry>();
        menu.ItemsSource = entries.Select(CreateContextMenuItem).ToList();
    }

    private static object CreateContextMenuItem(DesktopGameContextMenuEntry entry)
    {
        if (entry.IsSeparator)
        {
            return new Separator();
        }

        var item = new MenuItem
        {
            Header = entry.Header,
            IsEnabled = entry.IsEnabled,
            FontWeight = entry.IsBold ? FontWeight.Bold : FontWeight.Normal
        };
        if (entry.IsChecked.HasValue)
        {
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = entry.IsChecked.Value;
        }
        if (entry.Children.Count > 0)
        {
            item.ItemsSource = entry.Children.Select(CreateContextMenuItem).ToList();
        }
        else
        {
            item.Click += (_, _) => entry.Invoke();
        }
        return item;
    }

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
