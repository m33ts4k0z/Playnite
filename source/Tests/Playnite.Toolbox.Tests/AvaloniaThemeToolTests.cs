using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using Playnite.Avalonia.Theming;

namespace Playnite.Toolbox.Tests
{
    [TestFixture]
    public class AvaloniaThemeToolTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), $"PlayniteThemeToolTests_{Guid.NewGuid():N}");
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
        public void CreateValidateAndPackDesktopThemeTest()
        {
            var themeRoot = AvaloniaThemeTool.Create(
                AvaloniaThemeMode.Desktop,
                "Pilot Theme",
                Path.Combine(testRoot, "theme"));
            var package = AvaloniaThemeTool.Validate(themeRoot, AvaloniaThemeMode.Desktop);

            Assert.AreEqual("Pilot Theme", package.Name);
            Assert.AreEqual(AvaloniaThemeMode.Desktop, package.Mode);
            Assert.AreEqual(AvaloniaThemePackage.CurrentApiVersion.ToString(), package.Manifest.ThemeApiVersion);

            var archivePath = AvaloniaThemeTool.Pack(
                themeRoot,
                Path.Combine(testRoot, "packages"),
                AvaloniaThemeMode.Desktop);
            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries.Select(entry => entry.FullName).ToList();
            CollectionAssert.Contains(entries, "theme.yaml");
            CollectionAssert.Contains(entries, "Theme.axaml");
            CollectionAssert.Contains(entries, "Styles.axaml");
        }

        [Test]
        public void ValidateRejectsWrongApplicationModeTest()
        {
            var themeRoot = AvaloniaThemeTool.Create(
                AvaloniaThemeMode.Desktop,
                "Pilot Theme",
                Path.Combine(testRoot, "theme"));

            Assert.Throws<InvalidDataException>(() =>
                AvaloniaThemeTool.Validate(themeRoot, AvaloniaThemeMode.Fullscreen));
        }

        [Test]
        public void ValidateRejectsEscapingPackagePathTest()
        {
            var themeRoot = AvaloniaThemeTool.Create(
                AvaloniaThemeMode.Desktop,
                "Pilot Theme",
                Path.Combine(testRoot, "theme"));
            File.WriteAllText(Path.Combine(testRoot, "Outside.axaml"),
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" />");
            var manifestPath = Path.Combine(themeRoot, "theme.yaml");
            File.WriteAllText(
                manifestPath,
                File.ReadAllText(manifestPath).Replace(
                    "EntryPoint: Theme.axaml",
                    "EntryPoint: ../Outside.axaml"));

            Assert.Throws<InvalidDataException>(() =>
                AvaloniaThemeTool.Validate(themeRoot, AvaloniaThemeMode.Desktop));
        }

        [Test]
        public void ValidateRejectsFutureThemeApiTest()
        {
            var themeRoot = AvaloniaThemeTool.Create(
                AvaloniaThemeMode.Fullscreen,
                "Pilot Theme",
                Path.Combine(testRoot, "theme"));
            var manifestPath = Path.Combine(themeRoot, "theme.yaml");
            File.WriteAllText(
                manifestPath,
                File.ReadAllText(manifestPath).Replace(
                    "ThemeApiVersion: 3.0.0",
                    "ThemeApiVersion: 4.0.0"));

            Assert.Throws<InvalidDataException>(() =>
                AvaloniaThemeTool.Validate(themeRoot, AvaloniaThemeMode.Fullscreen));
        }

        [Test]
        public void ValidateRejectsWrongMarkupRootTest()
        {
            var themeRoot = AvaloniaThemeTool.Create(
                AvaloniaThemeMode.Desktop,
                "Pilot Theme",
                Path.Combine(testRoot, "theme"));
            File.WriteAllText(
                Path.Combine(themeRoot, "Theme.axaml"),
                "<Styles xmlns=\"https://github.com/avaloniaui\" />");

            Assert.Throws<InvalidDataException>(() =>
                AvaloniaThemeTool.Validate(themeRoot, AvaloniaThemeMode.Desktop));
        }
    }
}
