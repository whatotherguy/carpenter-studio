using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence.UnitOfWork;

public sealed class SqliteSessionAccessor
{
    private volatile SqliteSession? _current;

    internal SqliteSession? Current
    {
        get => _current;
        set => _current = value;
    }
}

internal sealed record SqliteSession(SqliteConnection Connection, SqliteTransaction Transaction);
