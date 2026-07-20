using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Playnite.DesktopApp.Avalonia.Controls;

public sealed record DesktopMediaDropRequest(string Target, string Path);

public sealed class DesktopMediaDropZone : ContentControl
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".ico", ".jpeg", ".jpg", ".png", ".webp"
    };

    public static readonly StyledProperty<string> TargetProperty =
        AvaloniaProperty.Register<DesktopMediaDropZone, string>(nameof(Target));

    public static readonly StyledProperty<ICommand> DropCommandProperty =
        AvaloniaProperty.Register<DesktopMediaDropZone, ICommand>(nameof(DropCommand));

    public string Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public ICommand DropCommand
    {
        get => GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    public DesktopMediaDropZone()
    {
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    private void OnDragOver(object sender, DragEventArgs args)
    {
        args.DragEffects = TryGetImagePath(args, out _)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        args.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs args)
    {
        if (TryGetImagePath(args, out var path))
        {
            var request = new DesktopMediaDropRequest(Target, path);
            if (DropCommand?.CanExecute(request) == true)
            {
                DropCommand.Execute(request);
                args.DragEffects = DragDropEffects.Copy;
            }
        }

        args.Handled = true;
    }

    private static bool TryGetImagePath(DragEventArgs args, out string path)
    {
        path = args.DataTransfer.TryGetFiles()
            ?.Select(item => item.TryGetLocalPath())
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate) &&
                File.Exists(candidate) &&
                ImageExtensions.Contains(Path.GetExtension(candidate)));
        return !string.IsNullOrWhiteSpace(path);
    }
}
