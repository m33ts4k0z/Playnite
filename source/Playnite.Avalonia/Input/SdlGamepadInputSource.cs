namespace Playnite.Avalonia.Input;

/// <summary>
/// Connects the shared SDL controller inventory to routed Avalonia gamepad
/// input. Both shells use the same live enable/disable and device policy.
/// </summary>
public sealed class SdlGamepadInputSource : IDisposable
{
    private readonly GamepadInputBridge bridge;
    private readonly SdlGameControllerManager manager;

    public event EventHandler DevicesChanged
    {
        add => manager.DevicesChanged += value;
        remove => manager.DevicesChanged -= value;
    }

    public int ControllerCount => manager.Devices.Count;
    public bool IsAvailable => manager.IsAvailable;
    public bool IsStarted => manager.IsStarted;
    public bool InputEnabled => manager.InputEnabled;
    public string Status => manager.InputEnabled
        ? manager.Status
        : "SDL game-controller input is disabled in settings.";
    public IReadOnlyList<SdlGameControllerDevice> Devices => manager.Devices;
    public IReadOnlyCollection<string> DisabledControllerIds => manager.DisabledControllerIds;

    public SdlGamepadInputSource(
        GamepadInputBridge bridge,
        bool inputEnabled = true,
        IEnumerable<string> disabledControllerIds = null)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        manager = new SdlGameControllerManager
        {
            InputEnabled = inputEnabled
        };
        manager.SetDisabledControllerIds(disabledControllerIds);
        manager.ButtonStateChanged += Manager_ButtonStateChanged;
    }

    public void Start()
    {
        if (manager.InputEnabled)
        {
            manager.Start();
        }
    }

    public void ApplySettings(bool inputEnabled, IEnumerable<string> disabledControllerIds)
    {
        manager.SetDisabledControllerIds(disabledControllerIds);
        manager.InputEnabled = inputEnabled;
        if (inputEnabled && !manager.IsStarted)
        {
            manager.Start();
        }
    }

    public void Dispose()
    {
        manager.ButtonStateChanged -= Manager_ButtonStateChanged;
        manager.Dispose();
    }

    private void Manager_ButtonStateChanged(object sender, GamepadButtonStateChangedEventArgs e)
    {
        if (e.IsPressed)
        {
            bridge.ButtonDown(e.Button);
        }
        else
        {
            bridge.ButtonUp(e.Button);
        }
    }
}
