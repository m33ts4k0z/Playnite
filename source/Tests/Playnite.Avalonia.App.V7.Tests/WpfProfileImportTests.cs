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
        Assert.That(defaults.FullscreenInterfaceVolume, Is.Null);
        Assert.That(defaults.FullscreenBackgroundVolume, Is.Null);
        Assert.That(defaults.FullscreenMuteInBackground, Is.Null);
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
    public void Read_FullscreenAudioAndGeneralDefaults_WithVolumeScaleBridge()
    {
        WriteFile("fullscreenConfig.json", """
        {
          "InterfaceVolume": 0.42,
          "BackgroundVolume": 25,
          "MuteInBackground": false,
          "UsePrimaryDisplay": true,
          "ShowClock": false,
          "ShowBattery": true,
          "ShowBatteryPercentage": true,
          "MinimizeAfterGameStartup": false
        }
        """);

        var defaults = WpfProfileImport.Read(profileDir);

        Assert.That(defaults.FullscreenInterfaceVolume, Is.EqualTo(42));
        Assert.That(defaults.FullscreenBackgroundVolume, Is.EqualTo(25));
        Assert.That(defaults.FullscreenMuteInBackground, Is.False);
        Assert.That(defaults.FullscreenUsePrimaryDisplay, Is.True);
        Assert.That(defaults.FullscreenShowClock, Is.False);
        Assert.That(defaults.FullscreenShowBattery, Is.True);
        Assert.That(defaults.FullscreenShowBatteryPercentage, Is.True);
        Assert.That(defaults.FullscreenMinimizeAfterGameStartup, Is.False);
    }

    [Test]
    public void Read_FullscreenLayoutAndVisualDefaults()
    {
        WriteFile("config.json", """{ "FullscreenItemSpacing": 24 }""");
        WriteFile("fullscreenConfig.json", """
        {
          "Theme": "theme-id",
          "Rows": 3,
          "Columns": 6,
          "HorizontalLayout": true,
          "SmoothScrolling": false,
          "DarkenUninstalledGamesGrid": true,
          "EnableMainBackgroundImage": true,
          "MainBackgroundImageBlurAmount": 17,
          "MainBackgroundImageDarkAmount": 45.5,
          "ShowGameTitles": true,
          "FontSize": 25,
          "FontSizeSmall": 19,
          "ButtonPrompts": 1
        }
        """);

        var defaults = WpfProfileImport.Read(profileDir);

        Assert.That(defaults.FullscreenTheme, Is.EqualTo("theme-id"));
        Assert.That(defaults.FullscreenRows, Is.EqualTo(3));
        Assert.That(defaults.FullscreenColumns, Is.EqualTo(6));
        Assert.That(defaults.FullscreenHorizontalLayout, Is.True);
        Assert.That(defaults.FullscreenItemSpacing, Is.EqualTo(24));
        Assert.That(defaults.FullscreenSmoothScrolling, Is.False);
        Assert.That(defaults.FullscreenDarkenUninstalledGamesGrid, Is.True);
        Assert.That(defaults.FullscreenEnableMainBackgroundImage, Is.True);
        Assert.That(defaults.FullscreenMainBackgroundImageBlurAmount, Is.EqualTo(17));
        Assert.That(defaults.FullscreenMainBackgroundImageDarkAmount, Is.EqualTo(45.5));
        Assert.That(defaults.FullscreenShowGameTitles, Is.True);
        Assert.That(defaults.FullscreenFontSize, Is.EqualTo(25));
        Assert.That(defaults.FullscreenFontSizeSmall, Is.EqualTo(19));
        Assert.That(defaults.FullscreenButtonPrompts, Is.EqualTo(1));
    }

    [Test]
    public void Read_FullscreenMenuDefaults()
    {
        WriteFile("fullscreenConfig.json", """
        {
          "MainMenuShowRestart": false,
          "MainMenuShowShutdown": false,
          "MainMenuShowSuspend": false,
          "MainMenuShowHibernate": false,
          "MainMenuShowMinimize": false,
          "MainMenuShowLogout": true,
          "MainMenuShowLock": true,
          "MainMenuShowTools": false,
          "MainMenuShowExtensions": false,
          "MainMenuShowClients": false
        }
        """);

        var defaults = WpfProfileImport.Read(profileDir);

        Assert.That(defaults.FullscreenMainMenuShowRestart, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowShutdown, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowSuspend, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowHibernate, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowMinimize, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowLogout, Is.True);
        Assert.That(defaults.FullscreenMainMenuShowLock, Is.True);
        Assert.That(defaults.FullscreenMainMenuShowTools, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowExtensions, Is.False);
        Assert.That(defaults.FullscreenMainMenuShowClients, Is.False);
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
