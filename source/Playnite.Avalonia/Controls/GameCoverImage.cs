using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Asynchronously loads local or HTTP image paths without blocking the UI thread.
/// The owned bitmap is replaced and disposed when a virtualized container is reused.
/// </summary>
public sealed class GameCoverImage : Image
{
    public static readonly StyledProperty<string> SourcePathProperty =
        AvaloniaProperty.Register<GameCoverImage, string>(nameof(SourcePath));

    private static readonly HttpClient httpClient = new();
    private CancellationTokenSource loadCancellation;
    private Bitmap ownedBitmap;

    public string SourcePath
    {
        get => GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcePathProperty)
        {
            BeginLoad(change.NewValue as string);
        }
    }

    private async void BeginLoad(string path)
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        var token = loadCancellation.Token;

        try
        {
            var bitmap = await Task.Run(async () =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var bytes = await httpClient.GetByteArrayAsync(uri, token);
                    return new Bitmap(new MemoryStream(bytes));
                }

                return File.Exists(path) ? new Bitmap(path) : null;
            }, token);

            if (token.IsCancellationRequested)
            {
                bitmap?.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var previous = ownedBitmap;
                ownedBitmap = bitmap;
                Source = bitmap;
                previous?.Dispose();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => Source = null);
        }
    }
}
