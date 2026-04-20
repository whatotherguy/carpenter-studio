using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using CabinetDesigner.Persistence.UnitOfWork;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Repositories;

public sealed class SnapshotRepositoryTests
{
    [Fact]
    public async Task WriteAsync_ConcurrentCallsSameRevision_OnlyOneSucceeds()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var snapshotRepository = new SnapshotRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var snapshot1 = new ApprovedSnapshot(
            state.Revision.Id,
            state.Project.Id,
            1,
            createdAt,
            "user1",
            "Rev 1",
            "hash-1",
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1));

        var snapshot2 = new ApprovedSnapshot(
            state.Revision.Id,
            state.Project.Id,
            1,
            createdAt.AddSeconds(1),
            "user2",
            "Rev 1",
            "hash-2",
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1));

        // Run both writes concurrently
        var task1 = snapshotRepository.WriteAsync(snapshot1);
        var task2 = snapshotRepository.WriteAsync(snapshot2);

        // One should succeed, one should throw
        var results = await Task.WhenAll(
            task1.ContinueWith(t => (Success: t.IsCompletedSuccessfully, Exception: t.Exception?.InnerException)),
            task2.ContinueWith(t => (Success: t.IsCompletedSuccessfully, Exception: t.Exception?.InnerException)));

        // Exactly one should succeed
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // The failure should be InvalidOperationException
        var failedResult = results.First(r => !r.Success);
        Assert.IsType<InvalidOperationException>(failedResult.Exception);
        Assert.Contains("already has an approved snapshot", failedResult.Exception?.Message ?? string.Empty);

        // Database should have exactly 1 row for this revision
        var rowCount = await CountSnapshotsForRevisionAsync(fixture, state.Revision.Id);
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task WriteAsync_WithRollback_NoRowPersisted()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var snapshot = new ApprovedSnapshot(
            state.Revision.Id,
            state.Project.Id,
            1,
            createdAt,
            "user1",
            "Rev 1",
            "hash-1",
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1));

        // Write inside a UnitOfWork and then discard (rollback)
        var unitOfWork = new SqliteUnitOfWork(fixture.ConnectionFactory, fixture.SessionAccessor);
        await unitOfWork.BeginAsync();
        try
        {
            var snapshotRepository = new SnapshotRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
            await snapshotRepository.WriteAsync(snapshot);
            // Don't commit - let it rollback when disposed
        }
        finally
        {
            await unitOfWork.DisposeAsync();
        }

        // After rollback, no row should exist
        var rowCount = await CountSnapshotsForRevisionAsync(fixture, state.Revision.Id);
        Assert.Equal(0, rowCount);
    }

    private static string JsonWithSchemaVersion(int version)
    {
        return $$$"""{"schema_version":{{{version}}},"payload":{"value":"test"}}""";
    }

    [Fact]
    public async Task ReadAsync_RoundTripsContentHash()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var snapshotRepository = new SnapshotRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var snapshot = new ApprovedSnapshot(
            state.Revision.Id,
            state.Project.Id,
            1,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            "user1",
            "Rev 1",
            "hash-content-123",
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1),
            JsonWithSchemaVersion(1));

        await snapshotRepository.WriteAsync(snapshot);
        var loaded = await snapshotRepository.ReadAsync(state.Revision.Id);

        Assert.NotNull(loaded);
        Assert.Equal("hash-content-123", loaded!.ContentHash);
    }

    private static async Task<int> CountSnapshotsForRevisionAsync(
        SqliteTestFixture fixture,
        RevisionId revisionId)
    {
        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM approved_snapshots WHERE revision_id = @revisionId;";
        cmd.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
