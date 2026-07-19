using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class ClientShutdownSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Func<IReadOnlyList<LibraryPlugin>> libraryPlugins;
    private bool shutdownLibraryClients;
    private decimal clientShutdownGraceSeconds;
    private decimal clientShutdownMinimumSessionSeconds;

    public override string Key => "ClientShutdown";
    public override string Title => "Behavior — Client shutdown";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<ClientShutdownPluginOption> Plugins { get; } = new();

    public bool ShutdownLibraryClients
    {
        get => shutdownLibraryClients;
        set => SetField(ref shutdownLibraryClients, value);
    }
    public decimal ClientShutdownGraceSeconds
    {
        get => clientShutdownGraceSeconds;
        set => SetField(ref clientShutdownGraceSeconds, Math.Clamp(value, 0, 4096));
    }
    public decimal ClientShutdownMinimumSessionSeconds
    {
        get => clientShutdownMinimumSessionSeconds;
        set => SetField(ref clientShutdownMinimumSessionSeconds, Math.Clamp(value, 0, 4096));
    }

    public ClientShutdownSettingsSection(
        DesktopSettings settings,
        Func<IReadOnlyList<LibraryPlugin>> libraryPlugins)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.libraryPlugins = libraryPlugins ?? (() => Array.Empty<LibraryPlugin>());
        Content = new ClientShutdownSettingsView { DataContext = this };
    }

    public override void Open()
    {
        ShutdownLibraryClients = settings.ShutdownLibraryClients;
        ClientShutdownGraceSeconds = settings.ClientShutdownGraceSeconds;
        ClientShutdownMinimumSessionSeconds = settings.ClientShutdownMinimumSessionSeconds;
        var selected = (settings.ClientShutdownPluginIds ?? new List<Guid>()).ToHashSet();
        Plugins.Clear();
        foreach (var plugin in libraryPlugins()
            .Where(plugin => plugin != null && plugin.Properties?.CanShutdownClient == true)
            .GroupBy(plugin => plugin.Id)
            .Select(group => group.First())
            .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Plugins.Add(new ClientShutdownPluginOption(plugin.Id, plugin.Name, selected.Contains(plugin.Id)));
        }
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.ShutdownLibraryClients = ShutdownLibraryClients;
        settings.ClientShutdownGraceSeconds = decimal.ToUInt32(ClientShutdownGraceSeconds);
        settings.ClientShutdownMinimumSessionSeconds = decimal.ToUInt32(ClientShutdownMinimumSessionSeconds);
        settings.ClientShutdownPluginIds = Plugins
            .Where(plugin => plugin.IsSelected)
            .Select(plugin => plugin.Id)
            .ToList();
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this && Plugins.All(plugin => plugin.Id != Guid.Empty);
        return new(Key, valid, valid
            ? $"grace/session policy and {Plugins.Count} shutdown-capable library client(s) are configurable"
            : "library-client shutdown options are incomplete");
    }
}

public sealed class ClientShutdownPluginOption : INotifyPropertyChanged
{
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public Guid Id { get; }
    public string Name { get; }
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public ClientShutdownPluginOption(Guid id, string name, bool isSelected)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id.ToString() : name;
        this.isSelected = isSelected;
    }
}
