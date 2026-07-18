using Playnite.SDK.Data;

namespace Playnite.SDK.V7.Host;

internal sealed class HostSqlite : ISQLite
{
    private readonly Func<string, object, object> hostRequest;
    private readonly Guid handle;
    private bool disposed;

    public HostSqlite(
        Func<string, object, object> hostRequest,
        string databasePath,
        SqliteOpenFlags openFlags)
    {
        this.hostRequest = hostRequest ?? throw new NotSupportedException(
            "The Avalonia host does not expose SDK v7 SQLite services.");
        handle = hostRequest("SQLite.Open", new object[] { databasePath, (int)openFlags }) is Guid opened
            ? opened
            : throw new InvalidDataException("Avalonia host returned an invalid SDK v7 SQLite handle.");
        if (handle == Guid.Empty)
        {
            throw new InvalidDataException("Avalonia host returned an empty SDK v7 SQLite handle.");
        }
    }

    public List<T> Query<T>(string query, params object[] args) where T : new()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var result = hostRequest("SQLite.Query", new object[]
        {
            handle,
            query,
            args ?? [],
            typeof(T)
        });
        return result as List<T>
            ?? throw new InvalidDataException(
                $"Avalonia host returned an invalid SDK v7 SQLite result for {typeof(T).FullName}.");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        hostRequest("SQLite.Dispose", handle);
        disposed = true;
    }
}
