using System.Windows.Input;

namespace Playnite.Avalonia.App.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action execute;
    private readonly Action<object> executeWithParameter;
    private readonly Func<bool> canExecute;

    public event EventHandler CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public RelayCommand(Action<object> execute, Func<bool> canExecute = null)
    {
        executeWithParameter = execute;
        this.canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object parameter)
    {
        if (CanExecute(parameter))
        {
            if (executeWithParameter != null)
            {
                executeWithParameter(parameter);
            }
            else
            {
                execute();
            }
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
