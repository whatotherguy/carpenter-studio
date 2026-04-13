using CabinetDesigner.Persistence.Tests.Fixtures;
using CabinetDesigner.Persistence.UnitOfWork;
using System.Data;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.UnitOfWork;

public sealed class SqliteUnitOfWorkTests : IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task DisposeAsync_WithoutCommit_RollsBackTransaction()
    {
        // Arrange
        const string projectId = "test-project-1";
        const string projectName = "Test Project";

        // Act: Begin transaction, insert data, dispose without committing
        var unitOfWork = new SqliteUnitOfWork(_fixture.ConnectionFactory, _fixture.SessionAccessor);
        await unitOfWork.BeginAsync();

        var connection = _fixture.SessionAccessor.Current?.Connection;
        var transaction = _fixture.SessionAccessor.Current?.Transaction;
        Assert.NotNull(connection);
        Assert.NotNull(transaction);

        // Insert a row within the transaction
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO projects (id, name, description, created_at, updated_at, current_state, file_path)
                VALUES (@id, @name, @description, @created_at, @updated_at, @current_state, @file_path)
                """;
            cmd.Parameters.AddWithValue("@id", projectId);
            cmd.Parameters.AddWithValue("@name", projectName);
            cmd.Parameters.AddWithValue("@description", (object?)null ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created_at", "2026-04-13T00:00:00Z");
            cmd.Parameters.AddWithValue("@updated_at", "2026-04-13T00:00:00Z");
            cmd.Parameters.AddWithValue("@current_state", "{}");
            cmd.Parameters.AddWithValue("@file_path", (object?)null ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // Dispose without committing
        await unitOfWork.DisposeAsync();

        // Assert: Row should NOT be in the database
        using var verifyConnection = (Microsoft.Data.Sqlite.SqliteConnection)await _fixture.ConnectionFactory.OpenConnectionAsync();
        using var verifyCmd = verifyConnection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM projects WHERE id = @id";
        verifyCmd.Parameters.AddWithValue("@id", projectId);
        var countResult = await verifyCmd.ExecuteScalarAsync();
        var count = countResult is null ? 0 : (long)countResult;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DisposeAsync_AfterCommit_PersistsData()
    {
        // Arrange
        const string projectId = "test-project-2";
        const string projectName = "Committed Project";

        // Act: Begin transaction, insert data, commit, then dispose
        var unitOfWork = new SqliteUnitOfWork(_fixture.ConnectionFactory, _fixture.SessionAccessor);
        await unitOfWork.BeginAsync();

        var connection = _fixture.SessionAccessor.Current?.Connection;
        var transaction = _fixture.SessionAccessor.Current?.Transaction;
        Assert.NotNull(connection);
        Assert.NotNull(transaction);

        // Insert a row within the transaction
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO projects (id, name, description, created_at, updated_at, current_state, file_path)
                VALUES (@id, @name, @description, @created_at, @updated_at, @current_state, @file_path)
                """;
            cmd.Parameters.AddWithValue("@id", projectId);
            cmd.Parameters.AddWithValue("@name", projectName);
            cmd.Parameters.AddWithValue("@description", (object?)null ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created_at", "2026-04-13T00:00:00Z");
            cmd.Parameters.AddWithValue("@updated_at", "2026-04-13T00:00:00Z");
            cmd.Parameters.AddWithValue("@current_state", "{}");
            cmd.Parameters.AddWithValue("@file_path", (object?)null ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // Commit the transaction
        await unitOfWork.CommitAsync();

        // Note: DisposeAsync is called as part of CommitAsync flow, but we can verify data is persisted

        // Assert: Row SHOULD be in the database
        using var verifyConnection = (Microsoft.Data.Sqlite.SqliteConnection)await _fixture.ConnectionFactory.OpenConnectionAsync();
        using var verifyCmd = verifyConnection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM projects WHERE id = @id AND name = @name";
        verifyCmd.Parameters.AddWithValue("@id", projectId);
        verifyCmd.Parameters.AddWithValue("@name", projectName);
        var countResult = await verifyCmd.ExecuteScalarAsync();
        var count = countResult is null ? 0 : (long)countResult;

        Assert.Equal(1, count);
    }
}
