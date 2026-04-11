using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using DomainRunPlacement = CabinetDesigner.Domain.Commands.Layout.RunPlacement;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class EditorSliceStageTests
{
    [Fact]
    public void InputCapture_AddCabinet_ResolvesRunIntoContext()
    {
        var store = CreateStoreWithRun(out var run, out _);
        var stage = new InputCaptureStage(store);
        var context = CreateContext(new AddCabinetToRunCommand(
            run.Id,
            "base-36",
            Length.FromInches(36m),
            DomainRunPlacement.EndOfRun,
            CommandOrigin.User,
            "add",
            DateTimeOffset.UnixEpoch));

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var resolved = Assert.IsType<ResolvedRunEntity>(context.InputCapture.ResolvedEntities["run"]);
        Assert.Same(run, resolved.Run);
    }

    [Fact]
    public void Interaction_AddCabinet_AppendsSlotAndRecordsDeltas()
    {
        var store = CreateStoreWithRun(out var run, out _);
        var deltaTracker = new InMemoryDeltaTracker();
        var inputContext = CreateContext(new AddCabinetToRunCommand(
            run.Id,
            "base-36",
            Length.FromInches(36m),
            DomainRunPlacement.EndOfRun,
            CommandOrigin.User,
            "add",
            DateTimeOffset.UnixEpoch));
        Assert.True(new InputCaptureStage(store).Execute(inputContext).Success);
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(inputContext);
        var deltas = deltaTracker.Finalize();

        Assert.True(result.Success);
        Assert.Single(run.Slots);
        Assert.Contains(deltas, delta => delta.EntityType == "Cabinet" && delta.Operation == DeltaOperation.Created);
    }

    [Fact]
    public void Interaction_AddCabinet_WhenRunAtCapacity_Fails()
    {
        var store = CreateStoreWithRun(out var run, out _);
        run.AppendCabinet(CabinetId.New(), Length.FromInches(96m));
        var deltaTracker = new InMemoryDeltaTracker();
        var context = CreateContext(new AddCabinetToRunCommand(
            run.Id,
            "overflow",
            Length.FromInches(1m),
            DomainRunPlacement.EndOfRun,
            CommandOrigin.User,
            "overflow",
            DateTimeOffset.UnixEpoch));
        Assert.True(new InputCaptureStage(store).Execute(context).Success);
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "INTERACTION_FAILED");
    }

    [Fact]
    public void Interaction_MoveCabinet_MovesBetweenRuns()
    {
        var store = CreateStoreWithRun(out var sourceRun, out var wall);
        var targetRun = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        store.AddRun(targetRun, new Point2D(0m, 24m), new Point2D(96m, 24m));
        var cabinetId = CabinetId.New();
        var sourceSlot = sourceRun.AppendCabinet(cabinetId, Length.FromInches(24m));
        store.AddCabinet(new CabinetStateRecord(cabinetId, "base-24", Length.FromInches(24m), Length.FromInches(24m), sourceRun.Id, sourceSlot.Id));

        var command = new MoveCabinetCommand(
            cabinetId,
            sourceRun.Id,
            targetRun.Id,
            DomainRunPlacement.EndOfRun,
            CommandOrigin.User,
            "move",
            DateTimeOffset.UnixEpoch);
        var context = CreateContext(command);
        Assert.True(new InputCaptureStage(store).Execute(context).Success);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);

        Assert.True(result.Success);
        Assert.Empty(sourceRun.Slots);
        Assert.Single(targetRun.Slots);
    }

    [Fact]
    public void SpatialResolution_SingleCabinet_StartsAtRunOrigin()
    {
        var store = CreateStoreWithRun(out var run, out _);
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(30m));
        store.AddCabinet(new CabinetStateRecord(cabinetId, "base-30", Length.FromInches(30m), Length.FromInches(24m), run.Id, slot.Id));

        var context = CreateContext(new AddCabinetToRunCommand(run.Id, "base-30", Length.FromInches(30m), DomainRunPlacement.EndOfRun, CommandOrigin.User, "preview", DateTimeOffset.UnixEpoch));
        context.Interpretation = new InteractionInterpretationResult
        {
            Operations = [],
            InterpretedParameters = new Dictionary<string, OverrideValue>()
        };

        var result = new SpatialResolutionStage(store).Execute(context);

        Assert.True(result.Success);
        var placement = Assert.Single(context.SpatialResult.Placements);
        Assert.Equal(0m, placement.Origin.X);
        Assert.Equal(0m, placement.Origin.Y);
    }

    [Fact]
    public void SpatialResolution_TwoCabinets_OffsetsSecondByFirstWidth()
    {
        var store = CreateStoreWithRun(out var run, out _);
        var firstCabinetId = CabinetId.New();
        var firstSlot = run.AppendCabinet(firstCabinetId, Length.FromInches(30m));
        store.AddCabinet(new CabinetStateRecord(firstCabinetId, "base-30", Length.FromInches(30m), Length.FromInches(24m), run.Id, firstSlot.Id));
        var secondCabinetId = CabinetId.New();
        var secondSlot = run.AppendCabinet(secondCabinetId, Length.FromInches(18m));
        store.AddCabinet(new CabinetStateRecord(secondCabinetId, "base-18", Length.FromInches(18m), Length.FromInches(24m), run.Id, secondSlot.Id));

        var context = CreateContext(new AddCabinetToRunCommand(run.Id, "base-18", Length.FromInches(18m), DomainRunPlacement.EndOfRun, CommandOrigin.User, "preview", DateTimeOffset.UnixEpoch));
        context.Interpretation = new InteractionInterpretationResult
        {
            Operations = [],
            InterpretedParameters = new Dictionary<string, OverrideValue>()
        };

        Assert.True(new SpatialResolutionStage(store).Execute(context).Success);

        var secondPlacement = context.SpatialResult.Placements.Single(placement => placement.CabinetId == secondCabinetId);
        Assert.Equal(30m, secondPlacement.Origin.X);
    }

    [Fact]
    public void SpatialResolution_DiagonalWall_FollowsWallDirection()
    {
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(48m, 48m), Thickness.Exact(Length.FromInches(4m)));
        store.AddWall(wall);
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(67.8822509939m));
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(24m));
        store.AddCabinet(new CabinetStateRecord(cabinetId, "diag", Length.FromInches(24m), Length.FromInches(24m), run.Id, slot.Id));
        var context = CreateContext(new AddCabinetToRunCommand(run.Id, "diag", Length.FromInches(24m), DomainRunPlacement.EndOfRun, CommandOrigin.User, "preview", DateTimeOffset.UnixEpoch));
        context.Interpretation = new InteractionInterpretationResult
        {
            Operations = [],
            InterpretedParameters = new Dictionary<string, OverrideValue>()
        };

        Assert.True(new SpatialResolutionStage(store).Execute(context).Success);

        var placement = Assert.Single(context.SpatialResult.Placements);
        Assert.Equal(wall.Direction, placement.Direction);
    }

    [Fact]
    public void Interaction_MoveCabinet_PreservesNonStandardDepth()
    {
        var store = CreateStoreWithRun(out var sourceRun, out var wall);
        var targetRun = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        store.AddRun(targetRun, new Point2D(0m, 24m), new Point2D(96m, 24m));
        var cabinetId = CabinetId.New();
        var sourceSlot = sourceRun.AppendCabinet(cabinetId, Length.FromInches(24m));
        store.AddCabinet(new CabinetStateRecord(cabinetId, "deep-30", Length.FromInches(24m), Length.FromInches(30m), sourceRun.Id, sourceSlot.Id));

        var command = new MoveCabinetCommand(
            cabinetId,
            sourceRun.Id,
            targetRun.Id,
            DomainRunPlacement.EndOfRun,
            CommandOrigin.User,
            "move",
            DateTimeOffset.UnixEpoch);
        var context = CreateContext(command);
        Assert.True(new InputCaptureStage(store).Execute(context).Success);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);

        Assert.True(result.Success);
        var movedCabinet = store.GetCabinet(cabinetId);
        Assert.NotNull(movedCabinet);
        Assert.Equal(Length.FromInches(30m), movedCabinet.NominalDepth);
    }

    [Fact]
    public void Interaction_AddCabinet_UsesDepthFromCommand()
    {
        // Guards against the DefaultCabinetDepth = 24" hardcoding: the depth must come
        // from AddCabinetToRunCommand.NominalDepth, not from a static fallback.
        var store = CreateStoreWithRun(out var run, out _);
        var deltaTracker = new InMemoryDeltaTracker();
        var command = new AddCabinetToRunCommand(
            run.Id,
            "base-36",
            Length.FromInches(36m),
            DomainRunPlacement.EndOfRun,
            CommandOrigin.User,
            "add",
            DateTimeOffset.UnixEpoch,
            nominalDepth: Length.FromInches(30m));
        var context = CreateContext(command);
        Assert.True(new InputCaptureStage(store).Execute(context).Success);
        deltaTracker.Begin();

        Assert.True(new InteractionInterpretationStage(deltaTracker, store).Execute(context).Success);

        var allCabinets = store.GetAllCabinets();
        Assert.Single(allCabinets);
        Assert.Equal(Length.FromInches(30m), allCabinets[0].NominalDepth);
    }

    [Fact]
    public void InputCapture_ResizeCabinet_ResolvesCabinetIntoContext()
    {
        var store = CreateStoreWithRun(out var run, out _);
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(24m));
        store.AddCabinet(new CabinetStateRecord(cabinetId, "base-24", Length.FromInches(24m), Length.FromInches(24m), run.Id, slot.Id));

        var command = new ResizeCabinetCommand(
            cabinetId,
            Length.FromInches(24m),
            Length.FromInches(30m),
            CommandOrigin.User,
            "resize",
            DateTimeOffset.UnixEpoch);
        var context = CreateContext(command);

        var result = new InputCaptureStage(store).Execute(context);

        Assert.True(result.Success);
        Assert.True(context.InputCapture.ResolvedEntities.ContainsKey("cabinet"));
        var resolved = Assert.IsType<ResolvedCabinetEntity>(context.InputCapture.ResolvedEntities["cabinet"]);
        Assert.Equal(cabinetId, resolved.Cabinet.CabinetId);
    }

    [Fact]
    public void Interaction_ResizeCabinet_UpdatesWidthAndPreservesDepth()
    {
        var store = CreateStoreWithRun(out var run, out _);
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(24m));
        store.AddCabinet(new CabinetStateRecord(cabinetId, "base-24", Length.FromInches(24m), Length.FromInches(30m), run.Id, slot.Id));

        var command = new ResizeCabinetCommand(
            cabinetId,
            Length.FromInches(24m),
            Length.FromInches(36m),
            CommandOrigin.User,
            "resize",
            DateTimeOffset.UnixEpoch);
        var context = CreateContext(command);
        Assert.True(new InputCaptureStage(store).Execute(context).Success);
        var deltaTracker = new InMemoryDeltaTracker();
        deltaTracker.Begin();

        var result = new InteractionInterpretationStage(deltaTracker, store).Execute(context);

        Assert.True(result.Success);
        var updatedCabinet = store.GetCabinet(cabinetId);
        Assert.NotNull(updatedCabinet);
        Assert.Equal(Length.FromInches(36m), updatedCabinet!.NominalWidth);
        Assert.Equal(Length.FromInches(30m), updatedCabinet.NominalDepth);
        Assert.Single(run.Slots);
        Assert.Equal(Length.FromInches(36m), run.Slots[0].OccupiedWidth);
    }

    [Fact]
    public void InputCapture_ResizeCabinet_WhenCabinetNotFound_ReturnsFailed()
    {
        var store = CreateStoreWithRun(out _, out _);
        var command = new ResizeCabinetCommand(
            CabinetId.New(),
            Length.FromInches(24m),
            Length.FromInches(30m),
            CommandOrigin.User,
            "resize",
            DateTimeOffset.UnixEpoch);
        var context = CreateContext(command);

        var result = new InputCaptureStage(store).Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "CABINET_NOT_FOUND");
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
