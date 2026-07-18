using Avalonia.Threading;
using Playnite.Avalonia.Input;
using static SDL2.SDL;

namespace Playnite.FullscreenApp.Avalonia.Input;

public sealed class SdlGamepadInputSource : IDisposable
{
    private const short AxisThreshold = 16_383;

    private sealed record Controller(IntPtr Handle, int InstanceId);

    private readonly GamepadInputBridge bridge;
    private readonly List<Controller> controllers = new();
    private readonly Dictionary<GamepadButton, bool> lastStates =
        Enum.GetValues<GamepadButton>().ToDictionary(button => button, _ => false);
    private DispatcherTimer timer;
    private bool initialized;

    public int ControllerCount => controllers.Count;
    public bool IsAvailable { get; private set; }
    public string Status { get; private set; } = "SDL gamepad input has not started.";

    public SdlGamepadInputSource(GamepadInputBridge bridge)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public void Start()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        try
        {
            if (SDL_InitSubSystem(SDL_INIT_GAMECONTROLLER) < 0)
            {
                Status = $"SDL initialization failed: {SDL_GetError()}";
                return;
            }

            IsAvailable = true;
            SDL_GameControllerEventState(SDL_ENABLE);
            var mappingsPath = Path.Combine(AppContext.BaseDirectory, "gamecontrollerdb.txt");
            if (File.Exists(mappingsPath))
            {
                SDL_GameControllerAddMappingsFromFile(mappingsPath);
            }

            for (var index = 0; index < SDL_NumJoysticks(); index++)
            {
                AddController(index);
            }

            Status = $"SDL gamepad input active ({ControllerCount} connected).";
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => Update();
            timer.Start();
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Status = $"SDL gamepad input unavailable: {exception.Message}";
        }
    }

    public void Dispose()
    {
        timer?.Stop();
        timer = null;

        foreach (var controller in controllers)
        {
            SDL_GameControllerClose(controller.Handle);
        }

        controllers.Clear();
        if (IsAvailable)
        {
            SDL_QuitSubSystem(SDL_INIT_GAMECONTROLLER);
        }

        IsAvailable = false;
    }

    private void Update()
    {
        while (SDL_PollEvent(out var sdlEvent) == 1)
        {
            if (sdlEvent.type == SDL_EventType.SDL_CONTROLLERDEVICEADDED)
            {
                AddController(sdlEvent.cdevice.which);
            }
            else if (sdlEvent.type == SDL_EventType.SDL_CONTROLLERDEVICEREMOVED)
            {
                RemoveController(sdlEvent.cdevice.which);
            }
        }

        SDL_GameControllerUpdate();
        var states = Enum.GetValues<GamepadButton>().ToDictionary(button => button, _ => false);
        foreach (var controller in controllers)
        {
            MergeState(states, controller.Handle);
        }

        foreach (var button in states.Keys)
        {
            if (states[button] == lastStates[button])
            {
                continue;
            }

            lastStates[button] = states[button];
            if (states[button])
            {
                bridge.ButtonDown(button);
            }
            else
            {
                bridge.ButtonUp(button);
            }
        }
    }

    private void AddController(int joystickIndex)
    {
        if (SDL_IsGameController(joystickIndex) != SDL_bool.SDL_TRUE)
        {
            return;
        }

        var handle = SDL_GameControllerOpen(joystickIndex);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var joystick = SDL_GameControllerGetJoystick(handle);
        var instanceId = SDL_JoystickInstanceID(joystick);
        if (controllers.Any(controller => controller.InstanceId == instanceId))
        {
            SDL_GameControllerClose(handle);
            return;
        }

        controllers.Add(new Controller(handle, instanceId));
        Status = $"SDL gamepad input active ({ControllerCount} connected).";
    }

    private void RemoveController(int instanceId)
    {
        var controller = controllers.FirstOrDefault(item => item.InstanceId == instanceId);
        if (controller == null)
        {
            return;
        }

        SDL_GameControllerClose(controller.Handle);
        controllers.Remove(controller);
        Status = $"SDL gamepad input active ({ControllerCount} connected).";
    }

    private static void MergeState(Dictionary<GamepadButton, bool> states, IntPtr controller)
    {
        states[GamepadButton.Confirm] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A);
        states[GamepadButton.Cancel] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B);
        states[GamepadButton.X] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X);
        states[GamepadButton.Y] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y);
        states[GamepadButton.Start] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START);
        states[GamepadButton.Back] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK);
        states[GamepadButton.Guide] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_GUIDE);
        states[GamepadButton.LeftShoulder] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER);
        states[GamepadButton.RightShoulder] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER);
        states[GamepadButton.LeftStick] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK);
        states[GamepadButton.RightStick] |= Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK);
        states[GamepadButton.TriggerLeft] |= Axis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT) > AxisThreshold;
        states[GamepadButton.TriggerRight] |= Axis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT) > AxisThreshold;

        var leftX = Axis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
        var leftY = Axis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
        states[GamepadButton.DPadLeft] |=
            Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT) || leftX < -AxisThreshold;
        states[GamepadButton.DPadRight] |=
            Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT) || leftX > AxisThreshold;
        states[GamepadButton.DPadUp] |=
            Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP) || leftY < -AxisThreshold;
        states[GamepadButton.DPadDown] |=
            Pressed(controller, SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN) || leftY > AxisThreshold;
    }

    private static bool Pressed(IntPtr controller, SDL_GameControllerButton button) =>
        SDL_GameControllerGetButton(controller, button) == 1;

    private static short Axis(IntPtr controller, SDL_GameControllerAxis axis) =>
        SDL_GameControllerGetAxis(controller, axis);
}
