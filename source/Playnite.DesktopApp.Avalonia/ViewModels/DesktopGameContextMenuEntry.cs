namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopGameContextMenuEntry
{
    private readonly Action action;

    public string Header { get; }
    public bool IsSeparator { get; }
    public bool IsBold { get; }
    public bool IsEnabled { get; }
    public bool? IsChecked { get; }
    public IReadOnlyList<DesktopGameContextMenuEntry> Children { get; }

    private DesktopGameContextMenuEntry(
        string header,
        Action action,
        IReadOnlyList<DesktopGameContextMenuEntry> children,
        bool isSeparator,
        bool isBold,
        bool isEnabled,
        bool? isChecked)
    {
        Header = header ?? string.Empty;
        this.action = action;
        Children = children ?? Array.Empty<DesktopGameContextMenuEntry>();
        IsSeparator = isSeparator;
        IsBold = isBold;
        IsEnabled = isEnabled;
        IsChecked = isChecked;
    }

    public static DesktopGameContextMenuEntry Command(
        string header,
        Action action,
        bool isBold = false,
        bool isEnabled = true,
        bool? isChecked = null) =>
        new(header, action, null, false, isBold, isEnabled, isChecked);

    public static DesktopGameContextMenuEntry Parent(
        string header,
        IReadOnlyList<DesktopGameContextMenuEntry> children) =>
        new(header, null, children, false, false, children?.Count > 0, null);

    public static DesktopGameContextMenuEntry Separator() =>
        new(string.Empty, null, null, true, false, false, null);

    public void Invoke()
    {
        if (IsEnabled && !IsSeparator && Children.Count == 0)
        {
            action?.Invoke();
        }
    }
}
