using Moq;
using NUnit.Framework;
using Playnite.SDK;
using System;
using System.Threading.Tasks;

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

        [Test]
        public async Task GoogleSearchWaitsForResultsAfterInitialPageShell()
        {
            const string imageUrl = "https://example.com/image.jpg";
            var webView = new Mock<IWebView>();
            webView.Setup(a => a.GetCurrentAddress()).Returns("https://www.google.com/search?tbm=isch");
            webView.SetupSequence(a => a.GetPageSourceAsync())
                .ReturnsAsync(
                    "<html><head><title>test game cover - Google Search</title></head>" +
                    "<body><main>Loading results</main></body></html>")
                .ReturnsAsync(
                    "<html><body>[\"https://encrypted-tbn0.gstatic.com/images?q=tbn:test\",100,150]," +
                    "[\"https://example.com/image.jpg\",600,900]</body></html>");
            GoogleImageDownloader.CreateOffscreenView = _ => webView.Object;

            using (var downloader = new GoogleImageDownloader(WebImageSearchSource.Google))
            {
                var images = await downloader.GetImages("test game cover", SafeSearchSettings.Default);

                Assert.That(images, Has.Count.EqualTo(1));
                Assert.That(images[0].ImageUrl, Is.EqualTo(imageUrl));
                webView.Verify(a => a.GetPageSourceAsync(), Times.Exactly(2));
            }
        }
    }
}
