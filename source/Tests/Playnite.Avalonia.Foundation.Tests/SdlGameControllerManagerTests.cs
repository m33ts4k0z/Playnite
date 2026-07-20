using System;
using NUnit.Framework;
using Playnite.Avalonia.Input;

namespace Playnite.Avalonia.Foundation.Tests;

[TestFixture]
public sealed class SdlGameControllerManagerTests
{
    [Test]
    public void PersistentIdIsStableAndIncludesHardwareIdentity()
    {
        var guid = Guid.Parse("99b432de-1b8e-4cb8-aae1-1ded95fea07c");

        var first = SdlGameControllerManager.CreatePersistentId(
            guid,
            0x045e,
            0x028e,
            "serial-7",
            "Controller");
        var second = SdlGameControllerManager.CreatePersistentId(
            guid,
            0x045e,
            0x028e,
            "serial-7",
            "Renamed Controller");

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Does.Contain("045e:028e:serial-7"));
    }

    [Test]
    public void DisabledControllerIdsAreCaseInsensitiveAndDeduplicated()
    {
        using var manager = new SdlGameControllerManager();

        manager.SetDisabledControllerIds(new[] { "Pad-A", "pad-a", "Pad-B", "" });

        Assert.That(manager.DisabledControllerIds, Is.EquivalentTo(new[] { "Pad-A", "Pad-B" }));
    }

    [Test]
    public void InputCanBeDisabledBeforeNativeSdlStarts()
    {
        using var manager = new SdlGameControllerManager
        {
            InputEnabled = false
        };

        Assert.Multiple(() =>
        {
            Assert.That(manager.InputEnabled, Is.False);
            Assert.That(manager.IsStarted, Is.False);
            Assert.That(manager.Devices, Is.Empty);
        });
    }

    [TestCase((short)17_000, true, false, true)]
    [TestCase((short)16_000, true, false, false)]
    [TestCase((short)13_000, true, true, true)]
    [TestCase((short)11_000, true, true, false)]
    [TestCase((short)-17_000, false, false, true)]
    [TestCase((short)-13_000, false, true, true)]
    [TestCase((short)-11_000, false, true, false)]
    public void AnalogDirectionsUseSeparatePressAndReleaseThresholds(
        short value,
        bool positive,
        bool wasPressed,
        bool expected)
    {
        Assert.That(
            SdlGameControllerManager.AxisDirectionPressed(value, positive, wasPressed),
            Is.EqualTo(expected));
    }
}
