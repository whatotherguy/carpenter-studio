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
        run.SetLeftEndCondition(EndConditionType.AgainstWall);
        run.SetRightEndCondition(EndConditionType.Open);

        var store = CreateStore([run], [CreateCabinet(cabinetId, runId, CabinetCategory.Base)]);
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
