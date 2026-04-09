using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence.UnitOfWork;

internal sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SqliteSessionAccessor _sessionAccessor;

    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;

    public SqliteUnitOfWork(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
    }

    public async Task BeginAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("Unit of work has already begun.");
        }

        _connection = (SqliteConnection)await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        _transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        _sessionAccessor.Current = new SqliteSession(_connection, _transaction);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("Unit of work has not begun.");
        }

        await _transaction.CommitAsync(ct).ConfigureAwait(false);
        await DisposeSessionAsync().ConfigureAwait(false);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(ct).ConfigureAwait(false);
        await DisposeSessionAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeSessionAsync().ConfigureAwait(false);
    }

    private async Task DisposeSessionAsync()
    {
        var transaction = _transaction;
        var connection = _connection;
        _sessionAccessor.Current = null;
        _transaction = null;
        _connection = null;

        if (transaction is not null)
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
