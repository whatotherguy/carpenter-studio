using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;
using CabinetDesigner.Domain.RunContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ManufacturingPlanningStageTests
{
    [Fact]
    public void Execute_WithCompleteValidUpstreamData_SetsManufacturingResultAndSucceeds()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000101"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000101"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(cabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m)
        ]);
        var context = CreateContext([CreatePlacement(cabinetId, runId)]);
        var partStage = new PartGenerationStage(store);
        var constraintStage = new ConstraintPropagationStage(new CatalogService(), store);
        var stage = new ManufacturingPlanningStage();

        Assert.True(partStage.Execute(context).Success);
        Assert.True(constraintStage.Execute(context).Success);
        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(context.ManufacturingResult.Plan.Readiness.IsReady);
        Assert.NotEmpty(context.ManufacturingResult.Plan.CutList);
        Assert.NotEmpty(context.ManufacturingResult.Plan.MaterialGroups);
        Assert.All(context.ManufacturingResult.Plan.CutList, item => Assert.NotEqual(default, item.MaterialId));
    }

    [Fact]
    public void Execute_WhenPartGenerationProducesNoParts_BlocksEndToEndFromB2()
    {
        var partStage = new PartGenerationStage(new InMemoryDesignStateStore());
        var context = CreateContext([]);
        var result = partStage.Execute(context);

        Assert.False(result.Success);
        Assert.Equal("PART_GEN_EMPTY", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Execute_WhenConstraintPropagationCannotResolveMaterial_BlocksEndToEndFromB3()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000102"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000102"));
        var unknownMaterial = new MaterialId(Guid.Parse("90000000-0000-0000-0000-000000000001"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(
                cabinetId,
                runId,
                "base-30",
                CabinetCategory.Base,
                ConstructionMethod.Frameless,
                30m,
                24m,
                34.5m,
                new Dictionary<string, OverrideValue>
                {
                    ["material.LeftSide"] = new OverrideValue.OfMaterialId(unknownMaterial)
                })
        ]);
        var context = CreateContext([CreatePlacement(cabinetId, runId)]);
        var partStage = new PartGenerationStage(store);
        var constraintStage = new ConstraintPropagationStage(new CatalogService(), store);

        Assert.True(partStage.Execute(context).Success);
        var result = constraintStage.Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "MATERIAL_UNRESOLVED" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Execute_WithInvalidDimensions_ReturnsFailed()
    {
        var stage = new ManufacturingPlanningStage();
        var context = CreateContext(new PartGenerationResult
        {
            Parts =
            [
                new GeneratedPart
                {
                    PartId = "part-1",
                    CabinetId = CabinetId.New(),
                    PartType = "panel",
                    Width = Length.Zero,
                    Height = Length.FromInches(24m),
                    MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
                    MaterialId = MaterialId.New(),
                    GrainDirection = GrainDirection.None,
                    Edges = new EdgeTreatment(null, null, null, null),
                    Label = "Impossible Panel"
                }
            ]
        });

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "manufacturing.InvalidDimensions");
        Assert.False(context.ManufacturingResult.Plan.Readiness.IsReady);
    }

    [Fact]
    public void Execute_CutListOrderIsDeterministicAcrossRepeatedRuns()
    {
        var firstCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000103"));
        var secondCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000104"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000103"));
        var store = CreateStore([
            CreateRun(runId),
            CreateCabinet(firstCabinetId, runId, "base-30", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m),
            CreateCabinet(secondCabinetId, runId, "wall-30", CabinetCategory.Wall, ConstructionMethod.Frameless, 30m, 12m, 30m)
        ]);

        var firstContext = CreateContext([CreatePlacement(firstCabinetId, runId), CreatePlacement(secondCabinetId, runId)]);
        var secondContext = CreateContext([CreatePlacement(firstCabinetId, runId), CreatePlacement(secondCabinetId, runId)]);

        var firstCutList = ExecuteManufacturingPipeline(store, firstContext);
        var secondCutList = ExecuteManufacturingPipeline(store, secondContext);

        Assert.Equal(firstCutList, secondCutList);
    }

    private static string[] ExecuteManufacturingPipeline(InMemoryDesignStateStore store, ResolutionContext context)
    {
        var partStage = new PartGenerationStage(store);
        var constraintStage = new ConstraintPropagationStage(new CatalogService(), store);
        var manufacturingStage = new ManufacturingPlanningStage();

        Assert.True(partStage.Execute(context).Success);
        Assert.True(constraintStage.Execute(context).Success);
        Assert.True(manufacturingStage.Execute(context).Success);

        return context.ManufacturingResult.Plan.CutList
            .Select(item => $"{item.MaterialId.Value:D}|{item.MaterialThickness.Nominal.Inches}|{item.MaterialThickness.Actual.Inches}|{item.GrainDirection}|{item.CabinetId.Value:D}|{item.PartType}|{item.Label}|{item.PartId}")
            .ToArray();
    }

    private static InMemoryDesignStateStore CreateStore(object[] entities)
    {
        var store = new InMemoryDesignStateStore();
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CabinetRun run:
                    store.AddRun(run, Point2D.Origin, new Point2D(run.Capacity.Inches, 0m));
                    break;
                case CabinetStateRecord cabinet:
                    store.AddCabinet(cabinet);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported test entity {entity.GetType().Name}.");
            }
        }

        return store;
    }

    private static CabinetRun CreateRun(RunId runId) =>
        new(runId, new WallId(Guid.Parse("40000000-0000-0000-0000-000000000001")), Length.FromInches(120m));

    private static CabinetStateRecord CreateCabinet(
        CabinetId cabinetId,
        RunId runId,
        string cabinetTypeId,
        CabinetCategory category,
        ConstructionMethod construction,
        decimal widthInches,
        decimal depthInches,
        decimal heightInches,
        IReadOnlyDictionary<string, OverrideValue>? overrides = null) =>
        new(
            cabinetId,
            cabinetTypeId,
            Length.FromInches(widthInches),
            Length.FromInches(depthInches),
            runId,
            new RunSlotId(Guid.Parse($"50000000-0000-0000-0000-{cabinetId.Value.ToString("N")[20..32]}")),
            category,
            construction,
            Length.FromInches(heightInches),
            overrides);

    private static RunPlacement CreatePlacement(CabinetId cabinetId, RunId runId) =>
        new(
            runId,
            cabinetId,
            Point2D.Origin,
            new Vector2D(1m, 0m),
            new Rect2D(Point2D.Origin, Length.FromInches(1m), Length.FromInches(1m)),
            Length.FromInches(24m));

    private static ResolutionContext CreateContext(IReadOnlyList<RunPlacement> placements)
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
            Placements = placements
        };

        context.PartResult = new PartGenerationResult
        {
            Parts = []
        };
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };

        return context;
    }

    private static ResolutionContext CreateContext(PartGenerationResult partResult)
    {
        var context = CreateContext([]);
        context.PartResult = partResult;
        return context;
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Manufacturing Stage Test", []);

        public string CommandType => "test.manufacturing_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
