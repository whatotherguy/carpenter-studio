using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests;

public sealed class WorkingRevisionReconstructionTests
{
    [Fact]
    public async Task LoadAsync_ReconstructsRunSlotsInSavedOrder()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var state = CreateMultiRunState();
        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Runs.Count);

        var firstRun = loaded.Runs.Single(run => run.Id == state.WorkingRevision.Runs[0].Id);
        var secondRun = loaded.Runs.Single(run => run.Id == state.WorkingRevision.Runs[1].Id);

        Assert.Equal(
            state.WorkingRevision.Runs[0].Slots.Select(slot => slot.CabinetId).ToArray(),
            firstRun.Slots.Select(slot => slot.CabinetId).ToArray());
        Assert.Equal(
            state.WorkingRevision.Runs[1].Slots.Select(slot => slot.CabinetId).ToArray(),
            secondRun.Slots.Select(slot => slot.CabinetId).ToArray());
        Assert.Equal(
            state.WorkingRevision.Runs[0].Slots.Select(slot => slot.OccupiedWidth).ToArray(),
            firstRun.Slots.Select(slot => slot.OccupiedWidth).ToArray());
        Assert.Equal(
            state.WorkingRevision.Runs[1].Slots.Select(slot => slot.OccupiedWidth).ToArray(),
            secondRun.Slots.Select(slot => slot.OccupiedWidth).ToArray());
    }

    [Fact]
    public async Task LoadAsync_ReplayIsDeterministic_AcrossMultipleLoads()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var state = CreateMultiRunState();
        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        var firstLoad = await workingRevisionRepository.LoadAsync(state.Project.Id);
        var secondLoad = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(firstLoad);
        Assert.NotNull(secondLoad);
        Assert.Equal(firstLoad!.Runs.Select(run => run.Id).ToArray(), secondLoad!.Runs.Select(run => run.Id).ToArray());
        Assert.Equal(firstLoad.Cabinets.Select(cabinet => cabinet.Id).ToArray(), secondLoad.Cabinets.Select(cabinet => cabinet.Id).ToArray());

        for (var i = 0; i < firstLoad.Runs.Count; i++)
        {
            Assert.Equal(
                firstLoad.Runs[i].Slots.Select(slot => slot.CabinetId).ToArray(),
                secondLoad.Runs[i].Slots.Select(slot => slot.CabinetId).ToArray());
            Assert.Equal(
                firstLoad.Runs[i].Slots.Select(slot => slot.OccupiedWidth).ToArray(),
                secondLoad.Runs[i].Slots.Select(slot => slot.OccupiedWidth).ToArray());
        }
    }

    [Fact]
    public async Task LoadAsync_CorruptedRowThrows_WithStableExceptionType()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var state = CreateMultiRunState();
        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        await using (var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA foreign_keys = OFF;
                UPDATE cabinets SET run_id = 'not-a-guid' WHERE revision_id = @revisionId;
                """;
            command.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => workingRevisionRepository.LoadAsync(state.Project.Id));

        Assert.Equal("WORKING_REVISION_CORRUPT", exception.Message);
        Assert.IsType<FormatException>(exception.InnerException);
    }

    private static PersistedProjectState CreateMultiRunState()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var room = new Room(RoomId.New(), revisionId, "Kitchen", Length.FromFeet(12m));
        var wall = new Wall(WallId.New(), room.Id, Point2D.Origin, new Point2D(180m, 0m), Thickness.Exact(Length.FromInches(4m)));

        var firstRun = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var secondRun = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(60m));

        var cabinetA = CreateCabinet(revisionId, "base-24", 24m);
        var cabinetB = CreateCabinet(revisionId, "base-30", 30m);
        var cabinetC = CreateCabinet(revisionId, "tall-18", 18m);

        firstRun.AppendCabinet(cabinetA.Id, cabinetA.NominalWidth);
        firstRun.AppendCabinet(cabinetB.Id, cabinetB.NominalWidth);
        secondRun.AppendCabinet(cabinetC.Id, cabinetC.NominalWidth);

        var project = new ProjectRecord(projectId, "Reconstruction", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Draft");
        var workingRevision = new WorkingRevision(revision, [room], [wall], [firstRun, secondRun], [cabinetC, cabinetA, cabinetB], Array.Empty<GeneratedPart>());
        return new PersistedProjectState(project, revision, workingRevision, null);
    }

    private static Cabinet CreateCabinet(RevisionId revisionId, string cabinetTypeId, decimal nominalWidthInches) =>
        new(
            CabinetId.New(),
            revisionId,
            cabinetTypeId,
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.FromInches(nominalWidthInches),
            Length.FromInches(24m),
            Length.FromInches(34.5m));
}
