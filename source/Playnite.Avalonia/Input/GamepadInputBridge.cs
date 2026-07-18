using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Playnite.Avalonia.Input;

public enum GamepadButton
{
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Confirm,
    Cancel,
    Start
}

/// <summary>
/// Cross-platform bridge from SDL button state to Avalonia routed input and
/// explicit commands. It replaces WPF InputManager and WM_KEYDOWN injection.
/// </summary>
public sealed class GamepadInputBridge : IDisposable
{
    private readonly TopLevel topLevel;
    private readonly Dictionary<GamepadButton, ICommand> commandMap = new();
    private readonly Dictionary<GamepadButton, DispatcherTimer> repeatTimers = new();

    private static readonly Dictionary<GamepadButton, Key> navigationKeys = new()
    {
        { GamepadButton.DPadUp, Key.Up },
        { GamepadButton.DPadDown, Key.Down },
        { GamepadButton.DPadLeft, Key.Left },
        { GamepadButton.DPadRight, Key.Right },
        { GamepadButton.Confirm, Key.Enter },
        { GamepadButton.Cancel, Key.Escape }
    };

    public TimeSpan RepeatDelay { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan RepeatRate { get; set; } = TimeSpan.FromMilliseconds(50);
    public int KeysSynthesized { get; private set; }

    public GamepadInputBridge(TopLevel topLevel)
    {
        this.topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
    }

    public void MapCommand(GamepadButton button, ICommand command)
    {
        commandMap[button] = command ?? throw new ArgumentNullException(nameof(command));
    }

    public void ButtonDown(GamepadButton button)
    {
        Dispatch(button);

        if (!navigationKeys.ContainsKey(button))
        {
            return;
        }

        ButtonUp(button);
        var timer = new DispatcherTimer { Interval = RepeatDelay };
        timer.Tick += (_, _) =>
        {
            timer.Interval = RepeatRate;
            Dispatch(button);
        };
        repeatTimers[button] = timer;
        timer.Start();
    }

    public void ButtonUp(GamepadButton button)
    {
        if (repeatTimers.Remove(button, out var timer))
        {
            timer.Stop();
        }
    }

    public void Dispose()
    {
        foreach (var timer in repeatTimers.Values)
        {
            timer.Stop();
        }

        repeatTimers.Clear();
    }

    private void Dispatch(GamepadButton button)
    {
        if (commandMap.TryGetValue(button, out var command))
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            return;
        }

        if (navigationKeys.TryGetValue(button, out var key))
        {
            var target = topLevel.FocusManager?.GetFocusedElement() as InputElement ?? topLevel;
            target.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                Source = target
            });
            KeysSynthesized++;
        }
    }
}
