using Playnite.Avalonia.Input;

namespace Playnite.FullscreenApp.Avalonia.Input;

/// <summary>
/// Fullscreen adapter over the shared SDL controller manager. Device inventory,
/// disabled-device policy and state aggregation remain reusable by Desktop.
/// </summary>
public sealed class SdlGamepadInputSource : IDisposable
{
    private readonly GamepadInputBridge bridge;
    private readonly SdlGameControllerManager manager;

    public int ControllerCount => manager.Devices.Count;
    public bool IsAvailable => manager.IsAvailable;
    public string Status => manager.Status;
    public IReadOnlyList<SdlGameControllerDevice> Devices => manager.Devices;

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

    public void Start() => manager.Start();

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
