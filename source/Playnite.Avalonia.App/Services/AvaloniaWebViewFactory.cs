using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Playnite.SDK;
using Playnite.SDK.Events;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvaloniaWindow = Avalonia.Controls.Window;
using SdkColor = System.Windows.Media.Color;
using WpfWindow = System.Windows.Window;

namespace Playnite.Avalonia.App.Services;

internal sealed class AvaloniaWebViewFactory : IWebViewFactory, IDisposable
{
    private readonly object viewsLock = new();
    private readonly HashSet<AvaloniaSdkWebView> views = new();
    private bool disposed;

    public IWebView CreateOffscreenView() => Create(new WebViewSettings(), true);

    public IWebView CreateOffscreenView(WebViewSettings settings) => Create(settings, true);

    public IWebView CreateView(int width, int height) => Create(
        new WebViewSettings
        {
            WindowWidth = width,
            WindowHeight = height
        },
        false);

    public IWebView CreateView(int width, int height, SdkColor background) => Create(
        new WebViewSettings
        {
            WindowWidth = width,
            WindowHeight = height,
            WindowBackground = background
        },
        false);

    public IWebView CreateView(WebViewSettings settings) => Create(settings, false);

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
            WindowBackground = SdkColor.FromArgb(
                source.BackgroundA,
                source.BackgroundR,
                source.BackgroundG,
                source.BackgroundB)
        };
        return new V7WebViewInstance(Create(settings, payload.Offscreen));
    }

    private AvaloniaSdkWebView Create(WebViewSettings settings, bool offscreen)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSettings(settings);

        AvaloniaSdkWebView view = null;
        try
        {
            view = new AvaloniaSdkWebView(settings, offscreen, () => Release(view));
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

    private static void ValidateSettings(WebViewSettings settings)
    {
        if (!settings.JavaScriptEnabled)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView does not expose per-view JavaScript disabling. " +
                "The web view was not created because ignoring this SDK setting would weaken the requested policy.");
        }

        if (settings.ResourceLoadedCallback != null ||
            settings.PassResourceContentStreamToCallback ||
            settings.ShouldPassResourceContentFunc != null)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView exposes outgoing web requests but not completed response metadata or response bodies. " +
                "SDK v6 resource-loaded callbacks cannot be preserved by this adapter and were not silently ignored.");
        }
    }

    private void Release(AvaloniaSdkWebView view)
    {
        lock (viewsLock)
        {
            views.Remove(view);
        }
    }

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
}

internal sealed class AvaloniaSdkWebView : IWebView
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    private readonly WebViewSettings settings;
    private readonly bool offscreen;
    private readonly Action release;
    private readonly object navigationLock = new();
    private readonly TaskCompletionSource<object> adapterReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private NativeWebView browser;
    private AvaloniaWindow window;
    private TaskCompletionSource<WebViewNavigationCompletedEventArgs> navigationCompletion;
    private DispatcherFrame dialogFrame;
    private bool initialized;
    private bool closed;
    private bool disposed;

    public bool CanExecuteJavascriptInMainFrame =>
        !disposed && !closed && adapterReady.Task.IsCompletedSuccessfully;
    internal Control AvaloniaView => browser;
    internal AvaloniaWindow AvaloniaWindowHost => offscreen ? null : window;

    public WpfWindow WindowHost => offscreen
        ? null
        : throw new NotSupportedException(
            "SDK v6 exposes WindowHost as a WPF Window. The Avalonia web-view window has no safe WPF Window equivalent.");

    public event EventHandler<WebViewLoadingChangedEventArgs> LoadingChanged;

    public AvaloniaSdkWebView(WebViewSettings settings, bool offscreen, Action release)
    {
        this.settings = settings;
        this.offscreen = offscreen;
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

    public void Open()
    {
        ThrowIfOffscreen(nameof(Open));
        EnsureInitialized();
        InvokeOnUi(ShowOnScreen);
    }

    public bool? OpenDialog()
    {
        ThrowIfOffscreen(nameof(OpenDialog));
        EnsureInitialized();
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(OpenDialog);
        }

        ThrowIfUnavailable();
        var owner = GetOwner();
        var ownerWasEnabled = owner?.IsEnabled == true;
        if (ownerWasEnabled)
        {
            owner.IsEnabled = false;
        }

        try
        {
            dialogFrame = new DispatcherFrame();
            ShowOnScreen();
            Dispatcher.UIThread.PushFrame(dialogFrame);
            return null;
        }
        finally
        {
            dialogFrame = null;
            if (ownerWasEnabled && owner != null)
            {
                owner.IsEnabled = true;
                owner.Activate();
            }
        }
    }

    public void NavigateAndWait(string url)
    {
        EnsureInitialized();
        var uri = ParseUrl(url);
        var completion = new TaskCompletionSource<WebViewNavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (navigationLock)
        {
            navigationCompletion = completion;
        }

        InvokeOnUi(() => browser.Navigate(uri));
        var result = WaitForTask(completion.Task, $"navigation to {uri}");
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"The web view could not navigate to {uri}.");
        }
    }

    public void Navigate(string url)
    {
        EnsureInitialized();
        var uri = ParseUrl(url);
        InvokeOnUi(() => browser.Navigate(uri));
    }

    public string GetPageText() => WaitForTask(GetPageTextAsync(), "reading page text");

    public Task<string> GetPageTextAsync() => EvaluateStringAsync(
        "document.body ? document.body.innerText : ''",
        "reading page text");

    public string GetPageSource() => WaitForTask(GetPageSourceAsync(), "reading page source");

    public Task<string> GetPageSourceAsync() => EvaluateStringAsync(
        "document.documentElement ? document.documentElement.outerHTML : ''",
        "reading page source");

    public string GetCurrentAddress()
    {
        EnsureInitialized();
        return InvokeOnUi(() => browser.Source?.AbsoluteUri ?? string.Empty);
    }

    public void DeleteDomainCookies(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        DeleteCookiesMatching(cookie => string.Equals(cookie.Domain, domain, StringComparison.OrdinalIgnoreCase));
    }

    public void DeleteDomainCookiesRegex(string domainRegex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainRegex);
        var expression = new Regex(domainRegex, RegexOptions.CultureInvariant);
        DeleteCookiesMatching(cookie => expression.IsMatch(cookie.Domain ?? string.Empty));
    }

    public void DeleteCookies(string url, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var uri = ParseUrl(url);
        DeleteCookiesMatching(cookie =>
            string.Equals(cookie.Name, name, StringComparison.Ordinal) &&
            DomainMatches(uri.Host, cookie.Domain));
    }

    public List<HttpCookie> GetCookies()
    {
        var cookies = WaitForTask(GetNativeCookiesAsync(), "reading web-view cookies");
        return cookies.Select(cookie => new HttpCookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = cookie.Path,
            Expires = cookie.Expires == DateTime.MinValue ? null : cookie.Expires,
            Creation = cookie.TimeStamp,
            LastAccess = DateTime.Now,
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure,
            SameSite = CookieSameSite.Unspecified,
            Priority = CookiePriority.Medium
        }).ToList();
    }

    public void SetCookies(
        string url,
        string domain,
        string name,
        string value,
        string path,
        DateTime expires)
    {
        var uri = ParseUrl(url);
        SetNativeCookie(new HttpCookie
        {
            Name = name,
            Value = value,
            Domain = string.IsNullOrWhiteSpace(domain) ? uri.Host : domain,
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
            Expires = expires
        });
    }

    public void SetCookies(string url, HttpCookie cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        var uri = ParseUrl(url);
        if (cookie.SameSite != CookieSameSite.Unspecified || cookie.Priority != CookiePriority.Medium)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView's common cookie API does not expose SameSite or Priority. " +
                "The cookie was not written with altered semantics.");
        }

        SetNativeCookie(new HttpCookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? uri.Host : cookie.Domain,
            Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
            Expires = cookie.Expires,
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure
        });
    }

    public void Close()
    {
        if (offscreen || disposed)
        {
            return;
        }

        InvokeOnUi(() =>
        {
            if (!closed)
            {
                window.Close();
            }
        });
    }

    public async Task<JavaScriptEvaluationResult> EvaluateScriptAsync(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return new JavaScriptEvaluationResult
            {
                Success = false,
                Message = "A JavaScript expression is required."
            };
        }

        try
        {
            EnsureInitialized();
            var encodedScript = JsonSerializer.Serialize(script);
            var wrappedScript =
                "(() => { try { " +
                $"const value = (0, eval)({encodedScript}); " +
                "return { playniteSdkSuccess: true, value: value }; " +
                "} catch (error) { " +
                "return { playniteSdkSuccess: false, message: String(error && (error.stack || error.message) || error) }; " +
                "} })()";
            var rawResult = await RunOnUiAsync(() => browser.InvokeScript(wrappedScript));
            return ParseEvaluationResult(rawResult);
        }
        catch (Exception exception)
        {
            return new JavaScriptEvaluationResult
            {
                Success = false,
                Message = exception.Message
            };
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        release?.Invoke();
        adapterReady.TrySetCanceled();
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
            browser.WebResourceRequested -= BrowserOnWebResourceRequested;
            if (!closed)
            {
                window.Close();
            }
        });
    }

    private void CreateControls()
    {
        var background = new Color(
            settings.WindowBackground.A,
            settings.WindowBackground.R,
            settings.WindowBackground.G,
            settings.WindowBackground.B);
        browser = new NativeWebView
        {
            Background = new SolidColorBrush(background)
        };
        browser.AdapterCreated += BrowserOnAdapterCreated;
        browser.NavigationStarted += BrowserOnNavigationStarted;
        browser.NavigationCompleted += BrowserOnNavigationCompleted;
        browser.WebResourceRequested += BrowserOnWebResourceRequested;

        window = new AvaloniaWindow
        {
            Title = "Playnite Web View",
            Width = settings.WindowWidth > 0 ? settings.WindowWidth : 1024,
            Height = settings.WindowHeight > 0 ? settings.WindowHeight : 768,
            MinWidth = 1,
            MinHeight = 1,
            Background = new SolidColorBrush(background),
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
            adapterReady.TrySetException(new InvalidOperationException(
                "The web-view window closed before its native adapter initialized."));
            lock (navigationLock)
            {
                navigationCompletion?.TrySetException(new InvalidOperationException(
                    "The web-view window closed before navigation completed."));
                navigationCompletion = null;
            }

            if (dialogFrame != null)
            {
                dialogFrame.Continue = false;
            }
        };
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

    private void BrowserOnNavigationStarted(object sender, WebViewNavigationStartingEventArgs args) =>
        LoadingChanged?.Invoke(this, new WebViewLoadingChangedEventArgs { IsLoading = true });

    private void BrowserOnNavigationCompleted(object sender, WebViewNavigationCompletedEventArgs args)
    {
        TaskCompletionSource<WebViewNavigationCompletedEventArgs> completion;
        lock (navigationLock)
        {
            completion = navigationCompletion;
            navigationCompletion = null;
        }

        completion?.TrySetResult(args);
        LoadingChanged?.Invoke(this, new WebViewLoadingChangedEventArgs { IsLoading = false });
    }

    private void BrowserOnWebResourceRequested(object sender, WebResourceRequestedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(settings.UserAgent))
        {
            args.Request.Headers.TrySet("User-Agent", settings.UserAgent);
        }
    }

    private void EnsureInitialized()
    {
        ThrowIfUnavailable();
        InvokeOnUi(() =>
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
        }, offscreen ? DispatcherPriority.Background : DispatcherPriority.Normal);
        WaitForTask(adapterReady.Task, "initializing the native web-view adapter");
    }

    private void ShowOnScreen()
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
    }

    private async Task<string> EvaluateStringAsync(string script, string operation)
    {
        var result = await EvaluateScriptAsync(script);
        if (!result.Success)
        {
            throw new InvalidOperationException($"The web view failed while {operation}: {result.Message}");
        }

        return result.Result?.ToString() ?? string.Empty;
    }

    private Task<IReadOnlyList<Cookie>> GetNativeCookiesAsync()
    {
        EnsureInitialized();
        return RunOnUiAsync(() =>
        {
            var manager = browser.TryGetCookieManager() ??
                throw new PlatformNotSupportedException(
                    "The active Avalonia web-view adapter does not expose cookie management.");
            return manager.GetCookiesAsync();
        });
    }

    private void DeleteCookiesMatching(Func<Cookie, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var cookies = WaitForTask(GetNativeCookiesAsync(), "reading cookies for deletion");
        InvokeOnUi(() =>
        {
            var manager = browser.TryGetCookieManager() ??
                throw new PlatformNotSupportedException(
                    "The active Avalonia web-view adapter does not expose cookie management.");
            foreach (var cookie in cookies.Where(predicate))
            {
                manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path);
                manager.AddOrUpdateCookie(new Cookie(cookie.Name, string.Empty, cookie.Path, cookie.Domain)
                {
                    Expires = DateTime.UnixEpoch.AddSeconds(1),
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure
                });
                manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path);
            }
        });
    }

    private void SetNativeCookie(HttpCookie cookie)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookie.Name);
        EnsureInitialized();
        var nativeCookie = new Cookie(
            cookie.Name,
            cookie.Value ?? string.Empty,
            string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
            cookie.Domain)
        {
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure
        };
        if (cookie.Expires.HasValue)
        {
            nativeCookie.Expires = cookie.Expires.Value;
        }

        InvokeOnUi(() =>
        {
            var manager = browser.TryGetCookieManager() ??
                throw new PlatformNotSupportedException(
                    "The active Avalonia web-view adapter does not expose cookie management.");
            manager.AddOrUpdateCookie(nativeCookie);
        });
    }

    private static JavaScriptEvaluationResult ParseEvaluationResult(string rawResult)
    {
        if (rawResult == null)
        {
            return new JavaScriptEvaluationResult
            {
                Success = false,
                Message = "The native web-view engine returned no JavaScript result."
            };
        }

        try
        {
            using var document = JsonDocument.Parse(rawResult);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("playniteSdkSuccess", out var successElement) ||
                successElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return new JavaScriptEvaluationResult
                {
                    Success = false,
                    Message = "The native web-view engine returned an unrecognized JavaScript result."
                };
            }

            if (!successElement.GetBoolean())
            {
                return new JavaScriptEvaluationResult
                {
                    Success = false,
                    Message = root.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : "JavaScript execution failed."
                };
            }

            return new JavaScriptEvaluationResult
            {
                Success = true,
                Result = root.TryGetProperty("value", out var valueElement)
                    ? ConvertJsonValue(valueElement)
                    : null
            };
        }
        catch (JsonException exception)
        {
            return new JavaScriptEvaluationResult
            {
                Success = false,
                Message = $"The native web-view engine returned invalid JavaScript JSON: {exception.Message}"
            };
        }
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

    private static Uri ParseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"'{url}' is not an absolute web-view URL.", nameof(url));
        }

        return uri;
    }

    private static bool DomainMatches(string host, string domain)
    {
        var normalizedDomain = domain?.TrimStart('.') ?? string.Empty;
        return string.Equals(host, normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{normalizedDomain}", StringComparison.OrdinalIgnoreCase);
    }

    private static AvaloniaWindow GetOwner() =>
        (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

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

    private static T WaitForTask<T>(Task<T> task, string operation)
    {
        if (!task.IsCompleted)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                var timedOut = false;
                var frame = new DispatcherFrame();
                using var timer = new Timer(_ => Dispatcher.UIThread.Post(() =>
                {
                    timedOut = true;
                    frame.Continue = false;
                }), null, OperationTimeout, Timeout.InfiniteTimeSpan);
                _ = task.ContinueWith(
                    _ => Dispatcher.UIThread.Post(() => frame.Continue = false),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                Dispatcher.UIThread.PushFrame(frame);
                if (timedOut && !task.IsCompleted)
                {
                    throw new TimeoutException($"Timed out after {OperationTimeout.TotalSeconds:N0} seconds while {operation}.");
                }
            }
            else
            {
                var completed = Task.WaitAny(new Task[] { task, Task.Delay(OperationTimeout) });
                if (completed != 0)
                {
                    throw new TimeoutException($"Timed out after {OperationTimeout.TotalSeconds:N0} seconds while {operation}.");
                }
            }
        }

        return task.GetAwaiter().GetResult();
    }

    private void ThrowIfOffscreen(string operation)
    {
        if (offscreen)
        {
            throw new NotSupportedException($"{operation} is not available for an offscreen web view.");
        }
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (closed)
        {
            throw new InvalidOperationException("The web-view window has already closed.");
        }
    }
}
