using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Playnite.Avalonia.App.Services;

namespace Playnite.DesktopApp.Avalonia.Services;

internal interface ISystemHotKeyService : IDisposable
{
    bool IsSupported { get; }
    HotKey RegisteredHotKey { get; }
    bool Register(HotKey hotKey, Action activated, out string error);
    void Unregister();
}

internal sealed class SystemHotKeyService : ISystemHotKeyService
{
    private const int hotKeyId = 0x504C;
    private const uint wmHotKey = 0x0312;
    private const uint modifierAlt = 0x0001;
    private const uint modifierControl = 0x0002;
    private const uint modifierShift = 0x0004;
    private const uint modifierWin = 0x0008;
    private const uint modifierNoRepeat = 0x4000;
    private readonly Window window;
    private readonly Win32Properties.CustomWndProcHookCallback wndProcHook;
    private Action activated;
    private bool disposed;

    public bool IsSupported => OperatingSystem.IsWindows();
    public HotKey RegisteredHotKey { get; private set; }

    public SystemHotKeyService(Window window)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        wndProcHook = WndProc;
        if (IsSupported)
        {
            Win32Properties.AddWndProcHookCallback(window, wndProcHook);
        }
    }

    public bool Register(HotKey hotKey, Action activated, out string error)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Unregister();
        error = string.Empty;
        if (hotKey == null)
        {
            return true;
        }
        if (!IsSupported)
        {
            error = "System-wide shortcuts are unavailable on this platform.";
            return false;
        }
        if (!TryGetVirtualKey(hotKey.Key, out var virtualKey))
        {
            error = $"The key '{hotKey.Key}' cannot be registered as a system-wide shortcut.";
            return false;
        }

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            error = "The Playnite window handle is not ready for system-wide shortcut registration.";
            return false;
        }

        var modifiers = modifierNoRepeat;
        if (hotKey.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= modifierAlt;
        }
        if (hotKey.Modifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= modifierControl;
        }
        if (hotKey.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= modifierShift;
        }
        if (hotKey.Modifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= modifierWin;
        }

        if (!RegisterHotKey(handle, hotKeyId, modifiers, virtualKey))
        {
            error = $"The shortcut '{hotKey}' is already reserved by Windows or another application.";
            return false;
        }

        RegisteredHotKey = hotKey;
        this.activated = activated ?? throw new ArgumentNullException(nameof(activated));
        return true;
    }

    public void Unregister()
    {
        if (RegisteredHotKey != null)
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero && IsSupported)
            {
                UnregisterHotKey(handle, hotKeyId);
            }
        }

        RegisteredHotKey = null;
        activated = null;
    }

    private IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == wmHotKey && wParam.ToInt32() == hotKeyId)
        {
            handled = true;
            var callback = activated;
            if (callback != null)
            {
                Dispatcher.UIThread.Post(callback);
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryGetVirtualKey(Key key, out uint virtualKey)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            virtualKey = 0x41u + (uint)(key - Key.A);
            return true;
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            virtualKey = 0x30u + (uint)(key - Key.D0);
            return true;
        }
        if (key is >= Key.F1 and <= Key.F24)
        {
            virtualKey = 0x70u + (uint)(key - Key.F1);
            return true;
        }

        virtualKey = key switch
        {
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Enter => 0x0D,
            Key.Pause => 0x13,
            Key.CapsLock => 0x14,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.End => 0x23,
            Key.Home => 0x24,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.PrintScreen => 0x2C,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            _ => 0
        };
        return virtualKey != 0;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Unregister();
        if (IsSupported)
        {
            Win32Properties.RemoveWndProcHookCallback(window, wndProcHook);
        }
        disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
