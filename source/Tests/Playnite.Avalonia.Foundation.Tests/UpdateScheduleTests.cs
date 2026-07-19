using NUnit.Framework;
using Playnite.Avalonia.App.Services;
using System;

namespace Playnite.Avalonia.Foundation.Tests;

[TestFixture]
public sealed class UpdateScheduleTests
{
    private static readonly DateTime now = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Local);

    [TestCase(UpdateCheckFrequency.Manually, false)]
    [TestCase(UpdateCheckFrequency.OnEveryStartup, true)]
    [TestCase(UpdateCheckFrequency.OnceADay, true)]
    [TestCase(UpdateCheckFrequency.OnceAWeek, true)]
    public void ProgramAndAddonStartupScheduleHonorsFrequency(
        UpdateCheckFrequency frequency,
        bool expected)
    {
        Assert.That(
            UpdateSchedule.ShouldRunOnStartup(frequency, now.AddDays(-8), now),
            Is.EqualTo(expected));
    }

    [Test]
    public void PeriodicScheduleNeverRepeatsStartupOnlyChecks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UpdateSchedule.ShouldRunPeriodically(
                UpdateCheckFrequency.OnEveryStartup,
                DateTime.MinValue,
                now), Is.False);
            Assert.That(UpdateSchedule.ShouldRunPeriodically(
                UpdateCheckFrequency.OnceADay,
                now.AddDays(-1),
                now), Is.True);
            Assert.That(UpdateSchedule.ShouldRunPeriodically(
                UpdateCheckFrequency.OnceAWeek,
                now.AddDays(-5),
                now), Is.False);
        });
    }

    [TestCase(LibraryUpdateCheckFrequency.Manually, false)]
    [TestCase(LibraryUpdateCheckFrequency.OnEveryStartup, true)]
    [TestCase(LibraryUpdateCheckFrequency.OnceADay, true)]
    [TestCase(LibraryUpdateCheckFrequency.OnceAWeek, true)]
    public void LibraryStartupScheduleHonorsFrequency(
        LibraryUpdateCheckFrequency frequency,
        bool expected)
    {
        Assert.That(
            UpdateSchedule.ShouldRunOnStartup(frequency, now.AddDays(-8), now),
            Is.EqualTo(expected));
    }
}
