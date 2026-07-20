using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Playnite.SDK;
using Playnite.WpfPluginSupport;
using System.Windows.Interop;
using LegacyResizeMode = System.Windows.ResizeMode;
using LegacyWindow = System.Windows.Window;
using LegacyWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace Playnite.Avalonia.App.Services;

/// <summary>
/// SDK v6 exposes WPF Window in its ABI. Keep that unavoidable compatibility
/// member isolated while all actual dialog primitives remain native Avalonia.
/// </summary>
public sealed class LegacyWpfWindowBridge
{
    private readonly Func<Window> currentWindow;
    private LegacyWindow ownerProxy;

    public LegacyWpfWindowBridge(Func<Window> currentWindow)
    {
        this.currentWindow = currentWindow ?? throw new ArgumentNullException(nameof(currentWindow));
    }

    public LegacyWindow CreateWindow(WindowCreationOptions options) => Invoke(() =>
    {
        WpfPluginSupportRuntime.EnsureApplication();
        options ??= new WindowCreationOptions();
        return new LegacyWindow
        {
            WindowStartupLocation = LegacyWindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            ResizeMode = options.ShowMinimizeButton || options.ShowMaximizeButton
                ? LegacyResizeMode.CanResize
                : LegacyResizeMode.NoResize
        };
    });

    public LegacyWindow GetCurrentWindow() => Invoke(() =>
    {
        if (ownerProxy != null)
        {
            ApplyNativeOwner(ownerProxy);
            return ownerProxy;
        }

        WpfPluginSupportRuntime.EnsureApplication();
        ownerProxy = new LegacyWindow
        {
            Width = 1,
            Height = 1,
            Left = -10_000,
            Top = -10_000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = LegacyWindowStartupLocation.Manual
        };
        _ = new WindowInteropHelper(ownerProxy).EnsureHandle();
        ApplyNativeOwner(ownerProxy);
        return ownerProxy;
    });

    private void ApplyNativeOwner(LegacyWindow window)
    {
        var handle = currentWindow()?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero)
        {
            new WindowInteropHelper(window).Owner = handle;
        }
    }

    private static T Invoke<T>(Func<T> action) => Dispatcher.UIThread.CheckAccess()
        ? action()
        : Dispatcher.UIThread.Invoke(action);
}
