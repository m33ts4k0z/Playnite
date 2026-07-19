using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using Playnite.Avalonia.Theming;

namespace Playnite.Avalonia.Foundation.Tests;

[TestFixture]
public sealed class AvaloniaThemeToolTests
{
    private string testRoot = null!;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(Path.GetTempPath(), $"PlayniteAvaloniaTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, true);
        }
    }

    [Test]
    public void CreateRejectsUndefinedMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AvaloniaThemeTool.Create((AvaloniaThemeMode)int.MaxValue, "Invalid", testRoot));
    }

    [Test]
    public void PackProducesStableEntryOrder()
    {
        var themeRoot = AvaloniaThemeTool.Create(
            AvaloniaThemeMode.Desktop,
            "Portable Theme",
            Path.Combine(testRoot, "theme"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "Assets"));
        File.WriteAllText(Path.Combine(themeRoot, "z-last.txt"), "z");
        File.WriteAllText(Path.Combine(themeRoot, "Assets", "a-first.txt"), "a");

        var archivePath = AvaloniaThemeTool.Pack(
            themeRoot,
            Path.Combine(testRoot, "packages"),
            AvaloniaThemeMode.Desktop);

        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
        CollectionAssert.AreEqual(entries.OrderBy(entry => entry, StringComparer.Ordinal), entries);
    }

    [Test]
    public void PackRejectsSymbolicLinks()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The native Linux test exercises symbolic-link package rejection.");
        }

        var themeRoot = AvaloniaThemeTool.Create(
            AvaloniaThemeMode.Desktop,
            "Portable Theme",
            Path.Combine(testRoot, "theme"));
        var externalFile = Path.Combine(testRoot, "external.txt");
        File.WriteAllText(externalFile, "outside");
        File.CreateSymbolicLink(Path.Combine(themeRoot, "linked.txt"), externalFile);

        Assert.Throws<InvalidDataException>(() => AvaloniaThemeTool.Pack(
            themeRoot,
            Path.Combine(testRoot, "packages"),
            AvaloniaThemeMode.Desktop));
    }

    [Test]
    public void ValidateRejectsCaseVariantPackageEscape()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The native Linux test exercises case-sensitive package boundaries.");
        }

        var themeRoot = AvaloniaThemeTool.Create(
            AvaloniaThemeMode.Desktop,
            "Portable Theme",
            Path.Combine(testRoot, "Theme"));
        var caseVariantDirectory = Path.Combine(testRoot, "theme");
        Directory.CreateDirectory(caseVariantDirectory);
        File.WriteAllText(
            Path.Combine(caseVariantDirectory, "Outside.axaml"),
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" />");
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath).Replace(
                "EntryPoint: Theme.axaml",
                "EntryPoint: ../theme/Outside.axaml",
                StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() =>
            AvaloniaThemeTool.Validate(themeRoot, AvaloniaThemeMode.Desktop));
    }

    [Test]
    public void PackageLoadsOrderedResourceDictionariesBeforeEntryPoint()
    {
        var themeRoot = AvaloniaThemeTool.Create(
            AvaloniaThemeMode.Desktop,
            "Modular Theme",
            Path.Combine(testRoot, "theme"));
        var viewsRoot = Path.Combine(themeRoot, "Views");
        Directory.CreateDirectory(viewsRoot);
        File.WriteAllText(
            Path.Combine(viewsRoot, "Library.axaml"),
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" />");
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath).Replace(
                "EntryPoint: Theme.axaml",
                "EntryPoint: Theme.axaml\nResources:\n  - Views/Library.axaml",
                StringComparison.Ordinal));

        var package = AvaloniaThemePackage.Load(themeRoot, AvaloniaThemeMode.Desktop);

        CollectionAssert.AreEqual(
            new[] { "Library.axaml", "Theme.axaml" },
            package.ResourceDictionaries.Select(Path.GetFileName));
        Assert.DoesNotThrow(package.ValidateMarkupStructure);
    }

    [Test]
    public void PackageRejectsDuplicateResourceDictionaryPaths()
    {
        var themeRoot = AvaloniaThemeTool.Create(
            AvaloniaThemeMode.Desktop,
            "Duplicate Resources",
            Path.Combine(testRoot, "theme"));
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath).Replace(
                "EntryPoint: Theme.axaml",
                "EntryPoint: Theme.axaml\nResources:\n  - Theme.axaml",
                StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() =>
            AvaloniaThemePackage.Load(themeRoot, AvaloniaThemeMode.Desktop));
    }

    [Test]
    public void PackageRejectsResourceDictionaryEscape()
    {
        var themeRoot = AvaloniaThemeTool.Create(
            AvaloniaThemeMode.Desktop,
            "Escaping Resources",
            Path.Combine(testRoot, "theme"));
        var outsidePath = Path.Combine(testRoot, "Outside.axaml");
        File.WriteAllText(
            outsidePath,
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" />");
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath).Replace(
                "EntryPoint: Theme.axaml",
                "EntryPoint: Theme.axaml\nResources:\n  - ../Outside.axaml",
                StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() =>
            AvaloniaThemePackage.Load(themeRoot, AvaloniaThemeMode.Desktop));
    }
}
