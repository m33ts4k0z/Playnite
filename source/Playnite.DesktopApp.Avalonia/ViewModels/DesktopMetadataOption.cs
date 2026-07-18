using System.ComponentModel;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopMetadataOption : INotifyPropertyChanged
{
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid Id { get; }
    public string Name { get; }
    public bool IsSelected
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

    public DesktopMetadataOption(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;
}
