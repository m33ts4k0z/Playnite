using Playnite.SDK;

namespace _namespace_;

public sealed class _name_Client : LibraryClient
{
    public override bool IsInstalled => false;

    public override void Open()
    {
        throw new InvalidOperationException("Configure the client launch command first.");
    }

    public override void Shutdown()
    {
        // Close the original library client when supported.
    }
}
