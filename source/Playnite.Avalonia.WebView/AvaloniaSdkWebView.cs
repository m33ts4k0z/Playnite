using System.Net;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Playnite.SDK;
using Playnite.SDK.Events;
using AvaloniaNavigationCompletedEventArgs = Avalonia.Controls.WebViewNavigationCompletedEventArgs;
using AvaloniaNavigationStartingEventArgs = Avalonia.Controls.WebViewNavigationStartingEventArgs;
using SdkJavaScriptResult = Playnite.SDK.JavaScriptEvaluationResult;

namespace Playnite.Avalonia.WebView;

internal sealed class AvaloniaSdkWebView : IWebView
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    private readonly WebViewSettings settings;
    private readonly bool offscreen;
    private readonly string dataDirectory;
    private readonly string cacheDirectory;
    private readonly Action release;
    private readonly object navigationLock = new();
    private readonly TaskCompletionSource<object> adapterReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object> windowClosed = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private NativeWebView browser;
    private Window window;
    private TaskCompletionSource<AvaloniaNavigationCompletedEventArgs> navigationCompletion;
    private bool initialized;
    private bool closed;
    private bool disposed;

    public AvaloniaSdkWebView(
        WebViewSettings settings,
        bool offscreen,
        string dataDirectory,
        string cacheDirectory,
        Action release)
    {
        this.settings = settings;
        this.offscreen = offscreen;
        this.dataDirectory = dataDirectory;
        this.cacheDirectory = cacheDirectory;
        this.release = release;
        if (offscreen)
        {
            InvokeOnUi(CreateControls, DispatcherPriority.Background);
        }
        else
        {
            InvokeOnUi(CreateControls);
        }
    }

    public bool CanExecuteJavascriptInMainFrame =>
        !disposed && !closed && adapterReady.Task.IsCompletedSuccessfully;

    public Control View => browser;

    public Window WindowHost => offscreen ? null : window;

    public Uri Address => InvokeOnUi(() => browser.Source);

    public event EventHandler<WebViewLoadingChangedEventArgs> LoadingChanged;

    public event EventHandler<WebViewResourceLoadedEventArgs> ResourceLoaded
    {
        add => throw new NotSupportedException(
            "Avalonia NativeWebView does not expose completed response metadata or response bodies.");
        remove { }
    }

    public async Task OpenAsync(bool modal = false, CancellationToken cancellationToken = default)
    {
        ThrowIfOffscreen(nameof(OpenAsync));
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var owner = await InvokeOnUiAsync(ShowOnScreen).ConfigureAwait(false);
        if (!modal)
        {
            return;
        }

        if (owner != null)
        {
            await InvokeOnUiAsync(() => owner.IsEnabled = false).ConfigureAwait(false);
        }

        try
        {
            await windowClosed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (owner != null)
            {
                await InvokeOnUiAsync(() =>
                {
                    owner.IsEnabled = true;
                    owner.Activate();
                }).ConfigureAwait(false);
            }
        }
    }

    public async Task NavigateAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri)
        {
            throw new ArgumentException("A web-view address must be absolute.", nameof(address));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var completion = new TaskCompletionSource<AvaloniaNavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (navigationLock)
        {
            if (navigationCompletion != null)
            {
                throw new InvalidOperationException("A web-view navigation is already in progress.");
            }

            navigationCompletion = completion;
        }

        try
        {
            await InvokeOnUiAsync(() => browser.Navigate(address)).ConfigureAwait(false);
            var result = await completion.Task.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"The web view could not navigate to {address}.");
            }
        }
        finally
        {
            lock (navigationLock)
            {
                if (ReferenceEquals(navigationCompletion, completion))
                {
                    navigationCompletion = null;
                }
            }
        }
    }

    public async Task<string> GetPageTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await EvaluateScriptAsync(
            "document.body ? document.body.innerText : ''",
            cancellationToken).ConfigureAwait(false);
        return RequireSuccessfulString(result, "reading page text");
    }

    public async Task<string> GetPageSourceAsync(CancellationToken cancellationToken = default)
    {
        var result = await EvaluateScriptAsync(
            "document.documentElement ? document.documentElement.outerHTML : ''",
            cancellationToken).ConfigureAwait(false);
        return RequireSuccessfulString(result, "reading page source");
    }

    public async Task<SdkJavaScriptResult> EvaluateScriptAsync(
        string script,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return new SdkJavaScriptResult
            {
                Success = false,
                Message = "A JavaScript expression is required."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            var encodedScript = JsonSerializer.Serialize(script);
            var wrappedScript =
                "JSON.stringify((() => { try { " +
                $"const value = (0, eval)({encodedScript}); " +
                "return { playniteSdkSuccess: true, value: value }; " +
                "} catch (error) { " +
                "return { playniteSdkSuccess: false, message: String(error && (error.stack || error.message) || error) }; " +
                "} })())";
            var rawResult = await RunOnUiAsync(() => browser.InvokeScript(wrappedScript))
                .WaitAsync(OperationTimeout, cancellationToken)
                .ConfigureAwait(false);
            return ParseEvaluationResult(rawResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new SdkJavaScriptResult
            {
                Success = false,
                Message = exception.Message
            };
        }
    }

    public async Task<IReadOnlyList<HttpCookie>> GetCookiesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var manager = await InvokeOnUiAsync(() => browser.TryGetCookieManager()).ConfigureAwait(false);
        if (manager != null)
        {
            var cookies = await manager.GetCookiesAsync()
                .WaitAsync(OperationTimeout, cancellationToken)
                .ConfigureAwait(false);
            return cookies.Select(MapManagedCookie).ToList();
        }

        await using var store = await GetWebKitGtkCookieStoreAsync(cancellationToken).ConfigureAwait(false);
        return await store.GetCookiesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCookieAsync(
        Uri address,
        HttpCookie cookie,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(cookie);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookie.Name);
        if (cookie.Priority != CookiePriority.Medium)
        {
            throw new NotSupportedException(
                "The native web engines do not expose cookie Priority. The cookie was not written with altered semantics.");
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeCookie(address, cookie);
        var manager = await InvokeOnUiAsync(() => browser.TryGetCookieManager()).ConfigureAwait(false);
        if (manager != null)
        {
            if (normalized.SameSite != CookieSameSite.Unspecified)
            {
                throw new NotSupportedException(
                    "The active native web engine's managed cookie API does not expose SameSite. " +
                    "The cookie was not written with altered semantics.");
            }

            await InvokeOnUiAsync(() => manager.AddOrUpdateCookie(MapSdkCookie(normalized)))
                .ConfigureAwait(false);
            return;
        }

        await using var store = await GetWebKitGtkCookieStoreAsync(cancellationToken).ConfigureAwait(false);
        await store.SetCookieAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCookiesAsync(
        Uri address,
        string name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var cookies = await GetCookiesAsync(cancellationToken).ConfigureAwait(false);
        var matches = cookies.Where(cookie =>
            WebViewPolicy.DomainMatches(address.Host, cookie.Domain) &&
            (string.IsNullOrWhiteSpace(name) || string.Equals(cookie.Name, name, StringComparison.Ordinal)))
            .ToList();

        var manager = await InvokeOnUiAsync(() => browser.TryGetCookieManager()).ConfigureAwait(false);
        if (manager != null)
        {
            await InvokeOnUiAsync(() =>
            {
                foreach (var cookie in matches)
                {
                    manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path);
                    manager.AddOrUpdateCookie(new Cookie(
                        cookie.Name,
                        string.Empty,
                        string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                        cookie.Domain)
                    {
                        Expires = DateTime.UnixEpoch.AddSeconds(1),
                        HttpOnly = cookie.HttpOnly,
                        Secure = cookie.Secure
                    });
                    manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path);
                }
            }).ConfigureAwait(false);
            return;
        }

        await using var store = await GetWebKitGtkCookieStoreAsync(cancellationToken).ConfigureAwait(false);
        foreach (var cookie in matches)
        {
            await store.DeleteCookieAsync(cookie, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Close()
    {
        if (offscreen || disposed || closed)
        {
            return;
        }

        InvokeOnUi(() => window.Close());
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        release?.Invoke();
        adapterReady.TrySetException(new ObjectDisposedException(nameof(AvaloniaSdkWebView)));
        lock (navigationLock)
        {
            navigationCompletion?.TrySetException(new ObjectDisposedException(nameof(AvaloniaSdkWebView)));
            navigationCompletion = null;
        }

        InvokeOnUi(() =>
        {
            browser.AdapterCreated -= BrowserOnAdapterCreated;
            browser.NavigationStarted -= BrowserOnNavigationStarted;
            browser.NavigationCompleted -= BrowserOnNavigationCompleted;
            browser.EnvironmentRequested -= BrowserOnEnvironmentRequested;
            if (!closed)
            {
                window.Close();
            }
        });
    }

    internal static SdkJavaScriptResult ParseEvaluationResult(string rawResult)
    {
        if (rawResult == null)
        {
            return new SdkJavaScriptResult
            {
                Success = false,
                Message = "The native web-view engine returned no JavaScript result."
            };
        }

        try
        {
            using var outerDocument = JsonDocument.Parse(rawResult);
            if (outerDocument.RootElement.ValueKind == JsonValueKind.String)
            {
                var innerJson = outerDocument.RootElement.GetString();
                using var innerDocument = JsonDocument.Parse(innerJson ?? string.Empty);
                return ParseEvaluationObject(innerDocument.RootElement);
            }

            return ParseEvaluationObject(outerDocument.RootElement);
        }
        catch (JsonException exception)
        {
            return new SdkJavaScriptResult
            {
                Success = false,
                Message = $"The native web-view engine returned invalid JavaScript JSON: {exception.Message}"
            };
        }
    }

    private void CreateControls()
    {
        var background = new SolidColorBrush(settings.WindowBackground);
        browser = new NativeWebView { Background = background };
        browser.EnvironmentRequested += BrowserOnEnvironmentRequested;
        browser.AdapterCreated += BrowserOnAdapterCreated;
        browser.NavigationStarted += BrowserOnNavigationStarted;
        browser.NavigationCompleted += BrowserOnNavigationCompleted;

        window = new Window
        {
            Title = "Playnite Web View",
            Width = settings.WindowWidth > 0 ? settings.WindowWidth : 1024,
            Height = settings.WindowHeight > 0 ? settings.WindowHeight : 768,
            MinWidth = 1,
            MinHeight = 1,
            Background = background,
            Content = browser,
            Opacity = offscreen ? 0 : 1,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(-32000, -32000)
        };
        window.Closed += (_, _) =>
        {
            closed = true;
            windowClosed.TrySetResult(null);
            adapterReady.TrySetException(new InvalidOperationException(
                "The web-view window closed before its native adapter initialized."));
            lock (navigationLock)
            {
                navigationCompletion?.TrySetException(new InvalidOperationException(
                    "The web-view window closed before navigation completed."));
                navigationCompletion = null;
            }
        };
    }

    private void BrowserOnEnvironmentRequested(object sender, WebViewEnvironmentRequestedEventArgs args)
    {
        switch (args)
        {
            case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                webView2.UserDataFolder = string.IsNullOrWhiteSpace(dataDirectory)
                    ? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Playnite",
                        "WebView2")
                    : dataDirectory;
                break;
            case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
                wpe.PreferWebKitGtkInstead = true;
                wpe.DataDirectory = dataDirectory;
                wpe.CacheDirectory = cacheDirectory;
                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.EphemeralDataManager = string.IsNullOrWhiteSpace(dataDirectory);
                gtk.BaseDataDirectory = dataDirectory;
                gtk.BaseCacheDirectory = cacheDirectory;
                break;
        }
    }

    private void BrowserOnAdapterCreated(object sender, WebViewAdapterEventArgs args)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.UserAgent))
            {
                browser.UserAgent = settings.UserAgent;
            }

            adapterReady.TrySetResult(null);
        }
        catch (Exception exception)
        {
            adapterReady.TrySetException(exception);
        }
    }

    private void BrowserOnNavigationStarted(object sender, AvaloniaNavigationStartingEventArgs args) =>
        LoadingChanged?.Invoke(this, new WebViewLoadingChangedEventArgs { IsLoading = true });

    private void BrowserOnNavigationCompleted(object sender, AvaloniaNavigationCompletedEventArgs args)
    {
        TaskCompletionSource<AvaloniaNavigationCompletedEventArgs> completion;
        lock (navigationLock)
        {
            completion = navigationCompletion;
        }

        completion?.TrySetResult(args);
        LoadingChanged?.Invoke(this, new WebViewLoadingChangedEventArgs { IsLoading = false });
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        await InvokeOnUiAsync(() =>
        {
            ThrowIfUnavailable();
            if (initialized)
            {
                return;
            }

            if (offscreen)
            {
                window.Show();
            }
            else if (GetOwner() is { IsVisible: true } owner)
            {
                window.Show(owner);
            }
            else
            {
                window.Show();
            }

            initialized = true;
        }, offscreen ? DispatcherPriority.Background : DispatcherPriority.Normal).ConfigureAwait(false);
        await adapterReady.Task.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private Window ShowOnScreen()
    {
        ThrowIfUnavailable();
        var owner = GetOwner();
        window.ShowInTaskbar = true;
        window.ShowActivated = true;
        if (owner != null)
        {
            var horizontalOffset = Math.Max(0, (owner.Bounds.Width - window.Width) / 2);
            var verticalOffset = Math.Max(0, (owner.Bounds.Height - window.Height) / 2);
            window.Position = new PixelPoint(
                owner.Position.X + (int)horizontalOffset,
                owner.Position.Y + (int)verticalOffset);
        }

        window.Activate();
        return owner;
    }

    private async Task<WebKitGtkCookieStore> GetWebKitGtkCookieStoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await InvokeOnUiAsync(() =>
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();

            var handle = browser.TryGetPlatformHandle();
            var webKitHandle = handle is IGtkWebViewPlatformHandle gtkHandle
                ? gtkHandle.WebKitWebView
                : handle?.HandleDescriptor == "WebKitWebView"
                    ? handle.Handle
                    : IntPtr.Zero;
            if (!OperatingSystem.IsLinux() || webKitHandle == IntPtr.Zero)
            {
                throw new PlatformNotSupportedException(
                    "The active Avalonia web-view adapter does not expose cookie management. " +
                    $"Adapter={browser.AdapterInfo}; handle={handle?.HandleDescriptor ?? "none"}.");
            }

            // Retain the native view while still on the Avalonia/GTK UI thread.
            // The cookie operations are asynchronous and may otherwise race a
            // concurrent SDK view disposal after this borrowed handle escapes.
            return new WebKitGtkCookieStore(webKitHandle);
        }).ConfigureAwait(false);
    }

    private static HttpCookie NormalizeCookie(Uri address, HttpCookie cookie) => new()
    {
        Name = cookie.Name,
        Value = cookie.Value ?? string.Empty,
        Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? address.Host : cookie.Domain,
        Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
        Expires = cookie.Expires,
        Creation = cookie.Creation,
        Secure = cookie.Secure,
        HttpOnly = cookie.HttpOnly,
        LastAccess = cookie.LastAccess,
        SameSite = cookie.SameSite,
        Priority = cookie.Priority
    };

    private static Cookie MapSdkCookie(HttpCookie cookie)
    {
        var nativeCookie = new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain)
        {
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure
        };
        if (cookie.Expires.HasValue)
        {
            nativeCookie.Expires = cookie.Expires.Value;
        }

        return nativeCookie;
    }

    private static HttpCookie MapManagedCookie(Cookie cookie) => new()
    {
        Name = cookie.Name,
        Value = cookie.Value,
        Domain = cookie.Domain,
        Path = cookie.Path,
        Expires = cookie.Expires == DateTime.MinValue ? null : cookie.Expires,
        Creation = cookie.TimeStamp,
        LastAccess = DateTime.UtcNow,
        HttpOnly = cookie.HttpOnly,
        Secure = cookie.Secure,
        SameSite = CookieSameSite.Unspecified,
        Priority = CookiePriority.Medium
    };

    private static SdkJavaScriptResult ParseEvaluationObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("playniteSdkSuccess", out var successElement) ||
            successElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return new SdkJavaScriptResult
            {
                Success = false,
                Message = "The native web-view engine returned an unrecognized JavaScript result."
            };
        }

        if (!successElement.GetBoolean())
        {
            return new SdkJavaScriptResult
            {
                Success = false,
                Message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : "JavaScript execution failed."
            };
        }

        return new SdkJavaScriptResult
        {
            Success = true,
            Result = root.TryGetProperty("value", out var valueElement)
                ? ConvertJsonValue(valueElement)
                : null
        };
    }

    private static object ConvertJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ConvertJsonValue(property.Value)),
        _ => element.ToString()
    };

    private static string RequireSuccessfulString(SdkJavaScriptResult result, string operation)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"The web view failed while {operation}: {result.Message}");
        }

        return result.Result?.ToString() ?? string.Empty;
    }

    private static Window GetOwner() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private void ThrowIfOffscreen(string operation)
    {
        if (offscreen)
        {
            throw new InvalidOperationException($"{operation} is not available for an offscreen web view.");
        }
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (closed)
        {
            throw new InvalidOperationException("The web-view window is closed.");
        }
    }

    private static void InvokeOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }

    private static void InvokeOnUi(Action action, DispatcherPriority priority)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(action, priority).GetAwaiter().GetResult();
        }
    }

    private static T InvokeOnUi<T>(Func<T> action) => Dispatcher.UIThread.CheckAccess()
        ? action()
        : Dispatcher.UIThread.Invoke(action);

    private static Task InvokeOnUiAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                action();
                completion.TrySetResult(null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    private static Task InvokeOnUiAsync(Action action, DispatcherPriority priority)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                action();
                completion.TrySetResult(null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }, priority);
        return completion.Task;
    }

    private static Task<T> InvokeOnUiAsync<T>(Func<T> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(action());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    private static Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                completion.TrySetResult(await action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }
}
