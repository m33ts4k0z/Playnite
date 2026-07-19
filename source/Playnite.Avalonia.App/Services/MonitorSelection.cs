namespace Playnite.Avalonia.App.Services;

// Decides which screen a fullscreen window should occupy, given the monitor
// index carried over from the WPF fullscreen profile. Kept free of Avalonia
// types so the placement decision is unit-testable without a display; the
// window supplies the live connected-screen count.
public static class MonitorSelection
{
    // Returns the screen index to place the window on, or null to keep the
    // platform default (primary). A requested index is honored only when it
    // addresses a currently connected screen — a stale index left over from a
    // monitor that is no longer attached falls back to the default.
    public static int? Resolve(int requestedMonitor, int connectedScreenCount)
    {
        if (requestedMonitor < 0 || connectedScreenCount <= 0)
        {
            return null;
        }

        if (requestedMonitor >= connectedScreenCount)
        {
            return null;
        }

        return requestedMonitor;
    }
}
