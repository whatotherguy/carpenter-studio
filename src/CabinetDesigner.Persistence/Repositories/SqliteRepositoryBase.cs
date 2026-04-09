using Microsoft.Data.Sqlite;
using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal abstract class SqliteRepositoryBase
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SqliteSessionAccessor _sessionAccessor;

    protected SqliteRepositoryBase(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
    }

    protected async Task<TResult> WithConnectionAsync<TResult>(
        Func<SqliteConnection, SqliteTransaction?, Task<TResult>> operation,
        CancellationToken ct)
    {
        var session = _sessionAccessor.Current;
        if (session is not null)
        {
            return await operation(session.Connection, session.Transaction).ConfigureAwait(false);
        }

        await using var connection = (SqliteConnection)await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await operation(connection, null).ConfigureAwait(false);
    }

    protected Task WithConnectionAsync(
        Func<SqliteConnection, SqliteTransaction?, Task> operation,
        CancellationToken ct) =>
        WithConnectionAsync<object?>(
            async (connection, transaction) =>
            {
                await operation(connection, transaction).ConfigureAwait(false);
                return null;
            },
            ct);

    protected static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
