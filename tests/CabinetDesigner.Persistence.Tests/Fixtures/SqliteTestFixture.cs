using CabinetDesigner.Persistence.Migrations;

namespace CabinetDesigner.Persistence.Tests.Fixtures;

public sealed class SqliteTestFixture : IAsyncDisposable
{
    public SqliteTestFixture()
    {
        var root = FindRepositoryRoot();
        var databaseDirectory = Path.Combine(root, ".artifacts", "test-databases");
        Directory.CreateDirectory(databaseDirectory);
        DatabasePath = Path.Combine(databaseDirectory, $"{Guid.NewGuid():N}.db");
        ConnectionFactory = new SqliteConnectionFactory(DatabasePath);
        SessionAccessor = new CabinetDesigner.Persistence.UnitOfWork.SqliteSessionAccessor();
        MigrationRunner = new MigrationRunner(ConnectionFactory, [new V1_InitialSchema(), new V2_RepairSchemaDrift()]);
    }

    public string DatabasePath { get; }

    public SqliteConnectionFactory ConnectionFactory { get; }

    public CabinetDesigner.Persistence.UnitOfWork.SqliteSessionAccessor SessionAccessor { get; }

    public MigrationRunner MigrationRunner { get; }

    public async Task InitializeAsync()
    {
        await MigrationRunner.RunAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Return pooled connections to the pool and close all physical handles so
        // the database file (and its WAL/SHM companions) can be deleted on all
        // platforms.  Without this call the pool may still hold an open file
        // handle and File.Delete will throw on Windows.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await DeleteWithRetryAsync(DatabasePath).ConfigureAwait(false);
        await DeleteWithRetryAsync($"{DatabasePath}-wal").ConfigureAwait(false);
        await DeleteWithRetryAsync($"{DatabasePath}-shm").ConfigureAwait(false);
    }

    private static async Task DeleteWithRetryAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        for (var i = 0; i < 10; i++)
        {
            try
            {
                File.Delete(path);
                break;
            }
            catch (IOException) when (i < 9)
            {
                await Task.Yield();
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src")) &&
                Directory.Exists(Path.Combine(current.FullName, "tests")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for persistence integration tests.");
    }
}
