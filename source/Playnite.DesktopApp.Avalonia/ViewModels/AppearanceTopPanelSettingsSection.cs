using Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class AppearanceTopPanelSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private Dock pluginTopPanelAlignment;

    public override string Key => "AppearanceTopPanel";
    public override string Title => "Appearance — Top panel";
    public override Control Content { get; }
    public IReadOnlyList<Dock> AlignmentOptions { get; } = new[] { Dock.Left, Dock.Right };
    public Dock PluginTopPanelAlignment { get => pluginTopPanelAlignment; set => SetField(ref pluginTopPanelAlignment, value); }

    public AppearanceTopPanelSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new AppearanceTopPanelSettingsView { DataContext = this };
    }

    public override void Open() => PluginTopPanelAlignment = settings.PluginTopPanelAlignment;

    public override SettingsSectionSaveResult Save()
    {
        settings.PluginTopPanelAlignment = PluginTopPanelAlignment is Dock.Left or Dock.Right
            ? PluginTopPanelAlignment
            : Dock.Right;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck() => new(
        Key,
        Content.DataContext == this && AlignmentOptions.Contains(PluginTopPanelAlignment),
        "plugin top-panel alignment has a portable left/right working copy");
}
