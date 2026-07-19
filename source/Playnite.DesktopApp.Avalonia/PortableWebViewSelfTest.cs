using System.Net;
using System.Net.Sockets;
using System.Text;
using Playnite.Avalonia.WebView;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia;

internal static class PortableWebViewSelfTest
{
    public static async Task<IReadOnlyList<(string Name, string Detail)>> RunAsync()
    {
        await using var server = new LoopbackWebServer();
        using var factory = new AvaloniaWebViewFactory();
        using var view = factory.CreateOffscreenView(new WebViewSettings
        {
            UserAgent = "Playnite-Phase8-WebView-SelfTest"
        });

        var loadingStarted = false;
        var loadingFinished = false;
        view.LoadingChanged += (_, args) =>
        {
            loadingStarted |= args.IsLoading;
            loadingFinished |= !args.IsLoading;
        };

        await view.NavigateAsync(server.Address);
        var pageText = await view.GetPageTextAsync();
        Require(pageText.Contains("Phase 8 WebView Ready", StringComparison.Ordinal),
            $"Unexpected page text: '{pageText}'.");

        var script = await view.EvaluateScriptAsync(
            "document.getElementById('phase8-status').dataset.value");
        Require(script.Success && string.Equals(script.Result?.ToString(), "ready", StringComparison.Ordinal),
            $"JavaScript result was success={script.Success}, value='{script.Result}', message='{script.Message}'.");

        var cookies = await view.GetCookiesAsync();
        var serverCookie = cookies.SingleOrDefault(cookie => cookie.Name == "phase8ServerAuth");
        Require(serverCookie != null && serverCookie.HttpOnly,
            "The HttpOnly cookie emitted by the loopback server was not returned by the native cookie store.");
        if (OperatingSystem.IsLinux())
        {
            Require(serverCookie.SameSite == CookieSameSite.StrictMode,
                $"The server cookie SameSite policy was {serverCookie.SameSite}, not Strict.");
        }

        var sdkCookie = new HttpCookie
        {
            Name = "phase8SdkAuth",
            Value = "sdk-secret",
            Domain = server.Address.Host,
            Path = "/",
            HttpOnly = true,
            Secure = false,
            SameSite = OperatingSystem.IsLinux()
                ? CookieSameSite.StrictMode
                : CookieSameSite.Unspecified,
            Priority = CookiePriority.Medium
        };
        await view.SetCookieAsync(server.Address, sdkCookie);
        cookies = await view.GetCookiesAsync();
        var writtenCookie = cookies.SingleOrDefault(cookie => cookie.Name == sdkCookie.Name);
        Require(writtenCookie != null && writtenCookie.HttpOnly && writtenCookie.Value == sdkCookie.Value,
            "The SDK cookie did not round-trip through the native cookie store.");

        await view.DeleteCookiesAsync(server.Address, "phase8ServerAuth");
        await view.DeleteCookiesAsync(server.Address, "phase8SdkAuth");
        cookies = await view.GetCookiesAsync();
        Require(cookies.All(cookie =>
                cookie.Name != "phase8ServerAuth" && cookie.Name != "phase8SdkAuth"),
            "The native cookie store retained a cookie after SDK deletion.");
        Require(loadingStarted && loadingFinished,
            $"Navigation events were incomplete: started={loadingStarted}, finished={loadingFinished}.");

        var engine = view.View is global::Avalonia.Controls.NativeWebView native
            ? native.AdapterInfo?.ToString() ?? "native adapter"
            : view.View.GetType().Name;
        return
        [
            ("Native webview navigates and exposes page content", server.Address.AbsoluteUri),
            ("Native webview executes SDK JavaScript", script.Result?.ToString() ?? string.Empty),
            ("Native webview returns HttpOnly authentication cookies", serverCookie.Domain),
            ("SDK cookies round-trip through the platform store", writtenCookie.Name),
            ("SDK cookie deletion reaches the platform store", "phase8ServerAuth removed"),
            ("Native webview reports loading transitions", engine)
        ];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class LoopbackWebServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task acceptLoop;

        public Uri Address { get; }

        public LoopbackWebServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            Address = new Uri($"http://127.0.0.1:{endpoint.Port}/");
            acceptLoop = AcceptLoopAsync(cancellation.Token);
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            listener.Stop();
            try
            {
                await acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (SocketException) when (cancellation.IsCancellationRequested)
            {
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                await RespondAsync(client, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task RespondAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            string line;
            do
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            while (!string.IsNullOrEmpty(line));

            const string body =
                "<!doctype html><html><body><main id=\"phase8-status\" data-value=\"ready\">" +
                "Phase 8 WebView Ready</main></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                "Set-Cookie: phase8ServerAuth=server-secret; Path=/; HttpOnly; SameSite=Strict\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(headers, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
