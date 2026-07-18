using Avalonia.Controls;

namespace Playnite.SDK;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum MessageBoxButton
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    OK,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    OKCancel,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    YesNo,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    YesNoCancel
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum MessageBoxResult
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    None,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    OK,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Cancel,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Yes,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    No
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum MessageBoxImage
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    None,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Information,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Warning,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Error,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Question
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public sealed class WindowCreationOptions
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Title { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public double Width { get; set; } = 900;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public double Height { get; set; } = 650;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool ShowInTaskbar { get; set; } = true;
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool CanResize { get; set; } = true;
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public interface IDialogsFactory
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task ShowErrorMessageAsync(string message, string caption = null);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<MessageBoxResult> ShowMessageAsync(
        string message,
        string caption = null,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<string> ShowChoiceAsync(
        string message,
        string caption,
        IReadOnlyList<string> choices,
        int defaultChoice = 0,
        int cancelChoice = -1);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<string> SelectFileAsync(string filter = null);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<IReadOnlyList<string>> SelectFilesAsync(string filter = null);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Task<string> SelectFolderAsync();
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Window CreateWindow(WindowCreationOptions options);
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Window GetCurrentAppWindow();
}
