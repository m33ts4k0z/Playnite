using System.ComponentModel;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopMetadataOption : INotifyPropertyChanged
{
    private bool? isSelected;
    private bool isVisible = true;
    private bool allowIndeterminate;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid Id { get; }
    public string Name { get; }
    public bool? IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value)
            {
                return;
            }

            isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }
    public bool AllowIndeterminate
    {
        get => allowIndeterminate;
        set
        {
            if (allowIndeterminate == value)
            {
                return;
            }

            allowIndeterminate = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllowIndeterminate)));
        }
    }

    public DesktopMetadataOption(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;
}
