using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using CabinetDesigner.Domain.Commands;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class CostingStageTests
{
    [Fact]
    public void Execute_MaterialCost_MatchesSumOfPartAreaTimesPrice()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var catalog = new CatalogService();
        var materialId = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var thickness = catalog.ResolvePartThickness("LeftSide", CabinetCategory.Base);
        var context = CreateContext(
            parts:
            [
                CreatePart(cabinetId, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness),
                CreatePart(cabinetId, "part:2", "RightSide", Length.FromInches(12m), Length.FromInches(30m), materialId, thickness)
            ],
            cutList:
            [
                CreateCutListItem(cabinetId, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness),
                CreateCutListItem(cabinetId, "part:2", "RightSide", Length.FromInches(12m), Length.FromInches(30m), materialId, thickness)
            ]);
        var stage = new CostingStage(catalog, new TestCostingPolicy());

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var expected = decimal.Round((((24m * 30m) / 144m) + ((12m * 30m) / 144m)) * catalog.GetMaterialPricePerSquareFoot(materialId, thickness), 2, MidpointRounding.ToEven);
        Assert.Equal(expected, context.CostingResult.MaterialCost);
        Assert.Equal(expected, context.CostingResult.Total);
    }

    [Fact]
    public void Execute_MissingMaterialPrice_FailsStage()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var materialId = new MaterialId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var thickness = Thickness.Exact(Length.FromInches(0.75m));
        var context = CreateContext(
            parts: [CreatePart(cabinetId, "part:1", "Panel", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness)],
            cutList: [CreateCutListItem(cabinetId, "part:1", "Panel", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness)]);
        var stage = new CostingStage(new CatalogService(), new TestCostingPolicy());

        var result = stage.Execute(context);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("COSTING_PRICE_MISSING", issue.Code);
    }

    [Fact]
    public void Execute_IsDeterministic_OnRepeatedRuns()
    {
        var cabinetA = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        var cabinetB = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000004"));
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000003"));
        var catalog = new CatalogService();
        var baseMaterial = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var baseThickness = catalog.ResolvePartThickness("LeftSide", CabinetCategory.Base);
        var hardwareId = catalog.ResolveHardwareForOpening(CreateOpeningId(cabinetA, 0), CabinetCategory.Base).First();
        var parts =
            new[]
            {
                CreatePart(cabinetB, "part:2", "RightSide", Length.FromInches(18m), Length.FromInches(30m), baseMaterial, baseThickness),
                CreatePart(cabinetA, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), baseMaterial, baseThickness)
            };
        var cutList =
            new[]
            {
                CreateCutListItem(cabinetB, "part:2", "RightSide", Length.FromInches(18m), Length.FromInches(30m), baseMaterial, baseThickness),
                CreateCutListItem(cabinetA, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), baseMaterial, baseThickness)
            };

        var firstContext = CreateContext(
            parts,
            cutList,
            operations:
            [
                CreateOperation("part:2", ManufacturingOperationKind.ApplyEdgeBanding, 1),
                CreateOperation("part:1", ManufacturingOperationKind.SawCutRectangle, 0)
            ],
            hardwareAssignments:
            [
                new HardwareAssignment(CreateOpeningId(cabinetA, 0), [hardwareId], null)
            ],
            steps:
            [
                CreateInstallStep("install:2", runId, cabinetB, 1),
                CreateInstallStep("install:1", runId, cabinetA, 0)
            ],
            placements:
            [
                new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(runId, cabinetB, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(18m), Length.FromInches(30m)), Length.FromInches(18m)),
                new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(runId, cabinetA, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)), Length.FromInches(24m))
            ]);
        var secondContext = CreateContext(
            parts.Reverse().ToArray(),
            cutList.Reverse().ToArray(),
            operations:
            [
                CreateOperation("part:1", ManufacturingOperationKind.SawCutRectangle, 0),
                CreateOperation("part:2", ManufacturingOperationKind.ApplyEdgeBanding, 1)
            ],
            hardwareAssignments:
            [
                new HardwareAssignment(CreateOpeningId(cabinetA, 0), [hardwareId], null)
            ],
            steps:
            [
                CreateInstallStep("install:1", runId, cabinetA, 0),
                CreateInstallStep("install:2", runId, cabinetB, 1)
            ],
            placements:
            [
                new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(runId, cabinetA, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)), Length.FromInches(24m)),
                new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(runId, cabinetB, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(18m), Length.FromInches(30m)), Length.FromInches(18m))
            ]);
        var stage = new CostingStage(catalog, new TestCostingPolicy(12.5m, 20m, 0.1m, 0.08m));

        var first = stage.Execute(firstContext);
        var second = stage.Execute(secondContext);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(firstContext.CostingResult.MaterialCost, secondContext.CostingResult.MaterialCost);
        Assert.Equal(firstContext.CostingResult.HardwareCost, secondContext.CostingResult.HardwareCost);
        Assert.Equal(firstContext.CostingResult.LaborCost, secondContext.CostingResult.LaborCost);
        Assert.Equal(firstContext.CostingResult.InstallCost, secondContext.CostingResult.InstallCost);
        Assert.Equal(firstContext.CostingResult.Subtotal, secondContext.CostingResult.Subtotal);
        Assert.Equal(firstContext.CostingResult.Markup, secondContext.CostingResult.Markup);
        Assert.Equal(firstContext.CostingResult.Tax, secondContext.CostingResult.Tax);
        Assert.Equal(firstContext.CostingResult.Total, secondContext.CostingResult.Total);
        Assert.Equal(
            firstContext.CostingResult.CabinetBreakdowns.Select(FormatBreakdown),
            secondContext.CostingResult.CabinetBreakdowns.Select(FormatBreakdown));
        var breakdownSubtotal = firstContext.CostingResult.CabinetBreakdowns.Sum(breakdown => breakdown.Subtotal);
        Assert.InRange(Math.Abs(breakdownSubtotal - firstContext.CostingResult.Subtotal), 0m, 0.01m);
    }

    [Fact]
    public void Execute_WithEmptyParts_Fails_WithBlocker()
    {
        var context = CreateContext(parts: [], cutList: []);
        var stage = new CostingStage(new CatalogService(), new TestCostingPolicy());

        var result = stage.Execute(context);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("COSTING_NO_PARTS", issue.Code);
    }

    [Fact]
    public void Execute_RevisionDelta_NullWhenNoPriorApprovedSnapshot()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000010"));
        var catalog = new CatalogService();
        var materialId = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var thickness = catalog.ResolvePartThickness("LeftSide", CabinetCategory.Base);
        var context = CreateContext(
            parts: [CreatePart(cabinetId, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness)],
            cutList: [CreateCutListItem(cabinetId, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness)]);
        var stage = new CostingStage(
            catalog,
            new TestCostingPolicy(),
            previousCostLookup: new StubPreviousCostLookup(null));

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Null(context.CostingResult.RevisionDelta);
    }

    [Fact]
    public void Execute_RevisionDelta_PopulatedWhenPriorApprovedSnapshotExists()
    {
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000011"));
        var catalog = new CatalogService();
        var materialId = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var thickness = catalog.ResolvePartThickness("LeftSide", CabinetCategory.Base);
        var context = CreateContext(
            parts: [CreatePart(cabinetId, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness)],
            cutList: [CreateCutListItem(cabinetId, "part:1", "LeftSide", Length.FromInches(24m), Length.FromInches(30m), materialId, thickness)]);
        var stage = new CostingStage(
            catalog,
            new TestCostingPolicy(),
            previousCostLookup: new StubPreviousCostLookup(100m));

        var result = stage.Execute(context);

        Assert.True(result.Success);
        var delta = Assert.IsType<CostDelta>(context.CostingResult.RevisionDelta);
        Assert.Equal(100m, delta.PreviousTotal);
        Assert.Equal(context.CostingResult.Total, delta.CurrentTotal);
        Assert.Equal(decimal.Round(context.CostingResult.Total - 100m, 2, MidpointRounding.ToEven), delta.Difference);
        Assert.False(string.IsNullOrWhiteSpace(delta.Summary));
    }

    private sealed class StubPreviousCostLookup(decimal? value) : IPreviousApprovedCostLookup
    {
        public decimal? GetMostRecentApprovedTotal() => value;
    }

    private static ResolutionContext CreateContext(
        IReadOnlyList<GeneratedPart> parts,
        IReadOnlyList<CutListItem> cutList,
        IReadOnlyList<ManufacturingOperation>? operations = null,
        IReadOnlyList<HardwareAssignment>? hardwareAssignments = null,
        IReadOnlyList<InstallStep>? steps = null,
        IReadOnlyList<CabinetDesigner.Application.Pipeline.StageResults.RunPlacement>? placements = null)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.PartResult = new PartGenerationResult
        {
            Parts = parts
        };
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = hardwareAssignments ?? [],
            Violations = []
        };
        context.ManufacturingResult = new ManufacturingPlanResult
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = cutList,
                Operations = operations ?? [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            }
        };
        context.InstallResult = new InstallPlanResult
        {
            Plan = new InstallPlan
            {
                Steps = steps ?? [],
                Dependencies = [],
                FasteningRequirements = [],
                Readiness = new InstallReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            }
        };
        context.SpatialResult = new CabinetDesigner.Application.Pipeline.StageResults.SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = placements ?? BuildPlacements(parts)
        };

        return context;
    }

    private static IReadOnlyList<CabinetDesigner.Application.Pipeline.StageResults.RunPlacement> BuildPlacements(IReadOnlyList<GeneratedPart> parts) =>
        parts
            .Select(part => part.CabinetId)
            .Distinct()
            .OrderBy(id => id.Value)
            .Select((cabinetId, index) => new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(
                new RunId(Guid.Parse($"20000000-0000-0000-0000-{(index + 1).ToString("D12")}")),
                cabinetId,
                Point2D.Origin,
                new Vector2D(1m, 0m),
                new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)),
                Length.FromInches(24m)))
            .ToArray();

    private static GeneratedPart CreatePart(
        CabinetId cabinetId,
        string partId,
        string partType,
        Length width,
        Length height,
        MaterialId materialId,
        Thickness thickness) =>
        new()
        {
            PartId = partId,
            CabinetId = cabinetId,
            PartType = partType,
            Width = width,
            Height = height,
            MaterialThickness = thickness,
            MaterialId = materialId,
            GrainDirection = GrainDirection.LengthWise,
            Edges = new EdgeTreatment(null, null, null, null),
            Label = $"{cabinetId}-{partType}"
        };

    private static CutListItem CreateCutListItem(
        CabinetId cabinetId,
        string partId,
        string partType,
        Length width,
        Length height,
        MaterialId materialId,
        Thickness thickness) =>
        new()
        {
            PartId = partId,
            CabinetId = cabinetId,
            PartType = partType,
            Label = $"{cabinetId}-{partType}",
            CutWidth = width,
            CutHeight = height,
            MaterialThickness = thickness,
            MaterialId = materialId,
            GrainDirection = GrainDirection.LengthWise,
            EdgeTreatment = new ManufacturedEdgeTreatment(null, null, null, null)
        };

    private static ManufacturingOperation CreateOperation(string partId, ManufacturingOperationKind kind, int sequence) =>
        new()
        {
            PartId = partId,
            Sequence = sequence,
            Kind = kind,
            Parameters = new Dictionary<string, OverrideValue>()
        };

    private static InstallStep CreateInstallStep(string stepKey, RunId runId, CabinetId? cabinetId, int order) =>
        new()
        {
            StepKey = stepKey,
            Order = order,
            Kind = InstallStepKind.CabinetInstall,
            CabinetId = cabinetId,
            RunId = runId,
            SequenceGroupIndex = 0,
            Footprint = new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)),
            Description = stepKey,
            DependsOn = [],
            Rationales = []
        };

    private static OpeningId CreateOpeningId(CabinetId cabinetId, int ordinal)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"opening:{cabinetId.Value:D}:{ordinal}");
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return new OpeningId(new Guid(hashBytes.AsSpan(0, 16)));
    }

    private static string FormatBreakdown(CabinetCostBreakdown breakdown) =>
        $"{breakdown.CabinetId}|{breakdown.MaterialCost}|{breakdown.HardwareCost}|{breakdown.LaborCost}|{breakdown.InstallCost}|{breakdown.Subtotal}";

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }

    private sealed class TestCostingPolicy(
        decimal sawCutRate = 0m,
        decimal installRatePerStep = 0m,
        decimal markupFraction = 0m,
        decimal taxFraction = 0m) : ICostingPolicy
    {
        public decimal GetLaborRate(ManufacturingOperationKind kind) =>
            kind switch
            {
                ManufacturingOperationKind.SawCutRectangle => sawCutRate,
                ManufacturingOperationKind.ApplyEdgeBanding => sawCutRate / 2m,
                _ => 0m
            };

        public decimal InstallRatePerStep => installRatePerStep;

        public decimal MarkupFraction => markupFraction;

        public decimal TaxFraction => taxFraction;
    }
}
