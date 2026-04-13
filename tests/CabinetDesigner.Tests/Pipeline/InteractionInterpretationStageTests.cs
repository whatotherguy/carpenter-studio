using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class InteractionInterpretationStageTests
{
    [Fact]
    public void InsertCabinet_WithTwoCabinets_InsertsAtSpecifiedIndex()
    {
        // Arrange: Create a run with 2 cabinets
        var store = CreateStoreWithRun(out var run, out _);
        var firstCabinetId = CabinetId.New();
        var firstSlot = run.AppendCabinet(firstCabinetId, Length.FromInches(30m));
        store.AddCabinet(new CabinetStateRecord(
            firstCabinetId, "base-30", Length.FromInches(30m), Length.FromInches(24m),
            run.Id, firstSlot.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        var secondCabinetId = CabinetId.New();
        var secondSlot = run.AppendCabinet(secondCabinetId, Length.FromInches(36m));
        store.AddCabinet(new CabinetStateRecord(
            secondCabinetId, "base-36", Length.FromInches(36m), Length.FromInches(24m),
            run.Id, secondSlot.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        Assert.Equal(2, store.GetAllCabinets().Count);

        // Act: Execute InsertCabinetIntoRunCommand at index 1
        var command = new InsertCabinetIntoRunCommand(
            run.Id,
            "base-24",
            Length.FromInches(24m),
            insertAtIndex: 1,
            leftNeighborId: firstCabinetId,
            rightNeighborId: secondCabinetId,
            origin: CommandOrigin.User,
            intentDescription: "insert cabinet",
            timestamp: DateTimeOffset.UnixEpoch,
            nominalDepth: Length.FromInches(24m),
            category: CabinetCategory.Base,
            construction: ConstructionMethod.Frameless);

        var context = CreateContext(command);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);
        var deltas = deltaTracker.Finalize();

        // Assert: Verify state store has 3 cabinets and inserted cabinet is at index 1
        Assert.True(result.Success);
        Assert.Equal(3, store.GetAllCabinets().Count);
        Assert.Equal(3, run.Slots.Count);
        Assert.Equal(1, run.Slots[1].SlotIndex);

        // The first cabinet should still be at index 0
        Assert.Equal(firstCabinetId, run.Slots[0].CabinetId);
        // The second cabinet should now be at index 2 (pushed right)
        Assert.Equal(secondCabinetId, run.Slots[2].CabinetId);

        // Verify deltas were recorded
        Assert.Contains(deltas, d => d.EntityType == "CabinetRun" && d.Operation == DeltaOperation.Modified);
        Assert.Contains(deltas, d => d.EntityType == "Cabinet" && d.Operation == DeltaOperation.Created);
    }

    [Fact]
    public void InsertCabinet_WithNonExistentRun_ReturnsFailed()
    {
        // Arrange: Create store without a run with the requested ID
        var store = new InMemoryDesignStateStore();
        var nonExistentRunId = RunId.New();

        var command = new InsertCabinetIntoRunCommand(
            nonExistentRunId,
            "base-24",
            Length.FromInches(24m),
            insertAtIndex: 0,
            leftNeighborId: CabinetId.New(),
            rightNeighborId: CabinetId.New(),
            origin: CommandOrigin.User,
            intentDescription: "insert into non-existent run",
            timestamp: DateTimeOffset.UnixEpoch);

        var context = CreateContext(command);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        // Act & Assert: Stage should fail, not silently succeed
        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "INTERACTION_FAILED");
    }

    private static InMemoryDesignStateStore CreateStoreWithRun(out CabinetRun run, out Wall wall)
    {
        var store = new InMemoryDesignStateStore();
        wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        return store;
    }

    private static ResolutionContext CreateContext(IDesignCommand command) =>
        new()
        {
            Command = command,
            Mode = ResolutionMode.Full
        };
}
