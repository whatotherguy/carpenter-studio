using CabinetDesigner.Application.Diagnostics;

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
    private readonly IAppLogger? _logger;

    public StartupOrchestrator(MigrationRunner migrationRunner, IAppLogger? logger = null)
    {
        _migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
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
}
