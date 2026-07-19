namespace Playnite.Avalonia.Theming;

public sealed class ResourceObserver<T> : IObserver<T>
{
    private readonly Action<T> onNext;
    private readonly Action<Exception> onError;

    public ResourceObserver(Action<T> onNext, Action<Exception> onError = null)
    {
        this.onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        this.onError = onError ?? (error => System.Diagnostics.Trace.TraceError(
            "Avalonia resource observation failed: {0}", error));
    }

    public void OnNext(T value) => onNext(value);
    public void OnCompleted() { }
    public void OnError(Exception error) => onError(error);
}
