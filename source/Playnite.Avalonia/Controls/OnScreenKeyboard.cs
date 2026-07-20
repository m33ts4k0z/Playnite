using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Shared controller-friendly QWERTY keyboard used by fullscreen search and text dialogs.
/// Controller hosts can invoke the public editing methods for their configured button chords.
/// </summary>
public sealed class OnScreenKeyboard : UserControl
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<OnScreenKeyboard, string>(
        nameof(Text),
        string.Empty,
        defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsCapsProperty = AvaloniaProperty.Register<OnScreenKeyboard, bool>(
        nameof(IsCaps));

    private static readonly string[][] keyRows =
    {
        new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
        new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p" },
        new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l" },
        new[] { "z", "x", "c", "v", "b", "n", "m" },
        new[] { "-", "_", ".", ",", ":", ";", "@", "/", "\\" }
    };

    private readonly List<Button> letterButtons = new();

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value ?? string.Empty);
    }

    public bool IsCaps
    {
        get => GetValue(IsCapsProperty);
        private set => SetValue(IsCapsProperty, value);
    }

    public OnScreenKeyboard()
    {
        Focusable = true;
        Content = BuildKeyboard();
    }

    public void AppendKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        Text += IsCaps && key.Length == 1 && char.IsLetter(key[0])
            ? key.ToUpperInvariant()
            : key;
    }

    public void AddSpace() => Text += " ";

    public void Backspace()
    {
        if (!string.IsNullOrEmpty(Text))
        {
            Text = Text[..^1];
        }
    }

    public void Clear() => Text = string.Empty;

    public void ToggleCaps()
    {
        IsCaps = !IsCaps;
        foreach (var button in letterButtons)
        {
            var key = button.Tag as string;
            button.Content = IsCaps ? key?.ToUpperInvariant() : key;
        }
    }

    private Control BuildKeyboard()
    {
        var keyboard = new StackPanel
        {
            Spacing = 7,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        foreach (var row in keyRows)
        {
            var rowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            foreach (var key in row)
            {
                var button = CreateButton(key, () => AppendKey(key));
                if (key.Length == 1 && char.IsLetter(key[0]))
                {
                    letterButtons.Add(button);
                }

                rowPanel.Children.Add(button);
            }

            keyboard.Children.Add(rowPanel);
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        actions.Children.Add(CreateButton("Caps", ToggleCaps, 100));
        actions.Children.Add(CreateButton("Space", AddSpace, 240));
        actions.Children.Add(CreateButton("⌫", Backspace, 100));
        actions.Children.Add(CreateButton("Clear", Clear, 100));
        keyboard.Children.Add(actions);
        return keyboard;
    }

    private static Button CreateButton(string label, Action action, double width = 58)
    {
        var button = new Button
        {
            Content = label,
            Tag = label,
            Width = width,
            MinHeight = 46,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += (_, _) => action();
        return button;
    }
}
