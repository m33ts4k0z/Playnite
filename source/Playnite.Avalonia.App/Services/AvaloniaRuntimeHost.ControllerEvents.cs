using Playnite.Avalonia.Input;
using Playnite.SDK.Events;

namespace Playnite.Avalonia.App.Services;

public sealed partial class AvaloniaRuntimeHost
{
    public void NotifyControllerButtonStateChanged(GamepadButton button, bool isPressed)
    {
        var input = MapControllerInput(button);
        var state = isPressed ? ControllerInputState.Pressed : ControllerInputState.Released;
        var args = new OnControllerButtonStateChangedArgs(input, state);
        foreach (var loaded in extensions.Plugins.Values)
        {
            try
            {
                loaded.Plugin.OnControllerButtonStateChanged(args);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"Plugin {loaded.Description.Name} controller event failed: {exception.Message}");
            }
        }

        v7Plugins.NotifyControllerButtonStateChanged((int)input, (int)state);
    }

    public void NotifyControllerConnected(SdlGameControllerDevice device) =>
        NotifyControllerConnection(device, true);

    public void NotifyControllerDisconnected(SdlGameControllerDevice device) =>
        NotifyControllerConnection(device, false);

    private void NotifyControllerConnection(SdlGameControllerDevice device, bool connected)
    {
        ArgumentNullException.ThrowIfNull(device);
        var controller = new GamepadController
        {
            InstanceId = device.InstanceId,
            Path = device.Id,
            Name = device.Name,
            Enabled = device.IsEnabled
        };
        foreach (var loaded in extensions.Plugins.Values)
        {
            try
            {
                if (connected)
                {
                    loaded.Plugin.OnControllerConnected(new OnControllerConnectedArgs { Controller = controller });
                }
                else
                {
                    loaded.Plugin.OnControllerDisconnected(new OnControllerDisconnectedArgs { Controller = controller });
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                callbacks.SetStatus(
                    $"Plugin {loaded.Description.Name} controller connection event failed: {exception.Message}");
            }
        }

        v7Plugins.NotifyControllerConnection(
            connected,
            device.InstanceId,
            device.Id,
            device.Name,
            device.IsEnabled);
    }

    internal static ControllerInput MapControllerInput(GamepadButton button) => button switch
    {
        GamepadButton.Start => ControllerInput.Start,
        GamepadButton.Back => ControllerInput.Back,
        GamepadButton.LeftStick => ControllerInput.LeftStick,
        GamepadButton.RightStick => ControllerInput.RightStick,
        GamepadButton.LeftShoulder => ControllerInput.LeftShoulder,
        GamepadButton.RightShoulder => ControllerInput.RightShoulder,
        GamepadButton.Guide => ControllerInput.Guide,
        GamepadButton.Confirm => ControllerInput.A,
        GamepadButton.Cancel => ControllerInput.B,
        GamepadButton.X => ControllerInput.X,
        GamepadButton.Y => ControllerInput.Y,
        GamepadButton.DPadLeft => ControllerInput.DPadLeft,
        GamepadButton.DPadRight => ControllerInput.DPadRight,
        GamepadButton.DPadUp => ControllerInput.DPadUp,
        GamepadButton.DPadDown => ControllerInput.DPadDown,
        GamepadButton.TriggerLeft => ControllerInput.TriggerLeft,
        GamepadButton.TriggerRight => ControllerInput.TriggerRight,
        _ => ControllerInput.None
    };
}
