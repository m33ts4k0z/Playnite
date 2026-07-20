using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenMenuItemViewModel
{
    public string Title { get; }
    public string Description { get; }
    public ObservableCollection<FullscreenMenuItemViewModel> Children { get; }
    public ICommand Command { get; }
    public bool HasChildren => Children.Count > 0;

    public FullscreenMenuItemViewModel(
        string title,
        Action action = null,
        IEnumerable<FullscreenMenuItemViewModel> children = null,
        string description = null)
    {
        Title = title ?? string.Empty;
        Description = description;
        Children = new ObservableCollection<FullscreenMenuItemViewModel>(
            children ?? Array.Empty<FullscreenMenuItemViewModel>());
        Command = action == null ? null : new RelayCommand(action);
    }
}
