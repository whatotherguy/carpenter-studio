using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = true
        }.ToString();
    }

    public async Task<IDbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        using var wal = connection.CreateCommand();
        wal.CommandText = "PRAGMA journal_mode=WAL;";
        await wal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys=ON;";
        await foreignKeys.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var busy = connection.CreateCommand();
        busy.CommandText = "PRAGMA busy_timeout=5000;";
        await busy.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return connection;
    }
}
