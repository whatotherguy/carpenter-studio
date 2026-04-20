using CabinetDesigner.Application;
using CabinetDesigner.Application.Explanation;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class EditorSliceOrchestratorTests
{
    [Fact]
    public void Execute_CreateRunAndAddCabinet_SucceedsAndPushesUndoEntry()
    {
        var store = CreateStoreWithWall(out var wall);
        var undoStack = new InMemoryUndoStack();
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            undoStack,
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        var createRun = new CreateRunCommand(Point2D.Origin, new Point2D(96m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "create", DateTimeOffset.UnixEpoch);
        var createResult = orchestrator.Execute(createRun);
        var run = Assert.Single(store.GetAllRuns());
        var addResult = orchestrator.Execute(new AddCabinetToRunCommand(run.Id, "base-36", Length.FromInches(36m), RunPlacement.EndOfRun, CommandOrigin.User, "add", DateTimeOffset.UnixEpoch));

        Assert.True(createResult.Success);
        Assert.True(addResult.Success);
        Assert.True(undoStack.CanUndo);
        Assert.Single(run.Slots);
    }

    [Fact]
    public void Execute_MoveCabinet_BetweenRuns_Succeeds()
    {
        var store = CreateStoreWithWall(out var wall);
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            new InMemoryUndoStack(),
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        Assert.True(orchestrator.Execute(new CreateRunCommand(Point2D.Origin, new Point2D(96m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "run-1", DateTimeOffset.UnixEpoch)).Success);
        Assert.True(orchestrator.Execute(new CreateRunCommand(new Point2D(0m, 24m), new Point2D(96m, 24m), wall.Id.Value.ToString(), CommandOrigin.User, "run-2", DateTimeOffset.UnixEpoch)).Success);
        var lowerRun = store.GetAllRuns().Single(run => store.GetRunSpatialInfo(run.Id)!.StartWorld.Y == 0m);
        var upperRun = store.GetAllRuns().Single(run => store.GetRunSpatialInfo(run.Id)!.StartWorld.Y == 24m);
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(lowerRun.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add", DateTimeOffset.UnixEpoch)).Success);
        var cabinet = Assert.Single(store.GetAllCabinets());

        var result = orchestrator.Execute(new MoveCabinetCommand(cabinet.CabinetId, lowerRun.Id, upperRun.Id, RunPlacement.EndOfRun, CommandOrigin.User, "move", DateTimeOffset.UnixEpoch));

        Assert.True(result.Success);
        Assert.Empty(lowerRun.Slots);
        Assert.Single(upperRun.Slots);
    }

    [Fact]
    public void Execute_MoveCabinet_WithinSameRun_ReordersSlots()
    {
        var store = CreateStoreWithWall(out var wall);
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            new InMemoryUndoStack(),
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        Assert.True(orchestrator.Execute(new CreateRunCommand(Point2D.Origin, new Point2D(120m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "run", DateTimeOffset.UnixEpoch)).Success);
        var run = Assert.Single(store.GetAllRuns());
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(run.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add-1", DateTimeOffset.UnixEpoch)).Success);
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(run.Id, "base-30", Length.FromInches(30m), RunPlacement.EndOfRun, CommandOrigin.User, "add-2", DateTimeOffset.UnixEpoch)).Success);
        var originalFirstCabinetId = store.GetRun(run.Id)!.Slots[0].CabinetId!.Value;
        var originalSecondCabinetId = store.GetRun(run.Id)!.Slots[1].CabinetId!.Value;

        var result = orchestrator.Execute(new MoveCabinetCommand(originalFirstCabinetId, run.Id, run.Id, RunPlacement.AtIndex, CommandOrigin.User, "move", DateTimeOffset.UnixEpoch, 1));

        Assert.True(result.Success);
        Assert.Equal(originalSecondCabinetId, store.GetRun(run.Id)!.Slots[0].CabinetId);
        Assert.Equal(originalFirstCabinetId, store.GetRun(run.Id)!.Slots[1].CabinetId);
    }

    [Fact]
    public void Execute_MoveCabinet_ToMissingRun_Fails()
    {
        var store = CreateStoreWithWall(out var wall);
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            new InMemoryUndoStack(),
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        Assert.True(orchestrator.Execute(new CreateRunCommand(Point2D.Origin, new Point2D(96m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "run", DateTimeOffset.UnixEpoch)).Success);
        var run = Assert.Single(store.GetAllRuns());
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(run.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add", DateTimeOffset.UnixEpoch)).Success);
        var cabinet = Assert.Single(store.GetAllCabinets());

        var result = orchestrator.Execute(new MoveCabinetCommand(cabinet.CabinetId, run.Id, RunId.New(), RunPlacement.EndOfRun, CommandOrigin.User, "move", DateTimeOffset.UnixEpoch));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "TARGET_RUN_NOT_FOUND");
    }

    [Fact]
    public void Undo_AddCabinet_RemovesCabinetFromRun()
    {
        var store = CreateStoreWithWall(out var wall);
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            new InMemoryUndoStack(),
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        Assert.True(orchestrator.Execute(new CreateRunCommand(Point2D.Origin, new Point2D(96m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "run", DateTimeOffset.UnixEpoch)).Success);
        var run = Assert.Single(store.GetAllRuns());
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(run.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add", DateTimeOffset.UnixEpoch)).Success);

        var result = orchestrator.Undo();

        Assert.NotNull(result);
        Assert.Empty(store.GetRun(run.Id)!.Slots);
        Assert.Empty(store.GetAllCabinets());
    }

    [Fact]
    public void Undo_MoveCabinet_RestoresCabinetToSourceRun()
    {
        var store = CreateStoreWithWall(out var wall);
        var undoStack = new InMemoryUndoStack();
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            undoStack,
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        Assert.True(orchestrator.Execute(new CreateRunCommand(Point2D.Origin, new Point2D(96m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "run-1", DateTimeOffset.UnixEpoch)).Success);
        Assert.True(orchestrator.Execute(new CreateRunCommand(new Point2D(0m, 24m), new Point2D(96m, 24m), wall.Id.Value.ToString(), CommandOrigin.User, "run-2", DateTimeOffset.UnixEpoch)).Success);
        var lowerRun = store.GetAllRuns().Single(run => store.GetRunSpatialInfo(run.Id)!.StartWorld.Y == 0m);
        var upperRun = store.GetAllRuns().Single(run => store.GetRunSpatialInfo(run.Id)!.StartWorld.Y == 24m);
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(lowerRun.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add", DateTimeOffset.UnixEpoch)).Success);
        var cabinet = Assert.Single(store.GetAllCabinets());
        Assert.True(orchestrator.Execute(new MoveCabinetCommand(cabinet.CabinetId, lowerRun.Id, upperRun.Id, RunPlacement.EndOfRun, CommandOrigin.User, "move", DateTimeOffset.UnixEpoch)).Success);

        var result = orchestrator.Undo();

        Assert.NotNull(result);
        Assert.Single(store.GetRun(lowerRun.Id)!.Slots);
        Assert.Empty(store.GetRun(upperRun.Id)!.Slots);
    }

    [Fact]
    public void Execute_MoveCabinet_WhenTargetRunOverCapacity_Fails()
    {
        var store = CreateStoreWithWall(out var wall);
        var deltaTracker = new InMemoryDeltaTracker();
        var orchestrator = new ResolutionOrchestrator(
            deltaTracker,
            new WhyEngine(),
            new InMemoryUndoStack(),
            store,
            store,
            stages: CreateEditorSliceStages(store, deltaTracker));

        Assert.True(orchestrator.Execute(new CreateRunCommand(Point2D.Origin, new Point2D(96m, 0m), wall.Id.Value.ToString(), CommandOrigin.User, "run-1", DateTimeOffset.UnixEpoch)).Success);
        Assert.True(orchestrator.Execute(new CreateRunCommand(new Point2D(0m, 24m), new Point2D(24m, 24m), wall.Id.Value.ToString(), CommandOrigin.User, "run-2", DateTimeOffset.UnixEpoch)).Success);
        var sourceRun = store.GetAllRuns().Single(run => store.GetRunSpatialInfo(run.Id)!.StartWorld.Y == 0m);
        var targetRun = store.GetAllRuns().Single(run => store.GetRunSpatialInfo(run.Id)!.StartWorld.Y == 24m);
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(sourceRun.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add-source", DateTimeOffset.UnixEpoch)).Success);
        Assert.True(orchestrator.Execute(new AddCabinetToRunCommand(targetRun.Id, "base-24", Length.FromInches(24m), RunPlacement.EndOfRun, CommandOrigin.User, "add-target", DateTimeOffset.UnixEpoch)).Success);
        var movableCabinet = store.GetAllCabinets().Single(cabinet => cabinet.RunId == sourceRun.Id);

        var result = orchestrator.Execute(new MoveCabinetCommand(movableCabinet.CabinetId, sourceRun.Id, targetRun.Id, RunPlacement.EndOfRun, CommandOrigin.User, "overflow", DateTimeOffset.UnixEpoch));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "INTERACTION_FAILED");
    }

    private static InMemoryDesignStateStore CreateStoreWithWall(out Wall wall)
    {
        var store = new InMemoryDesignStateStore();
        wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));
        store.AddWall(wall);
        return store;
    }

    private static IReadOnlyList<IResolutionStage> CreateEditorSliceStages(InMemoryDesignStateStore store, InMemoryDeltaTracker deltaTracker) =>
        [
            new InputCaptureStage(store),
            new InteractionInterpretationStage(deltaTracker, store),
            new SpatialResolutionStage(store)
        ];
}
