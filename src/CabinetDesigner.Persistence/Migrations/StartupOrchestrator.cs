using System.Data;
using System.Globalization;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Persistence.Migrations;

/// <summary>
/// Orchestrates the startup sequence for the persistence layer.
/// Call <see cref="RunAsync"/> from the WPF startup path inside a <c>Task.Run</c> wrapper so that
/// SQLite work (which completes synchronously in Microsoft.Data.Sqlite) executes on a thread-pool
/// thread rather than blocking the UI dispatcher.
/// </summary>
public sealed class StartupOrchestrator
{
    private readonly MigrationRunner _migrationRunner;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAppLogger? _logger;

    public StartupOrchestrator(MigrationRunner migrationRunner, IDbConnectionFactory connectionFactory, IAppLogger? logger = null)
    {
        _migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger;
    }

    /// <summary>
    /// Runs all pending schema migrations.  This method itself is async, but because
    /// Microsoft.Data.Sqlite completes its async operations synchronously the caller
    /// must ensure the method is invoked on a thread-pool thread (e.g. via Task.Run).
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Startup",
            Message = "Running schema migrations.",
            Timestamp = DateTimeOffset.UtcNow
        });

        try
        {
            await _migrationRunner.RunAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Fatal,
                Category = "Startup",
                Message = "Schema migration sequence failed; application cannot start.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = exception
            });
            throw;
        }

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Info,
            Category = "Startup",
            Message = "Schema migrations completed successfully.",
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Returns the latest autosave checkpoint for the specified project, if one exists.
    /// This is intentionally narrow so crash recovery can inspect persisted state without
    /// exposing broader persistence internals from the startup path.
    /// </summary>
    public async Task<AutosaveCheckpoint?> FindLatestAutosaveCheckpointAsync(ProjectId projectId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projectId);

        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, revision_id, saved_at, last_command_id, is_clean
            FROM autosave_checkpoints
            WHERE project_id = @projectId
            ORDER BY saved_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return new AutosaveCheckpoint(
            reader.GetString(0),
            new ProjectId(Guid.Parse(reader.GetString(1))),
            new RevisionId(Guid.Parse(reader.GetString(2))),
            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.IsDBNull(4) ? null : new CommandId(Guid.Parse(reader.GetString(4))),
            reader.GetInt32(5) == 1);
    }
}
