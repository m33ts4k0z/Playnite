using NUnit.Framework;
using System;
using System.IO;

namespace Playnite.Tests
{
    [TestFixture]
    public class PlaynitePathsShellSelectionTests
    {
        private static readonly string programPath = PlaynitePaths.ProgramPath;
        private static readonly string wpfDesktop = Path.Combine(programPath, "Playnite.DesktopApp.exe");
        private static readonly string avaloniaDesktop = Path.Combine(programPath, "Playnite.DesktopApp.Avalonia.exe");
        private static readonly string flagFile = Path.Combine(programPath, "avalonia.flag");

        private bool createdWpf;
        private bool createdAvalonia;

        [SetUp]
        public void SetUp()
        {
            // The test host directory contains neither shell executable; create
            // stand-ins as each case needs them.
            createdWpf = false;
            createdAvalonia = false;
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("PLAYNITE_AVALONIA", null);
            File.Delete(flagFile);
            if (createdWpf)
            {
                File.Delete(wpfDesktop);
            }

            if (createdAvalonia)
            {
                File.Delete(avaloniaDesktop);
            }

            PlaynitePaths.RefreshShellExecutables();
        }

        private void CreateWpfStandIn()
        {
            File.WriteAllText(wpfDesktop, string.Empty);
            createdWpf = true;
        }

        private void CreateAvaloniaStandIn()
        {
            File.WriteAllText(avaloniaDesktop, string.Empty);
            createdAvalonia = true;
        }

        [Test]
        public void DefaultsToWpfShellWithoutOptIn()
        {
            CreateWpfStandIn();
            CreateAvaloniaStandIn();
            PlaynitePaths.RefreshShellExecutables();
            Assert.AreEqual(wpfDesktop, PlaynitePaths.DesktopExecutablePath);
        }

        [Test]
        public void FlagFileSelectsAvaloniaShell()
        {
            CreateWpfStandIn();
            CreateAvaloniaStandIn();
            File.WriteAllText(flagFile, string.Empty);
            PlaynitePaths.RefreshShellExecutables();
            Assert.AreEqual(avaloniaDesktop, PlaynitePaths.DesktopExecutablePath);
        }

        [Test]
        public void EnvironmentVariableSelectsAvaloniaShell()
        {
            CreateWpfStandIn();
            CreateAvaloniaStandIn();
            Environment.SetEnvironmentVariable("PLAYNITE_AVALONIA", "1");
            PlaynitePaths.RefreshShellExecutables();
            Assert.AreEqual(avaloniaDesktop, PlaynitePaths.DesktopExecutablePath);
        }

        [Test]
        public void MissingWpfShellFallsBackToAvalonia()
        {
            CreateAvaloniaStandIn();
            PlaynitePaths.RefreshShellExecutables();
            Assert.AreEqual(avaloniaDesktop, PlaynitePaths.DesktopExecutablePath);
        }

        [Test]
        public void OptInWithoutAvaloniaBinariesKeepsWpfShell()
        {
            CreateWpfStandIn();
            File.WriteAllText(flagFile, string.Empty);
            PlaynitePaths.RefreshShellExecutables();
            Assert.AreEqual(wpfDesktop, PlaynitePaths.DesktopExecutablePath);
        }
    }
}
