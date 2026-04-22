using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Persistence;
using CabinetDesigner.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CabinetDesigner.Tests.Persistence;

public sealed class RoomRoundTripTests
{
    [Fact]
    public async Task SaveRoomWithWallsAndObstacles_LoadAgain_PreservesAllFields()
    {
        await using var context = await CreateContextAsync();
        var projectRepository = context.Scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var revisionRepository = context.Scope.ServiceProvider.GetRequiredService<IRevisionRepository>();
        var repository = context.Scope.ServiceProvider.GetRequiredService<IWorkingRevisionRepository>();
        var revision = CreateRevision();
        await SeedRevisionAsync(projectRepository, revisionRepository, revision);
        var room = new Room(RoomId.New(), revision.Id, "Kitchen", Length.FromInches(96m));
        var firstWall = room.AddWall(Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var secondWall = room.AddWall(new Point2D(96m, 0m), new Point2D(96m, 96m), Thickness.Exact(Length.FromInches(4m)));
        var obstacle = room.AddObstacle(new Rect2D(new Point2D(12m, 18m), Length.FromInches(24m), Length.FromInches(30m)), "Island");
        var workingRevision = new WorkingRevision(revision, [room], [firstWall, secondWall], [], [], []);

        await repository.SaveAsync(workingRevision);
        var loaded = await repository.LoadAsync(revision.ProjectId);

        var loadedRoom = Assert.Single(loaded!.Rooms);
        Assert.Equal(room.Id, loadedRoom.Id);
        Assert.Equal("Kitchen", loadedRoom.Name);
        Assert.Equal(room.CeilingHeight, loadedRoom.CeilingHeight);
        Assert.Collection(loadedRoom.Walls, _ => { }, _ => { });
        Assert.Single(loadedRoom.Obstacles);
        Assert.Equal(obstacle.Description, loadedRoom.Obstacles[0].Description);
    }

    [Fact]
    public async Task WallThicknessAndEndpoints_RoundTripExactly_ToShopTolerance()
    {
        await using var context = await CreateContextAsync();
        var projectRepository = context.Scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var revisionRepository = context.Scope.ServiceProvider.GetRequiredService<IRevisionRepository>();
        var repository = context.Scope.ServiceProvider.GetRequiredService<IWorkingRevisionRepository>();
        var revision = CreateRevision();
        await SeedRevisionAsync(projectRepository, revisionRepository, revision);
        var room = new Room(RoomId.New(), revision.Id, "Kitchen", Length.FromInches(96m));
        var wall = room.AddWall(
            new Point2D(12.125m, 4.5m),
            new Point2D(132.875m, 4.5m),
            Thickness.Exact(Length.FromInches(4m)));
        var workingRevision = new WorkingRevision(revision, [room], [wall], [], [], []);

        await repository.SaveAsync(workingRevision);
        var loaded = await repository.LoadAsync(revision.ProjectId);
        var loadedWall = Assert.Single(loaded!.Rooms.Single().Walls);

        Assert.Equal(wall.WallThickness.Actual, loadedWall.WallThickness.Actual);
        Assert.True(wall.StartPoint.DistanceTo(loadedWall.StartPoint) <= GeometryTolerance.DefaultShopTolerance);
        Assert.True(wall.EndPoint.DistanceTo(loadedWall.EndPoint) <= GeometryTolerance.DefaultShopTolerance);
    }

    private static async Task<RoomRoundTripContext> CreateContextAsync()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"carpenter-room-roundtrip-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddPersistence(filePath);
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<MigrationRunner>().RunAsync();
        return new RoomRoundTripContext(provider.CreateScope(), provider, filePath);
    }

    private static RevisionRecord CreateRevision()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-18T12:00:00Z");
        var projectId = ProjectId.New();
        var revisionId = RevisionId.New();
        return new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
    }

    private static async Task SeedRevisionAsync(IProjectRepository projectRepository, IRevisionRepository revisionRepository, RevisionRecord revision)
    {
        var createdAt = revision.CreatedAt;
        var project = new ProjectRecord(revision.ProjectId, "Kitchen Shop", null, createdAt, createdAt, ApprovalState.Draft);
        await projectRepository.SaveAsync(project);
        await revisionRepository.SaveAsync(revision);
    }

    private sealed class RoomRoundTripContext : IAsyncDisposable
    {
        public RoomRoundTripContext(IServiceScope scope, IServiceProvider provider, string filePath)
        {
            Scope = scope;
            Provider = provider;
            FilePath = filePath;
        }

        public IServiceScope Scope { get; }

        public IServiceProvider Provider { get; }

        public string FilePath { get; }

        public ValueTask DisposeAsync()
        {
            Scope.Dispose();
            if (Provider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            try
            {
                File.Delete(FilePath);
            }
            catch
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
