using Avalonia.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK.Events;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Playnite.SDK.V7.Host;

public sealed class HostWebViewFactory : IWebViewFactory
{
    private readonly Func<string, string, object> hostObjectCall;

    public HostWebViewFactory(Func<string, string, object> hostObjectCall)
    {
        this.hostObjectCall = hostObjectCall ?? ((_, _) => throw new NotSupportedException(
            "The Playnite host did not provide SDK v7 web-view services."));
    }

    public IWebView CreateOffscreenView(WebViewSettings settings = null) => Create(settings, true);
    public IWebView CreateView(WebViewSettings settings = null) => Create(settings, false);

    private IWebView Create(WebViewSettings settings, bool offscreen)
    {
        settings ??= new WebViewSettings();
        if (!settings.JavaScriptEnabled)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView does not expose per-view JavaScript disabling. " +
                "The SDK v7 view was not created because changing that policy is unsafe.");
        }
        if (settings.CaptureResponseContent || settings.ShouldCaptureResponseContent != null)
        {
            throw new NotSupportedException(
                "Avalonia NativeWebView does not expose completed response metadata or owned response streams. " +
                "SDK v7 response capture cannot be preserved by the current backend.");
        }

        var payload = JsonConvert.SerializeObject(new
        {
            Offscreen = offscreen,
            Settings = new
            {
                settings.JavaScriptEnabled,
                settings.UserAgent,
                settings.WindowWidth,
                settings.WindowHeight,
                BackgroundA = settings.WindowBackground.A,
                BackgroundR = settings.WindowBackground.R,
                BackgroundG = settings.WindowBackground.G,
                BackgroundB = settings.WindowBackground.B
            }
        });
        return new HostWebView(hostObjectCall("CreateWebView", payload));
    }
}

public sealed class HostWebView : IWebView
{
    private readonly object instance;
    private readonly Type type;
    private readonly MethodInfo openAsync;
    private readonly MethodInfo navigateAsync;
    private readonly MethodInfo getPageTextAsync;
    private readonly MethodInfo getPageSourceAsync;
    private readonly MethodInfo evaluateScriptAsync;
    private readonly MethodInfo getCookiesAsync;
    private readonly MethodInfo setCookieAsync;
    private readonly MethodInfo deleteCookiesAsync;
    private readonly MethodInfo close;
    private readonly MethodInfo dispose;

    public bool CanExecuteJavascriptInMainFrame => ReadRequired<bool>(nameof(CanExecuteJavascriptInMainFrame));
    public Control View => ReadRequired<Control>(nameof(View));
    public Window WindowHost => ReadOptional<Window>(nameof(WindowHost));
    public Uri Address => ReadOptional<Uri>(nameof(Address));

    public event EventHandler<WebViewLoadingChangedEventArgs> LoadingChanged;
    public event EventHandler<WebViewResourceLoadedEventArgs> ResourceLoaded
    {
        add => throw new NotSupportedException(
            "Avalonia NativeWebView does not expose completed response metadata. " +
            "SDK v7 ResourceLoaded subscriptions are not available on the current backend.");
        remove { }
    }

    public HostWebView(object instance)
    {
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        type = instance.GetType();
        openAsync = GetRequiredMethod("OpenAsync");
        navigateAsync = GetRequiredMethod("NavigateAsync");
        getPageTextAsync = GetRequiredMethod("GetPageTextAsync");
        getPageSourceAsync = GetRequiredMethod("GetPageSourceAsync");
        evaluateScriptAsync = GetRequiredMethod("EvaluateScriptAsync");
        getCookiesAsync = GetRequiredMethod("GetCookiesAsync");
        setCookieAsync = GetRequiredMethod("SetCookieAsync");
        deleteCookiesAsync = GetRequiredMethod("DeleteCookiesAsync");
        close = GetRequiredMethod(nameof(Close));
        dispose = GetRequiredMethod(nameof(IDisposable.Dispose));
        var subscribe = GetRequiredMethod("SubscribeLoading");
        Invoke(subscribe, (Action<bool>)(isLoading => LoadingChanged?.Invoke(
            this,
            new WebViewLoadingChangedEventArgs { IsLoading = isLoading })));
    }

    public Task OpenAsync(bool modal = false, CancellationToken cancellationToken = default) =>
        (Task)Invoke(openAsync, modal, cancellationToken);

    public Task NavigateAsync(Uri address, CancellationToken cancellationToken = default) =>
        (Task)Invoke(navigateAsync, address, cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken = default) =>
        (Task<string>)Invoke(getPageTextAsync, cancellationToken);

    public Task<string> GetPageSourceAsync(CancellationToken cancellationToken = default) =>
        (Task<string>)Invoke(getPageSourceAsync, cancellationToken);

    public async Task<JavaScriptEvaluationResult> EvaluateScriptAsync(
        string script,
        CancellationToken cancellationToken = default)
    {
        var json = await (Task<string>)Invoke(evaluateScriptAsync, script, cancellationToken);
        var payload = JObject.Parse(json);
        return new JavaScriptEvaluationResult
        {
            Success = payload.Value<bool>(nameof(JavaScriptEvaluationResult.Success)),
            Message = payload.Value<string>(nameof(JavaScriptEvaluationResult.Message)),
            Result = ConvertToken(payload[nameof(JavaScriptEvaluationResult.Result)])
        };
    }

    public async Task<IReadOnlyList<HttpCookie>> GetCookiesAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await (Task<string>)Invoke(getCookiesAsync, cancellationToken);
        return JArray.Parse(json).Select(token => new HttpCookie
        {
            Name = token.Value<string>(nameof(HttpCookie.Name)),
            Value = token.Value<string>(nameof(HttpCookie.Value)),
            Domain = token.Value<string>(nameof(HttpCookie.Domain)),
            Path = token.Value<string>(nameof(HttpCookie.Path)),
            Expires = token.Value<DateTime?>(nameof(HttpCookie.Expires)),
            Creation = token.Value<DateTime>(nameof(HttpCookie.Creation)),
            Secure = token.Value<bool>(nameof(HttpCookie.Secure)),
            HttpOnly = token.Value<bool>(nameof(HttpCookie.HttpOnly)),
            LastAccess = token.Value<DateTime>(nameof(HttpCookie.LastAccess)),
            SameSite = Enum.Parse<CookieSameSite>(token.Value<string>(nameof(HttpCookie.SameSite))),
            Priority = Enum.Parse<CookiePriority>(token.Value<string>(nameof(HttpCookie.Priority)))
        }).ToList();
    }

    public Task SetCookieAsync(
        Uri address,
        HttpCookie cookie,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        var json = JsonConvert.SerializeObject(new
        {
            cookie.Name,
            cookie.Value,
            cookie.Domain,
            cookie.Path,
            cookie.Expires,
            cookie.Creation,
            cookie.Secure,
            cookie.HttpOnly,
            cookie.LastAccess,
            SameSite = cookie.SameSite.ToString(),
            Priority = cookie.Priority.ToString()
        });
        return (Task)Invoke(setCookieAsync, address, json, cancellationToken);
    }

    public Task DeleteCookiesAsync(
        Uri address,
        string name = null,
        CancellationToken cancellationToken = default) =>
        (Task)Invoke(deleteCookiesAsync, address, name, cancellationToken);

    public void Close() => Invoke(close);
    public void Dispose() => Invoke(dispose);

    private T ReadRequired<T>(string name) => Read<T>(name)
        ?? throw new InvalidDataException(
            $"Avalonia host returned no SDK v7 web-view value for {name}.");

    private T ReadOptional<T>(string name) => Read<T>(name);

    private T Read<T>(string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(type.FullName, name);
        var value = property.GetValue(instance);
        if (value == null)
        {
            if (typeof(T).IsValueType)
            {
                throw new InvalidDataException(
                    $"Avalonia host returned no SDK v7 web-view value for {name}.");
            }
            return default;
        }
        return value is T typed
            ? typed
            : throw new InvalidDataException(
                $"Avalonia host returned {value.GetType().FullName} instead of {typeof(T).FullName} for {name}.");
    }

    private MethodInfo GetRequiredMethod(string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new MissingMethodException(type.FullName, name);

    private object Invoke(MethodInfo method, params object[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object ConvertToken(JToken token) => token?.Type switch
    {
        null or JTokenType.Null or JTokenType.Undefined => null,
        JTokenType.Integer => token.Value<long>(),
        JTokenType.Float => token.Value<double>(),
        JTokenType.Boolean => token.Value<bool>(),
        JTokenType.String => token.Value<string>(),
        JTokenType.Array => token.Children().Select(ConvertToken).ToList(),
        JTokenType.Object => token.Children<JProperty>().ToDictionary(
            property => property.Name,
            property => ConvertToken(property.Value)),
        _ => token.ToString()
    };
}
