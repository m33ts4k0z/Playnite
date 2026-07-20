using System.Runtime.InteropServices;
using Playnite.SDK;

namespace Playnite.Avalonia.WebView;

internal sealed class WebKitGtkCookieStore : IAsyncDisposable
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    private IntPtr webView;

    static WebKitGtkCookieStore()
    {
        NativeLibrary.SetDllImportResolver(typeof(WebKitGtkCookieStore).Assembly, ResolveLibrary);
    }

    public WebKitGtkCookieStore(IntPtr webView)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("WebKitGTK cookie access is available only on Linux.");
        }

        if (webView == IntPtr.Zero)
        {
            throw new ArgumentException("A live WebKitWebView handle is required.", nameof(webView));
        }

        this.webView = Native.g_object_ref(webView);
        if (this.webView == IntPtr.Zero)
        {
            throw new InvalidOperationException("WebKitGTK could not retain the web view for cookie access.");
        }
    }

    public async Task<IReadOnlyList<HttpCookie>> GetCookiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operation = new GetCookiesOperation();
        await GlibDispatcher.RunAsync(() => operation.Start(GetCookieManager())).ConfigureAwait(false);
        return await operation.Task.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCookieAsync(HttpCookie cookie, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        cancellationToken.ThrowIfCancellationRequested();
        var operation = new CookieMutationOperation(delete: false);
        await GlibDispatcher.RunAsync(() => operation.Start(GetCookieManager(), CreateNativeCookie(cookie)))
            .ConfigureAwait(false);
        await operation.Task.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCookieAsync(HttpCookie cookie, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        cancellationToken.ThrowIfCancellationRequested();
        var operation = new CookieMutationOperation(delete: true);
        await GlibDispatcher.RunAsync(() => operation.Start(GetCookieManager(), CreateNativeCookie(cookie)))
            .ConfigureAwait(false);
        await operation.Task.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        var retainedWebView = Interlocked.Exchange(ref webView, IntPtr.Zero);
        if (retainedWebView != IntPtr.Zero)
        {
            await GlibDispatcher.RunAsync(() => Native.g_object_unref(retainedWebView)).ConfigureAwait(false);
        }
    }

    private IntPtr GetCookieManager()
    {
        var retainedWebView = webView;
        ObjectDisposedException.ThrowIf(retainedWebView == IntPtr.Zero, this);

        var major = Native.webkit_get_major_version();
        var minor = Native.webkit_get_minor_version();
        if (!SupportsCompleteCookieEnumeration(major, minor))
        {
            throw new PlatformNotSupportedException(
                $"WebKitGTK {major}.{minor} does not expose complete cookie enumeration; version 2.42 or newer is required.");
        }

        var context = Native.webkit_web_view_get_context(retainedWebView);
        if (context == IntPtr.Zero)
        {
            throw new InvalidOperationException("The WebKitGTK web view did not expose a web context.");
        }

        var manager = Native.webkit_web_context_get_cookie_manager(context);
        return manager != IntPtr.Zero
            ? manager
            : throw new InvalidOperationException("The WebKitGTK web context did not expose a cookie manager.");
    }

    internal static bool SupportsCompleteCookieEnumeration(uint major, uint minor) =>
        major > 2 || major == 2 && minor >= 42;

    private static IntPtr CreateNativeCookie(HttpCookie cookie)
    {
        var nativeCookie = Native.soup_cookie_new(
            cookie.Name,
            cookie.Value ?? string.Empty,
            cookie.Domain,
            string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
            -1);
        if (nativeCookie == IntPtr.Zero)
        {
            throw new InvalidOperationException("libsoup could not allocate the requested cookie.");
        }

        try
        {
            Native.soup_cookie_set_http_only(nativeCookie, cookie.HttpOnly ? 1 : 0);
            Native.soup_cookie_set_secure(nativeCookie, cookie.Secure ? 1 : 0);
            if (cookie.Expires.HasValue)
            {
                var expires = Native.g_date_time_new_from_unix_utc(
                    new DateTimeOffset(cookie.Expires.Value.ToUniversalTime()).ToUnixTimeSeconds());
                if (expires == IntPtr.Zero)
                {
                    throw new InvalidOperationException("GLib could not represent the requested cookie expiration.");
                }

                try
                {
                    Native.soup_cookie_set_expires(nativeCookie, expires);
                }
                finally
                {
                    Native.g_date_time_unref(expires);
                }
            }

            if (cookie.SameSite != CookieSameSite.Unspecified)
            {
                Native.soup_cookie_set_same_site_policy(nativeCookie, MapSameSite(cookie.SameSite));
            }

            return nativeCookie;
        }
        catch
        {
            Native.soup_cookie_free(nativeCookie);
            throw;
        }
    }

    internal static CookieSameSite MapSameSite(SoupSameSitePolicy policy) => policy switch
    {
        SoupSameSitePolicy.None => CookieSameSite.NoRestriction,
        SoupSameSitePolicy.Lax => CookieSameSite.LaxMode,
        SoupSameSitePolicy.Strict => CookieSameSite.StrictMode,
        _ => throw new InvalidOperationException($"WebKitGTK returned unknown SameSite policy {(int)policy}.")
    };

    internal static SoupSameSitePolicy MapSameSite(CookieSameSite policy) => policy switch
    {
        CookieSameSite.NoRestriction => SoupSameSitePolicy.None,
        CookieSameSite.LaxMode => SoupSameSitePolicy.Lax,
        CookieSameSite.StrictMode => SoupSameSitePolicy.Strict,
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "An explicit SameSite policy is required.")
    };

    private static HttpCookie MapNativeCookie(IntPtr cookie)
    {
        var expires = Native.soup_cookie_get_expires(cookie);
        return new HttpCookie
        {
            Name = ReadUtf8(Native.soup_cookie_get_name(cookie)),
            Value = ReadUtf8(Native.soup_cookie_get_value(cookie)),
            Domain = ReadUtf8(Native.soup_cookie_get_domain(cookie)),
            Path = ReadUtf8(Native.soup_cookie_get_path(cookie)),
            Expires = expires == IntPtr.Zero
                ? null
                : DateTimeOffset.FromUnixTimeSeconds(Native.g_date_time_to_unix(expires)).UtcDateTime,
            Creation = DateTime.MinValue,
            LastAccess = DateTime.UtcNow,
            HttpOnly = Native.soup_cookie_get_http_only(cookie) != 0,
            Secure = Native.soup_cookie_get_secure(cookie) != 0,
            SameSite = MapSameSite(Native.soup_cookie_get_same_site_policy(cookie)),
            Priority = CookiePriority.Medium
        };
    }

    private static string ReadUtf8(IntPtr value) =>
        value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(value) ?? string.Empty;

    private static Exception TakeNativeError(IntPtr error, string operation)
    {
        if (error == IntPtr.Zero)
        {
            return new InvalidOperationException($"WebKitGTK failed while {operation} without error details.");
        }

        try
        {
            var native = Marshal.PtrToStructure<GError>(error);
            var message = ReadUtf8(native.Message);
            return new InvalidOperationException(
                $"WebKitGTK failed while {operation} (domain {native.Domain}, code {native.Code}): {message}");
        }
        finally
        {
            Native.g_error_free(error);
        }
    }

    private static IntPtr ResolveLibrary(
        string libraryName,
        System.Reflection.Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        string[] candidates = libraryName switch
        {
            Native.WebKitLibrary => ["libwebkit2gtk-4.1.so.0", "libwebkit2gtk-4.1.so"],
            Native.SoupLibrary => ["libsoup-3.0.so.0", "libsoup-3.0.so"],
            Native.GLibLibrary => ["libglib-2.0.so.0", "libglib-2.0.so"],
            Native.GObjectLibrary => ["libgobject-2.0.so.0", "libgobject-2.0.so"],
            _ => null
        };
        if (candidates == null)
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        throw new DllNotFoundException(
            $"Could not load Linux web-view dependency '{libraryName}'. Tried: {string.Join(", ", candidates)}.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct GError
    {
        public readonly uint Domain;
        public readonly int Code;
        public readonly IntPtr Message;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct GList
    {
        public readonly IntPtr Data;
        public readonly IntPtr Next;
        public readonly IntPtr Previous;
    }

    internal enum SoupSameSitePolicy
    {
        None = 0,
        Lax = 1,
        Strict = 2
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AsyncReadyCallback(IntPtr source, IntPtr result, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GlibSourceCallback(IntPtr userData);

    private sealed class GetCookiesOperation
    {
        private static readonly AsyncReadyCallback Callback = Complete;
        private readonly TaskCompletionSource<IReadOnlyList<HttpCookie>> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<HttpCookie>> Task => completion.Task;

        public void Start(IntPtr manager)
        {
            var handle = GCHandle.Alloc(this);
            try
            {
                Native.webkit_cookie_manager_get_all_cookies(
                    manager,
                    IntPtr.Zero,
                    Callback,
                    GCHandle.ToIntPtr(handle));
            }
            catch
            {
                handle.Free();
                throw;
            }
        }

        private static void Complete(IntPtr source, IntPtr result, IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var operation = (GetCookiesOperation)handle.Target;
            try
            {
                var list = Native.webkit_cookie_manager_get_all_cookies_finish(source, result, out var error);
                if (error != IntPtr.Zero)
                {
                    operation.completion.TrySetException(TakeNativeError(error, "reading cookies"));
                    return;
                }

                var nativeCookies = new List<IntPtr>();
                var node = list;
                try
                {
                    while (node != IntPtr.Zero)
                    {
                        var item = Marshal.PtrToStructure<GList>(node);
                        if (item.Data != IntPtr.Zero)
                        {
                            nativeCookies.Add(item.Data);
                        }

                        node = item.Next;
                    }
                }
                finally
                {
                    if (list != IntPtr.Zero)
                    {
                        Native.g_list_free(list);
                    }
                }

                try
                {
                    operation.completion.TrySetResult(nativeCookies.Select(MapNativeCookie).ToList());
                }
                finally
                {
                    foreach (var cookie in nativeCookies)
                    {
                        Native.soup_cookie_free(cookie);
                    }
                }
            }
            catch (Exception exception)
            {
                operation.completion.TrySetException(exception);
            }
            finally
            {
                handle.Free();
            }
        }
    }

    private sealed class CookieMutationOperation
    {
        private static readonly AsyncReadyCallback AddCallback = CompleteAdd;
        private static readonly AsyncReadyCallback DeleteCallback = CompleteDelete;
        private readonly TaskCompletionSource<object> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool delete;
        private IntPtr cookie;

        public CookieMutationOperation(bool delete) => this.delete = delete;

        public Task Task => completion.Task;

        public void Start(IntPtr manager, IntPtr nativeCookie)
        {
            cookie = nativeCookie;
            var handle = GCHandle.Alloc(this);
            try
            {
                if (delete)
                {
                    Native.webkit_cookie_manager_delete_cookie(
                        manager,
                        cookie,
                        IntPtr.Zero,
                        DeleteCallback,
                        GCHandle.ToIntPtr(handle));
                }
                else
                {
                    Native.webkit_cookie_manager_add_cookie(
                        manager,
                        cookie,
                        IntPtr.Zero,
                        AddCallback,
                        GCHandle.ToIntPtr(handle));
                }
            }
            catch
            {
                Native.soup_cookie_free(cookie);
                cookie = IntPtr.Zero;
                handle.Free();
                throw;
            }
        }

        private static void CompleteAdd(IntPtr source, IntPtr result, IntPtr userData) =>
            Complete(source, result, userData, delete: false);

        private static void CompleteDelete(IntPtr source, IntPtr result, IntPtr userData) =>
            Complete(source, result, userData, delete: true);

        private static void Complete(IntPtr source, IntPtr result, IntPtr userData, bool delete)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var operation = (CookieMutationOperation)handle.Target;
            try
            {
                var succeeded = delete
                    ? Native.webkit_cookie_manager_delete_cookie_finish(source, result, out var error)
                    : Native.webkit_cookie_manager_add_cookie_finish(source, result, out error);
                if (error != IntPtr.Zero)
                {
                    operation.completion.TrySetException(TakeNativeError(
                        error,
                        delete ? "deleting a cookie" : "setting a cookie"));
                }
                else if (succeeded == 0)
                {
                    operation.completion.TrySetException(new InvalidOperationException(
                        $"WebKitGTK did not {(delete ? "delete" : "set")} the cookie."));
                }
                else
                {
                    operation.completion.TrySetResult(null);
                }
            }
            catch (Exception exception)
            {
                operation.completion.TrySetException(exception);
            }
            finally
            {
                Native.soup_cookie_free(operation.cookie);
                operation.cookie = IntPtr.Zero;
                handle.Free();
            }
        }
    }

    private static class GlibDispatcher
    {
        private static readonly GlibSourceCallback Callback = Dispatch;

        public static Task RunAsync(Action action)
        {
            var context = Native.g_main_context_default();
            if (context == IntPtr.Zero)
            {
                throw new InvalidOperationException("The GTK main context is unavailable.");
            }

            if (Native.g_main_context_is_owner(context) != 0)
            {
                action();
                return Task.CompletedTask;
            }

            var work = new GlibWork(action);
            var handle = GCHandle.Alloc(work);
            var source = Native.g_timeout_add(0, Callback, GCHandle.ToIntPtr(handle));
            if (source == 0)
            {
                handle.Free();
                throw new InvalidOperationException("GLib could not schedule work on the GTK main context.");
            }

            return work.Task;
        }

        private static int Dispatch(IntPtr userData)
        {
            var handle = GCHandle.FromIntPtr(userData);
            var work = (GlibWork)handle.Target;
            handle.Free();
            work.Run();
            return 0;
        }

        private sealed class GlibWork
        {
            private readonly Action action;
            private readonly TaskCompletionSource<object> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public GlibWork(Action action) => this.action = action;

            public Task Task => completion.Task;

            public void Run()
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
            }
        }
    }

    private static class Native
    {
        public const string WebKitLibrary = "Playnite.WebKitGtk";
        public const string SoupLibrary = "Playnite.Soup3";
        public const string GLibLibrary = "Playnite.GLib";
        public const string GObjectLibrary = "Playnite.GObject";

        [DllImport(WebKitLibrary)]
        public static extern uint webkit_get_major_version();

        [DllImport(WebKitLibrary)]
        public static extern uint webkit_get_minor_version();

        [DllImport(WebKitLibrary)]
        public static extern IntPtr webkit_web_view_get_context(IntPtr webView);

        [DllImport(WebKitLibrary)]
        public static extern IntPtr webkit_web_context_get_cookie_manager(IntPtr context);

        [DllImport(WebKitLibrary)]
        public static extern void webkit_cookie_manager_get_all_cookies(
            IntPtr manager,
            IntPtr cancellable,
            AsyncReadyCallback callback,
            IntPtr userData);

        [DllImport(WebKitLibrary)]
        public static extern IntPtr webkit_cookie_manager_get_all_cookies_finish(
            IntPtr manager,
            IntPtr result,
            out IntPtr error);

        [DllImport(WebKitLibrary)]
        public static extern void webkit_cookie_manager_add_cookie(
            IntPtr manager,
            IntPtr cookie,
            IntPtr cancellable,
            AsyncReadyCallback callback,
            IntPtr userData);

        [DllImport(WebKitLibrary)]
        public static extern int webkit_cookie_manager_add_cookie_finish(
            IntPtr manager,
            IntPtr result,
            out IntPtr error);

        [DllImport(WebKitLibrary)]
        public static extern void webkit_cookie_manager_delete_cookie(
            IntPtr manager,
            IntPtr cookie,
            IntPtr cancellable,
            AsyncReadyCallback callback,
            IntPtr userData);

        [DllImport(WebKitLibrary)]
        public static extern int webkit_cookie_manager_delete_cookie_finish(
            IntPtr manager,
            IntPtr result,
            out IntPtr error);

        [DllImport(SoupLibrary, CharSet = CharSet.Ansi)]
        public static extern IntPtr soup_cookie_new(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string domain,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            int maxAge);

        [DllImport(SoupLibrary)]
        public static extern void soup_cookie_free(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern IntPtr soup_cookie_get_name(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern IntPtr soup_cookie_get_value(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern IntPtr soup_cookie_get_domain(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern IntPtr soup_cookie_get_path(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern IntPtr soup_cookie_get_expires(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern int soup_cookie_get_http_only(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern int soup_cookie_get_secure(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern SoupSameSitePolicy soup_cookie_get_same_site_policy(IntPtr cookie);

        [DllImport(SoupLibrary)]
        public static extern void soup_cookie_set_http_only(IntPtr cookie, int httpOnly);

        [DllImport(SoupLibrary)]
        public static extern void soup_cookie_set_secure(IntPtr cookie, int secure);

        [DllImport(SoupLibrary)]
        public static extern void soup_cookie_set_expires(IntPtr cookie, IntPtr expires);

        [DllImport(SoupLibrary)]
        public static extern void soup_cookie_set_same_site_policy(IntPtr cookie, SoupSameSitePolicy policy);

        [DllImport(GLibLibrary)]
        public static extern IntPtr g_main_context_default();

        [DllImport(GLibLibrary)]
        public static extern int g_main_context_is_owner(IntPtr context);

        [DllImport(GLibLibrary)]
        public static extern uint g_timeout_add(uint interval, GlibSourceCallback callback, IntPtr data);

        [DllImport(GLibLibrary)]
        public static extern void g_error_free(IntPtr error);

        [DllImport(GLibLibrary)]
        public static extern void g_list_free(IntPtr list);

        [DllImport(GLibLibrary)]
        public static extern IntPtr g_date_time_new_from_unix_utc(long seconds);

        [DllImport(GLibLibrary)]
        public static extern long g_date_time_to_unix(IntPtr dateTime);

        [DllImport(GLibLibrary)]
        public static extern void g_date_time_unref(IntPtr dateTime);

        [DllImport(GObjectLibrary)]
        public static extern IntPtr g_object_ref(IntPtr instance);

        [DllImport(GObjectLibrary)]
        public static extern void g_object_unref(IntPtr instance);
    }
}
