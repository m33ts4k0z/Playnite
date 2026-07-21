using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Playnite.DesktopApp.Avalonia.ViewModels;

namespace Playnite.DesktopApp.Avalonia.Services;

internal sealed class DesktopTrayService : IDisposable
{
    private readonly DesktopAppViewModel viewModel;
    private readonly DesktopSettings settings;
    private readonly string iconPath;
    private readonly Action restoreWindow;
    private readonly Action requestExit;
    private readonly Func<bool> canOpenFullscreen;
    private readonly Action openFullscreen;
    private readonly NativeMenu menu = new();
    private TrayIcon trayIcon;
    private string currentIconPath;
    private readonly List<IDisposable> menuIcons = new();

    internal NativeMenu Menu => menu;
    internal bool IsEnabled => trayIcon != null;
    internal int QuickLaunchItemCount { get; private set; }
    internal int FavoriteItemCount { get; private set; }

    public DesktopTrayService(
        DesktopAppViewModel viewModel,
        DesktopSettings settings,
        string iconPath,
        Action restoreWindow,
        Action requestExit,
        Func<bool> canOpenFullscreen,
        Action openFullscreen)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.iconPath = iconPath ?? throw new ArgumentNullException(nameof(iconPath));
        this.restoreWindow = restoreWindow ?? throw new ArgumentNullException(nameof(restoreWindow));
        this.requestExit = requestExit ?? throw new ArgumentNullException(nameof(requestExit));
        this.canOpenFullscreen = canOpenFullscreen ?? throw new ArgumentNullException(nameof(canOpenFullscreen));
        this.openFullscreen = openFullscreen ?? throw new ArgumentNullException(nameof(openFullscreen));

        menu.NeedsUpdate += Menu_NeedsUpdate;
        RefreshMenu();
    }

    internal void ApplySettings(bool enabled) => ApplySettings(enabled, null);

    internal void ApplySettings(bool enabled, string trayIconPath)
    {
        if (!enabled)
        {
            DisposeTrayIcon();
            currentIconPath = null;
            return;
        }

        var resolvedPath = string.IsNullOrEmpty(trayIconPath) || !File.Exists(trayIconPath)
            ? iconPath
            : trayIconPath;

        if (trayIcon == null)
        {
            trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(resolvedPath),
                ToolTipText = "Playnite",
                Menu = menu,
                IsVisible = true
            };
            trayIcon.Clicked += TrayIcon_Clicked;
            currentIconPath = resolvedPath;
        }
        else if (resolvedPath != currentIconPath)
        {
            trayIcon.Icon = new WindowIcon(resolvedPath);
            currentIconPath = resolvedPath;
        }
    }

    internal void RefreshMenu()
    {
        foreach (var icon in menuIcons)
        {
            icon.Dispose();
        }
        menuIcons.Clear();
        menu.Items.Clear();

        var quickLaunchGames = viewModel.LibraryGames
            .Where(game => game.IsInstalled && (settings.ShowHiddenInQuickLaunch || !game.Game.Hidden))
            .OrderByDescending(game => game.Game.LastActivity ?? DateTime.MinValue)
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(settings.QuickLaunchItems)
            .ToList();
        QuickLaunchItemCount = quickLaunchGames.Count;
        foreach (var game in quickLaunchGames)
        {
            menu.Items.Add(CreateGameItem(game));
        }

        var favoriteGames = viewModel.LibraryGames
            .Where(game => game.IsInstalled && game.Favorite)
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        FavoriteItemCount = favoriteGames.Count;
        if (favoriteGames.Count > 0)
        {
            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
            }

            var favoritesMenu = new NativeMenu();
            foreach (var game in favoriteGames)
            {
                favoritesMenu.Items.Add(CreateGameItem(game));
            }

            menu.Items.Add(new NativeMenuItem(Localize("LOCQuickFilterFavorites", "Favorites"))
            {
                Menu = favoritesMenu
            });
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new NativeMenuItemSeparator());
        }

        menu.Items.Add(CreateItem(Localize("LOCOpenPlaynite", "Open Playnite"), restoreWindow));
        menu.Items.Add(CreateItem(
            Localize("LOCOpenFullscreen", "Open Fullscreen"), openFullscreen, canOpenFullscreen()));
        var clients = new NativeMenu();
        foreach (var plugin in viewModel.LibraryClients)
        {
            try
            {
                if (plugin.Client?.IsInstalled == true)
                {
                    clients.Items.Add(CreateItem(plugin.Name, plugin.Client.Open));
                }
            }
            catch
            {
                // A broken client probe must not prevent the tray from opening.
            }
        }
        menu.Items.Add(new NativeMenuItem(Localize("LOCLibraryClients", "Library clients"))
        {
            Menu = clients,
            IsEnabled = clients.Items.Count > 0
        });

        var tools = new NativeMenu();
        foreach (var app in viewModel.SoftwareTools)
        {
            var item = CreateItem(app.Name, () => viewModel.StartSoftwareTool(app));
            SetIcon(item, viewModel.ResolveDatabaseFile(app.Icon));
            tools.Items.Add(item);
        }
        menu.Items.Add(new NativeMenuItem(Localize("LOCMenuSoftwareTools", "Software tools"))
        {
            Menu = tools,
            IsEnabled = tools.Items.Count > 0
        });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateItem(Localize("LOCExitPlaynite", "Exit Playnite"), requestExit));
    }

    public void Dispose()
    {
        menu.NeedsUpdate -= Menu_NeedsUpdate;
        foreach (var icon in menuIcons)
        {
            icon.Dispose();
        }
        menuIcons.Clear();
        DisposeTrayIcon();
    }

    private NativeMenuItem CreateGameItem(DesktopGameItemViewModel game)
    {
        var item = CreateItem(game.Name, () =>
        {
            restoreWindow();
            viewModel.ActivateGame(game.Game.Id);
        });
        SetIcon(item, game.IconPath);
        return item;
    }

    private void SetIcon(NativeMenuItem item, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var icon = new Bitmap(path);
            menuIcons.Add(icon);
            item.Icon = icon;
        }
        catch
        {
            // Unsupported image formats remain valid menu entries without icons.
        }
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

    private static string Localize(string key, string fallback)
        => DesktopLocalization.Resolve(key, fallback);

    private void Menu_NeedsUpdate(object sender, EventArgs e) => RefreshMenu();

    private void TrayIcon_Clicked(object sender, EventArgs e) => restoreWindow();

    private void DisposeTrayIcon()
    {
        if (trayIcon == null)
        {
            return;
        }

        trayIcon.Clicked -= TrayIcon_Clicked;
        trayIcon.IsVisible = false;
        trayIcon.Dispose();
        trayIcon = null;
    }
}
