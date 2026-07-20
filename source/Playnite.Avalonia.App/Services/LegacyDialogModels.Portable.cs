#if !WINDOWS
using Avalonia.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Playnite.SDK;

// Native shell code shares these progress/selection models with the SDK v6
// dialog adapter on Windows. SDK v7 intentionally does not expose them to
// plugins, but the portable host keeps the shell-facing contract TFM-neutral.
public sealed class MessageBoxToggle : INotifyPropertyChanged
{
    private bool selected;

    public event PropertyChangedEventHandler PropertyChanged;

    public string Title { get; set; }

    public bool Selected
    {
        get => selected;
        set
        {
            if (selected == value)
            {
                return;
            }

            selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public MessageBoxToggle(string title, bool selected = false)
    {
        Title = title?.StartsWith("LOC", StringComparison.Ordinal) == true
            ? ResourceProvider.GetString(title)
            : title;
        Selected = selected;
    }
}

public sealed class GlobalProgressActionArgs : INotifyPropertyChanged
{
    private double progressMaxValue;
    private double currentProgressValue;
    private string text;
    private bool isIndeterminate;

    public event PropertyChangedEventHandler PropertyChanged;

    public SynchronizationContext MainContext { get; }

    public Dispatcher MainDispatcher { get; }

    public CancellationToken CancelToken { get; }

    public double ProgressMaxValue
    {
        get => progressMaxValue;
        set => SetField(ref progressMaxValue, value);
    }

    public double CurrentProgressValue
    {
        get => currentProgressValue;
        set => SetField(ref currentProgressValue, value);
    }

    public string Text
    {
        get => text;
        set => SetField(
            ref text,
            value?.StartsWith("LOC", StringComparison.Ordinal) == true
                ? ResourceProvider.GetString(value)
                : value);
    }

    public bool IsIndeterminate
    {
        get => isIndeterminate;
        set => SetField(ref isIndeterminate, value);
    }

    public GlobalProgressActionArgs(
        SynchronizationContext mainContext,
        Dispatcher mainDispatcher,
        CancellationToken cancelToken)
    {
        MainContext = mainContext;
        MainDispatcher = mainDispatcher;
        CancelToken = cancelToken;
    }

    private void SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class GlobalProgressResult
{
    public Exception Error { get; }

    public bool? Result { get; }

    public bool Canceled { get; }

    public GlobalProgressResult(bool? result, bool canceled, Exception error)
    {
        Result = result;
        Error = error;
        Canceled = canceled;
    }
}

public sealed class GlobalProgressOptions
{
    public string Text { get; set; }

    public bool Cancelable { get; set; }

    public bool IsIndeterminate { get; set; } = true;

    public GlobalProgressOptions(string text)
    {
        Text = text;
    }

    public GlobalProgressOptions(string text, bool cancelable) : this(text)
    {
        Cancelable = cancelable;
    }
}

public class GenericItemOption
{
    public string Name { get; set; }

    public string Description { get; set; }

    public GenericItemOption()
    {
    }

    public GenericItemOption(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

public sealed class ImageFileOption : GenericItemOption
{
    public string Path { get; set; }

    public ImageFileOption()
    {
    }

    public ImageFileOption(string path)
    {
        Path = path;
    }
}

public sealed class StringSelectionDialogResult
{
    public bool Result { get; set; }

    public string SelectedString { get; set; }

    public StringSelectionDialogResult(bool result, string selectedString)
    {
        Result = result;
        SelectedString = selectedString;
    }
}
#endif
