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
    public void Execute_WithInstallReadyUpstream_SetsInstallResultAndSucceeds()
    {
        var stage = new InstallPlanningStage();
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var context = CreateContext(
            CreateSpatialResult(new RunPlacement(
                runId,
                cabinetId,
                Point2D.Origin,
                Vector2D.UnitX,
                new Rect2D(Point2D.Origin, Length.FromInches(30m), Length.FromInches(24m)),
                Length.FromInches(30m))),
            CreateEngineeringResult(
                new AssemblyResolution(cabinetId, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new EndConditionUpdate(runId, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.AgainstWall())),
            CreateManufacturingResult(isReady: true, cabinetId));

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(context.InstallResult.Plan.Readiness.IsReady);
        Assert.Single(context.InstallResult.Plan.Steps);
        Assert.Single(context.InstallResult.Plan.FasteningRequirements);
        Assert.Contains("BaseCabinetAssembly", context.InstallResult.Plan.Steps[0].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_WithMissingEngineeringAndManufacturingData_ReturnsExplicitBlockers()
    {
        var stage = new InstallPlanningStage();
        var runId = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var cabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var context = CreateContext(
            CreateSpatialResult(new RunPlacement(
                runId,
                cabinetId,
                Point2D.Origin,
                Vector2D.UnitX,
                new Rect2D(Point2D.Origin, Length.FromInches(36m), Length.FromInches(24m)),
                Length.FromInches(36m))),
            CreateEngineeringResult(),
            CreateManufacturingResult(isReady: true));

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.False(context.InstallResult.Plan.Readiness.IsReady);
        Assert.Equal(
            new[]
            {
                "install.MissingCabinetManufacturingParts",
                "install.MissingEngineeringAssembly",
                "install.MissingManufacturingCutList",
                "install.MissingRunEndConditions"
            },
            result.Issues.Select(issue => issue.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray());
        Assert.Contains(
            context.InstallResult.Plan.Readiness.Blockers,
            blocker => blocker.Message.Contains("safe install sequence", StringComparison.Ordinal));
        Assert.Empty(context.InstallResult.Plan.Steps);
    }

    [Fact]
    public void Execute_WithEquivalentInputs_ProducesDeterministicOrderedPlan()
    {
        var stage = new InstallPlanningStage();
        var runNorth = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000010"));
        var runSouth = new RunId(Guid.Parse("20000000-0000-0000-0000-000000000011"));
        var cabinetNorth = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000010"));
        var cabinetSouthA = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000011"));
        var cabinetSouthB = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000012"));
        var spatial = CreateSpatialResult(
            new RunPlacement(
                runNorth,
                cabinetNorth,
                new Point2D(0m, 40m),
                Vector2D.UnitX,
                new Rect2D(new Point2D(0m, 40m), Length.FromInches(30m), Length.FromInches(24m)),
                Length.FromInches(30m)),
            new RunPlacement(
                runSouth,
                cabinetSouthB,
                new Point2D(40m, 0m),
                Vector2D.UnitX,
                new Rect2D(new Point2D(40m, 0m), Length.FromInches(30m), Length.FromInches(24m)),
                Length.FromInches(30m)),
            new RunPlacement(
                runSouth,
                cabinetSouthA,
                Point2D.Origin,
                Vector2D.UnitX,
                new Rect2D(Point2D.Origin, Length.FromInches(30m), Length.FromInches(24m)),
                Length.FromInches(30m)));
        var engineering = CreateEngineeringResult(
            new AssemblyResolution(cabinetNorth, "WallCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
            new AssemblyResolution(cabinetSouthA, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
            new AssemblyResolution(cabinetSouthB, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
            new EndConditionUpdate(runNorth, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.Open()),
            new EndConditionUpdate(runSouth, CabinetDesigner.Domain.RunContext.EndCondition.AgainstWall(), CabinetDesigner.Domain.RunContext.EndCondition.Open()),
            new FillerRequirement(runSouth, Length.FromInches(2m), "scribe gap"));
        var manufacturing = CreateManufacturingResult(isReady: true, cabinetSouthA, cabinetSouthB, cabinetNorth);

        var firstContext = CreateContext(spatial, engineering, manufacturing);
        var secondContext = CreateContext(spatial, engineering, manufacturing);

        var first = stage.Execute(firstContext);
        var second = stage.Execute(secondContext);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            firstContext.InstallResult.Plan.Steps.Select(step => $"{step.Order}:{step.StepKey}:{string.Join(",", step.DependsOn)}").ToArray(),
            secondContext.InstallResult.Plan.Steps.Select(step => $"{step.Order}:{step.StepKey}:{string.Join(",", step.DependsOn)}").ToArray());
        Assert.Equal(
            new[]
            {
                $"cabinet:{cabinetSouthA.Value:D}",
                $"cabinet:{cabinetSouthB.Value:D}",
                $"filler:{runSouth.Value:D}:0",
                $"cabinet:{cabinetNorth.Value:D}"
            },
            firstContext.InstallResult.Plan.Steps.OrderBy(step => step.Order).Select(step => step.StepKey).ToArray());
    }

    private static ResolutionContext CreateContext(
        SpatialResolutionResult spatialResult,
        EngineeringResolutionResult engineeringResult,
        ManufacturingPlanResult manufacturingResult)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.SpatialResult = spatialResult;
        context.EngineeringResult = engineeringResult;
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

    private static EngineeringResolutionResult CreateEngineeringResult(
        params object[] items)
    {
        var assemblies = items.OfType<AssemblyResolution>().ToArray();
        var fillers = items.OfType<FillerRequirement>().ToArray();
        var endConditions = items.OfType<EndConditionUpdate>().ToArray();

        return new EngineeringResolutionResult
        {
            Assemblies = assemblies,
            FillerRequirements = fillers,
            EndConditionUpdates = endConditions
        };
    }

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
