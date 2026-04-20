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

public sealed class EditorSlicePreviewTests
{
    [Fact]
    public void Preview_MoveCabinet_RunsFastPathAndReturnsSpatialResult()
    {
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));
        store.AddWall(wall);
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

        var preview = orchestrator.Preview(new MoveCabinetCommand(cabinet.CabinetId, lowerRun.Id, upperRun.Id, RunPlacement.EndOfRun, CommandOrigin.User, "move", DateTimeOffset.UnixEpoch));

        Assert.True(preview.Success);
        Assert.NotNull(preview.SpatialResult);
        Assert.NotEmpty(preview.SpatialResult!.Placements);
    }

    private static IReadOnlyList<IResolutionStage> CreateEditorSliceStages(InMemoryDesignStateStore store, InMemoryDeltaTracker deltaTracker) =>
        [
            new InputCaptureStage(store),
            new InteractionInterpretationStage(deltaTracker, store),
            new SpatialResolutionStage(store)
        ];
}
