using CabinetDesigner.Application.Diagnostics;

namespace CabinetDesigner.Persistence.Migrations;

public sealed class MigrationRunner
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IReadOnlyList<ISchemaMigration> _migrations;
    private readonly IAppLogger? _logger;

    public MigrationRunner(IDbConnectionFactory connectionFactory, IEnumerable<ISchemaMigration> migrations, IAppLogger? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _migrations = migrations
            .OrderBy(migration => migration.Version)
            .ToArray();
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);

        await EnsureSchemaMigrationsTableAsync(connection, ct).ConfigureAwait(false);

        var appliedVersions = await LoadAppliedVersionsAsync(connection, ct).ConfigureAwait(false);

        foreach (var migration in _migrations)
        {
            if (appliedVersions.Contains(migration.Version))
            {
                continue;
            }

            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Info,
                Category = "Migration",
                Message = "Applying schema migration.",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, string>
                {
                    ["version"] = migration.Version.ToString(),
                    ["description"] = migration.Description
                }
            });

            await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                migration.Apply(connection, transaction);

                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO schema_migrations(version, applied_at, description)
                    VALUES(@version, @appliedAt, @description);
                    """;
                insert.Parameters.AddWithValue("@version", migration.Version);
                insert.Parameters.AddWithValue("@appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                insert.Parameters.AddWithValue("@description", migration.Description);
                await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger?.Log(new LogEntry
                {
                    Level = LogLevel.Error,
                    Category = "Migration",
                    Message = "Schema migration failed; transaction rolled back.",
                    Timestamp = DateTimeOffset.UtcNow,
                    Properties = new Dictionary<string, string>
                    {
                        ["version"] = migration.Version.ToString(),
                        ["description"] = migration.Description
                    },
                    Exception = exception
                });
                throw;
            }
        }
    }

    private static async Task EnsureSchemaMigrationsTableAsync(Microsoft.Data.Sqlite.SqliteConnection connection, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL,
                description TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<HashSet<int>> LoadAppliedVersionsAsync(Microsoft.Data.Sqlite.SqliteConnection connection, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations;";

        var versions = new HashSet<int>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }
}
