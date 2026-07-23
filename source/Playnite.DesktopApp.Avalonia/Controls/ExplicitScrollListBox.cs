using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Playnite.DesktopApp.Avalonia.Controls;

/// <summary>
/// Keeps a nested list from taking over mouse-wheel scrolling until the user
/// explicitly focuses the list or one of its children.
/// </summary>
public sealed class ExplicitScrollListBox : ListBox
{
    private const double WheelStep = 48;

    public ExplicitScrollListBox()
    {
        AddHandler(
            PointerWheelChangedEvent,
            OnPreviewPointerWheelChanged,
            RoutingStrategies.Tunnel);
    }

    private void OnPreviewPointerWheelChanged(object sender, PointerWheelEventArgs args)
    {
        if (IsKeyboardFocusWithin)
        {
            return;
        }

        var parentViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (parentViewer == null)
        {
            return;
        }

        var current = parentViewer.Offset;
        var maximum = new Vector(
            Math.Max(0, parentViewer.Extent.Width - parentViewer.Viewport.Width),
            Math.Max(0, parentViewer.Extent.Height - parentViewer.Viewport.Height));
        var horizontal = Math.Abs(args.Delta.X) > Math.Abs(args.Delta.Y);
        var delta = horizontal
            ? new Vector(-args.Delta.X * WheelStep, 0)
            : new Vector(0, -args.Delta.Y * WheelStep);
        parentViewer.Offset = new Vector(
            Math.Clamp(current.X + delta.X, 0, maximum.X),
            Math.Clamp(current.Y + delta.Y, 0, maximum.Y));
        args.Handled = true;
    }
}
