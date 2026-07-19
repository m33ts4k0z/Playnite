using System.Collections.ObjectModel;
using Playnite.Avalonia.Input;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenInputSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool swapConfirmCancelButtons;
    private bool swapStartDetailsAction;
    private bool guideButtonFocus;
    private bool hideMouseCursor;
    private bool enableGameControllerSupport;
    private Func<IReadOnlyList<SdlGameControllerDevice>> getDevices =
        () => Array.Empty<SdlGameControllerDevice>();
    private Action<bool, IReadOnlyCollection<string>> applyControllerSettings = (_, _) => { };

    public override string Key => "Input";
    public override string Title => "Input";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool SwapConfirmCancelButtons
    {
        get => swapConfirmCancelButtons;
        set => SetField(ref swapConfirmCancelButtons, value);
    }
    public bool SwapStartDetailsAction
    {
        get => swapStartDetailsAction;
        set => SetField(ref swapStartDetailsAction, value);
    }
    public bool GuideButtonFocus
    {
        get => guideButtonFocus;
        set => SetField(ref guideButtonFocus, value);
    }
    public bool HideMouseCursor
    {
        get => hideMouseCursor;
        set => SetField(ref hideMouseCursor, value);
    }
    public bool EnableGameControllerSupport
    {
        get => enableGameControllerSupport;
        set => SetField(ref enableGameControllerSupport, value);
    }
    public ObservableCollection<GameControllerSettingsOption> Controllers { get; } = new();
    public string ControllerStatus => Controllers.Count == 0
        ? "No compatible controllers are currently connected."
        : $"{Controllers.Count:N0} compatible controller(s) connected.";

    public FullscreenInputSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenInputSettingsView { DataContext = this };
    }

    public void ConfigureControllerSource(
        Func<IReadOnlyList<SdlGameControllerDevice>> getDevices,
        Action<bool, IReadOnlyCollection<string>> applyControllerSettings)
    {
        this.getDevices = getDevices ?? (() => Array.Empty<SdlGameControllerDevice>());
        this.applyControllerSettings = applyControllerSettings ?? ((_, _) => { });
    }

    public override void Open()
    {
        swapConfirmCancelButtons = settings.SwapConfirmCancelButtons;
        swapStartDetailsAction = settings.SwapStartDetailsAction;
        guideButtonFocus = settings.GuideButtonFocus;
        hideMouseCursor = settings.HideMouseCursor;
        enableGameControllerSupport = settings.EnableGameControllerSupport;
        OnPropertyChanged(nameof(SwapConfirmCancelButtons));
        OnPropertyChanged(nameof(SwapStartDetailsAction));
        OnPropertyChanged(nameof(GuideButtonFocus));
        OnPropertyChanged(nameof(HideMouseCursor));
        OnPropertyChanged(nameof(EnableGameControllerSupport));
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

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.SwapConfirmCancelButtons = SwapConfirmCancelButtons;
        settings.SwapStartDetailsAction = SwapStartDetailsAction;
        settings.GuideButtonFocus = GuideButtonFocus;
        settings.HideMouseCursor = HideMouseCursor;
        settings.EnableGameControllerSupport = EnableGameControllerSupport;
        var connectedIds = Controllers.Select(controller => controller.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        settings.DisabledGameControllers = settings.DisabledGameControllers
            .Where(id => !connectedIds.Contains(id))
            .Concat(Controllers.Where(controller => !controller.IsEnabled).Select(controller => controller.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        applyControllerSettings(settings.EnableGameControllerSupport, settings.DisabledGameControllers);
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenInputSettingsView,
            "button mapping, guide focus, cursor, SDL processing, and per-device policy are configurable");
}
