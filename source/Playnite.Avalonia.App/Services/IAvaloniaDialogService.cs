namespace Playnite.Avalonia.App.Services;

public interface IAvaloniaDialogService
{
    string ShowMessage(
        string message,
        string caption,
        IReadOnlyList<string> options,
        int defaultIndex = 0,
        int cancelIndex = -1);

    IReadOnlyList<string> SelectFiles(string filter, bool allowMultiple);
    string SelectFolder();
}
