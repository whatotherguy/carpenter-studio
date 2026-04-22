using CabinetDesigner.Application.Pipeline.Parts;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class EngineeringResolutionStage : IResolutionStage
{
    private static readonly Length FillerTolerance = Length.FromInches(0.125m);
    private static readonly Length EndpointMatchTolerance = Length.FromInches(0.001m);

    private readonly IDesignStateStore _stateStore;

    public EngineeringResolutionStage(IDesignStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int StageNumber => 4;

    public string StageName => "Engineering Resolution";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        var assemblies = new List<AssemblyResolution>();
        var fillerRequirements = new List<FillerRequirement>();
        var endConditionUpdates = new List<EndConditionUpdate>();

        var allRuns = _stateStore.GetAllRuns().OrderBy(r => r.Id.Value).ToArray();

        foreach (var run in allRuns)
        {
            foreach (var slot in run.Slots.Where(s => s.SlotType == RunSlotType.Cabinet).OrderBy(s => s.SlotIndex))
            {
                var cabinetId = slot.CabinetId!.Value;
                var cabinet = _stateStore.GetCabinet(cabinetId);
                if (cabinet is null)
                {
                    continue;
                }

                var assemblyType = $"{cabinet.Category}CabinetAssembly";
                var parameters = BuildAssemblyParameters(cabinet);
                assemblies.Add(new AssemblyResolution(cabinetId, assemblyType, parameters));
            }

            var gap = run.RemainingLength;
            if (gap > FillerTolerance)
            {
                fillerRequirements.Add(new FillerRequirement(run.Id, gap, "Run end filler"));
            }

            var (leftCondition, rightCondition) = DeriveEndConditions(run, allRuns);
            endConditionUpdates.Add(new EndConditionUpdate(run.Id, leftCondition, rightCondition));
        }

        context.EngineeringResult = new EngineeringResolutionResult
        {
            Assemblies = assemblies
                .OrderBy(a => a.CabinetId.Value)
                .ToArray(),
            FillerRequirements = fillerRequirements
                .OrderBy(f => f.RunId.Value)
                .ToArray(),
            EndConditionUpdates = endConditionUpdates
                .OrderBy(e => e.RunId.Value)
                .ToArray()
        };

        return StageResult.Succeeded(StageNumber);
    }

    private (EndCondition left, EndCondition right) DeriveEndConditions(
        CabinetRun run,
        IReadOnlyList<CabinetRun> allRuns)
    {
        var spatialInfo = _stateStore.GetRunSpatialInfo(run.Id);
        if (spatialInfo is null)
        {
            return (EndCondition.Open(), EndCondition.Open());
        }

        var leftPoint = spatialInfo.StartWorld;
        var rightPoint = spatialInfo.EndWorld;

        var wall = _stateStore.GetWall(run.WallId);

        var leftCondition = DeriveEndCondition(leftPoint, isLeft: true, wall, run.Id, allRuns);
        var rightCondition = DeriveEndCondition(rightPoint, isLeft: false, wall, run.Id, allRuns);

        return (leftCondition, rightCondition);
    }

    private EndCondition DeriveEndCondition(
        Point2D endpoint,
        bool isLeft,
        Domain.SpatialContext.Wall? wall,
        Domain.Identifiers.RunId currentRunId,
        IReadOnlyList<CabinetRun> allRuns)
    {
        if (wall is not null)
        {
            if (endpoint.DistanceTo(wall.StartPoint) <= EndpointMatchTolerance ||
                endpoint.DistanceTo(wall.EndPoint) <= EndpointMatchTolerance)
            {
                return EndCondition.AgainstWall();
            }
        }

        foreach (var otherRun in allRuns)
        {
            if (otherRun.Id == currentRunId)
            {
                continue;
            }

            var otherSpatial = _stateStore.GetRunSpatialInfo(otherRun.Id);
            if (otherSpatial is null)
            {
                continue;
            }

            var otherPoint = isLeft ? otherSpatial.EndWorld : otherSpatial.StartWorld;
            if (endpoint.DistanceTo(otherPoint) <= EndpointMatchTolerance)
            {
                return EndCondition.AdjacentCabinet();
            }
        }

        return EndCondition.Open();
    }

    private static IReadOnlyDictionary<string, string> BuildAssemblyParameters(
        CabinetStateRecord cabinet)
    {
        var toeKick = cabinet.Category is CabinetCategory.Wall
            ? 0m
            : PartGeometry.ResolveToeKickHeight(cabinet).Inches;

        var doorCount = cabinet.EffectiveOpenings.Count(o =>
            o.Type is OpeningType.Door or OpeningType.SingleDoor or OpeningType.DoubleDoor);

        var drawerCount = cabinet.EffectiveOpenings.Count(o =>
            o.Type is OpeningType.Drawer or OpeningType.DrawerBank);

        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["category"] = cabinet.Category.ToString(),
            ["construction"] = cabinet.Construction.ToString(),
            ["depth_inches"] = cabinet.NominalDepth.Inches.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            ["door_count"] = doorCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["drawer_count"] = drawerCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["height_inches"] = cabinet.EffectiveNominalHeight.Inches.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            ["shelf_count"] = PartGeometry.ResolveShelfCount(cabinet).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["toe_kick"] = toeKick.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            ["width_inches"] = cabinet.NominalWidth.Inches.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
