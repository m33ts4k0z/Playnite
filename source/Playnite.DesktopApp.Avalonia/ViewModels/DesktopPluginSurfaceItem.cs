using Playnite.Avalonia.App.Services;
using Playnite.Avalonia.App.ViewModels;
using System.ComponentModel;
using System.Windows.Input;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopPluginSidebarItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public AvaloniaPluginSidebarItem Source { get; }
    public string Title => Source.Title;
    public string PluginName => Source.PluginName;
    public object Icon => Source.Icon;
    public bool Visible => Source.Visible;
    public bool IsView => Source.IsView;
    public double ProgressValue => Source.ProgressValue;
    public double ProgressMaximum => Source.ProgressMaximum;
    public ICommand Command { get; }

    public DesktopPluginSidebarItem(
        AvaloniaPluginSidebarItem source,
        Action<AvaloniaPluginSidebarItem> activate)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(activate);
        Command = new RelayCommand(() => activate(Source));
        Source.PropertyChanged += Source_PropertyChanged;
    }

    private void Source_PropertyChanged(object sender, PropertyChangedEventArgs args) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
}

public sealed class DesktopPluginTopPanelItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public AvaloniaPluginTopPanelItem Source { get; }
    public string Title => Source.Title;
    public string PluginName => Source.PluginName;
    public object Icon => Source.Icon;
    public bool Visible => Source.Visible;
    public ICommand Command { get; }

    public DesktopPluginTopPanelItem(
        AvaloniaPluginTopPanelItem source,
        Action<AvaloniaPluginTopPanelItem> activate)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(activate);
        Command = new RelayCommand(() => activate(Source));
        Source.PropertyChanged += Source_PropertyChanged;
    }

    private void Source_PropertyChanged(object sender, PropertyChangedEventArgs args) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
}
