using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class InstallPlanningStageTests
{
    [Fact]
    public void Execute_WithReadyManufacturingPlan_SetsInstallResultAndSucceeds()
    {
        var stage = new InstallPlanningStage();
        var runId = RunId.New();
        var cabinetId = CabinetId.New();
        var context = CreateContext(
            CreateSpatialResult(new RunPlacement(
                runId,
                cabinetId,
                Point2D.Origin,
                Vector2D.UnitX,
                new Rect2D(Point2D.Origin, Length.FromInches(30m), Length.FromInches(24m)),
                Length.FromInches(30m))),
            CreateManufacturingResult(isReady: true, cabinetId));

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(context.InstallResult.Plan.Readiness.IsReady);
        Assert.Single(context.InstallResult.Plan.Steps);
        Assert.Single(context.InstallResult.Plan.FasteningRequirements);
    }

    [Fact]
    public void Execute_WithManufacturingBlocker_ReturnsFailed()
    {
        var stage = new InstallPlanningStage();
        var context = CreateContext(
            CreateSpatialResult(),
            CreateManufacturingResult(isReady: false));

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.False(context.InstallResult.Plan.Readiness.IsReady);
        Assert.Equal("install.ManufacturingNotReady", Assert.Single(result.Issues).Code);
    }

    private static ResolutionContext CreateContext(
        SpatialResolutionResult spatialResult,
        ManufacturingPlanResult manufacturingResult)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.SpatialResult = spatialResult;
        context.EngineeringResult = new EngineeringResolutionResult
        {
            Assemblies = [],
            FillerRequirements = [],
            EndConditionUpdates = []
        };
        context.ManufacturingResult = manufacturingResult;

        return context;
    }

    private static SpatialResolutionResult CreateSpatialResult(params RunPlacement[] placements) =>
        new()
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = placements
        };

    private static ManufacturingPlanResult CreateManufacturingResult(bool isReady, params CabinetId[] cabinetIds) =>
        new()
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = cabinetIds.Select((cabinetId, index) => new CutListItem
                {
                    PartId = $"part-{index}",
                    CabinetId = cabinetId,
                    PartType = "panel",
                    Label = $"Panel {index}",
                    CutWidth = Length.FromInches(20m),
                    CutHeight = Length.FromInches(30m),
                    MaterialThickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.71m)),
                    MaterialId = MaterialId.New(),
                    GrainDirection = GrainDirection.None,
                    EdgeTreatment = new ManufacturedEdgeTreatment(null, null, null, null)
                }).ToArray(),
                Operations = [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = isReady,
                    Blockers = isReady
                        ? []
                        :
                        [
                            new ManufacturingBlocker
                            {
                                Code = ManufacturingBlockerCode.PartTooSmall,
                                Message = "Manufacturing blocked.",
                                AffectedEntityIds = ["part-0"]
                            }
                        ]
                }
            }
        };

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Install Stage Test", []);

        public string CommandType => "test.install_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
