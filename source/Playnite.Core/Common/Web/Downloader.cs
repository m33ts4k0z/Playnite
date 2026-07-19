using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Playnite.Common.Web
{
    public sealed class DownloadProgress
    {
        public long BytesReceived { get; }

        public long? TotalBytesToReceive { get; }

        public int ProgressPercentage => TotalBytesToReceive > 0
            ? (int)Math.Min(100, BytesReceived * 100 / TotalBytesToReceive.Value)
            : 0;

        public DownloadProgress(long bytesReceived, long? totalBytesToReceive)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
        }
    }

    public interface IDownloader
    {
        string DownloadString(IEnumerable<string> mirrors);

        string DownloadString(string url);

        string DownloadString(string url, Encoding encoding);

        string DownloadString(string url, List<Cookie> cookies);

        string DownloadString(string url, List<Cookie> cookies, Encoding encoding);

        void DownloadString(string url, string path);

        void DownloadString(string url, string path, Encoding encoding);

        byte[] DownloadData(string url);

        void DownloadFile(string url, string path);

        void DownloadFile(IEnumerable<string> mirrors, string path);

        Task DownloadFileAsync(string url, string path, Action<DownloadProgress> progressHandler);

        Task DownloadFileAsync(IEnumerable<string> mirrors, string path, Action<DownloadProgress> progressHandler);
    }

    public class Downloader : IDownloader
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string PlayniteUserAgent = "Playnite 10";
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
        private static readonly HttpClient sharedClient = CreateClient();
        private readonly HttpClient client;

        public Downloader()
        {
            client = sharedClient;
        }

        internal Downloader(HttpClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string DownloadString(IEnumerable<string> mirrors)
        {
            logger.Debug("Downloading string content from multiple mirrors.");
            foreach (var mirror in mirrors)
            {
                try
                {
                    return DownloadString(mirror);
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Failed to download {mirror} string.");
                }
            }

            throw new Exception("Failed to download string from all mirrors.");
        }

        public string DownloadString(string url)
        {
            return DownloadString(url, Encoding.UTF8);
        }

        public string DownloadString(string url, CancellationToken cancelToken)
        {
            logger.Debug($"Downloading string content from {url} using UTF8 encoding.");
            try
            {
                return DownloadStringAsync(url, Encoding.UTF8, null, cancelToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                logger.Warn("Download canceled.");
                return null;
            }
        }

        public string DownloadString(string url, Encoding encoding)
        {
            logger.Debug($"Downloading string content from {url} using {encoding} encoding.");
            return DownloadStringAsync(url, encoding, null, CancellationToken.None).GetAwaiter().GetResult();
        }

        public string DownloadString(string url, List<Cookie> cookies)
        {
            return DownloadString(url, cookies, Encoding.UTF8);
        }

        public string DownloadString(string url, List<Cookie> cookies, Encoding encoding)
        {
            logger.Debug($"Downloading string content from {url} using cookies and {encoding} encoding.");
            return DownloadStringAsync(url, encoding, cookies, CancellationToken.None).GetAwaiter().GetResult();
        }

        public void DownloadString(string url, string path)
        {
            DownloadString(url, path, Encoding.UTF8);
        }

        public void DownloadString(string url, string path, Encoding encoding)
        {
            logger.Debug($"Downloading string content from {url} to {path} using {encoding} encoding.");
            var data = DownloadString(url, encoding);
            File.WriteAllText(path, data, encoding);
        }

        public byte[] DownloadData(string url)
        {
            logger.Debug($"Downloading data from {url}.");
            return DownloadDataAsync(url, CancellationToken.None).GetAwaiter().GetResult();
        }

        public byte[] DownloadData(string url, CancellationToken cancelToken)
        {
            logger.Debug($"Downloading data from {url}.");
            try
            {
                return DownloadDataAsync(url, cancelToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                logger.Warn("Download canceled.");
                return Array.Empty<byte>();
            }
        }

        public void DownloadFile(string url, string path)
        {
            DownloadFile(url, path, CancellationToken.None);
        }

        public void DownloadFile(string url, string path, CancellationToken cancelToken)
        {
            logger.Debug($"Downloading data from {url} to {path}.");
            try
            {
                DownloadFileInternalAsync(url, path, null, cancelToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                logger.Warn("Download canceled.");
            }
        }

        public Task DownloadFileAsync(string url, string path, Action<DownloadProgress> progressHandler)
        {
            logger.Debug($"Downloading data async from {url} to {path}.");
            return DownloadFileInternalAsync(url, path, progressHandler, CancellationToken.None);
        }

        public async Task DownloadFileAsync(
            IEnumerable<string> mirrors,
            string path,
            Action<DownloadProgress> progressHandler)
        {
            logger.Debug("Downloading data async from multiple mirrors.");
            foreach (var mirror in mirrors)
            {
                try
                {
                    await DownloadFileAsync(mirror, path, progressHandler).ConfigureAwait(false);
                    return;
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Failed to download {mirror} file.");
                }
            }

            throw new Exception("Failed to download file from all mirrors.");
        }

        public void DownloadFile(IEnumerable<string> mirrors, string path)
        {
            logger.Debug("Downloading data from multiple mirrors.");
            foreach (var mirror in mirrors)
            {
                try
                {
                    DownloadFile(mirror, path);
                    return;
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Failed to download {mirror} file.");
                }
            }

            throw new Exception("Failed to download file from all mirrors.");
        }

        private static HttpClient CreateClient()
        {
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = ConnectTimeout,
                AutomaticDecompression = DecompressionMethods.All
            };
            var client = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", PlayniteUserAgent);
            return client;
        }

        private async Task<string> DownloadStringAsync(
            string url,
            Encoding encoding,
            IEnumerable<Cookie> cookies,
            CancellationToken cancelToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (cookies?.Any() == true)
                {
                    request.Headers.TryAddWithoutValidation(
                        "Cookie",
                        string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}")));
                }

                using (var response = await client.SendAsync(request, cancelToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var data = await response.Content.ReadAsByteArrayAsync(cancelToken).ConfigureAwait(false);
                    return encoding.GetString(data);
                }
            }
        }

        private async Task<byte[]> DownloadDataAsync(string url, CancellationToken cancelToken)
        {
            using (var response = await client.GetAsync(url, cancelToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancelToken).ConfigureAwait(false);
            }
        }

        private async Task DownloadFileInternalAsync(
            string url,
            string path,
            Action<DownloadProgress> progressHandler,
            CancellationToken cancelToken)
        {
            FileSystem.CreateDirectory(Path.GetDirectoryName(path));
            using (var response = await client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancelToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;
                using (var input = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false))
                using (var output = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true))
                {
                    var buffer = new byte[81920];
                    long bytesReceived = 0;
                    int bytesRead;
                    while ((bytesRead = await input.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancelToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, bytesRead, cancelToken).ConfigureAwait(false);
                        bytesReceived += bytesRead;
                        progressHandler?.Invoke(new DownloadProgress(bytesReceived, totalBytes));
                    }
                }
            }
        }
    }
}
