using Avalonia.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Events;

namespace Playnite.Avalonia.App.Services;

internal sealed class V7WebViewSettingsPayload
{
    public bool JavaScriptEnabled { get; set; } = true;
    public string UserAgent { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public byte BackgroundA { get; set; }
    public byte BackgroundR { get; set; }
    public byte BackgroundG { get; set; }
    public byte BackgroundB { get; set; }
}

internal sealed class V7WebViewCreationPayload
{
    public bool Offscreen { get; set; }
    public V7WebViewSettingsPayload Settings { get; set; }
}

internal sealed class V7WebViewInstance : IDisposable
{
    private readonly AvaloniaSdkWebView view;
    private EventHandler<WebViewLoadingChangedEventArgs> loadingHandler;
    private bool disposed;

    public bool CanExecuteJavascriptInMainFrame => view.CanExecuteJavascriptInMainFrame;
    public Control View => view.AvaloniaView;
    public Window WindowHost => view.AvaloniaWindowHost;
    public Uri Address
    {
        get
        {
            var address = view.GetCurrentAddress();
            return string.IsNullOrWhiteSpace(address) ? null : new Uri(address);
        }
    }

    public V7WebViewInstance(AvaloniaSdkWebView view) =>
        this.view = view ?? throw new ArgumentNullException(nameof(view));

    public void SubscribeLoading(Action<bool> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (loadingHandler != null)
        {
            throw new InvalidOperationException("The SDK v7 web-view loading bridge is already subscribed.");
        }

        loadingHandler = (_, args) => callback(args.IsLoading);
        view.LoadingChanged += loadingHandler;
    }

    public Task OpenAsync(bool modal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (modal)
        {
            view.OpenDialog();
        }
        else
        {
            view.Open();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task NavigateAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        view.NavigateAndWait(address.AbsoluteUri);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return view.GetPageTextAsync();
    }

    public Task<string> GetPageSourceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return view.GetPageSourceAsync();
    }

    public async Task<string> EvaluateScriptAsync(string script, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await view.EvaluateScriptAsync(script);
        cancellationToken.ThrowIfCancellationRequested();
        return JsonConvert.SerializeObject(result);
    }

    public Task<string> GetCookiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = view.GetCookies().Select(cookie => new
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
        return Task.FromResult(JsonConvert.SerializeObject(result));
    }

    public Task SetCookieAsync(
        Uri address,
        string cookieJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JObject.Parse(cookieJson);
        view.SetCookies(address.AbsoluteUri, new HttpCookie
        {
            Name = payload.Value<string>("Name"),
            Value = payload.Value<string>("Value"),
            Domain = payload.Value<string>("Domain"),
            Path = payload.Value<string>("Path"),
            Expires = payload.Value<DateTime?>("Expires"),
            Creation = payload.Value<DateTime>("Creation"),
            Secure = payload.Value<bool>("Secure"),
            HttpOnly = payload.Value<bool>("HttpOnly"),
            LastAccess = payload.Value<DateTime>("LastAccess"),
            SameSite = Enum.Parse<CookieSameSite>(payload.Value<string>("SameSite")),
            Priority = Enum.Parse<CookiePriority>(payload.Value<string>("Priority"))
        });
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task DeleteCookiesAsync(
        Uri address,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name))
        {
            view.DeleteDomainCookies(address.Host);
        }
        else
        {
            view.DeleteCookies(address.AbsoluteUri, name);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public void Close() => view.Close();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (loadingHandler != null)
        {
            view.LoadingChanged -= loadingHandler;
            loadingHandler = null;
        }
        view.Dispose();
    }
}
