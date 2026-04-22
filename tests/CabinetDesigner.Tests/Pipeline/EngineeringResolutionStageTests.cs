using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class EngineeringResolutionStageTests
{
    private static readonly WallId TestWallId = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static readonly RoomId TestRoomId = new(Guid.Parse("90000000-0000-0000-0000-000000000001"));
    private static readonly Thickness TestWallThickness = Thickness.Exact(Length.FromInches(5.5m));

    [Fact]
    public void Execute_ProducesOneAssemblyPerCabinet()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var cabinetId1 = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var cabinetId2 = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000002"));

        var run = CreateRun(runId, 120m);
        run.AppendCabinet(cabinetId1, Length.FromInches(30m));
        run.AppendCabinet(cabinetId2, Length.FromInches(36m));

        var store = CreateStore(
            [run],
            [
                CreateCabinet(cabinetId1, runId, CabinetCategory.Base),
                CreateCabinet(cabinetId2, runId, CabinetCategory.Wall)
            ]);

        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(2, context.EngineeringResult.Assemblies.Count);
        Assert.Equal("BaseCabinetAssembly", context.EngineeringResult.Assemblies
            .Single(a => a.CabinetId == cabinetId1).AssemblyType);
        Assert.Equal("WallCabinetAssembly", context.EngineeringResult.Assemblies
            .Single(a => a.CabinetId == cabinetId2).AssemblyType);
    }

    [Fact]
    public void Execute_EmitsFiller_WhenRunEndGapExceedsTolerance()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000003"));

        var run = CreateRun(runId, 120m);
        run.AppendCabinet(cabinetId, Length.FromInches(30m));

        var store = CreateStore([run], [CreateCabinet(cabinetId, runId, CabinetCategory.Base)]);
        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();

        stage.Execute(context);

        var filler = Assert.Single(context.EngineeringResult.FillerRequirements);
        Assert.Equal(runId, filler.RunId);
        Assert.Equal(Length.FromInches(90m), filler.Width);
    }

    [Fact]
    public void Execute_NoFiller_WhenGapWithinTolerance()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000003"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000004"));

        var run = CreateRun(runId, 30.1m);
        run.AppendCabinet(cabinetId, Length.FromInches(30m));

        var store = CreateStore([run], [CreateCabinet(cabinetId, runId, CabinetCategory.Base)]);
        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();

        stage.Execute(context);

        Assert.Empty(context.EngineeringResult.FillerRequirements);
    }

    [Fact]
    public void Execute_EndConditions_DerivedFromWallAttachment()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000004"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000005"));

        var run = CreateRun(runId, 120m);
        run.AppendCabinet(cabinetId, Length.FromInches(30m));

        // Wall whose start endpoint coincides with the run's world start (0,0)
        var wall = new Wall(TestWallId, TestRoomId, Point2D.Origin, new Point2D(200m, 0m), TestWallThickness);

        var store = CreateStore([run], [CreateCabinet(cabinetId, runId, CabinetCategory.Base)]);
        store.AddWall(wall);
        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();

        stage.Execute(context);

        var update = Assert.Single(context.EngineeringResult.EndConditionUpdates);
        Assert.Equal(runId, update.RunId);
        Assert.Equal(EndConditionType.AgainstWall, update.LeftEndCondition.Type);
        Assert.Equal(EndConditionType.Open, update.RightEndCondition.Type);
    }

    [Fact]
    public void Execute_IsDeterministic()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000005"));
        var cabinetId1 = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000006"));
        var cabinetId2 = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000007"));

        var run = CreateRun(runId, 120m);
        run.AppendCabinet(cabinetId1, Length.FromInches(30m));
        run.AppendCabinet(cabinetId2, Length.FromInches(36m));

        var store = CreateStore(
            [run],
            [
                CreateCabinet(cabinetId1, runId, CabinetCategory.Base),
                CreateCabinet(cabinetId2, runId, CabinetCategory.Wall)
            ]);
        var stage = new EngineeringResolutionStage(store);

        var firstContext = CreateContext();
        var secondContext = CreateContext();
        stage.Execute(firstContext);
        stage.Execute(secondContext);

        Assert.Equal(
            firstContext.EngineeringResult.Assemblies.Select(FormatAssembly),
            secondContext.EngineeringResult.Assemblies.Select(FormatAssembly));
        Assert.Equal(firstContext.EngineeringResult.FillerRequirements, secondContext.EngineeringResult.FillerRequirements);
        Assert.Equal(firstContext.EngineeringResult.EndConditionUpdates, secondContext.EngineeringResult.EndConditionUpdates);
    }

    [Fact]
    public void Execute_ProducesOneAssemblyPerCabinet_InStableOrder()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000040"));
        var cabinetIdLow = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var cabinetIdHigh = new CabinetId(Guid.Parse("10000000-0000-0000-FFFF-000000000001"));

        var run = CreateRun(runId, 120m);
        run.AppendCabinet(cabinetIdHigh, Length.FromInches(30m));
        run.AppendCabinet(cabinetIdLow, Length.FromInches(30m));

        var store = CreateStore(
            [run],
            [
                CreateCabinet(cabinetIdHigh, runId, CabinetCategory.Base),
                CreateCabinet(cabinetIdLow, runId, CabinetCategory.Wall)
            ]);

        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();
        stage.Execute(context);

        Assert.Equal(2, context.EngineeringResult.Assemblies.Count);
        Assert.True(
            context.EngineeringResult.Assemblies[0].CabinetId.Value.CompareTo(
                context.EngineeringResult.Assemblies[1].CabinetId.Value) < 0,
            "Assemblies must be in ascending CabinetId order regardless of insertion order.");
        var assembly = context.EngineeringResult.Assemblies[0];
        Assert.True(assembly.ResolvedParameters.ContainsKey("toe_kick"));
        Assert.True(assembly.ResolvedParameters.ContainsKey("shelf_count"));
        Assert.True(assembly.ResolvedParameters.ContainsKey("door_count"));
        Assert.True(assembly.ResolvedParameters.ContainsKey("drawer_count"));
    }

    [Fact]
    public void Execute_EndConditions_WallMetadataPresent_EmitsWall()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000020"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000020"));
        var wallId = new WallId(Guid.Parse("40000000-0000-0000-0000-000000000020"));

        var run = new CabinetRun(runId, wallId, Length.FromInches(30m));
        run.AppendCabinet(cabinetId, Length.FromInches(30m));

        var wall = new Wall(wallId, TestRoomId, Point2D.Origin, new Point2D(200m, 0m), TestWallThickness);

        var store = new InMemoryDesignStateStore();
        store.AddRun(run, Point2D.Origin, new Point2D(30m, 0m));
        store.AddCabinet(CreateCabinet(cabinetId, runId, CabinetCategory.Base));
        store.AddWall(wall);

        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();
        stage.Execute(context);

        var update = Assert.Single(context.EngineeringResult.EndConditionUpdates);
        Assert.Equal(EndConditionType.AgainstWall, update.LeftEndCondition.Type);
        Assert.Equal(EndConditionType.Open, update.RightEndCondition.Type);
    }

    [Fact]
    public void Execute_EndConditions_AdjacentRun_EmitsAdjacentCabinet()
    {
        var run1Id = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000030"));
        var run2Id = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000031"));
        var cabinet1Id = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000030"));
        var cabinet2Id = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000031"));

        var run1 = new CabinetRun(run1Id, TestWallId, Length.FromInches(30m));
        run1.AppendCabinet(cabinet1Id, Length.FromInches(30m));

        var run2 = new CabinetRun(run2Id, TestWallId, Length.FromInches(60m));
        run2.AppendCabinet(cabinet2Id, Length.FromInches(30m));

        var store = new InMemoryDesignStateStore();
        store.AddRun(run1, Point2D.Origin, new Point2D(30m, 0m));
        store.AddRun(run2, new Point2D(30m, 0m), new Point2D(90m, 0m));
        store.AddCabinet(CreateCabinet(cabinet1Id, run1Id, CabinetCategory.Base));
        store.AddCabinet(CreateCabinet(cabinet2Id, run2Id, CabinetCategory.Base));

        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();
        stage.Execute(context);

        var run2Update = context.EngineeringResult.EndConditionUpdates.Single(u => u.RunId == run2Id);
        Assert.Equal(EndConditionType.AdjacentCabinet, run2Update.LeftEndCondition.Type);
        Assert.Equal(EndConditionType.Open, run2Update.RightEndCondition.Type);
    }

    [Fact]
    public void Execute_EndConditions_NoAttachment_EmitsOpenEnd()
    {
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000021"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000021"));
        var wallId = new WallId(Guid.Parse("40000000-0000-0000-0000-000000000021"));

        var run = new CabinetRun(runId, wallId, Length.FromInches(30m));
        run.AppendCabinet(cabinetId, Length.FromInches(30m));

        var wall = new Wall(wallId, TestRoomId, new Point2D(50m, 0m), new Point2D(150m, 0m), TestWallThickness);

        var store = new InMemoryDesignStateStore();
        store.AddRun(run, Point2D.Origin, new Point2D(30m, 0m));
        store.AddCabinet(CreateCabinet(cabinetId, runId, CabinetCategory.Base));
        store.AddWall(wall);

        var stage = new EngineeringResolutionStage(store);
        var context = CreateContext();
        stage.Execute(context);

        var update = Assert.Single(context.EngineeringResult.EndConditionUpdates);
        Assert.Equal(EndConditionType.Open, update.LeftEndCondition.Type);
        Assert.Equal(EndConditionType.Open, update.RightEndCondition.Type);
    }

    private static CabinetRun CreateRun(RunId runId, decimal capacityInches) =>
        new(runId, TestWallId, Length.FromInches(capacityInches));

    private static CabinetStateRecord CreateCabinet(CabinetId cabinetId, RunId runId, CabinetCategory category) =>
        new(
            cabinetId,
            cabinetId.Value.ToString("N")[..8],
            Length.FromInches(30m),
            Length.FromInches(24m),
            runId,
            RunSlotId.New(),
            category,
            ConstructionMethod.Frameless,
            Length.FromInches(category == CabinetCategory.Wall ? 30m : 34.5m));

    private static InMemoryDesignStateStore CreateStore(
        IReadOnlyList<CabinetRun> runs,
        IReadOnlyList<CabinetStateRecord> cabinets)
    {
        var store = new InMemoryDesignStateStore();
        foreach (var run in runs)
        {
            store.AddRun(run, Point2D.Origin, new Point2D(run.Capacity.Inches, 0m));
        }

        foreach (var cabinet in cabinets)
        {
            store.AddCabinet(cabinet);
        }

        return store;
    }

    private static ResolutionContext CreateContext()
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };
        context.SpatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = []
        };
        return context;
    }

    private static string FormatAssembly(AssemblyResolution a) =>
        $"{a.CabinetId}|{a.AssemblyType}|{string.Join(",", a.ResolvedParameters.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"))}";

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Engineering Resolution Stage Test", []);

        public string CommandType => "test.engineering_resolution_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
