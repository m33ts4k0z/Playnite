using Avalonia.Controls;
using Playnite.Common;

namespace Playnite.DesktopApp.Avalonia;

internal sealed class PortableTrayService : IDisposable
{
    private readonly TrayIcon trayIcon;

    internal int MenuItemCount { get; }
    internal bool IsVisible => trayIcon.IsVisible;

    internal PortableTrayService(
        string iconPath,
        Action restoreWindow,
        Action requestExit,
        bool visible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ArgumentNullException.ThrowIfNull(restoreWindow);
        ArgumentNullException.ThrowIfNull(requestExit);

        var menu = new NativeMenu();
        menu.Items.Add(CreateItem("Open Playnite", restoreWindow));

        var fullscreenPath = PlaynitePaths.FullscreenExecutablePath;
        menu.Items.Add(CreateItem(
            "Open Fullscreen",
            () => ProcessStarter.StartProcess(fullscreenPath),
            File.Exists(fullscreenPath)));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateItem("Exit Playnite", requestExit));
        MenuItemCount = menu.Items.Count;

        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconPath),
            ToolTipText = "Playnite",
            Menu = menu,
            IsVisible = visible
        };
        trayIcon.Clicked += (_, _) => restoreWindow();
    }

    public void Dispose()
    {
        trayIcon.IsVisible = false;
        trayIcon.Dispose();
    }

    private static NativeMenuItem CreateItem(string header, Action action, bool enabled = true)
    {
        var item = new NativeMenuItem(header)
        {
            IsEnabled = enabled
        };
        item.Click += (_, _) => action();
        return item;
    }
}
