using System.Data;
using System.Globalization;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Persistence.Migrations;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class StartupOrchestratorTests
{
    [Fact]
    public async Task RunAsync_AppliesMigrationsOnFreshDatabase()
    {
        await using var fixture = new SqliteTestFixture();
        var orchestrator = new StartupOrchestrator(fixture.MigrationRunner);

        await orchestrator.RunAsync();

        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM schema_migrations;";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_WhenCalledOnAlreadyMigratedDatabase()
    {
        await using var fixture = new SqliteTestFixture();
        var orchestrator = new StartupOrchestrator(fixture.MigrationRunner);

        await orchestrator.RunAsync();
        var exception = await Record.ExceptionAsync(() => orchestrator.RunAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RunAsync_PropagatesException_FromFailingMigration()
    {
        await using var fixture = new SqliteTestFixture();
        var failingRunner = new MigrationRunner(fixture.ConnectionFactory, [new ThrowingMigration()]);
        var orchestrator = new StartupOrchestrator(failingRunner);

        // The migration table setup runs before Apply is called, so we need an empty DB
        // (fixture has not been initialized) – the runner will create schema_migrations and
        // then attempt to Apply our throwing migration.
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.RunAsync());
    }

    private sealed class ThrowingMigration : ISchemaMigration
    {
        public int Version => 999;
        public string Description => "always throws";
        public void Apply(IDbConnection connection, IDbTransaction transaction)
            => throw new InvalidOperationException("intentional failure");
    }
}
