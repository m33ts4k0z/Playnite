using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenMenuSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool showRestart;
    private bool showShutdown;
    private bool showSuspend;
    private bool showHibernate;
    private bool showMinimize;
    private bool showLogout;
    private bool showLock;
    private bool showTools;
    private bool showExtensions;
    private bool showClients;

    public override string Key => "Menus";
    public override string Title => "Menus";
    public override global::Avalonia.Controls.Control Content { get; }
    public bool ShowRestart { get => showRestart; set => SetField(ref showRestart, value); }
    public bool ShowShutdown { get => showShutdown; set => SetField(ref showShutdown, value); }
    public bool ShowSuspend { get => showSuspend; set => SetField(ref showSuspend, value); }
    public bool ShowHibernate { get => showHibernate; set => SetField(ref showHibernate, value); }
    public bool ShowMinimize { get => showMinimize; set => SetField(ref showMinimize, value); }
    public bool ShowLogout { get => showLogout; set => SetField(ref showLogout, value); }
    public bool ShowLock { get => showLock; set => SetField(ref showLock, value); }
    public bool ShowTools { get => showTools; set => SetField(ref showTools, value); }
    public bool ShowExtensions { get => showExtensions; set => SetField(ref showExtensions, value); }
    public bool ShowClients { get => showClients; set => SetField(ref showClients, value); }

    public FullscreenMenuSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenMenuSettingsView { DataContext = this };
    }

    public override void Open()
    {
        showRestart = settings.MainMenuShowRestart;
        showShutdown = settings.MainMenuShowShutdown;
        showSuspend = settings.MainMenuShowSuspend;
        showHibernate = settings.MainMenuShowHibernate;
        showMinimize = settings.MainMenuShowMinimize;
        showLogout = settings.MainMenuShowLogout;
        showLock = settings.MainMenuShowLock;
        showTools = settings.MainMenuShowTools;
        showExtensions = settings.MainMenuShowExtensions;
        showClients = settings.MainMenuShowClients;
        OnPropertyChanged(string.Empty);
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.MainMenuShowRestart = ShowRestart;
        settings.MainMenuShowShutdown = ShowShutdown;
        settings.MainMenuShowSuspend = ShowSuspend;
        settings.MainMenuShowHibernate = ShowHibernate;
        settings.MainMenuShowMinimize = ShowMinimize;
        settings.MainMenuShowLogout = ShowLogout;
        settings.MainMenuShowLock = ShowLock;
        settings.MainMenuShowTools = ShowTools;
        settings.MainMenuShowExtensions = ShowExtensions;
        settings.MainMenuShowClients = ShowClients;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenMenuSettingsView,
            "All ten configurable Fullscreen menu items are registered");
}
