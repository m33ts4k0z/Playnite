using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Shared mouse-wheel behavior for Playnite's desktop and fullscreen views.
/// It supports sensitivity scaling and optional time-based smooth scrolling
/// without relying on platform-specific input APIs.
/// </summary>
public static class ScrollBehavior
{
    private const double WheelStep = 48;
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollAnimationState> states = new();

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsEnabled",
            typeof(ScrollBehavior),
            false);

    public static readonly AttachedProperty<double> WheelSensitivityProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, double>(
            "WheelSensitivity",
            typeof(ScrollBehavior),
            1,
            validate: value => double.IsFinite(value) && value > 0);

    public static readonly AttachedProperty<bool> SmoothScrollingEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "SmoothScrollingEnabled",
            typeof(ScrollBehavior),
            false);

    public static readonly AttachedProperty<TimeSpan> SmoothScrollDurationProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, TimeSpan>(
            "SmoothScrollDuration",
            typeof(ScrollBehavior),
            TimeSpan.FromMilliseconds(250),
            validate: value => value > TimeSpan.Zero && value <= TimeSpan.FromSeconds(5));

    static ScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) =>
        {
            if (GetIsEnabled(viewer))
            {
                viewer.PointerWheelChanged += Viewer_PointerWheelChanged;
            }
            else
            {
                viewer.PointerWheelChanged -= Viewer_PointerWheelChanged;
                if (states.TryGetValue(viewer, out var state))
                {
                    state.Stop();
                }
            }
        });
    }

    public static bool GetIsEnabled(AvaloniaObject target) => target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(AvaloniaObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    public static double GetWheelSensitivity(AvaloniaObject target) => target.GetValue(WheelSensitivityProperty);
    public static void SetWheelSensitivity(AvaloniaObject target, double value) => target.SetValue(WheelSensitivityProperty, value);
    public static bool GetSmoothScrollingEnabled(AvaloniaObject target) => target.GetValue(SmoothScrollingEnabledProperty);
    public static void SetSmoothScrollingEnabled(AvaloniaObject target, bool value) => target.SetValue(SmoothScrollingEnabledProperty, value);
    public static TimeSpan GetSmoothScrollDuration(AvaloniaObject target) => target.GetValue(SmoothScrollDurationProperty);
    public static void SetSmoothScrollDuration(AvaloniaObject target, TimeSpan value) => target.SetValue(SmoothScrollDurationProperty, value);

    private static void Viewer_PointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        var sensitivity = GetWheelSensitivity(viewer);
        var horizontal = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y);
        var current = viewer.Offset;
        var maximum = new Vector(
            Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width),
            Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height));
        var delta = horizontal
            ? new Vector(-e.Delta.X * WheelStep * sensitivity, 0)
            : new Vector(0, -e.Delta.Y * WheelStep * sensitivity);
        var target = new Vector(
            Math.Clamp(current.X + delta.X, 0, maximum.X),
            Math.Clamp(current.Y + delta.Y, 0, maximum.Y));
        if (target == current)
        {
            return;
        }

        e.Handled = true;
        if (!GetSmoothScrollingEnabled(viewer))
        {
            viewer.Offset = target;
            return;
        }

        states.GetValue(viewer, static value => new ScrollAnimationState(value))
            .AnimateTo(target, GetSmoothScrollDuration(viewer));
    }

    private sealed class ScrollAnimationState
    {
        private readonly ScrollViewer viewer;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch stopwatch = new();
        private Vector start;
        private Vector target;
        private TimeSpan duration;

        public ScrollAnimationState(ScrollViewer viewer)
        {
            this.viewer = viewer;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += Timer_Tick;
        }

        public void AnimateTo(Vector nextTarget, TimeSpan nextDuration)
        {
            start = viewer.Offset;
            target = nextTarget;
            duration = nextDuration;
            stopwatch.Restart();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            stopwatch.Reset();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            viewer.Offset = start + (target - start) * eased;
            if (progress >= 1)
            {
                Stop();
            }
        }
    }
}
