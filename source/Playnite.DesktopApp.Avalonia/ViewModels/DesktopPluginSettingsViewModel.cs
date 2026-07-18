using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Plugins;
using Playnite.WpfPluginSupport;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopPluginSettingsViewModel : INotifyPropertyChanged
{
    private readonly WpfPluginSettingsHost settingsHost;
    private readonly Action<string, bool> showMessage;
    private ExtensionFactory extensions;
    private Func<IntPtr> ownerHandle = () => IntPtr.Zero;
    private bool isVisible;
    private bool isRunning;
    private DesktopPluginSettingsOption selectedPlugin;
    private string statusText = string.Empty;
    private string errorText;

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<DesktopPluginSettingsOption> Plugins { get; } = new();

    public bool IsVisible
    {
        get => isVisible;
        private set => SetField(ref isVisible, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetField(ref isRunning, value))
            {
                OnPropertyChanged(nameof(CanConfigure));
                RaiseCommandStates();
            }
        }
    }

    public bool CanConfigure => !IsRunning;

    public DesktopPluginSettingsOption SelectedPlugin
    {
        get => selectedPlugin;
        set
        {
            if (SetField(ref selectedPlugin, value))
            {
                ((AppRelayCommand)OpenSelectedCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }
    public string ErrorText { get => errorText; private set => SetField(ref errorText, value); }
    public string PluginSummary => $"{Plugins.Count:N0} loaded plugin(s)";

    public ICommand OpenSelectedCommand { get; }
    public ICommand CloseCommand { get; }

    public DesktopPluginSettingsViewModel(Action<string, bool> showMessage)
    {
        this.showMessage = showMessage ?? ((_, _) => { });
        settingsHost = new WpfPluginSettingsHost();
        OpenSelectedCommand = new AppRelayCommand(
            () => OpenSettings(SelectedPlugin.Id),
            () => IsVisible && !IsRunning && SelectedPlugin != null);
        CloseCommand = new AppRelayCommand(Close, () => !IsRunning);
    }

    public void Configure(ExtensionFactory extensionFactory) => extensions = extensionFactory;

    public void ConfigureOwnerHandle(Func<IntPtr> handleProvider) =>
        ownerHandle = handleProvider ?? (() => IntPtr.Zero);

    internal void ConfigureAutomationForTesting(PluginSettingsAutomation automation) =>
        settingsHost.Automation = automation;

    public bool Open()
    {
        if (extensions == null || IsVisible || IsRunning)
        {
            return false;
        }

        Plugins.Clear();
        foreach (var entry in extensions.Plugins
                     .OrderBy(item => item.Value.Description.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var manifest = entry.Value.Description;
            Plugins.Add(new DesktopPluginSettingsOption(
                entry.Key,
                string.IsNullOrWhiteSpace(manifest.Name) ? entry.Value.Plugin.GetType().Name : manifest.Name,
                GetPluginTypeName(manifest.Type),
                HasAdvertisedSettings(entry.Value.Plugin)));
        }

        SelectedPlugin = Plugins.FirstOrDefault();
        StatusText = Plugins.Count == 0
            ? "No loaded plugins are available."
            : "Choose a plugin to open its legacy settings view.";
        ErrorText = null;
        OnPropertyChanged(nameof(PluginSummary));
        IsVisible = true;
        RaiseCommandStates();
        return true;
    }

    public bool OpenSettings(Guid pluginId)
    {
        if (extensions == null || IsRunning)
        {
            return false;
        }

        if (!extensions.Plugins.TryGetValue(pluginId, out var loaded))
        {
            ErrorText = $"Plugin {pluginId} is not loaded.";
            showMessage(ErrorText, true);
            return false;
        }

        IsRunning = true;
        ErrorText = null;
        var displayName = string.IsNullOrWhiteSpace(loaded.Description.Name)
            ? loaded.Plugin.GetType().Name
            : loaded.Description.Name;
        StatusText = $"Opening settings for {displayName}…";
        try
        {
            var result = settingsHost.Show(loaded.Plugin, displayName, ownerHandle());
            StatusText = result.Message;
            if (result.Failed)
            {
                ErrorText = result.Message;
                showMessage(result.Message, true);
                return false;
            }

            if (!result.ViewAvailable)
            {
                ErrorText = result.Message;
                showMessage(result.Message, false);
                return false;
            }

            if (result.Saved)
            {
                showMessage(result.Message, false);
                IsVisible = false;
                return true;
            }

            return false;
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void Close()
    {
        if (!IsRunning)
        {
            IsVisible = false;
        }
    }

    private static bool HasAdvertisedSettings(Playnite.SDK.Plugins.Plugin plugin)
    {
        var properties = plugin.GetType().GetProperty("Properties")?.GetValue(plugin) as
            Playnite.SDK.Plugins.PluginProperties;
        return properties?.HasSettings == true;
    }

    private static string GetPluginTypeName(ExtensionType type) => type switch
    {
        ExtensionType.GameLibrary => "Library integration",
        ExtensionType.MetadataProvider => "Metadata provider",
        ExtensionType.GenericPlugin => "Generic plugin",
        _ => type.ToString()
    };

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)OpenSelectedCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CloseCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class DesktopPluginSettingsOption
{
    public Guid Id { get; }
    public string Name { get; }
    public string PluginType { get; }
    public bool HasAdvertisedSettings { get; }
    public string SettingsState => HasAdvertisedSettings
        ? "Settings view advertised"
        : "Plugin may not provide settings";

    public DesktopPluginSettingsOption(
        Guid id,
        string name,
        string pluginType,
        bool hasAdvertisedSettings)
    {
        Id = id;
        Name = name;
        PluginType = pluginType;
        HasAdvertisedSettings = hasAdvertisedSettings;
    }
}
