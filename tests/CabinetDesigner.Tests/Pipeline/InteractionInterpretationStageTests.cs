using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;
using RunPlacement = CabinetDesigner.Domain.Commands.Layout.RunPlacement;

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

    [Fact]
    public void MoveCabinet_SameRunEndOfRun_PlacesAtCorrectPosition()
    {
        // Arrange: Create a run with 3 cabinets [A, B, C]
        var store = CreateStoreWithRun(out var run, out _);
        var cabinetAId = CabinetId.New();
        var slotA = run.AppendCabinet(cabinetAId, Length.FromInches(30m));
        var cabinetA = new CabinetStateRecord(
            cabinetAId, "base-30", Length.FromInches(30m), Length.FromInches(24m),
            run.Id, slotA.Id, CabinetCategory.Base, ConstructionMethod.Frameless);
        store.AddCabinet(cabinetA);

        var cabinetBId = CabinetId.New();
        var slotB = run.AppendCabinet(cabinetBId, Length.FromInches(30m));
        store.AddCabinet(new CabinetStateRecord(
            cabinetBId, "base-30", Length.FromInches(30m), Length.FromInches(24m),
            run.Id, slotB.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        var cabinetCId = CabinetId.New();
        var slotC = run.AppendCabinet(cabinetCId, Length.FromInches(30m));
        store.AddCabinet(new CabinetStateRecord(
            cabinetCId, "base-30", Length.FromInches(30m), Length.FromInches(24m),
            run.Id, slotC.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        Assert.Equal(3, run.Slots.Count);
        Assert.Equal(cabinetAId, run.Slots[0].CabinetId);
        Assert.Equal(cabinetBId, run.Slots[1].CabinetId);
        Assert.Equal(cabinetCId, run.Slots[2].CabinetId);

        // Act: Move cabinet A to EndOfRun (same run)
        var moveCommand = new MoveCabinetCommand(
            cabinetAId,
            run.Id,
            run.Id,
            RunPlacement.EndOfRun,
            origin: CommandOrigin.User,
            intentDescription: "move A to end",
            timestamp: DateTimeOffset.UnixEpoch,
            targetIndex: null);

        var context = CreateMoveContext(moveCommand, run, run, cabinetA);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);
        var deltas = deltaTracker.Finalize();

        // Assert: Result should be [B, A, C] (A moved to before the last position)
        Assert.True(result.Success);
        Assert.Equal(3, run.Slots.Count);
        Assert.Equal(cabinetBId, run.Slots[0].CabinetId);
        Assert.Equal(cabinetAId, run.Slots[1].CabinetId);
        Assert.Equal(cabinetCId, run.Slots[2].CabinetId);

        // Verify deltas were recorded
        Assert.Contains(deltas, d => d.EntityType == "CabinetRun" && d.Operation == DeltaOperation.Modified);
        Assert.Contains(deltas, d => d.EntityType == "Cabinet" && d.Operation == DeltaOperation.Modified);
    }

    [Fact]
    public void MoveCabinet_CrossRunEndOfRun_AppendsCorrectly()
    {
        // Arrange: Create two runs with cabinets
        var store = CreateStoreWithRun(out var run1, out var wall1);

        var cabinetAId = CabinetId.New();
        var slotA = run1.AppendCabinet(cabinetAId, Length.FromInches(30m));
        var cabinetA = new CabinetStateRecord(
            cabinetAId, "base-30", Length.FromInches(30m), Length.FromInches(24m),
            run1.Id, slotA.Id, CabinetCategory.Base, ConstructionMethod.Frameless);
        store.AddCabinet(cabinetA);

        // Create run2
        var wall2 = new Wall(WallId.New(), RoomId.New(), new Point2D(120m, 0m), new Point2D(216m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run2 = new CabinetRun(RunId.New(), wall2.Id, Length.FromInches(96m));
        store.AddWall(wall2);
        store.AddRun(run2, wall2.StartPoint, wall2.EndPoint);

        var cabinetBId = CabinetId.New();
        var slotB = run2.AppendCabinet(cabinetBId, Length.FromInches(36m));
        store.AddCabinet(new CabinetStateRecord(
            cabinetBId, "base-36", Length.FromInches(36m), Length.FromInches(24m),
            run2.Id, slotB.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        Assert.Single(run1.Slots);
        Assert.Single(run2.Slots);

        // Act: Move cabinet A from run1 to EndOfRun of run2
        var moveCommand = new MoveCabinetCommand(
            cabinetAId,
            run1.Id,
            run2.Id,
            RunPlacement.EndOfRun,
            origin: CommandOrigin.User,
            intentDescription: "move A to run2 end",
            timestamp: DateTimeOffset.UnixEpoch,
            targetIndex: null);

        var context = CreateMoveContext(moveCommand, run1, run2, cabinetA);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);
        var deltas = deltaTracker.Finalize();

        // Assert: run2 should have [B, A] at correct indices
        Assert.True(result.Success);
        Assert.Empty(run1.Slots);
        Assert.Equal(2, run2.Slots.Count);
        Assert.Equal(cabinetBId, run2.Slots[0].CabinetId);
        Assert.Equal(cabinetAId, run2.Slots[1].CabinetId);

        // Verify deltas were recorded
        Assert.Contains(deltas, d => d.EntityType == "CabinetRun" && d.Operation == DeltaOperation.Modified);
        Assert.Contains(deltas, d => d.EntityType == "Cabinet" && d.Operation == DeltaOperation.Modified);
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

    private static ResolutionContext CreateMoveContext(
        MoveCabinetCommand command,
        CabinetRun sourceRun,
        CabinetRun targetRun,
        CabinetStateRecord cabinet)
    {
        // Create resolved entities for MoveCabinetCommand
        var resolvedEntities = new Dictionary<string, IDomainEntity>
        {
            { "sourceRun", new ResolvedRunEntity(sourceRun) },
            { "targetRun", new ResolvedRunEntity(targetRun) },
            { "cabinet", new ResolvedCabinetEntity(cabinet) }
        };

        var context = new ResolutionContext
        {
            Command = command,
            Mode = ResolutionMode.Full
        };

        context.InputCapture = new InputCaptureResult
        {
            ResolvedEntities = resolvedEntities,
            NormalizedParameters = new Dictionary<string, OverrideValue>(),
            TemplateExpansions = []
        };

        return context;
    }
}
