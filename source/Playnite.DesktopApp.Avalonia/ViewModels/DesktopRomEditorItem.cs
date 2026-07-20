using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.ViewModels;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopRomEditorItem : INotifyPropertyChanged
{
    private string name;
    private string path;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Name { get => name; set => SetField(ref name, value); }
    public string Path { get => path; set => SetField(ref path, value); }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand SelectPathCommand { get; }
    public ICommand RemoveCommand { get; }

    public DesktopRomEditorItem(
        GameRom rom,
        Action<DesktopRomEditorItem> moveUp,
        Action<DesktopRomEditorItem> moveDown,
        Action<DesktopRomEditorItem> remove,
        Action<DesktopRomEditorItem> selectPath)
    {
        name = rom?.Name ?? string.Empty;
        path = rom?.Path ?? string.Empty;
        MoveUpCommand = new RelayCommand(() => moveUp?.Invoke(this));
        MoveDownCommand = new RelayCommand(() => moveDown?.Invoke(this));
        SelectPathCommand = new RelayCommand(() => selectPath?.Invoke(this));
        RemoveCommand = new RelayCommand(() => remove?.Invoke(this));
    }

    public GameRom ToGameRom() => new(Name?.Trim(), Path?.Trim());

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
