using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Playnite.Common;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Playnite.Tests
{
    [TestFixture]
    public class ExtensionFactoryTests
    {
        [Test]
        public void DeduplicateExtListTest()
        {
            var manifests = new List<BaseExtensionManifest>
            {
                new ExtensionManifest { Id = "test1", Version = "1.0" },
                new ExtensionManifest { Id = "test1", Version = "3.0" },
                new ExtensionManifest { Id = "test1", Version = "2.0.2" },
                new ExtensionManifest { Id = "test2", Version = "5.0.1.2" },
                new ExtensionManifest { Id = "test3", Version = "1.0" },
                new ExtensionManifest { Id = "test3", Version = "1.0.1" },
            };

            var list = ExtensionFactory.DeduplicateExtList(manifests).ToList();
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("3.0", list[0].Version);
            Assert.AreEqual("5.0.1.2", list[1].Version);
            Assert.AreEqual("1.0.1", list[2].Version);
        }

        [Test]
        public void LoadPluginFromExtensionDirectoryTest()
        {
            var extensionDirectory = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Extensions",
                "TestGameLibrary");
            var controllers = new GameControllerFactory();
            using (var factory = new ExtensionFactory(
                Mock.Of<IGameDatabase>(),
                controllers,
                _ => Mock.Of<IPlayniteAPI>()))
            {
                factory.LoadPlugins(
                    new List<string> { "Test_Generic_Plugin" },
                    false,
                    new List<string> { extensionDirectory });

                Assert.IsEmpty(factory.FailedExtensions);
                Assert.AreEqual(1, factory.LibraryPlugins.Count);
                var plugin = factory.LibraryPlugins.Single();
                Assert.AreEqual("Test Library", plugin.Name);

                var loadContext = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly);
                Assert.IsNotNull(loadContext);
                Assert.AreNotSame(AssemblyLoadContext.Default, loadContext);
                Assert.IsTrue(loadContext.IsCollectible);
                Assert.AreSame(typeof(Plugin).Assembly, plugin.GetType().BaseType.Assembly);
            }
        }

        [Test]
        public void PluginLoadFailureRetainsDiagnosticTest()
        {
            using (var tempDirectory = TempDirectory.Create())
            {
                File.WriteAllText(
                    Path.Combine(tempDirectory.TempPath, PlaynitePaths.ExtensionManifestFileName),
                    "Id: Missing_Module_Test\n" +
                    "Name: Missing module test\n" +
                    "Version: 1.0\n" +
                    "Module: Missing.Plugin.dll\n" +
                    "Type: GenericPlugin\n");
                var controllers = new GameControllerFactory();
                using (var factory = new ExtensionFactory(
                    Mock.Of<IGameDatabase>(),
                    controllers,
                    _ => Mock.Of<IPlayniteAPI>()))
                {
                    factory.LoadPlugins(
                        new List<string>(),
                        false,
                        new List<string> { tempDirectory.TempPath });

                    Assert.AreEqual(factory.FailedExtensions.Count, factory.LoadFailures.Count);
                    var failure = factory.LoadFailures.Single(item =>
                        item.Manifest.Id == "Missing_Module_Test");
                    Assert.AreEqual("Missing_Module_Test", failure.Manifest.Id);
                    StringAssert.Contains("Missing.Plugin.dll", failure.Message);
                    Assert.IsNotEmpty(failure.ExceptionType);
                    Assert.IsNotEmpty(failure.Details);
                }
            }
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void IsolatedPluginLoadsWpfResourcesTest()
        {
            using (var tempDirectory = TempDirectory.Create())
            {
                var paths = new Mock<IPlaynitePathsAPI>();
                paths.SetupGet(item => item.ExtensionsDataPath).Returns(tempDirectory.TempPath);
                var api = new Mock<IPlayniteAPI>();
                api.SetupGet(item => item.Paths).Returns(paths.Object);
                api.SetupGet(item => item.Notifications).Returns(Mock.Of<INotificationsAPI>());

                var extensionDirectory = Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "Extensions",
                    "Tests");
                var controllers = new GameControllerFactory();
                using (var factory = new ExtensionFactory(
                    Mock.Of<IGameDatabase>(),
                    controllers,
                    _ => api.Object))
                {
                    factory.LoadPlugins(
                        new List<string> { "Test_GameLibrary" },
                        false,
                        new List<string> { extensionDirectory });

                    Assert.IsEmpty(factory.FailedExtensions);
                    Assert.AreEqual(1, factory.GenericPlugins.Count);
                    var plugin = factory.GenericPlugins.Single();
                    var settingsView = plugin.GetSettingsView(false);
                    Assert.IsNotNull(settingsView);
                    Assert.AreEqual("TestPluginSettingsView", settingsView.GetType().Name);
                    Assert.AreSame(plugin.GetType().Assembly, settingsView.GetType().Assembly);
                }
            }
        }
    }
}
