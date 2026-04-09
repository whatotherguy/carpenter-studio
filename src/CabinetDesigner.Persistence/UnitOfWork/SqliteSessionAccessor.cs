using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence.UnitOfWork;

public sealed class SqliteSessionAccessor
{
    private readonly AsyncLocal<SqliteSession?> _current = new();

    internal SqliteSession? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

internal sealed record SqliteSession(SqliteConnection Connection, SqliteTransaction Transaction);
