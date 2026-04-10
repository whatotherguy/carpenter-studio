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

    public StartupOrchestrator(MigrationRunner migrationRunner)
    {
        _migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
    }

    /// <summary>
    /// Runs all pending schema migrations.  This method itself is async, but because
    /// Microsoft.Data.Sqlite completes its async operations synchronously the caller
    /// must ensure the method is invoked on a thread-pool thread (e.g. via Task.Run).
    /// </summary>
    public Task RunAsync(CancellationToken ct = default)
        => _migrationRunner.RunAsync(ct);
}
