using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using Playnite.Avalonia.App.Services;
using Playnite.Plugins;
using Playnite.WpfPluginSupport;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopPluginSettingsViewModel : INotifyPropertyChanged
{
    private readonly WpfPluginSettingsHost settingsHost;
    private readonly Action<string, bool> showMessage;
    private ExtensionFactory extensions;
    private IReadOnlyList<V7LoadedPlugin> v7Plugins = [];
    private V7LoadedPlugin activeV7Plugin;
    private Func<IntPtr> ownerHandle = () => IntPtr.Zero;
    private bool isVisible;
    private bool isRunning;
    private DesktopPluginSettingsOption selectedPlugin;
    private string statusText = string.Empty;
    private string errorText;
    private Control avaloniaSettingsView;

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
    public Control AvaloniaSettingsView
    {
        get => avaloniaSettingsView;
        private set => SetField(ref avaloniaSettingsView, value);
    }
    public bool IsAvaloniaSettingsVisible => activeV7Plugin != null && AvaloniaSettingsView != null;
    public bool IsPluginListVisible => !IsAvaloniaSettingsVisible;

    public ICommand OpenSelectedCommand { get; }
    public ICommand SaveAvaloniaSettingsCommand { get; }
    public ICommand CancelAvaloniaSettingsCommand { get; }
    public ICommand CloseCommand { get; }

    public DesktopPluginSettingsViewModel(Action<string, bool> showMessage)
    {
        this.showMessage = showMessage ?? ((_, _) => { });
        settingsHost = new WpfPluginSettingsHost();
        OpenSelectedCommand = new AppRelayCommand(
            () => OpenSettings(SelectedPlugin.Id),
            () => IsVisible && !IsRunning && SelectedPlugin != null);
        SaveAvaloniaSettingsCommand = new AppRelayCommand(
            () => SaveAvaloniaSettings(),
            () => IsAvaloniaSettingsVisible && !IsRunning);
        CancelAvaloniaSettingsCommand = new AppRelayCommand(
            CancelAvaloniaSettings,
            () => IsAvaloniaSettingsVisible && !IsRunning);
        CloseCommand = new AppRelayCommand(Close, () => !IsRunning);
    }

    public void Configure(
        ExtensionFactory extensionFactory,
        IReadOnlyList<V7LoadedPlugin> sdkSevenPlugins = null)
    {
        extensions = extensionFactory;
        v7Plugins = sdkSevenPlugins ?? [];
    }

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
        foreach (var plugin in v7Plugins
                     .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Plugins.Add(new DesktopPluginSettingsOption(
                plugin.Id,
                plugin.Name,
                GetV7PluginTypeName(plugin.Kind),
                plugin.HasSettings,
                true));
        }

        SelectedPlugin = Plugins.FirstOrDefault();
        StatusText = Plugins.Count == 0
            ? "No loaded plugins are available."
            : "Choose a plugin to open its settings view.";
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

        var v7Plugin = v7Plugins.FirstOrDefault(plugin => plugin.Id == pluginId);
        if (v7Plugin != null)
        {
            return OpenAvaloniaSettings(v7Plugin);
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

    private bool OpenAvaloniaSettings(V7LoadedPlugin plugin)
    {
        IsRunning = true;
        ErrorText = null;
        StatusText = $"Opening Avalonia settings for {plugin.Name}…";
        try
        {
            var view = plugin.BeginSettingsEdit();
            if (view == null)
            {
                ErrorText = $"{plugin.Name} does not provide both a settings object and Avalonia settings view.";
                StatusText = ErrorText;
                showMessage(ErrorText, false);
                return false;
            }

            activeV7Plugin = plugin;
            AvaloniaSettingsView = view;
            IsVisible = true;
            StatusText = $"Editing settings for {plugin.Name}.";
            OnPropertyChanged(nameof(IsAvaloniaSettingsVisible));
            OnPropertyChanged(nameof(IsPluginListVisible));
            RaiseCommandStates();
            return true;
        }
        catch (Exception exception)
        {
            ErrorText = $"Failed to open settings for {plugin.Name}: {exception.Message}";
            StatusText = ErrorText;
            showMessage(ErrorText, true);
            return false;
        }
        finally
        {
            IsRunning = false;
        }
    }

    public bool SaveAvaloniaSettings()
    {
        if (activeV7Plugin == null || IsRunning)
        {
            return false;
        }

        IsRunning = true;
        ErrorText = null;
        var plugin = activeV7Plugin;
        try
        {
            var validation = plugin.VerifySettings();
            if (!validation.IsValid)
            {
                ErrorText = validation.Errors.Count == 0
                    ? $"{plugin.Name} rejected its settings."
                    : string.Join(Environment.NewLine, validation.Errors);
                StatusText = ErrorText;
                showMessage(ErrorText, true);
                return false;
            }

            plugin.EndSettingsEdit();
            ClearAvaloniaSettings();
            StatusText = $"Settings for {plugin.Name} were saved.";
            showMessage(StatusText, false);
            IsVisible = false;
            return true;
        }
        catch (Exception exception)
        {
            ErrorText = $"Failed to save settings for {plugin.Name}: {exception.Message}";
            StatusText = ErrorText;
            showMessage(ErrorText, true);
            return false;
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void CancelAvaloniaSettings()
    {
        if (activeV7Plugin == null)
        {
            return;
        }

        var plugin = activeV7Plugin;
        try
        {
            plugin.CancelSettingsEdit();
            StatusText = $"Settings changes for {plugin.Name} were cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = $"Failed to cancel settings for {plugin.Name}: {exception.Message}";
            showMessage(ErrorText, true);
        }
        finally
        {
            ClearAvaloniaSettings();
            IsVisible = false;
        }
    }

    private void ClearAvaloniaSettings()
    {
        activeV7Plugin = null;
        AvaloniaSettingsView = null;
        OnPropertyChanged(nameof(IsAvaloniaSettingsVisible));
        OnPropertyChanged(nameof(IsPluginListVisible));
        RaiseCommandStates();
    }

    public void Close()
    {
        if (!IsRunning)
        {
            if (activeV7Plugin != null)
            {
                CancelAvaloniaSettings();
            }
            else
            {
                IsVisible = false;
            }
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

    private static string GetV7PluginTypeName(string kind) => kind switch
    {
        "LibraryPlugin" => "SDK v7 library integration",
        "MetadataPlugin" => "SDK v7 metadata provider",
        "GenericPlugin" => "SDK v7 generic plugin",
        _ => "SDK v7 plugin"
    };

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)OpenSelectedCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)SaveAvaloniaSettingsCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CancelAvaloniaSettingsCommand).RaiseCanExecuteChanged();
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
    public bool IsSdkSeven { get; }
    public string SettingsState => HasAdvertisedSettings
        ? IsSdkSeven ? "Native Avalonia settings" : "Settings view advertised"
        : "Plugin may not provide settings";

    public DesktopPluginSettingsOption(
        Guid id,
        string name,
        string pluginType,
        bool hasAdvertisedSettings,
        bool isSdkSeven = false)
    {
        Id = id;
        Name = name;
        PluginType = pluginType;
        HasAdvertisedSettings = hasAdvertisedSettings;
        IsSdkSeven = isSdkSeven;
    }
}
