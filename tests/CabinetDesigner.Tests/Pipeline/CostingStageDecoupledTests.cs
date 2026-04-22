using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class CostingStageDecoupledTests
{
    [Fact]
    public void Execute_NoPricingConfigured_ReturnsSuccessWithNotConfiguredStatus()
    {
        var stage = new CostingStage(new PricingCatalogStub(false), new TestCostingPolicy());
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(CostingStatus.NotConfigured, context.CostingResult.Status);
        Assert.Equal(0m, context.CostingResult.Total);
        Assert.Empty(context.CostingResult.CabinetBreakdowns);
        Assert.False(string.IsNullOrWhiteSpace(context.CostingResult.StatusReason));
    }

    [Fact]
    public void Execute_PricingConfigured_StillCalculates()
    {
        var catalog = new PricingCatalogStub(true);
        var stage = new CostingStage(catalog, new TestCostingPolicy(12.5m, 8.25m, 0.1m, 0.08m));
        var context = BuildContext(catalog);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(CostingStatus.Calculated, context.CostingResult.Status);
        Assert.True(context.CostingResult.MaterialCost > 0m);
        Assert.True(context.CostingResult.HardwareCost > 0m);
        Assert.True(context.CostingResult.LaborCost > 0m);
        Assert.True(context.CostingResult.InstallCost > 0m);
        Assert.True(context.CostingResult.Total > 0m);
        Assert.NotEmpty(context.CostingResult.CabinetBreakdowns);
    }

    private static ResolutionContext BuildContext(PricingCatalogStub catalog)
    {
        var cabinetId = CabinetId.New();
        var runId = RunId.New();
        var materialId = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var thickness = catalog.ResolvePartThickness("LeftSide", CabinetCategory.Base);
        var hardwareId = new HardwareItemId(Guid.Parse("55000000-0000-0000-0000-000000000001"));

        var part = new GeneratedPart
        {
            PartId = "part:1",
            CabinetId = cabinetId,
            PartType = "LeftSide",
            Width = Length.FromInches(24m),
            Height = Length.FromInches(30m),
            MaterialThickness = thickness,
            MaterialId = materialId,
            GrainDirection = GrainDirection.LengthWise,
            Edges = new EdgeTreatment(null, null, null, null),
            Label = "Cabinet A-LeftSide"
        };

        var cutListItem = new CutListItem
        {
            PartId = part.PartId,
            CabinetId = cabinetId,
            PartType = part.PartType,
            Label = part.Label,
            CutWidth = part.Width,
            CutHeight = part.Height,
            MaterialThickness = part.MaterialThickness,
            MaterialId = part.MaterialId,
            GrainDirection = part.GrainDirection,
            EdgeTreatment = new ManufacturedEdgeTreatment(null, null, null, null)
        };

        return new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full,
            SpatialResult = new SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries = [new RunSummary(runId, Length.FromInches(96m), Length.FromInches(24m), Length.FromInches(72m), 1)],
                Placements = [new RunPlacement(runId, cabinetId, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)), Length.FromInches(24m))]
            },
            ConstraintResult = new ConstraintPropagationResult
            {
                MaterialAssignments = [],
                HardwareAssignments = [new HardwareAssignment(CreateOpeningId(cabinetId, 0), [hardwareId], null)],
                Violations = []
            },
            PartResult = new PartGenerationResult { Parts = [part] },
            ManufacturingResult = new ManufacturingPlanResult
            {
                Plan = new ManufacturingPlan
                {
                    MaterialGroups = [],
                    CutList = [cutListItem],
                    Operations =
                    [
                        new ManufacturingOperation
                        {
                            PartId = part.PartId,
                            Sequence = 0,
                            Kind = ManufacturingOperationKind.SawCutRectangle,
                            Parameters = new Dictionary<string, OverrideValue>()
                        },
                        new ManufacturingOperation
                        {
                            PartId = part.PartId,
                            Sequence = 1,
                            Kind = ManufacturingOperationKind.ApplyEdgeBanding,
                            Parameters = new Dictionary<string, OverrideValue>()
                        }
                    ],
                    EdgeBandingRequirements = [],
                    Readiness = new ManufacturingReadinessResult
                    {
                        IsReady = true,
                        Blockers = []
                    }
                }
            },
            InstallResult = new InstallPlanResult
            {
                Plan = new InstallPlan
                {
                    Steps =
                    [
                        new InstallStep
                        {
                            StepKey = "install:1",
                            Order = 0,
                            Kind = InstallStepKind.CabinetInstall,
                            CabinetId = cabinetId,
                            RunId = runId,
                            SequenceGroupIndex = 0,
                            Footprint = new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)),
                            Description = "install:1",
                            DependsOn = [],
                            Rationales = []
                        }
                    ],
                    Dependencies = [],
                    FasteningRequirements = [],
                    Readiness = new InstallReadinessResult
                    {
                        IsReady = true,
                        Blockers = []
                    }
                }
            },
            CostingResult = new CostingResult
            {
                Status = CostingStatus.NotConfigured,
                StatusReason = "placeholder",
                MaterialCost = 0m,
                HardwareCost = 0m,
                LaborCost = 0m,
                InstallCost = 0m,
                Subtotal = 0m,
                Markup = 0m,
                Tax = 0m,
                Total = 0m,
                CabinetBreakdowns = []
            }
        };
    }

    private static OpeningId CreateOpeningId(CabinetId cabinetId, int ordinal)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"opening:{cabinetId.Value:D}:{ordinal}");
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return new OpeningId(new Guid(hashBytes.AsSpan(0, 16)));
    }

    private sealed class PricingCatalogStub(bool isConfigured) : ICatalogService
    {
        private readonly CatalogService _catalog = new();

        public bool IsPricingConfigured => isConfigured;

        public IReadOnlyList<CatalogItemDto> GetAllItems() => _catalog.GetAllItems();

        public MaterialId ResolvePartMaterial(string partType, CabinetCategory category, ConstructionMethod construction) =>
            _catalog.ResolvePartMaterial(partType, category, construction);

        public Thickness ResolvePartThickness(string partType, CabinetCategory category) =>
            _catalog.ResolvePartThickness(partType, category);

        public bool IsKnownMaterial(MaterialId id) => _catalog.IsKnownMaterial(id);

        public string GetMaterialDisplayName(MaterialId id) => _catalog.GetMaterialDisplayName(id);

        public Thickness ResolveMaterialThickness(MaterialId id) => _catalog.ResolveMaterialThickness(id);

        public GrainDirection ResolveMaterialGrain(MaterialId id) => _catalog.ResolveMaterialGrain(id);

        public IReadOnlyList<HardwareItemId> ResolveHardwareForOpening(OpeningId openingId, CabinetCategory category) =>
            _catalog.ResolveHardwareForOpening(openingId, category);

        public decimal GetMaterialPricePerSquareFoot(MaterialId id, Thickness thickness) =>
            IsPricingConfigured && _catalog.IsKnownMaterial(id)
                ? 5m
                : 0m;

        public decimal GetHardwarePrice(HardwareItemId id) =>
            IsPricingConfigured
                ? 9.5m
                : 0m;
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Costing Test", []);

        public string CommandType => "test.costing";

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


