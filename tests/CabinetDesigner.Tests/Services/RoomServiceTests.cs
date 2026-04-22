using System.Threading;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Services;

public sealed class RoomServiceTests
{
    [Fact]
    public async Task CreateRoomAsync_PersistsRoom_WithName_AndCeilingHeight()
    {
        var fixture = CreateFixture();

        var room = await fixture.Service.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);

        Assert.Equal("Kitchen", room.Name);
        Assert.Equal(Length.FromInches(96m), room.CeilingHeight);
        Assert.Equal(room.Id, Assert.Single(fixture.StateStore.GetAllRooms()).Id);
        Assert.Single(fixture.WorkingRevisionRepository.SavedWorkingRevisions);
    }

    [Fact]
    public async Task CreateRoomAsync_RejectsEmptyName()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateRoomAsync("  ", Length.FromInches(96m), default));
    }

    [Fact]
    public async Task CreateRoomAsync_RejectsNonPositiveCeilingHeight()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateRoomAsync("Kitchen", Length.Zero, default));
    }

    [Fact]
    public async Task AddWallAsync_PersistsWall_WithExactStartEndThickness()
    {
        var fixture = CreateFixture();
        var room = await fixture.Service.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);

        var wall = await fixture.Service.AddWallAsync(room.Id, new Point2D(1m, 2m), new Point2D(121.5m, 2m), Thickness.Exact(Length.FromInches(4m)), default);

        Assert.Equal(room.Id, wall.RoomId);
        Assert.Equal(new Point2D(1m, 2m), wall.StartPoint);
        Assert.Equal(new Point2D(121.5m, 2m), wall.EndPoint);
        Assert.Equal(Length.FromInches(4m), wall.WallThickness.Actual);
        Assert.Single(fixture.StateStore.GetRoom(room.Id)!.Walls);
    }

    [Fact]
    public async Task AddWallAsync_RejectsZeroLengthWall()
    {
        var fixture = CreateFixture();
        var room = await fixture.Service.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.AddWallAsync(room.Id, Point2D.Origin, Point2D.Origin, Thickness.Exact(Length.FromInches(4m)), default));
    }

    [Fact]
    public async Task RemoveWallAsync_RemovesWall_AndLeavesOtherWallsIntact()
    {
        var fixture = CreateFixture();
        var room = await fixture.Service.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var firstWall = await fixture.Service.AddWallAsync(room.Id, Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
        var secondWall = await fixture.Service.AddWallAsync(room.Id, new Point2D(96m, 0m), new Point2D(96m, 96m), Thickness.Exact(Length.FromInches(4m)), default);

        await fixture.Service.RemoveWallAsync(firstWall.Id, default);

        var loadedRoom = fixture.StateStore.GetRoom(room.Id)!;
        Assert.Single(loadedRoom.Walls);
        Assert.Equal(secondWall.Id, loadedRoom.Walls[0].Id);
    }

    [Fact]
    public async Task ListRoomsAsync_IncludesRoomsCreatedThisSession()
    {
        var fixture = CreateFixture();

        var first = await fixture.Service.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var second = await fixture.Service.CreateRoomAsync("Pantry", Length.FromInches(90m), default);

        var rooms = await fixture.Service.ListRoomsAsync(default);

        Assert.Equal(2, rooms.Count);
        Assert.Contains(first.Id, rooms.Select(room => room.Id));
        Assert.Contains(second.Id, rooms.Select(room => room.Id));
    }

    private static Fixture CreateFixture()
    {
        var stateStore = new InMemoryDesignStateStore();
        var currentState = CreateCurrentState(stateStore);
        var workingRevisionRepository = new RecordingWorkingRevisionRepository();

        return new Fixture(
            new RoomService(
                workingRevisionRepository,
                new RecordingUnitOfWork(),
                currentState,
                currentState,
                stateStore,
                new InMemoryDeltaTracker(),
                new InMemoryUndoStack(),
                new ApplicationEventBus(),
                new FixedClock()),
            stateStore,
            workingRevisionRepository,
            currentState);
    }

    private static CurrentWorkingRevisionSource CreateCurrentState(InMemoryDesignStateStore stateStore)
    {
        var currentState = new CurrentWorkingRevisionSource(stateStore);
        var createdAt = DateTimeOffset.Parse("2026-04-18T12:00:00Z");
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        var project = new ProjectRecord(projectId, "Sample", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint(Guid.NewGuid().ToString("N"), projectId, revisionId, createdAt, null, true);
        currentState.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
        return currentState;
    }

    private sealed record Fixture(RoomService Service, InMemoryDesignStateStore StateStore, RecordingWorkingRevisionRepository WorkingRevisionRepository, CurrentWorkingRevisionSource CurrentState);

    private sealed class RecordingWorkingRevisionRepository : IWorkingRevisionRepository
    {
        public List<WorkingRevision> SavedWorkingRevisions { get; } = [];

        public Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<WorkingRevision?>(SavedWorkingRevisions.LastOrDefault(revision => revision.Revision.ProjectId == projectId));

        public Task SaveAsync(WorkingRevision revision, CancellationToken ct = default)
        {
            SavedWorkingRevisions.Add(revision);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.Parse("2026-04-18T12:00:00Z");
    }
}
