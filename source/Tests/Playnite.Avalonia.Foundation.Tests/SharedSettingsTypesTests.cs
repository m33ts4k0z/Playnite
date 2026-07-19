using Avalonia.Input;
using NUnit.Framework;
using Playnite.Avalonia.App.Services;
using System.Collections.Generic;
using System.Globalization;
using System;

namespace Playnite.Avalonia.Foundation.Tests;

[TestFixture]
public sealed class SharedSettingsTypesTests
{
    [Test]
    public void SerializedEnumValuesRetainLegacyOrdering()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)GameSearchItemAction.None, Is.EqualTo(4));
            Assert.That((int)DefaultIconSourceOptions.None, Is.EqualTo(3));
            Assert.That((int)DefaultCoverSourceOptions.None, Is.EqualTo(2));
            Assert.That((int)DefaultBackgroundSourceOptions.None, Is.EqualTo(3));
            Assert.That((int)UpdateCheckFrequency.Manually, Is.EqualTo(3));
            Assert.That((int)LibraryUpdateCheckFrequency.Manually, Is.Zero);
            Assert.That((int)AutoBackupFrequency.OnceAWeek, Is.EqualTo(2));
        });
    }

    [Test]
    public void VisibilityModelsNotifyOnlyForChanges()
    {
        var settings = new DetailsVisibilitySettings();
        var changes = new List<string>();
        settings.PropertyChanged += (_, args) => changes.Add(args.PropertyName!);

        settings.Library = true;
        settings.Library = false;
        settings.Platform = false;

        CollectionAssert.AreEqual(new[] { "Library", "Platform" }, changes);
    }

    [Test]
    public void HotKeyUsesPortableAvaloniaGestureTypes()
    {
        var hotKey = new HotKey(Key.K, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.That(hotKey.ToString(), Is.EqualTo("Ctrl + Shift + K"));
    }

    [Test]
    public void DetailsVisibilityCloneIsIndependentAndCopyable()
    {
        var source = new DetailsVisibilitySettings
        {
            Name = false,
            CoverImage = false,
            UserScore = true
        };

        var clone = source.Clone();
        clone.Name = true;
        var destination = new DetailsVisibilitySettings();
        destination.CopyFrom(source);

        Assert.Multiple(() =>
        {
            Assert.That(source.Name, Is.False);
            Assert.That(clone.Name, Is.True);
            Assert.That(clone.CoverImage, Is.False);
            Assert.That(destination.Name, Is.False);
            Assert.That(destination.CoverImage, Is.False);
            Assert.That(destination.UserScore, Is.True);
        });
    }

    [Test]
    public void DateFormattingSupportsRelativeAndPartialDates()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var relative = new DateFormattingOptions { Format = "yyyy-MM-dd", PastWeekRelativeFormat = true };
        var release = new ReleaseDateFormattingOptions { Format = "yyyy-MM-dd", PartialFormat = "MMMM yyyy" };
        var today = new DateTime(2026, 7, 19);

        Assert.Multiple(() =>
        {
            Assert.That(DateFormattingService.Format(today, relative, today, culture), Is.EqualTo("Today"));
            Assert.That(DateFormattingService.Format(today.AddDays(-1), relative, today, culture), Is.EqualTo("Yesterday"));
            Assert.That(DateFormattingService.Format(today.AddDays(-3), relative, today, culture), Is.EqualTo("Thursday"));
            Assert.That(DateFormattingService.FormatReleaseDate(
                new DateTime(2026, 4, 1), true, false, release, today, culture), Is.EqualTo("April 2026"));
            Assert.That(DateFormattingService.FormatReleaseDate(
                new DateTime(2026, 1, 1), false, false, release, today, culture), Is.EqualTo("2026"));
        });
    }

    [Test]
    public void InvalidDateFormatsAreRejectedAndNormalized()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DateFormattingService.IsValidFormat("yyyy-MM-dd"), Is.True);
            Assert.That(DateFormattingService.IsValidFormat("%"), Is.False);
            Assert.That(DateFormattingService.NormalizeFormat("%", "d"), Is.EqualTo("d"));
        });
    }
}
