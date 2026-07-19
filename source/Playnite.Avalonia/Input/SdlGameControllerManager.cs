using System.Security.Cryptography;
using System.Text;
using Avalonia.Threading;
using static SDL2.SDL;

namespace Playnite.Avalonia.Input;

public sealed record SdlGameControllerDevice(
    string Id,
    string Name,
    int InstanceId,
    bool IsEnabled);

public sealed class GamepadButtonStateChangedEventArgs : EventArgs
{
    public GamepadButton Button { get; }
    public bool IsPressed { get; }

    public GamepadButtonStateChangedEventArgs(GamepadButton button, bool isPressed)
    {
        Button = button;
        IsPressed = isPressed;
    }
}

/// <summary>
/// Shared SDL2 controller inventory and input state aggregator. Controller IDs
/// are stable across reconnects when SDL exposes a GUID or serial number, so the
/// disabled-device list can be persisted by either shell.
/// </summary>
public sealed class SdlGameControllerManager : IDisposable
{
    private const short AxisThreshold = 16_383;

    private sealed record Controller(
        IntPtr Handle,
        int InstanceId,
        string PersistentId,
        string Name);

    private readonly List<Controller> controllers = new();
    private readonly HashSet<string> disabledControllerIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GamepadButton, bool> lastStates =
        Enum.GetValues<GamepadButton>().ToDictionary(button => button, _ => false);
    private DispatcherTimer timer;
    private bool initialized;
    private bool inputEnabled = true;

    public event EventHandler DevicesChanged;
    public event EventHandler<GamepadButtonStateChangedEventArgs> ButtonStateChanged;

    public bool IsAvailable { get; private set; }
    public bool IsStarted => initialized;
    public string Status { get; private set; } = "SDL game-controller input has not started.";
    public IReadOnlyList<SdlGameControllerDevice> Devices => controllers
        .Select(controller => new SdlGameControllerDevice(
            controller.PersistentId,
            controller.Name,
            controller.InstanceId,
            InputEnabled && !disabledControllerIds.Contains(controller.PersistentId)))
        .ToList();
    public IReadOnlyCollection<string> DisabledControllerIds => disabledControllerIds;

    public bool InputEnabled
    {
        get => inputEnabled;
        set
        {
            if (inputEnabled == value)
            {
                return;
            }

            inputEnabled = value;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            if (!inputEnabled)
            {
                ReleaseAllButtons();
            }
        }
    }

    public void SetDisabledControllerIds(IEnumerable<string> controllerIds)
    {
        var next = new HashSet<string>(
            controllerIds?.Where(id => !string.IsNullOrWhiteSpace(id)) ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        if (disabledControllerIds.SetEquals(next))
        {
            return;
        }

        disabledControllerIds.Clear();
        disabledControllerIds.UnionWith(next);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        UpdateButtonStates();
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

            UpdateStatus();
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Status = $"SDL game-controller input unavailable: {exception.Message}";
        }
    }

    public void Dispose()
    {
        if (timer != null)
        {
            timer.Tick -= Timer_Tick;
            timer.Stop();
            timer = null;
        }

        ReleaseAllButtons();
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
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string CreatePersistentId(
        Guid guid,
        ushort vendor,
        ushort product,
        string serial,
        string name)
    {
        var hardwareId = guid != Guid.Empty
            ? guid.ToString("N")
            : Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(name?.Trim() ?? "unknown-controller")))[..12];
        var serialPart = string.IsNullOrWhiteSpace(serial) ? string.Empty : $":{serial.Trim()}";
        return $"{hardwareId}:{vendor:x4}:{product:x4}{serialPart}";
    }

    private void Timer_Tick(object sender, EventArgs e) => Update();

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
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var states = Enum.GetValues<GamepadButton>().ToDictionary(button => button, _ => false);
        if (InputEnabled)
        {
            foreach (var controller in controllers.Where(controller =>
                         !disabledControllerIds.Contains(controller.PersistentId)))
            {
                MergeState(states, controller.Handle);
            }
        }

        foreach (var button in states.Keys)
        {
            if (states[button] == lastStates[button])
            {
                continue;
            }

            lastStates[button] = states[button];
            ButtonStateChanged?.Invoke(
                this,
                new GamepadButtonStateChangedEventArgs(button, states[button]));
        }
    }

    private void ReleaseAllButtons()
    {
        foreach (var button in lastStates.Keys.ToList())
        {
            if (!lastStates[button])
            {
                continue;
            }

            lastStates[button] = false;
            ButtonStateChanged?.Invoke(this, new GamepadButtonStateChangedEventArgs(button, false));
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

        var name = SDL_GameControllerName(handle) ?? $"Controller {instanceId}";
        var persistentId = CreatePersistentId(
            SDL_JoystickGetDeviceGUID(joystickIndex),
            SDL_GameControllerGetVendor(handle),
            SDL_GameControllerGetProduct(handle),
            SDL_GameControllerGetSerial(handle),
            name);
        controllers.Add(new Controller(handle, instanceId, persistentId, name));
        UpdateStatus();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
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
        UpdateStatus();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        UpdateButtonStates();
    }

    private void UpdateStatus() =>
        Status = $"SDL game-controller input active ({controllers.Count} connected).";

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
