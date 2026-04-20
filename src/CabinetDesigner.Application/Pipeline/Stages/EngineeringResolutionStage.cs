using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class EngineeringResolutionStage : IResolutionStage
{
    private static readonly Length FillerTolerance = Length.FromInches(0.125m);

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

        foreach (var run in _stateStore.GetAllRuns().OrderBy(r => r.Id.Value))
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

            endConditionUpdates.Add(new EndConditionUpdate(
                run.Id,
                run.LeftEndCondition,
                run.RightEndCondition));
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

    private static IReadOnlyDictionary<string, string> BuildAssemblyParameters(
        CabinetStateRecord cabinet) =>
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["category"] = cabinet.Category.ToString(),
            ["construction"] = cabinet.Construction.ToString(),
            ["depth_inches"] = cabinet.NominalDepth.Inches.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            ["height_inches"] = cabinet.EffectiveNominalHeight.Inches.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            ["width_inches"] = cabinet.NominalWidth.Inches.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
        };
}
