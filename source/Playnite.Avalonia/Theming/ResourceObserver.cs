namespace Playnite.Avalonia.Theming;

public sealed class ResourceObserver<T> : IObserver<T>
{
    private readonly Action<T> onNext;

    public ResourceObserver(Action<T> onNext)
    {
        this.onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
    }

    public void OnNext(T value) => onNext(value);
    public void OnCompleted() { }
    public void OnError(Exception error) { }
}
