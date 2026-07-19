using System;
using Avalonia.Controls;
using Avalonia.Layout;
using NUnit.Framework;
using Playnite.Avalonia.Controls;

namespace Playnite.Avalonia.Foundation.Tests;

[TestFixture]
public sealed class SharedViewPrimitivesTests
{
    [Test]
    public void UniformGridExposesPortableGeometryControls()
    {
        var panel = new UniformGridVirtualizingPanel
        {
            ItemWidth = 180,
            ItemHeight = 250,
            ItemAspectRatio = 0.75,
            ItemSpacing = 12,
            Columns = 4,
            Rows = 3,
            Orientation = Orientation.Horizontal
        };

        Assert.Multiple(() =>
        {
            Assert.That(panel.EffectiveItemWidth, Is.EqualTo(180));
            Assert.That(panel.EffectiveItemHeight, Is.EqualTo(240));
            Assert.That(panel.ItemSpacing, Is.EqualTo(12));
            Assert.That(panel.Columns, Is.EqualTo(4));
            Assert.That(panel.Rows, Is.EqualTo(3));
            Assert.That(panel.Orientation, Is.EqualTo(Orientation.Horizontal));
        });
    }

    [Test]
    public void UniformGridRejectsInvalidGeometry()
    {
        var panel = new UniformGridVirtualizingPanel();

        Assert.Multiple(() =>
        {
            Assert.That(() => panel.ItemWidth = 0, Throws.ArgumentException);
            Assert.That(() => panel.ItemAspectRatio = -1, Throws.ArgumentException);
            Assert.That(() => panel.ItemSpacing = double.NaN, Throws.ArgumentException);
            Assert.That(() => panel.Columns = -1, Throws.ArgumentException);
            Assert.That(() => panel.Rows = -1, Throws.ArgumentException);
        });
    }

    [Test]
    public void ScrollBehaviorStoresSharedScrollPolicy()
    {
        var viewer = new ScrollViewer();
        ScrollBehavior.SetIsEnabled(viewer, true);
        ScrollBehavior.SetWheelSensitivity(viewer, 1.75);
        ScrollBehavior.SetSmoothScrollingEnabled(viewer, true);
        ScrollBehavior.SetSmoothScrollDuration(viewer, TimeSpan.FromMilliseconds(180));

        Assert.Multiple(() =>
        {
            Assert.That(ScrollBehavior.GetIsEnabled(viewer), Is.True);
            Assert.That(ScrollBehavior.GetWheelSensitivity(viewer), Is.EqualTo(1.75));
            Assert.That(ScrollBehavior.GetSmoothScrollingEnabled(viewer), Is.True);
            Assert.That(ScrollBehavior.GetSmoothScrollDuration(viewer), Is.EqualTo(TimeSpan.FromMilliseconds(180)));
        });
    }
}
