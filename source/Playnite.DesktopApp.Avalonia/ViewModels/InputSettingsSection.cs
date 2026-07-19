using System.Collections.ObjectModel;
using Playnite.Avalonia.Input;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class InputSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private Func<IReadOnlyList<SdlGameControllerDevice>> getDevices =
        () => Array.Empty<SdlGameControllerDevice>();
    private Action<bool, IReadOnlyCollection<string>> applySettings = (_, _) => { };
    private bool enableGameControllerSupport;

    public override string Key => "Input";
    public override string Title => "Input";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<GameControllerSettingsOption> Controllers { get; } = new();

    public bool EnableGameControllerSupport
    {
        get => enableGameControllerSupport;
        set => SetField(ref enableGameControllerSupport, value);
    }

    public string ControllerStatus => Controllers.Count == 0
        ? "No compatible controllers are currently connected."
        : $"{Controllers.Count:N0} compatible controller(s) connected.";

    public InputSettingsSection(DesktopSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Content = new InputSettingsView { DataContext = this };
    }

    public void ConfigureControllerSource(
        Func<IReadOnlyList<SdlGameControllerDevice>> getDevices,
        Action<bool, IReadOnlyCollection<string>> applySettings)
    {
        this.getDevices = getDevices ?? (() => Array.Empty<SdlGameControllerDevice>());
        this.applySettings = applySettings ?? ((_, _) => { });
    }

    public override void Open()
    {
        EnableGameControllerSupport = settings.EnableGameControllerSupport;
        RefreshControllers();
    }

    public void RefreshControllers()
    {
        Controllers.Clear();
        foreach (var device in getDevices())
        {
            Controllers.Add(new GameControllerSettingsOption(
                device.Id,
                device.Name,
                !settings.DisabledGameControllers.Contains(device.Id, StringComparer.OrdinalIgnoreCase)));
        }

        OnPropertyChanged(nameof(ControllerStatus));
    }

    public override SettingsSectionSaveResult Save()
    {
        var connectedIds = Controllers.Select(controller => controller.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disabled = settings.DisabledGameControllers
            .Where(id => !connectedIds.Contains(id))
            .Concat(Controllers.Where(controller => !controller.IsEnabled).Select(controller => controller.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.EnableGameControllerSupport = EnableGameControllerSupport;
        settings.DisabledGameControllers = disabled;
        applySettings(EnableGameControllerSupport, disabled);
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this;
        return new(Key, valid, valid
            ? "SDL controller processing and per-device policy are configurable"
            : "input settings view is not connected");
    }
}
