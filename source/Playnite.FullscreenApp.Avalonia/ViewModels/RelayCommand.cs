using System.Windows.Input;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object> execute;
    private readonly Func<object, bool> canExecute;

    public event EventHandler CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = _ => execute();
        this.canExecute = canExecute == null ? null : _ => canExecute();
    }

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
