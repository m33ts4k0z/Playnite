using Playnite.Avalonia.App.Services;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public GameSearchItemAction PrimaryGameSearchItemAction { get; set; } = GameSearchItemAction.SwitchTo;
    public GameSearchItemAction SecondaryGameSearchItemAction { get; set; } = GameSearchItemAction.Play;
    public bool GlobalSearchOpenWithLegacySearch { get; set; } = true;
    public bool SaveGlobalSearchFilterSettings { get; set; } = true;
    public bool IncludeCommandsInDefaultSearch { get; set; } = true;
    public Dictionary<string, string> CustomSearchKeywords { get; set; } = new();
    public HotKey SystemSearchHotkey { get; set; }
    public SearchWindowVisibilitySettings SearchWindowVisibility { get; set; } = new();
    public bool GlobalSearchIncludeUninstalled { get; set; } = true;
    public bool GlobalSearchIncludeHidden { get; set; }
}
