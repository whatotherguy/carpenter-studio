using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Projection;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.Projection;

public sealed class InstallProjectorTests
{
    private readonly InstallProjector _projector = new();

    [Fact]
    public void Project_SequenceGeneration_OrdersCabinetsByRunPosition()
    {
        var runId = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000101"));
        var cabinetA = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000201"));
        var cabinetB = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000202"));
        var cabinetC = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000203"));

        var plan = _projector.Project(
            CreateSpatialResult(
                (runId, cabinetB, new Point2D(36m, 0m)),
                (runId, cabinetC, new Point2D(72m, 0m)),
                (runId, cabinetA, new Point2D(0m, 0m))),
            CreateEngineeringResult(
                new AssemblyResolution(cabinetA, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new AssemblyResolution(cabinetB, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new AssemblyResolution(cabinetC, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new EndConditionUpdate(runId, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.Open())),
            CreateManufacturingResult(cabinetA, cabinetB, cabinetC));

        Assert.True(plan.Readiness.IsReady);
        Assert.Equal(
            new[] { cabinetA, cabinetB, cabinetC },
            plan.Steps
                .Where(step => step.CabinetId is not null)
                .OrderBy(step => step.Order)
                .Select(step => step.CabinetId!.Value)
                .ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, plan.Steps.Select(step => step.Order).ToArray());
    }

    [Fact]
    public void Project_DependencyOrdering_FillerDependsOnCabinetsInRun()
    {
        var runId = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000102"));
        var cabinetA = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000204"));
        var cabinetB = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000205"));

        var plan = _projector.Project(
            CreateSpatialResult(
                (runId, cabinetA, new Point2D(0m, 0m)),
                (runId, cabinetB, new Point2D(36m, 0m))),
            CreateEngineeringResult(
                new AssemblyResolution(cabinetA, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new AssemblyResolution(cabinetB, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new EndConditionUpdate(runId, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.Open()),
                new FillerRequirement(runId, Length.FromInches(3m), "scribe gap")),
            CreateManufacturingResult(cabinetA, cabinetB));

        var filler = Assert.Single(plan.Steps.Where(step => step.Kind == InstallStepKind.FillerInstall));
        Assert.Equal(
            new[] { CreateCabinetStepKey(cabinetA), CreateCabinetStepKey(cabinetB) },
            filler.DependsOn);
        Assert.True(
            plan.Steps.Single(step => step.StepKey == CreateCabinetStepKey(cabinetA)).Order < filler.Order);
        Assert.True(
            plan.Steps.Single(step => step.StepKey == CreateCabinetStepKey(cabinetB)).Order < filler.Order);
    }

    [Fact]
    public void Project_DeterministicOutput_ProducesStableSequenceAndDependencies()
    {
        var runA = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var runB = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var cabinet1 = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000011"));
        var cabinet2 = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000012"));
        var cabinet3 = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000013"));

        var spatial = CreateSpatialResult(
            (runB, cabinet3, new Point2D(10m, 20m)),
            (runA, cabinet2, new Point2D(40m, 0m)),
            (runA, cabinet1, new Point2D(0m, 0m)));
        var engineering = CreateEngineeringResult(
            new AssemblyResolution(cabinet1, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
            new AssemblyResolution(cabinet2, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
            new AssemblyResolution(cabinet3, "WallCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
            new EndConditionUpdate(runA, CabinetDesigner.Domain.RunContext.EndCondition.AgainstWall(), CabinetDesigner.Domain.RunContext.EndCondition.Open()),
            new EndConditionUpdate(runB, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.Open()),
            new FillerRequirement(runA, Length.FromInches(2m), "left shim"),
            new FillerRequirement(runA, Length.FromInches(4m), "right shim"));
        var manufacturing = CreateManufacturingResult(cabinet1, cabinet2, cabinet3);

        var first = _projector.Project(spatial, engineering, manufacturing);
        var second = _projector.Project(spatial, engineering, manufacturing);

        Assert.Equal(
            first.Steps.Select(step => $"{step.Order}:{step.StepKey}:{string.Join(",", step.DependsOn)}").ToArray(),
            second.Steps.Select(step => $"{step.Order}:{step.StepKey}:{string.Join(",", step.DependsOn)}").ToArray());
        Assert.Equal(
            first.Dependencies.Select(dependency => $"{dependency.PrerequisiteStepKey}>{dependency.DependentStepKey}:{dependency.Reason}").ToArray(),
            second.Dependencies.Select(dependency => $"{dependency.PrerequisiteStepKey}>{dependency.DependentStepKey}:{dependency.Reason}").ToArray());
        Assert.Equal(
            first.FasteningRequirements.Select(requirement => $"{requirement.CabinetId.Value:D}:{requirement.Requirements}").ToArray(),
            second.FasteningRequirements.Select(requirement => $"{requirement.CabinetId.Value:D}:{requirement.Requirements}").ToArray());
    }

    [Fact]
    public void Project_WithMissingEngineeringAndManufacturingEvidence_EmitsExplicitBlockers()
    {
        var runId = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000103"));
        var cabinetId = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000206"));

        var plan = _projector.Project(
            CreateSpatialResult((runId, cabinetId, new Point2D(0m, 0m))),
            CreateEngineeringResult(),
            CreateManufacturingResult());

        Assert.False(plan.Readiness.IsReady);
        Assert.Equal(
            new[]
            {
                InstallBlockerCode.MissingEngineeringAssembly,
                InstallBlockerCode.MissingRunEndConditions,
                InstallBlockerCode.MissingManufacturingCutList,
                InstallBlockerCode.MissingCabinetManufacturingParts
            },
            plan.Readiness.Blockers.Select(blocker => blocker.Code).ToArray());
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void Project_OrdersIndependentRunsByPhysicalLocationBeforeIdentifier()
    {
        var runNorth = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000200"));
        var runSouth = new RunId(Guid.Parse("00000000-0000-0000-0000-000000000100"));
        var cabinetNorth = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000207"));
        var cabinetSouth = new CabinetId(Guid.Parse("00000000-0000-0000-0000-000000000208"));

        var plan = _projector.Project(
            CreateSpatialResult(
                (runNorth, cabinetNorth, new Point2D(0m, 40m)),
                (runSouth, cabinetSouth, new Point2D(0m, 0m))),
            CreateEngineeringResult(
                new AssemblyResolution(cabinetNorth, "WallCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new AssemblyResolution(cabinetSouth, "BaseCabinetAssembly", new Dictionary<string, string>(StringComparer.Ordinal)),
                new EndConditionUpdate(runNorth, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.Open()),
                new EndConditionUpdate(runSouth, CabinetDesigner.Domain.RunContext.EndCondition.Open(), CabinetDesigner.Domain.RunContext.EndCondition.Open())),
            CreateManufacturingResult(cabinetNorth, cabinetSouth));

        Assert.Equal(
            new[] { cabinetSouth, cabinetNorth },
            plan.Steps.OrderBy(step => step.Order).Select(step => step.CabinetId!.Value).ToArray());
    }

    private static SpatialResolutionResult CreateSpatialResult(
        params (RunId RunId, CabinetId CabinetId, Point2D Origin)[] placements)
    {
        return new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = placements
                .Select(item => item.RunId)
                .Distinct()
                .OrderBy(runId => runId.Value)
                .Select(runId => new RunSummary(
                    runId,
                    Length.FromInches(120m),
                    Length.FromInches(36m),
                    Length.FromInches(84m),
                    placements.Count(item => item.RunId == runId)))
                .ToArray(),
            Placements = placements
                .Select(item => new RunPlacement(
                    item.RunId,
                    item.CabinetId,
                    item.Origin,
                    Vector2D.UnitX,
                    new Rect2D(item.Origin, Length.FromInches(30m), Length.FromInches(24m)),
                    Length.FromInches(30m)))
                .ToArray()
        };
    }

    private static EngineeringResolutionResult CreateEngineeringResult(params object[] items) =>
        new()
        {
            Assemblies = items.OfType<AssemblyResolution>().ToArray(),
            FillerRequirements = items.OfType<FillerRequirement>().ToArray(),
            EndConditionUpdates = items.OfType<EndConditionUpdate>().ToArray()
        };

    private static ManufacturingPlanResult CreateManufacturingResult(params CabinetId[] cabinetIds) =>
        new()
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = cabinetIds
                    .OrderBy(id => id.Value)
                    .Select((cabinetId, index) => new CutListItem
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
                    })
                    .ToArray(),
                Operations = [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            }
        };

    private static string CreateCabinetStepKey(CabinetId cabinetId) => $"cabinet:{cabinetId.Value:D}";
}
