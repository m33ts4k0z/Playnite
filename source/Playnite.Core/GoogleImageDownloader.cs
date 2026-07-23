using AngleSharp.Html.Parser;
using Flurl;
using Newtonsoft.Json;
using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Playnite
{
    public enum WebImageSearchSource
    {
        Google,
        DuckDuckGo
    }

    public enum SafeSearchSettings
    {
        [Description(LOC.Default)]
        Default,
        [Description(LOC.EnabledTitle)]
        On,
        [Description(LOC.DisabledTitle)]
        Off
    }

    public class GoogleImage
    {
        [JsonProperty("ow")]
        public uint Width { get; set; }

        [JsonProperty("oh")]
        public uint Height { get; set; }

        [JsonProperty("ou")]
        public string ImageUrl { get; set; }

        [JsonProperty("tu")]
        public string ThumbUrl { get; set; }

        public string Size => $"{Width}x{Height}";
    }

    public class DDGImageSearchResult
    {
        public class Results
        {
            public uint height { get; set; }
            public uint width { get; set; }
            public string thumbnail { get; set; }
            public string image { get; set; }
        }

        public Results[] results { get; set; }
    }

    public class GoogleImageDownloader : IDisposable
    {
        private const int GoogleResultsReadAttempts = 50;
        private static readonly TimeSpan GoogleResultsReadDelay = TimeSpan.FromMilliseconds(100);
        private static ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// Host-supplied offscreen web view provider; the CEF-backed
        /// implementation lives in the UI assembly.
        /// </summary>
        public static Func<WebViewSettings, IWebView> CreateOffscreenView { get; set; } = _ =>
            throw new InvalidOperationException("No offscreen web view provider has been configured.");

        private readonly IWebView webView;
        private TaskCompletionSource<DDGImageSearchResult> ddgResult = null;

        public GoogleImageDownloader()
            : this(true)
        {
        }

        public GoogleImageDownloader(WebImageSearchSource source)
            : this(source == WebImageSearchSource.DuckDuckGo)
        {
        }

        private GoogleImageDownloader(bool captureDdgResponses)
        {
#if WINDOWS
            var settings = new WebViewSettings();
            if (captureDdgResponses)
            {
                settings.PassResourceContentStreamToCallback = true;
                settings.ShouldPassResourceContentFunc = (a) => UrlMatchesDdgImageSearch(a.Request.Url);
                settings.ResourceLoadedCallback = ResourceLoadedCallback;
            }

            webView = CreateOffscreenView(settings);
#else
            var settings = new WebViewSettings();
            if (captureDdgResponses)
            {
                settings.CaptureResponseContent = true;
                settings.ShouldCaptureResponseContent = (request, response) => UrlMatchesDdgImageSearch(request.Url);
            }

            webView = CreateOffscreenView(settings);
            if (captureDdgResponses)
            {
                webView.ResourceLoaded += ResourceLoaded;
            }
#endif
        }

        public void Dispose()
        {
#if !WINDOWS
            webView.ResourceLoaded -= ResourceLoaded;
#endif
            webView.Dispose();
        }

        private bool UrlMatchesDdgImageSearch(string url)
        {
            return url?.Contains("duckduckgo.com/i.js", StringComparison.OrdinalIgnoreCase) == true;
        }

#if WINDOWS
        private void ResourceLoadedCallback(WebViewResourceLoadedCallback args)
        {
            if (!UrlMatchesDdgImageSearch(args.Request.Url))
                return;

            args.ResponseContent.Seek(0, SeekOrigin.Begin);
            if (Serialization.TryFromJsonStream<DDGImageSearchResult>(args.ResponseContent, out var searchResult))
                ddgResult.TrySetResult(searchResult);
        }
#else
        private void ResourceLoaded(object sender, WebViewResourceLoadedEventArgs args)
        {
            if (!UrlMatchesDdgImageSearch(args.Request?.Url) || args.ResponseContent == null)
            {
                return;
            }

            if (args.ResponseContent.CanSeek)
            {
                args.ResponseContent.Seek(0, SeekOrigin.Begin);
            }

            if (Serialization.TryFromJsonStream<DDGImageSearchResult>(args.ResponseContent, out var searchResult))
            {
                ddgResult?.TrySetResult(searchResult);
            }
        }
#endif

        public List<GoogleImage> GetDdgImages(string searchTerm, bool transparent = false)
        {
            ddgResult = new TaskCompletionSource<DDGImageSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var url = new Url("https://duckduckgo.com");
            url.SetQueryParam("ia", "images");
            url.SetQueryParam("iax", "images");
            url.SetQueryParam("q", searchTerm);

            if (transparent)
                url.SetQueryParam("iaf", "type:transparent");

#if WINDOWS
            webView.NavigateAndWait(url.ToString());
#else
            webView.NavigateAsync(new Uri(url.ToString())).GetAwaiter().GetResult();
#endif
            if (!ddgResult.Task.Wait(TimeSpan.FromSeconds(10)))
                return new List<GoogleImage>();

            var results = ddgResult.Task.Result;
            if (results?.results.HasItems() == true)
            {
                return results.results.Select(a => new GoogleImage
                {
                    Height = a.height,
                    Width = a.width,
                    ThumbUrl = a.thumbnail,
                    ImageUrl = a.image,
                }).ToList();
            }

            return new List<GoogleImage>();
        }

        public async Task<List<GoogleImage>> GetImages(string searchTerm, SafeSearchSettings safeSearch, bool transparent = false)
        {
            var images = new List<GoogleImage>();
            var parser = new HtmlParser();
            var url = new Url(@"https://www.google.com/search");
            url.SetQueryParam("tbm", "isch");
            url.SetQueryParam("client", "firefox-b-d");
            url.SetQueryParam("source", "lnt");
            url.SetQueryParam("q", searchTerm);

            if (safeSearch == SafeSearchSettings.On)
            {
                url.SetQueryParam("safe", "on");
            }
            else if (safeSearch == SafeSearchSettings.Off)
            {
                url.SetQueryParam("safe", "off");
            }

            if (transparent)
            {
                url.SetQueryParam("tbs", "ic:trans");
            }

#if WINDOWS
            webView.NavigateAndWait(url.ToString());
            var currentAddress = webView.GetCurrentAddress();
#else
            await webView.NavigateAsync(new Uri(url.ToString())).ConfigureAwait(false);
            var currentAddress = webView.Address?.ToString();
#endif
            if (currentAddress?.StartsWith(@"https://consent.google.com", StringComparison.OrdinalIgnoreCase) == true)
            {
                // This rejects Google's consent form for cookies
                await webView.EvaluateScriptAsync(@"document.getElementsByTagName('form')[0].submit();");
                await Task.Delay(3000);
#if WINDOWS
                webView.NavigateAndWait(url.ToString());
#else
                await webView.NavigateAsync(new Uri(url.ToString())).ConfigureAwait(false);
#endif
            }

            var googleContent = string.Empty;
            for (var attempt = 0; attempt < GoogleResultsReadAttempts; attempt++)
            {
                googleContent = await webView.GetPageSourceAsync();
                images = ParseGoogleImages(googleContent, parser);
                if (images.HasItems())
                {
                    break;
                }

                if (attempt + 1 < GoogleResultsReadAttempts)
                {
                    await Task.Delay(GoogleResultsReadDelay);
                }
            }

            if (!images.HasItems())
            {
                logger.Error("Failed to parse any Google image results.");
                logger.Debug(googleContent);
            }

            return images;
        }

        private static List<GoogleImage> ParseGoogleImages(string googleContent, HtmlParser parser)
        {
            var images = new List<GoogleImage>();
            if (string.IsNullOrWhiteSpace(googleContent))
            {
                return images;
            }

            if (googleContent.Contains(".rg_meta", StringComparison.Ordinal))
            {
                var document = parser.ParseDocument(googleContent);
                foreach (var imageElem in document.QuerySelectorAll(".rg_meta"))
                {
                    images.Add(Serialization.FromJson<GoogleImage>(imageElem.InnerHtml));
                }

                return images;
            }

            var formatted = Regex.Replace(googleContent, @"\r\n?|\n", string.Empty);
            var matches = Regex.Matches(
                formatted,
                @"\[""(https:\/\/encrypted-[^,]+?)"",\d+,\d+\],\[""(http.+?)"",(\d+),(\d+)\]");
            foreach (Match match in matches)
            {
                var data = Serialization.FromJson<List<List<object>>>($"[{match.Value}]");
                var imageUrl = data[1][0].ToString();
                if (images.Any(a => a.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase)))
                    continue;

                images.Add(new GoogleImage
                {
                    ThumbUrl = data[0][0].ToString(),
                    ImageUrl = imageUrl,
                    Height = uint.Parse(data[1][1].ToString()),
                    Width = uint.Parse(data[1][2].ToString())
                });
            }

            return images;
        }
    }
}
