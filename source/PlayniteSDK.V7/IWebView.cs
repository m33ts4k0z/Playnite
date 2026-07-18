using System.IO;
using Avalonia.Controls;
using Avalonia.Media;
using Playnite.SDK.Events;
using Playnite.SDK.WebViewModels;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public sealed class WebViewResourceLoadedEventArgs : EventArgs, IDisposable
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Request Request { get; init; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Response Response { get; init; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public UrlRequestStatus Status { get; init; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Stream ResponseContent { get; init; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public long ReceivedContentLength { get; init; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public void Dispose() => ResponseContent?.Dispose();
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public sealed class WebViewSettings
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool JavaScriptEnabled { get; set; } = true;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string UserAgent { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public int WindowWidth { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public int WindowHeight { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Color WindowBackground { get; set; } = Colors.Transparent;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool CaptureResponseContent { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Func<Request, Response, bool> ShouldCaptureResponseContent { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public sealed class JavaScriptEvaluationResult
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Message { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool Success { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public object Result { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IWebView : IDisposable
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    bool CanExecuteJavascriptInMainFrame { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Control View { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Window WindowHost { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Uri Address { get; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    event EventHandler<WebViewLoadingChangedEventArgs> LoadingChanged;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    event EventHandler<WebViewResourceLoadedEventArgs> ResourceLoaded;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task OpenAsync(bool modal = false, CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task NavigateAsync(Uri address, CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<string> GetPageTextAsync(CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<string> GetPageSourceAsync(CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<JavaScriptEvaluationResult> EvaluateScriptAsync(
        string script,
        CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<IReadOnlyList<HttpCookie>> GetCookiesAsync(CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task SetCookieAsync(Uri address, HttpCookie cookie, CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task DeleteCookiesAsync(Uri address, string name = null, CancellationToken cancellationToken = default);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    void Close();
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IWebViewFactory
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IWebView CreateOffscreenView(WebViewSettings settings = null);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    IWebView CreateView(WebViewSettings settings = null);
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public sealed class HttpCookie
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Name { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Value { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Domain { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Path { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public DateTime? Expires { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public DateTime Creation { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool Secure { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool HttpOnly { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public DateTime LastAccess { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public CookieSameSite SameSite { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public CookiePriority Priority { get; set; }
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum CookieSameSite
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Unspecified,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    NoRestriction,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    LaxMode,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    StrictMode
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum CookiePriority
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Low = -1,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Medium = 0,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    High = 1
}
