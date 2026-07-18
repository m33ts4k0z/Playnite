using Avalonia;
using Avalonia.Controls;
using Playnite.Avalonia.Controls;
using Playnite.SDK.Models;
using System.ComponentModel;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

internal sealed class V7RemotePluginElementHost : ContentControl, IPluginElementContextSink
{
    private readonly object instance;
    private readonly MethodInfo setGameContext;

    public object GameContext
    {
        set => V7Reflection.Invoke(
            instance,
            setGameContext,
            V7DatabaseTransport.Serialize(value as Game));
    }

    public V7RemotePluginElementHost(object instance)
    {
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        var type = instance.GetType();
        setGameContext = V7Reflection.GetRequiredMethod(type, "SetGameContext");
        Content = type.GetProperty("Control", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(instance) as Control
            ?? throw new MissingMemberException(type.FullName, "Control");
    }
}

public sealed class AvaloniaPluginSidebarItem : INotifyPropertyChanged
{
    private readonly object instance;
    private readonly Type type;
    private readonly MethodInfo activate;
    private readonly MethodInfo open;
    private readonly MethodInfo close;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid PluginId { get; }
    public string PluginName { get; }
    public bool IsView => string.Equals(Read<string>(nameof(Type)), "View", StringComparison.Ordinal);
    public object Icon => Read<object>(nameof(Icon));
    public string Title => Read<string>(nameof(Title));
    public bool Visible => Read<bool>(nameof(Visible));
    public double ProgressValue => Read<double>(nameof(ProgressValue));
    public double ProgressMaximum => Read<double>(nameof(ProgressMaximum));
    public Thickness IconPadding => Read<Thickness>(nameof(IconPadding));

    internal AvaloniaPluginSidebarItem(Guid pluginId, string pluginName, object instance)
    {
        PluginId = pluginId;
        PluginName = pluginName;
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        type = instance.GetType();
        activate = V7Reflection.GetRequiredMethod(type, "Activate");
        open = V7Reflection.GetRequiredMethod(type, "Open");
        close = V7Reflection.GetRequiredMethod(type, "Close");
        if (instance is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += (_, args) => PropertyChanged?.Invoke(this, args);
        }
    }

    public void Activate() => V7Reflection.Invoke(instance, activate);
    public Control Open() => (Control)V7Reflection.Invoke(instance, open);
    public void Close() => V7Reflection.Invoke(instance, close);

    private T Read<T>(string name) =>
        (T)(type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance)
            ?? default(T));
}

public sealed class AvaloniaPluginTopPanelItem : INotifyPropertyChanged
{
    private readonly object instance;
    private readonly Type type;
    private readonly MethodInfo activate;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid PluginId { get; }
    public string PluginName { get; }
    public object Icon => Read<object>(nameof(Icon));
    public string Title => Read<string>(nameof(Title));
    public bool Visible => Read<bool>(nameof(Visible));

    internal AvaloniaPluginTopPanelItem(Guid pluginId, string pluginName, object instance)
    {
        PluginId = pluginId;
        PluginName = pluginName;
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        type = instance.GetType();
        activate = V7Reflection.GetRequiredMethod(type, "Activate");
        if (instance is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += (_, args) => PropertyChanged?.Invoke(this, args);
        }
    }

    public void Activate() => V7Reflection.Invoke(instance, activate);

    private T Read<T>(string name) =>
        (T)(type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance)
            ?? default(T));
}
