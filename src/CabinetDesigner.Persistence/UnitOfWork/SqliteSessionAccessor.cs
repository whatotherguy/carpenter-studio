using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence.UnitOfWork;

public sealed class SqliteSessionAccessor
{
    internal SqliteSession? Current
    {
        get;
        set;
    }
}

internal sealed record SqliteSession(SqliteConnection Connection, SqliteTransaction Transaction);
