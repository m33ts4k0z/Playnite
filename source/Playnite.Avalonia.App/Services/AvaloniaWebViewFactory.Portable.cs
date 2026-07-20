#if !WINDOWS
using Avalonia.Controls;
using Playnite.SDK;
using PortableWebViewFactory = Playnite.Avalonia.WebView.AvaloniaWebViewFactory;

namespace Playnite.Avalonia.App.Services;

/// <summary>
/// Portable SDK v7 facade over the shared cross-platform web-view project.
/// The Windows leg keeps its SDK v6 adapter because that contract is
/// synchronous and WPF-typed.
/// </summary>
internal sealed class AvaloniaWebViewFactory : IWebViewFactory, IDisposable
{
    private readonly PortableWebViewFactory inner = new();

    public IWebView CreateOffscreenView(WebViewSettings settings = null) =>
        inner.CreateOffscreenView(settings);

    public IWebView CreateView(WebViewSettings settings = null) => inner.CreateView(settings);

    internal V7WebViewInstance CreateV7View(V7WebViewCreationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var source = payload.Settings ?? new V7WebViewSettingsPayload();
        var settings = new WebViewSettings
        {
            JavaScriptEnabled = source.JavaScriptEnabled,
            UserAgent = source.UserAgent,
            WindowWidth = source.WindowWidth,
            WindowHeight = source.WindowHeight,
            WindowBackground = new global::Avalonia.Media.Color(
                source.BackgroundA,
                source.BackgroundR,
                source.BackgroundG,
                source.BackgroundB)
        };
        var view = payload.Offscreen
            ? inner.CreateOffscreenView(settings)
            : inner.CreateView(settings);
        return new V7WebViewInstance(new AvaloniaSdkWebView(view));
    }

    public void Dispose() => inner.Dispose();
}

/// <summary>
/// Adapts the portable asynchronous SDK v7 web view to the internal bridge
/// shape used by the isolated SDK v7 host.
/// </summary>
internal sealed class AvaloniaSdkWebView : IDisposable
{
    private readonly IWebView inner;

    public bool CanExecuteJavascriptInMainFrame => inner.CanExecuteJavascriptInMainFrame;
    public Control AvaloniaView => inner.View;
    public Window AvaloniaWindowHost => inner.WindowHost;

    public event EventHandler<Playnite.SDK.Events.WebViewLoadingChangedEventArgs> LoadingChanged
    {
        add => inner.LoadingChanged += value;
        remove => inner.LoadingChanged -= value;
    }

    public AvaloniaSdkWebView(IWebView inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public string GetCurrentAddress() => inner.Address?.AbsoluteUri ?? string.Empty;

    public void Open() => inner.OpenAsync(false).GetAwaiter().GetResult();

    public void OpenDialog() => inner.OpenAsync(true).GetAwaiter().GetResult();

    public void NavigateAndWait(string url) =>
        inner.NavigateAsync(new Uri(url, UriKind.Absolute)).GetAwaiter().GetResult();

    public Task<string> GetPageTextAsync() => inner.GetPageTextAsync();

    public Task<string> GetPageSourceAsync() => inner.GetPageSourceAsync();

    public Task<JavaScriptEvaluationResult> EvaluateScriptAsync(string script) =>
        inner.EvaluateScriptAsync(script);

    public List<HttpCookie> GetCookies() =>
        inner.GetCookiesAsync().GetAwaiter().GetResult().ToList();

    public void SetCookies(string url, HttpCookie cookie) =>
        inner.SetCookieAsync(new Uri(url, UriKind.Absolute), cookie).GetAwaiter().GetResult();

    public void DeleteDomainCookies(string domain) =>
        inner.DeleteCookiesAsync(CreateDomainUri(domain)).GetAwaiter().GetResult();

    public void DeleteCookies(string url, string name) =>
        inner.DeleteCookiesAsync(new Uri(url, UriKind.Absolute), name).GetAwaiter().GetResult();

    public void Close() => inner.Close();

    public void Dispose() => inner.Dispose();

    private static Uri CreateDomainUri(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var normalized = domain.Trim().TrimStart('.');
        return new Uri($"https://{normalized}/", UriKind.Absolute);
    }
}
#endif
