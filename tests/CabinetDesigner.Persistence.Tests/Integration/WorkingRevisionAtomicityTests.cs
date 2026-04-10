using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class WorkingRevisionAtomicityTests
{
    [Fact]
    public async Task SaveAsync_NormalOperation_PersistsAllEntitiesCorrectly()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Equal(state.WorkingRevision.Revision.Id, loaded!.Revision.Id);
        Assert.Single(loaded.Rooms);
        Assert.Single(loaded.Walls);
        Assert.Single(loaded.Runs);
        Assert.Single(loaded.Cabinets);
        Assert.Single(loaded.Parts);
    }

    [Fact]
    public async Task SaveAsync_MidSaveConstraintViolation_DoesNotLosePriorRevisionData()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        // Establish baseline: save a valid working revision.
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        // Build a bad revision: two cabinet entries sharing the same CabinetId cause a UNIQUE
        // constraint failure on the second insert, simulating a mid-save write failure.
        var cabinet = state.WorkingRevision.Cabinets[0];
        var badRevision = new WorkingRevision(
            state.Revision,
            state.WorkingRevision.Rooms,
            state.WorkingRevision.Walls,
            state.WorkingRevision.Runs,
            new[] { cabinet, cabinet },
            Array.Empty<GeneratedPart>());

        // The save must throw because of the duplicate primary key.
        await Assert.ThrowsAsync<SqliteException>(() => workingRevisionRepository.SaveAsync(badRevision));

        // After the failed (rolled-back) save, the original revision data must be intact.
        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Equal(state.WorkingRevision.Revision.Id, loaded!.Revision.Id);
        Assert.Single(loaded.Rooms);
        Assert.Single(loaded.Walls);
        Assert.Single(loaded.Runs);
        Assert.Single(loaded.Cabinets);
        Assert.Single(loaded.Parts);
        Assert.Equal(cabinet.Id, loaded.Cabinets[0].Id);
    }

    [Fact]
    public async Task SaveAsync_MidSaveConstraintViolation_TransactionRollbackLeavesRevisionIntact()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        // Count rows before the attempted bad save.
        var countBefore = await CountRevisionRowsAsync(fixture, state.Revision.Id);

        var cabinet = state.WorkingRevision.Cabinets[0];
        var badRevision = new WorkingRevision(
            state.Revision,
            state.WorkingRevision.Rooms,
            state.WorkingRevision.Walls,
            state.WorkingRevision.Runs,
            new[] { cabinet, cabinet },
            Array.Empty<GeneratedPart>());

        await Assert.ThrowsAsync<SqliteException>(() => workingRevisionRepository.SaveAsync(badRevision));

        // Row counts must be identical: rollback restored every deleted row.
        var countAfter = await CountRevisionRowsAsync(fixture, state.Revision.Id);

        Assert.Equal(countBefore.rooms, countAfter.rooms);
        Assert.Equal(countBefore.walls, countAfter.walls);
        Assert.Equal(countBefore.runs, countAfter.runs);
        Assert.Equal(countBefore.cabinets, countAfter.cabinets);
        Assert.Equal(countBefore.parts, countAfter.parts);
    }

    private static async Task<(int rooms, int walls, int runs, int cabinets, int parts)> CountRevisionRowsAsync(
        SqliteTestFixture fixture,
        RevisionId revisionId)
    {
        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        var id = revisionId.Value.ToString();

        return (
            await CountAsync(connection, "rooms", id),
            await CountAsync(connection, "walls", id),
            await CountAsync(connection, "runs", id),
            await CountAsync(connection, "cabinets", id),
            await CountAsync(connection, "parts", id));
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, string revisionId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE revision_id = @id;";
        cmd.Parameters.AddWithValue("@id", revisionId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
