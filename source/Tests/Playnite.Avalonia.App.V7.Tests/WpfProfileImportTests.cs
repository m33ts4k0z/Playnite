using NUnit.Framework;
using Playnite.Avalonia.App.Services;
using System.IO;

namespace Playnite.Avalonia.App.V7.Tests;

[TestFixture]
public class WpfProfileImportTests
{
    private string profileDir;

    [SetUp]
    public void SetUp()
    {
        profileDir = Path.Combine(
            Path.GetTempPath(),
            "PlayniteWpfImportTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(profileDir))
            {
                Directory.Delete(profileDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private void WriteFile(string name, string content) =>
        File.WriteAllText(Path.Combine(profileDir, name), content);

    [Test]
    public void Read_MissingProfile_ReturnsAllNull()
    {
        var defaults = WpfProfileImport.Read(profileDir);

        Assert.That(defaults.Language, Is.Null);
        Assert.That(defaults.DisableHwAcceleration, Is.Null);
        Assert.That(defaults.AsyncImageLoading, Is.Null);
        Assert.That(defaults.ShowImagePerformanceWarning, Is.Null);
        Assert.That(defaults.FullscreenMonitor, Is.Null);
        Assert.That(defaults.MainWindow, Is.Null);
    }

    [Test]
    public void Read_Language_FromConfig()
    {
        WriteFile("config.json", """{ "Language": "german", "DatabasePath": "{PlayniteDir}\\library" }""");

        Assert.That(WpfProfileImport.Read(profileDir).Language, Is.EqualTo("german"));
    }

    [Test]
    public void Read_BlankLanguage_IsIgnored()
    {
        WriteFile("config.json", """{ "Language": "   " }""");

        Assert.That(WpfProfileImport.Read(profileDir).Language, Is.Null);
    }

    [Test]
    public void Read_PerformanceDefaults_FromConfig()
    {
        WriteFile("config.json", """
        {
          "DisableHwAcceleration": true,
          "AsyncImageLoading": false,
          "ShowImagePerformanceWarning": false
        }
        """);

        var defaults = WpfProfileImport.Read(profileDir);

        Assert.That(defaults.DisableHwAcceleration, Is.True);
        Assert.That(defaults.AsyncImageLoading, Is.False);
        Assert.That(defaults.ShowImagePerformanceWarning, Is.False);
    }

    [Test]
    public void Read_FullscreenMonitor_WhenNonNegative()
    {
        WriteFile("fullscreenConfig.json", """{ "Monitor": 2 }""");

        Assert.That(WpfProfileImport.Read(profileDir).FullscreenMonitor, Is.EqualTo(2));
    }

    [Test]
    public void Read_FullscreenMonitor_NegativeIsIgnored()
    {
        WriteFile("fullscreenConfig.json", """{ "Monitor": -1 }""");

        Assert.That(WpfProfileImport.Read(profileDir).FullscreenMonitor, Is.Null);
    }

    [Test]
    public void Read_MainWindowPlacement_FromNumericState()
    {
        WriteFile("windowPositions.json", """
        {
          "Positions": {
            "Main_V2": {
              "Position": { "X": 120, "Y": 80 },
              "Size": { "X": 1600, "Y": 1000 },
              "State": 2
            }
          }
        }
        """);

        var placement = WpfProfileImport.Read(profileDir).MainWindow;

        Assert.That(placement, Is.Not.Null);
        Assert.That(placement.X, Is.EqualTo(120));
        Assert.That(placement.Y, Is.EqualTo(80));
        Assert.That(placement.Width, Is.EqualTo(1600));
        Assert.That(placement.Height, Is.EqualTo(1000));
        Assert.That(placement.Maximized, Is.True);
    }

    [Test]
    public void Read_MainWindowPlacement_FromNamedState()
    {
        WriteFile("windowPositions.json", """
        {
          "Positions": {
            "Main_V2": {
              "Position": { "X": 10, "Y": 20 },
              "Size": { "X": 800, "Y": 600 },
              "State": "Normal"
            }
          }
        }
        """);

        var placement = WpfProfileImport.Read(profileDir).MainWindow;

        Assert.That(placement, Is.Not.Null);
        Assert.That(placement.Maximized, Is.False);
        Assert.That(placement.Width, Is.EqualTo(800));
    }

    [Test]
    public void Read_MainWindowPlacement_MissingEntryIsNull()
    {
        WriteFile("windowPositions.json", """{ "Positions": { "SettingsWindow": { "State": 0 } } }""");

        Assert.That(WpfProfileImport.Read(profileDir).MainWindow, Is.Null);
    }

    [Test]
    public void Read_CorruptFiles_DoNotThrow()
    {
        WriteFile("config.json", "{ this is not valid json");
        WriteFile("fullscreenConfig.json", "also broken");
        WriteFile("windowPositions.json", "]");

        var defaults = WpfProfileImport.Read(profileDir);

        Assert.That(defaults.Language, Is.Null);
        Assert.That(defaults.FullscreenMonitor, Is.Null);
        Assert.That(defaults.MainWindow, Is.Null);
    }

    [TestCase(-1, 3, ExpectedResult = null)]
    [TestCase(0, 0, ExpectedResult = null)]
    [TestCase(2, 2, ExpectedResult = null)]
    [TestCase(3, 2, ExpectedResult = null)]
    [TestCase(0, 2, ExpectedResult = 0)]
    [TestCase(1, 2, ExpectedResult = 1)]
    public int? MonitorSelection_Resolve(int requested, int screenCount) =>
        MonitorSelection.Resolve(requested, screenCount);
}
