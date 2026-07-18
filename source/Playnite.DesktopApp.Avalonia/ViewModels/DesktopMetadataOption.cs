namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopMetadataOption
{
    public Guid Id { get; }
    public string Name { get; }

    public DesktopMetadataOption(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;
}
