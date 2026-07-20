using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ICommand = System.Windows.Input.ICommand;
using Key = Avalonia.Input.Key;

namespace Playnite.Avalonia.Input;

public enum GamepadButton
{
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Confirm,
    Cancel,
    Start,
    Back,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    LeftStick,
    RightStick,
    TriggerLeft,
    TriggerRight,
    Guide
}

/// <summary>
/// Cross-platform bridge from SDL button state to Avalonia routed input and
/// explicit commands. It replaces WPF InputManager and WM_KEYDOWN injection.
/// </summary>
public sealed class GamepadInputBridge : IDisposable
{
    private readonly TopLevel topLevel;
    private TopLevel activeTopLevel;
    private readonly Dictionary<GamepadButton, ICommand> commandMap = new();
    private readonly Dictionary<GamepadButton, DispatcherTimer> repeatTimers = new();
    private bool disposed;

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
        activeTopLevel = topLevel;
    }

    public IDisposable RedirectTo(TopLevel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        TopLevel previous = null;
        RunOnUiThread(() =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            previous = activeTopLevel;
            activeTopLevel = target;
        });
        return new InputTargetScope(this, previous);
    }

    public void MapCommand(GamepadButton button, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RunOnUiThread(() =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            commandMap[button] = command;
        });
    }

    public void UnmapCommand(GamepadButton button)
    {
        RunOnUiThread(() =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            commandMap.Remove(button);
        });
    }

    public void ButtonDown(GamepadButton button)
    {
        RunOnUiThread(() => ButtonDownCore(button));
    }

    private void ButtonDownCore(GamepadButton button)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Dispatch(button);

        if (!navigationKeys.ContainsKey(button))
        {
            return;
        }

        ButtonUpCore(button);
        var timer = new DispatcherTimer { Interval = RepeatDelay };
        timer.Tick += (_, _) =>
        {
            timer.Interval = RepeatRate;
            if (!disposed)
            {
                Dispatch(button);
            }
        };
        repeatTimers[button] = timer;
        timer.Start();
    }

    public void ButtonUp(GamepadButton button)
    {
        RunOnUiThread(() => ButtonUpCore(button));
    }

    private void ButtonUpCore(GamepadButton button)
    {
        if (repeatTimers.Remove(button, out var timer))
        {
            timer.Stop();
        }
    }

    public void Dispose()
    {
        RunOnUiThread(DisposeCore);
    }

    private void DisposeCore()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var timer in repeatTimers.Values)
        {
            timer.Stop();
        }

        repeatTimers.Clear();
        commandMap.Clear();
    }

    private void Dispatch(GamepadButton button)
    {
        var targetTopLevel = activeTopLevel ?? topLevel;
        if (ReferenceEquals(targetTopLevel, topLevel) &&
            commandMap.TryGetValue(button, out var command))
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
                return;
            }
        }

        if (navigationKeys.TryGetValue(button, out var key))
        {
            var target = targetTopLevel.FocusManager?.GetFocusedElement() as InputElement ?? targetTopLevel;
            target.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                Source = target
            });
            KeysSynthesized++;
        }
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Input).GetAwaiter().GetResult();
        }
    }

    private sealed class InputTargetScope : IDisposable
    {
        private GamepadInputBridge owner;
        private readonly TopLevel previous;

        public InputTargetScope(GamepadInputBridge owner, TopLevel previous)
        {
            this.owner = owner;
            this.previous = previous;
        }

        public void Dispose()
        {
            var bridge = Interlocked.Exchange(ref owner, null);
            if (bridge == null)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                if (!bridge.disposed)
                {
                    bridge.activeTopLevel = previous ?? bridge.topLevel;
                }
            });
        }
    }
}
