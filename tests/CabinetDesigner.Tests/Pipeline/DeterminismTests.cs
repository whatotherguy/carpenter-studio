using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class DeterminismTests
{
    [Fact]
    public void SpatialResolutionStage_WithSameState_ProducesIdenticalPlacements()
    {
        var store = CreateDesignState();
        var stage = new SpatialResolutionStage(store);

        var firstContext = CreateContext();
        var secondContext = CreateContext();

        var firstResult = stage.Execute(firstContext);
        var secondResult = stage.Execute(secondContext);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.Equal(firstContext.SpatialResult.SlotPositionUpdates, secondContext.SpatialResult.SlotPositionUpdates);
        Assert.Equal(firstContext.SpatialResult.AdjacencyChanges, secondContext.SpatialResult.AdjacencyChanges);
        Assert.Equal(firstContext.SpatialResult.RunSummaries, secondContext.SpatialResult.RunSummaries);
        Assert.Equal(firstContext.SpatialResult.Placements, secondContext.SpatialResult.Placements);
    }

    private static InMemoryDesignStateStore CreateDesignState()
    {
        var store = new InMemoryDesignStateStore();
        var roomId = RoomId.New();
        var wall = new Wall(WallId.New(), roomId, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var firstCabinetId = CabinetId.New();
        var secondCabinetId = CabinetId.New();
        var firstSlot = run.AppendCabinet(firstCabinetId, Length.FromInches(30m));
        var secondSlot = run.AppendCabinet(secondCabinetId, Length.FromInches(36m));

        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(new CabinetStateRecord(firstCabinetId, "base-30", Length.FromInches(30m), Length.FromInches(24m), run.Id, firstSlot.Id));
        store.AddCabinet(new CabinetStateRecord(secondCabinetId, "drawer-36", Length.FromInches(36m), Length.FromInches(24m), run.Id, secondSlot.Id));
        return store;
    }

    private static ResolutionContext CreateContext() =>
        new()
        {
            Command = new DeterministicTestCommand(),
            Mode = ResolutionMode.Full
        };

    private sealed record DeterministicTestCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.System, "Determinism", []);

        public string CommandType => "test.determinism";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
