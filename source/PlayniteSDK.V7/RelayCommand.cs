using System.Windows.Input;
using Avalonia.Input;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class RelayCommand : ICommand
{
    private readonly Action execute;
    private readonly Func<bool> canExecute;

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public KeyGesture Gesture { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public event EventHandler CanExecuteChanged;

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public RelayCommand(Action execute, Func<bool> canExecute = null, KeyGesture gesture = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
        Gesture = gesture;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool CanExecute(object parameter) => canExecute?.Invoke() ?? true;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void Execute(object parameter) => execute();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> execute;
    private readonly Predicate<T> canExecute;

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public KeyGesture Gesture { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public event EventHandler CanExecuteChanged;

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public RelayCommand(Action<T> execute, Predicate<T> canExecute = null, KeyGesture gesture = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
        Gesture = gesture;
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool CanExecute(object parameter) => canExecute?.Invoke(ConvertParameter(parameter)) ?? true;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void Execute(object parameter) => execute(ConvertParameter(parameter));
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T ConvertParameter(object parameter) => parameter is T value ? value : default;
}
