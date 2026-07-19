using NUnit.Framework;
using Playnite.Common;
using Playnite.Common.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Playnite.Tests.Web
{
    [TestFixture]
    public class DownloaderTests
    {
        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;

            public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                this.send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return send(request, cancellationToken);
            }
        }

        [Test]
        public void DownloadStringPreservesCookiesAndRequestedEncoding()
        {
            string cookieHeader = null;
            using (var client = new HttpClient(new DelegateHandler((request, _) =>
            {
                cookieHeader = string.Join("; ", request.Headers.GetValues("Cookie"));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.Latin1.GetBytes("smörgås"))
                });
            })))
            {
                var downloader = new Downloader(client);
                var result = downloader.DownloadString(
                    "https://localhost/content",
                    new List<Cookie>
                    {
                        new Cookie("session", "abc123"),
                        new Cookie("locale", "sv-SE")
                    },
                    Encoding.Latin1);

                Assert.AreEqual("smörgås", result);
                Assert.AreEqual("session=abc123; locale=sv-SE", cookieHeader);
            }
        }

        [Test]
        public async Task DownloadFileReportsProgressAndWritesCompletePayload()
        {
            var payload = Encoding.UTF8.GetBytes(new string('x', 200_000));
            using (var temp = TempDirectory.Create())
            using (var client = new HttpClient(new DelegateHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                }))))
            {
                var progress = new List<DownloadProgress>();
                var path = Path.Combine(temp.TempPath, "downloads", "payload.bin");

                await new Downloader(client).DownloadFileAsync(
                    "https://localhost/payload",
                    path,
                    progress.Add);

                CollectionAssert.AreEqual(payload, File.ReadAllBytes(path));
                Assert.IsNotEmpty(progress);
                Assert.AreEqual(payload.Length, progress[^1].BytesReceived);
                Assert.AreEqual(100, progress[^1].ProgressPercentage);
            }
        }

        [Test]
        public void DownloadDataReturnsEmptyPayloadWhenCallerCancels()
        {
            using (var client = new HttpClient(new DelegateHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("The cancellation token was ignored.");
            })))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var result = new Downloader(client).DownloadData(
                    "https://localhost/cancel",
                    cancellation.Token);

                Assert.IsEmpty(result);
            }
        }
    }
}
