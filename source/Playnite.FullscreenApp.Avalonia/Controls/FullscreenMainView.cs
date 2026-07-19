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
    private TextBox searchBox;
    private TextBox pluginSearchBox;
    private ListBox filterList;
    private ListBox notificationsList;
    private ListBox actionList;
    private ListBox dialogOptions;
    private ContentControl settingsContent;
    private Button firstMenuButton;
    private Button detailsPrimaryButton;
    private IReadOnlyList<Button> configuredMenuButtons = Array.Empty<Button>();
    private FullscreenAppViewModel observedViewModel;

    public int TemplateAppliedCount { get; private set; }
    public ListBox GameList => gameList;
    public ListBox FilterList => filterList;
    public ListBox NotificationsList => notificationsList;
    public ListBox ActionList => actionList;
    public TextBox PluginSearchBox => pluginSearchBox;
    public UniformGridVirtualizingPanel TilePanel =>
        gameList?.GetVisualDescendants().OfType<UniformGridVirtualizingPanel>().FirstOrDefault();
    public ScrollViewer GameScrollViewer =>
        gameList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    public IReadOnlyList<Button> ConfiguredMenuButtons => configuredMenuButtons;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;
        gameList = e.NameScope.Find<ListBox>("PART_GameList");
        searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        pluginSearchBox = e.NameScope.Find<TextBox>("PART_PluginSearchBox");
        filterList = e.NameScope.Find<ListBox>("PART_FilterList");
        notificationsList = e.NameScope.Find<ListBox>("PART_NotificationsList");
        actionList = e.NameScope.Find<ListBox>("PART_ActionList");
        dialogOptions = e.NameScope.Find<ListBox>("PART_DialogOptions");
        settingsContent = e.NameScope.Find<ContentControl>("PART_SettingsContent");
        firstMenuButton = e.NameScope.Find<Button>("PART_MenuFirstButton");
        detailsPrimaryButton = e.NameScope.Find<Button>("PART_DetailsPrimaryButton");
        configuredMenuButtons = new[]
        {
            e.NameScope.Find<Button>("PART_MenuRestart"),
            e.NameScope.Find<Button>("PART_MenuShutdown"),
            e.NameScope.Find<Button>("PART_MenuSuspend"),
            e.NameScope.Find<Button>("PART_MenuHibernate"),
            e.NameScope.Find<Button>("PART_MenuMinimize"),
            e.NameScope.Find<Button>("PART_MenuLogout"),
            e.NameScope.Find<Button>("PART_MenuLock"),
            e.NameScope.Find<Button>("PART_MenuTools"),
            e.NameScope.Find<Button>("PART_MenuExtensions"),
            e.NameScope.Find<Button>("PART_MenuClients")
        }.Where(button => button != null).ToList();
        ObserveViewModel();
        ApplyScrollSettings();
        Dispatcher.UIThread.Post(FocusSelectedGame, DispatcherPriority.Loaded);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ObserveViewModel();
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

        observedViewModel = DataContext as FullscreenAppViewModel;
        if (observedViewModel != null)
        {
            observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FullscreenAppViewModel.SmoothScrolling))
        {
            ApplyScrollSettings();
        }

        Control target = e.PropertyName switch
        {
            nameof(FullscreenAppViewModel.IsSearchVisible) when observedViewModel.IsSearchVisible => searchBox,
            nameof(FullscreenAppViewModel.IsPluginSearchVisible) when observedViewModel.IsPluginSearchVisible => pluginSearchBox,
            nameof(FullscreenAppViewModel.IsFiltersVisible) when observedViewModel.IsFiltersVisible => filterList,
            nameof(FullscreenAppViewModel.IsSettingsVisible) when observedViewModel.IsSettingsVisible => settingsContent,
            nameof(FullscreenAppViewModel.IsNotificationsVisible) when observedViewModel.IsNotificationsVisible => notificationsList,
            nameof(FullscreenAppViewModel.IsActionPickerVisible) when observedViewModel.IsActionPickerVisible => actionList,
            nameof(FullscreenAppViewModel.IsDialogVisible) when observedViewModel.IsDialogVisible => dialogOptions,
            nameof(FullscreenAppViewModel.IsMenuVisible) when observedViewModel.IsMenuVisible => firstMenuButton,
            nameof(FullscreenAppViewModel.IsDetailsVisible) when observedViewModel.IsDetailsVisible => detailsPrimaryButton,
            _ => null
        };

        if (target != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(target, settingsContent))
                {
                    settingsContent.GetVisualDescendants()
                        .OfType<Control>()
                        .FirstOrDefault(control => control.Focusable && control.IsEffectivelyEnabled && control.IsVisible)
                        ?.Focus();
                }
                else if (target is ListBox listBox && listBox.ItemCount > 0)
                {
                    if (listBox.SelectedIndex < 0)
                    {
                        listBox.SelectedIndex = 0;
                    }

                    listBox.ScrollIntoView(listBox.SelectedIndex);
                    (listBox.ContainerFromIndex(listBox.SelectedIndex) as Control)?.Focus();
                }
                else
                {
                    target.Focus();
                }

                if (target is TextBox textBox)
                {
                    textBox.SelectAll();
                }
            }, DispatcherPriority.Input);
        }
    }

    private void ApplyScrollSettings()
    {
        var viewer = GameScrollViewer;
        if (viewer == null || observedViewModel == null)
        {
            return;
        }

        ScrollBehavior.SetIsEnabled(viewer, true);
        ScrollBehavior.SetWheelSensitivity(viewer, 1);
        ScrollBehavior.SetSmoothScrollingEnabled(viewer, observedViewModel.SmoothScrolling);
    }
}
