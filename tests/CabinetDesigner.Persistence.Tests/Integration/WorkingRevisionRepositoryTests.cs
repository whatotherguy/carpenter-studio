using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using CabinetDesigner.Persistence.UnitOfWork;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

/// <summary>
/// Regression tests for WorkingRevisionRepository error paths, null-handling
/// branches, empty-collection edge cases, and the UoW enrollment path.
/// </summary>
public sealed class WorkingRevisionRepositoryTests
{
    // -------------------------------------------------------------------------
    // Missing-data / null-result read scenarios
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: LoadRevisionAsync throws or returns garbage when the
    /// projects table has no matching draft revision (e.g. project never
    /// persisted, or the project ID is stale).
    /// </summary>
    [Fact]
    public async Task LoadAsync_UnknownProjectId_ReturnsNull()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var repo = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var unknownProjectId = ProjectId.New();

        var result = await repo.LoadAsync(unknownProjectId);

        Assert.Null(result);
    }

    /// <summary>
    /// Guards against: Removing the <c>state = 'Draft'</c> filter from the
    /// revision query, which would allow an already-approved revision to be
    /// treated as an editable working copy.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ProjectHasOnlyApprovedRevision_ReturnsNull()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        // Save the revision as Approved — not Draft.
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision with
        {
            State = ApprovalState.Approved,
            ApprovedAt = DateTimeOffset.UtcNow,
            ApprovedBy = "tester"
        });

        var repo = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        var result = await repo.LoadAsync(state.Project.Id);

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Null optional-field round-trips (tests the IsDBNull branches)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: Removing the <c>IsDBNull</c> guard for
    /// <c>overrides_json</c> so that a NULL DB value causes a
    /// <see cref="InvalidCastException"/> instead of an empty overrides map.
    /// Simulates a legacy row written before overrides were introduced.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CabinetWithNullOverridesJson_ReturnsEmptyOverrides()
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

        // Simulate a legacy cabinet row with no overrides stored.
        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE cabinets SET overrides_json = NULL WHERE revision_id = @revisionId;";
        update.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        await update.ExecuteNonQueryAsync();

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Cabinets);
        Assert.Empty(loaded.Cabinets[0].Overrides);
    }

    /// <summary>
    /// Guards against: Removing the <c>IsDBNull</c> / empty-string guards for
    /// <c>end_condition_start</c> and <c>end_condition_end</c> so that NULL DB
    /// values throw during enum parsing instead of defaulting to
    /// <see cref="EndConditionType.Open"/>.
    /// </summary>
    [Fact]
    public async Task LoadAsync_RunWithNullEndConditions_DefaultsToOpen()
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

        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE runs SET end_condition_start = NULL, end_condition_end = NULL WHERE revision_id = @revisionId;";
        update.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        await update.ExecuteNonQueryAsync();

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Runs);
        Assert.Equal(EndConditionType.Open, loaded.Runs[0].LeftEndCondition.Type);
        Assert.Equal(EndConditionType.Open, loaded.Runs[0].RightEndCondition.Type);
    }

    /// <summary>
    /// Guards against: Removing the <c>IsDBNull</c> guards for
    /// <c>grain_direction</c> and <c>edge_treatment_json</c> so that NULL DB
    /// values throw instead of yielding their domain defaults
    /// (<see cref="GrainDirection.None"/> and empty edge treatment).
    /// </summary>
    [Fact]
    public async Task LoadAsync_PartWithNullGrainDirectionAndEdgeTreatment_UsesDefaults()
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

        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE parts SET grain_direction = NULL, edge_treatment_json = NULL WHERE revision_id = @revisionId;";
        update.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        await update.ExecuteNonQueryAsync();

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Parts);
        Assert.Equal(GrainDirection.None, loaded.Parts[0].GrainDirection);
        Assert.Equal(new EdgeTreatment(null, null, null, null), loaded.Parts[0].Edges);
    }

    /// <summary>
    /// Guards against: Removing the <c>row.Name ?? "Room"</c> fallback in
    /// <c>RoomMapper.ToDomain</c> so that a NULL DB name causes a domain
    /// constructor validation failure instead of a graceful default.
    /// </summary>
    [Fact]
    public async Task LoadAsync_RoomWithNullName_FallsBackToDefaultName()
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

        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE rooms SET name = NULL WHERE revision_id = @revisionId;";
        update.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        await update.ExecuteNonQueryAsync();

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Rooms);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Rooms[0].Name), "Room name should fall back to a non-empty default.");
    }

    // -------------------------------------------------------------------------
    // Empty-collections edge case
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: Crashes in the insert loops when collections are empty,
    /// or incorrect assumptions that a saved revision must have at least one entity.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithEmptyCollections_LoadReturnsRevisionWithNoEntities()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var emptyRevision = new WorkingRevision(
            state.Revision,
            Rooms: [],
            Walls: [],
            Runs: [],
            Cabinets: [],
            Parts: Array.Empty<GeneratedPart>());

        await workingRevisionRepository.SaveAsync(emptyRevision);

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Equal(state.Revision.Id, loaded!.Revision.Id);
        Assert.Empty(loaded.Rooms);
        Assert.Empty(loaded.Walls);
        Assert.Empty(loaded.Runs);
        Assert.Empty(loaded.Cabinets);
        Assert.Empty(loaded.Parts);
    }

    // -------------------------------------------------------------------------
    // External UnitOfWork enrollment
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: The repository creating its own local transaction when
    /// an external UoW session is already active, which would break the
    /// "if transaction is not null → skip local transaction" branch and prevent
    /// the outer rollback from undoing saves made through the repository.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithinExternalUnitOfWork_UnitOfWorkRollbackUndoesSave()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        // Save the baseline working revision outside any UoW.
        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        // Begin an outer UoW and save a modified revision with no cabinets.
        await using var uow = new SqliteUnitOfWork(fixture.ConnectionFactory, fixture.SessionAccessor);
        await uow.BeginAsync();

        var modifiedRevision = new WorkingRevision(
            state.Revision,
            state.WorkingRevision.Rooms,
            state.WorkingRevision.Walls,
            state.WorkingRevision.Runs,
            Cabinets: [],
            Parts: Array.Empty<GeneratedPart>());

        await workingRevisionRepository.SaveAsync(modifiedRevision);

        // Roll back the outer UoW — the modified save must be fully reverted.
        await uow.RollbackAsync();

        // The original data (with one cabinet) must be visible after rollback.
        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Cabinets);
        Assert.Equal(state.WorkingRevision.Cabinets[0].Id, loaded.Cabinets[0].Id);
    }
}
