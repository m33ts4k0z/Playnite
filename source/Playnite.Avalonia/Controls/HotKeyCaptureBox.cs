using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Playnite.Avalonia.App.Services;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Cross-platform hotkey editor. Delete or Backspace clears the value; modifier-only
/// key presses are ignored until a non-modifier key completes the gesture.
/// </summary>
public sealed class HotKeyCaptureBox : TextBox
{
    public static readonly StyledProperty<HotKey> HotKeyProperty =
        AvaloniaProperty.Register<HotKeyCaptureBox, HotKey>(nameof(HotKey));

    public HotKey HotKey
    {
        get => GetValue(HotKeyProperty);
        set => SetValue(HotKeyProperty, value);
    }

    public HotKeyCaptureBox()
    {
        IsReadOnly = true;
        PlaceholderText = "Press a shortcut";
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HotKeyProperty)
        {
            Text = HotKey?.ToString() ?? string.Empty;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back)
        {
            HotKey = null;
            e.Handled = true;
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or
            Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        HotKey = new HotKey(e.Key, e.KeyModifiers);
        e.Handled = true;
    }
}
