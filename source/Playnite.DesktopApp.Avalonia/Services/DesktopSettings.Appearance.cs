using Playnite.SDK;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed partial class DesktopSettings
{
    public string ThemePath { get; set; }
    public string ViewMode { get; set; } = "Grid";
    public SortOrder SortOrder { get; set; } = SortOrder.Name;
    public SortOrderDirection SortDirection { get; set; } = SortOrderDirection.Ascending;
    public GroupableField Grouping { get; set; } = GroupableField.None;
    public Guid ActiveFilterPreset { get; set; }
}
