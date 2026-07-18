using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Playnite.Avalonia.Controls;

public interface IPluginElementContextSink
{
    object GameContext { set; }
}

public static class PluginElementRuntime
{
    public static Func<string, string, object, Control> Resolver { get; set; } = (_, _, _) => null;

    public static event EventHandler RegistrationsChanged;

    public static void NotifyRegistrationsChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RegistrationsChanged?.Invoke(null, EventArgs.Empty);
        }
        else
        {
            Dispatcher.UIThread.Post(
                () => RegistrationsChanged?.Invoke(null, EventArgs.Empty),
                DispatcherPriority.Background);
        }
    }
}

/// <summary>
/// Avalonia theme placeholder for one legacy plugin game-view element. The
/// platform-specific bridge is resolved lazily so loose themes can load before plugins.
/// </summary>
public sealed class PluginElementHost : ContentControl
{
    public static readonly StyledProperty<string> PluginProperty =
        AvaloniaProperty.Register<PluginElementHost, string>(nameof(Plugin));

    public static readonly StyledProperty<string> ElementProperty =
        AvaloniaProperty.Register<PluginElementHost, string>(nameof(Element));

    public static readonly StyledProperty<object> GameContextProperty =
        AvaloniaProperty.Register<PluginElementHost, object>(nameof(GameContext));

    public static readonly DirectProperty<PluginElementHost, bool> HasPluginContentProperty =
        AvaloniaProperty.RegisterDirect<PluginElementHost, bool>(
            nameof(HasPluginContent),
            host => host.HasPluginContent);

    private bool hasPluginContent;
    private bool isListening;

    public string Plugin
    {
        get => GetValue(PluginProperty);
        set => SetValue(PluginProperty, value);
    }

    public string Element
    {
        get => GetValue(ElementProperty);
        set => SetValue(ElementProperty, value);
    }

    public object GameContext
    {
        get => GetValue(GameContextProperty);
        set => SetValue(GameContextProperty, value);
    }

    public bool HasPluginContent
    {
        get => hasPluginContent;
        private set => SetAndRaise(HasPluginContentProperty, ref hasPluginContent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameContextProperty)
        {
            if (Content is IPluginElementContextSink contextSink)
            {
                contextSink.GameContext = change.NewValue;
            }
            else
            {
                TryResolve();
            }
        }
        else if (change.Property == PluginProperty || change.Property == ElementProperty)
        {
            Content = null;
            HasPluginContent = false;
            TryResolve();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!isListening)
        {
            PluginElementRuntime.RegistrationsChanged += PluginRegistrationsChanged;
            isListening = true;
        }

        TryResolve();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (isListening)
        {
            PluginElementRuntime.RegistrationsChanged -= PluginRegistrationsChanged;
            isListening = false;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void PluginRegistrationsChanged(object sender, EventArgs e)
    {
        if (Content == null)
        {
            TryResolve();
        }
    }

    private void TryResolve()
    {
        if (Content != null || string.IsNullOrWhiteSpace(Plugin) || string.IsNullOrWhiteSpace(Element))
        {
            return;
        }

        var resolved = PluginElementRuntime.Resolver(Plugin, Element, GameContext);
        if (resolved == null)
        {
            return;
        }

        Content = resolved;
        HasPluginContent = true;
    }
}
