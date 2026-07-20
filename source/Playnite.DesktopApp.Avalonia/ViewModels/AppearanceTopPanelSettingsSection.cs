using Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceTopPanelSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private Dock pluginTopPanelAlignment;
    private bool showLibrarySummary;
    private bool showNotifications;
    private bool showFilterStatus;
    private bool showUpdateStatus;

    public override string Key => "AppearanceTopPanel";
    public override string Title => "Appearance — Top panel";
    public override Control Content { get; }
    public IReadOnlyList<Dock> AlignmentOptions { get; } = new[] { Dock.Left, Dock.Right };
    public Dock PluginTopPanelAlignment { get => pluginTopPanelAlignment; set => SetField(ref pluginTopPanelAlignment, value); }
    public bool ShowLibrarySummary { get => showLibrarySummary; set => SetField(ref showLibrarySummary, value); }
    public bool ShowNotifications { get => showNotifications; set => SetField(ref showNotifications, value); }
    public bool ShowFilterStatus { get => showFilterStatus; set => SetField(ref showFilterStatus, value); }
    public bool ShowUpdateStatus { get => showUpdateStatus; set => SetField(ref showUpdateStatus, value); }

    public AppearanceTopPanelSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceTopPanelSettingsView { DataContext = this };
    }

    public override void Open()
    {
        PluginTopPanelAlignment = settings.PluginTopPanelAlignment;
        ShowLibrarySummary = settings.TopPanelShowLibrarySummary;
        ShowNotifications = settings.TopPanelShowNotifications;
        ShowFilterStatus = settings.TopPanelShowFilterStatus;
        ShowUpdateStatus = settings.TopPanelShowUpdateStatus;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.PluginTopPanelAlignment = PluginTopPanelAlignment is Dock.Left or Dock.Right
            ? PluginTopPanelAlignment
            : Dock.Right;
        settings.TopPanelShowLibrarySummary = ShowLibrarySummary;
        settings.TopPanelShowNotifications = ShowNotifications;
        settings.TopPanelShowFilterStatus = ShowFilterStatus;
        settings.TopPanelShowUpdateStatus = ShowUpdateStatus;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck() => new(
        Key,
        Content.DataContext == this && AlignmentOptions.Contains(PluginTopPanelAlignment),
        "plugin alignment and built-in top-panel visibility use a portable working copy");
}
