using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.ViewModels;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopLinkEditorItem : INotifyPropertyChanged
{
    private string name;
    private string url;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Name { get => name; set => SetField(ref name, value); }
    public string Url { get => url; set => SetField(ref url, value); }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand RemoveCommand { get; }

    public DesktopLinkEditorItem(
        Link link,
        Action<DesktopLinkEditorItem> moveUp,
        Action<DesktopLinkEditorItem> moveDown,
        Action<DesktopLinkEditorItem> remove)
    {
        name = link?.Name ?? string.Empty;
        url = link?.Url ?? string.Empty;
        MoveUpCommand = new RelayCommand(() => moveUp?.Invoke(this));
        MoveDownCommand = new RelayCommand(() => moveDown?.Invoke(this));
        RemoveCommand = new RelayCommand(() => remove?.Invoke(this));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
