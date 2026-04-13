using System.Reflection;
using CabinetDesigner.Persistence.Migrations;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Migrations;

public sealed class V2RepairSchemaDriftTests
{
    [Fact]
    public void Apply_WithInjectedTableName_ThrowsArgumentException()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Set up basic schema so the migration can attempt to run
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE projects (id TEXT PRIMARY KEY);
            CREATE TABLE revisions (id TEXT PRIMARY KEY);
            CREATE TABLE cabinets (id TEXT PRIMARY KEY);
            CREATE TABLE approved_snapshots (id TEXT PRIMARY KEY);
        ";
        cmd.ExecuteNonQuery();

        using var transaction = connection.BeginTransaction();
        var migration = new V2_RepairSchemaDrift();

        // Calling Apply with a crafted connection that has an injected table name
        // would normally be caught at the EnsureColumn level.
        // We test this by directly testing that the migration rejects invalid table names.
        // Since EnsureColumn is private, we verify the behavior indirectly.
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            var ensureColumnMethod = migration.GetType()
                .GetMethod("EnsureColumn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            ensureColumnMethod!.Invoke(null, new object[] { connection, transaction, "users; DROP TABLE cabinets;--", "column", "TEXT" });
        });

        var innerException = ex.InnerException as ArgumentException;
        Assert.NotNull(innerException);
        Assert.Contains("not on the schema migration allowlist", innerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_WithInjectedColumnName_ThrowsArgumentException()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Set up basic schema
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE projects (id TEXT PRIMARY KEY);
            CREATE TABLE revisions (id TEXT PRIMARY KEY);
            CREATE TABLE cabinets (id TEXT PRIMARY KEY);
            CREATE TABLE approved_snapshots (id TEXT PRIMARY KEY);
        ";
        cmd.ExecuteNonQuery();

        using var transaction = connection.BeginTransaction();
        var migration = new V2_RepairSchemaDrift();

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            var ensureColumnMethod = migration.GetType()
                .GetMethod("EnsureColumn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            ensureColumnMethod!.Invoke(null, new object[] { connection, transaction, "projects", "col; DROP TABLE data;--", "TEXT" });
        });

        var innerException = ex.InnerException as ArgumentException;
        Assert.NotNull(innerException);
        Assert.Contains("not on the schema migration allowlist", innerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_WithValidInputs_CompletesSuccessfully()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        // If initialization succeeded, the migration ran successfully without errors.
        // Verify that all expected columns were added by querying the schema.
        using var connection = await fixture.ConnectionFactory.OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(projects);";

        using var reader = command.ExecuteReader();
        var columnNames = new List<string>();
        while (reader.Read())
        {
            columnNames.Add(reader["name"]?.ToString() ?? string.Empty);
        }

        Assert.Contains("file_path", columnNames);
    }

    [Fact]
    public async Task Apply_CreatesImmutabilityTrigger_Successfully()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        using var connection = await fixture.ConnectionFactory.OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='trigger' AND name='trg_snapshots_no_delete';";

        var result = command.ExecuteScalar();

        Assert.NotNull(result);
        Assert.Equal("trg_snapshots_no_delete", result);
    }
}
