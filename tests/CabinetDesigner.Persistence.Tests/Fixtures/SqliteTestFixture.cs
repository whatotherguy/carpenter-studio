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
        await Task.Delay(25).ConfigureAwait(false);
        DeleteIfExists(DatabasePath);
        DeleteIfExists($"{DatabasePath}-wal");
        DeleteIfExists($"{DatabasePath}-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
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
