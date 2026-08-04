using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Animation;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Asynchronously loads local or HTTP image paths without blocking the UI thread.
/// The owned bitmap is replaced and disposed when a virtualized container is reused.
/// </summary>
public sealed class GameCoverImage : Image
{
    private const long MaximumDownloadBytes = 64L * 1024 * 1024;

    public static readonly StyledProperty<string> SourcePathProperty =
        AvaloniaProperty.Register<GameCoverImage, string>(nameof(SourcePath));
    public static readonly StyledProperty<TimeSpan> FadeDurationProperty =
        AvaloniaProperty.Register<GameCoverImage, TimeSpan>(nameof(FadeDuration));
    public static readonly StyledProperty<double> BottomFadeStartProperty =
        AvaloniaProperty.Register<GameCoverImage, double>(nameof(BottomFadeStart), -1);

    private static readonly HttpClient httpClient = new();
    private CancellationTokenSource loadCancellation;
    private Bitmap ownedBitmap;

    /// <summary>
    /// Controls whether bitmap decoding is moved off the UI thread. Shells set
    /// this once from their persisted performance settings before creating a
    /// window; separate Desktop and Fullscreen processes therefore keep their
    /// own policy without imposing it on themes.
    /// </summary>
    public static bool AsyncLoadingEnabled { get; set; } = true;

    public string SourcePath
    {
        get => GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    public TimeSpan FadeDuration
    {
        get => GetValue(FadeDurationProperty);
        set => SetValue(FadeDurationProperty, value);
    }

    /// <summary>
    /// Relative vertical position where the image starts fading to transparent.
    /// A negative value disables the fade.
    /// </summary>
    public double BottomFadeStart
    {
        get => GetValue(BottomFadeStartProperty);
        set => SetValue(BottomFadeStartProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcePathProperty)
        {
            BeginLoad(change.NewValue as string);
        }
        else if (change.Property == BottomFadeStartProperty)
        {
            UpdateBottomFadeMask();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Source == null && !string.IsNullOrWhiteSpace(SourcePath))
        {
            BeginLoad(SourcePath);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelLoad();
        ClearBitmap();
        base.OnDetachedFromVisualTree(e);
    }

    private async void BeginLoad(string path)
    {
        CancelLoad();
        var cancellation = new CancellationTokenSource();
        loadCancellation = cancellation;
        var token = cancellation.Token;
        Bitmap loadedBitmap = null;

        try
        {
            loadedBitmap = AsyncLoadingEnabled
                ? await Task.Run(() => LoadBitmap(path, token), token).ConfigureAwait(false)
                : await LoadBitmap(path, token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || !ReferenceEquals(loadCancellation, cancellation))
                {
                    loadedBitmap?.Dispose();
                    loadedBitmap = null;
                    return;
                }

                var previous = ownedBitmap;
                ownedBitmap = loadedBitmap;
                loadedBitmap = null;
                PrepareFadeTransition();
                Source = ownedBitmap;
                Opacity = 1;
                previous?.Dispose();
                loadCancellation = null;
                cancellation.Dispose();
            });
        }
        catch (OperationCanceledException)
        {
            loadedBitmap?.Dispose();
        }
        catch (Exception exception)
        {
            loadedBitmap?.Dispose();
            Trace.TraceError("Failed to load game cover '{0}': {1}", path, exception);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(loadCancellation, cancellation))
                {
                    loadCancellation = null;
                    cancellation.Dispose();
                    ClearBitmap();
                }
            });
        }
    }

    private static async Task<Bitmap> LoadBitmap(string path, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            using (var response = await httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                ValidateContentLength(response.Content.Headers);
                using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                using (var image = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        if (image.Length + read > MaximumDownloadBytes)
                        {
                            throw new InvalidDataException(
                                $"Game cover exceeds the {MaximumDownloadBytes / (1024 * 1024)} MiB limit.");
                        }

                        await image.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    }

                    image.Position = 0;
                    return new Bitmap(image);
                }
            }
        }

        token.ThrowIfCancellationRequested();
        return File.Exists(path) ? new Bitmap(path) : null;
    }

    private static void ValidateContentLength(HttpContentHeaders headers)
    {
        if (headers.ContentLength > MaximumDownloadBytes)
        {
            throw new InvalidDataException(
                $"Game cover exceeds the {MaximumDownloadBytes / (1024 * 1024)} MiB limit.");
        }
    }

    private void CancelLoad()
    {
        var cancellation = loadCancellation;
        loadCancellation = null;
        if (cancellation != null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private void ClearBitmap()
    {
        Source = null;
        ownedBitmap?.Dispose();
        ownedBitmap = null;
    }

    private void PrepareFadeTransition()
    {
        Transitions = null;
        Opacity = FadeDuration > TimeSpan.Zero ? 0 : 1;
        if (FadeDuration > TimeSpan.Zero)
        {
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = FadeDuration
                }
            };
        }
    }

    private void UpdateBottomFadeMask()
    {
        var fadeStart = BottomFadeStart;
        if (fadeStart < 0 || fadeStart >= 1)
        {
            OpacityMask = null;
            return;
        }

        fadeStart = Math.Clamp(fadeStart, 0, 1);
        var fadeLength = 1 - fadeStart;
        OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.White, 0),
                new GradientStop(Colors.White, fadeStart),
                new GradientStop(Color.FromArgb(0xE8, 0xFF, 0xFF, 0xFF), fadeStart + (fadeLength * 0.22)),
                new GradientStop(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF), fadeStart + (fadeLength * 0.42)),
                new GradientStop(Color.FromArgb(0x78, 0xFF, 0xFF, 0xFF), fadeStart + (fadeLength * 0.62)),
                new GradientStop(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF), fadeStart + (fadeLength * 0.82)),
                new GradientStop(Colors.Transparent, 1)
            }
        };
    }
}
