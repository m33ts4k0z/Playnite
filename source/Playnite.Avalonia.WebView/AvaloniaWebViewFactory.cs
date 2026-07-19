using Avalonia.Controls;
using Playnite.SDK;

namespace Playnite.Avalonia.WebView;

/// <summary>
/// Creates SDK v7 web views backed by Avalonia's native web engine for the active operating system.
/// </summary>
public sealed class AvaloniaWebViewFactory : IWebViewFactory, IDisposable
{
    private readonly object viewsLock = new();
    private readonly HashSet<AvaloniaSdkWebView> views = new();
    private readonly string dataDirectory;
    private readonly string cacheDirectory;
    private bool disposed;

    /// <summary>Creates a factory using the platform web engine's default profile directories.</summary>
    public AvaloniaWebViewFactory()
    {
    }

    /// <summary>Creates a factory using explicit persistent data and cache directories.</summary>
    public AvaloniaWebViewFactory(string dataDirectory, string cacheDirectory = null)
    {
        this.dataDirectory = NormalizeDirectory(dataDirectory, nameof(dataDirectory));
        this.cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(this.dataDirectory, "Cache")
            : NormalizeDirectory(cacheDirectory, nameof(cacheDirectory));
    }

    /// <inheritdoc />
    public IWebView CreateOffscreenView(WebViewSettings settings = null) => Create(settings, true);

    /// <inheritdoc />
    public IWebView CreateView(WebViewSettings settings = null) => Create(settings, false);

    /// <inheritdoc />
    public void Dispose()
    {
        List<AvaloniaSdkWebView> openViews;
        lock (viewsLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            openViews = views.ToList();
            views.Clear();
        }

        foreach (var view in openViews)
        {
            view.Dispose();
        }
    }

    private AvaloniaSdkWebView Create(WebViewSettings settings, bool offscreen)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        settings ??= new WebViewSettings();
        WebViewPolicy.Validate(settings);

        AvaloniaSdkWebView view = null;
        try
        {
            view = new AvaloniaSdkWebView(
                settings,
                offscreen,
                dataDirectory,
                cacheDirectory,
                () => Release(view));
            lock (viewsLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                views.Add(view);
            }

            return view;
        }
        catch
        {
            view?.Dispose();
            throw;
        }
    }

    private void Release(AvaloniaSdkWebView view)
    {
        lock (viewsLock)
        {
            views.Remove(view);
        }
    }

    private static string NormalizeDirectory(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        return Path.GetFullPath(path);
    }
}

internal static class WebViewPolicy
{
    public static void Validate(WebViewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.JavaScriptEnabled)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView does not expose per-view JavaScript disabling. " +
                "The web view was not created because ignoring this setting would weaken the requested policy.");
        }

        if (settings.CaptureResponseContent || settings.ShouldCaptureResponseContent != null)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView does not expose completed response bodies. " +
                "Response capture was requested and cannot be silently omitted.");
        }
    }

    public static bool DomainMatches(string host, string domain)
    {
        var normalizedDomain = domain?.TrimStart('.') ?? string.Empty;
        return normalizedDomain.Length > 0 &&
            (string.Equals(host, normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
             host.EndsWith($".{normalizedDomain}", StringComparison.OrdinalIgnoreCase));
    }
}
