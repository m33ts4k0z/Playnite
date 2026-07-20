namespace Playnite.Avalonia.App.Services;

public sealed class AvaloniaSelectionItem<T>
{
    public string Name { get; }
    public string Description { get; }
    public T Value { get; }
    public bool Selected { get; set; }

    public AvaloniaSelectionItem(
        string name,
        T value,
        string description = null,
        bool selected = false)
    {
        Name = name ?? string.Empty;
        Description = description;
        Value = value;
        Selected = selected;
    }
}

public sealed class AvaloniaSelectionResult<T>
{
    public bool Confirmed { get; }
    public IReadOnlyList<T> SelectedItems { get; }

    public T SelectedItem => SelectedItems.Count == 0 ? default : SelectedItems[0];

    public AvaloniaSelectionResult(bool confirmed, IReadOnlyList<T> selectedItems)
    {
        Confirmed = confirmed;
        SelectedItems = selectedItems ?? Array.Empty<T>();
    }
}
