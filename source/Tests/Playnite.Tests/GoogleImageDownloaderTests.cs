using Moq;
using NUnit.Framework;
using Playnite.SDK;
using System;

namespace Playnite.Tests
{
    [TestFixture]
    [NonParallelizable]
    public class GoogleImageDownloaderTests
    {
        private Func<WebViewSettings, IWebView> originalWebViewFactory;

        [SetUp]
        public void SetUp()
        {
            originalWebViewFactory = GoogleImageDownloader.CreateOffscreenView;
        }

        [TearDown]
        public void TearDown()
        {
            GoogleImageDownloader.CreateOffscreenView = originalWebViewFactory;
        }

        [Test]
        public void GoogleOnlyDownloaderDoesNotRequestResponseCapture()
        {
            WebViewSettings capturedSettings = null;
            var webView = new Mock<IWebView>();
            GoogleImageDownloader.CreateOffscreenView = settings =>
            {
                capturedSettings = settings;
                return webView.Object;
            };

            using (var downloader = new GoogleImageDownloader(WebImageSearchSource.Google))
            {
                Assert.That(capturedSettings, Is.Not.Null);
                Assert.That(capturedSettings.PassResourceContentStreamToCallback, Is.False);
                Assert.That(capturedSettings.ShouldPassResourceContentFunc, Is.Null);
                Assert.That(capturedSettings.ResourceLoadedCallback, Is.Null);
            }
        }

        [Test]
        public void DefaultDownloaderPreservesDuckDuckGoResponseCapture()
        {
            WebViewSettings capturedSettings = null;
            var webView = new Mock<IWebView>();
            GoogleImageDownloader.CreateOffscreenView = settings =>
            {
                capturedSettings = settings;
                return webView.Object;
            };

            using (var downloader = new GoogleImageDownloader())
            {
                Assert.That(capturedSettings, Is.Not.Null);
                Assert.That(capturedSettings.PassResourceContentStreamToCallback, Is.True);
                Assert.That(capturedSettings.ShouldPassResourceContentFunc, Is.Not.Null);
                Assert.That(capturedSettings.ResourceLoadedCallback, Is.Not.Null);
            }
        }
    }
}
