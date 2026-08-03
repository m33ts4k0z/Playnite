using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia.Controls;

public sealed class DesktopExtensionsMenuItem : MenuItem
{
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RefreshItems();
    }

    protected override void OnSubmenuOpened(RoutedEventArgs e)
    {
        RefreshItems();
        base.OnSubmenuOpened(e);
    }

    public void RefreshItems()
    {
        if (DataContext is not DesktopAppViewModel viewModel)
        {
            ItemsSource = null;
            return;
        }

        var items = new List<object>
        {
            new MenuItem
            {
                Header = DesktopLocalization.Resolve("LOCReloadScripts", "Reload Scripts"),
                Command = viewModel.ReloadScriptsCommand,
                InputGesture = new KeyGesture(Key.F12)
            },
            new MenuItem
            {
                Header = DesktopLocalization.Resolve(
                    "LOCStartInteractivePowerShell",
                    "Interactive SDK PowerShell"),
                Command = viewModel.OpenInteractivePowerShellCommand
            }
        };
        var pluginEntries = viewModel.BuildExtensionMainMenu();
        if (pluginEntries.Count > 0)
        {
            items.Add(new Separator());
            items.AddRange(pluginEntries.Select(CreateMenuItem));
        }

        ItemsSource = items;
    }

    private static object CreateMenuItem(DesktopGameContextMenuEntry entry)
    {
        if (entry.IsSeparator)
        {
            return new Separator();
        }

        var item = new MenuItem
        {
            Header = entry.Header,
            IsEnabled = entry.IsEnabled
        };
        if (entry.Children.Count > 0)
        {
            item.ItemsSource = entry.Children.Select(CreateMenuItem).ToList();
        }
        else
        {
            item.Click += (_, _) => entry.Invoke();
        }

        return item;
    }
}
