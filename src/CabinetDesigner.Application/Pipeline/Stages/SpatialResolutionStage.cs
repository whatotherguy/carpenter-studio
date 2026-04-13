using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Projection;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class SpatialResolutionStage : IResolutionStage
{
    private readonly IDesignStateStore _stateStore;

    public SpatialResolutionStage()
        : this(new InMemoryDesignStateStore())
    {
    }

    public SpatialResolutionStage(IDesignStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int StageNumber => 3;

    public string StageName => "Spatial Resolution";

    public bool ShouldExecute(ResolutionMode mode) => true;

    public StageResult Execute(ResolutionContext context)
    {
        var slotPositionUpdates = new List<SlotPositionUpdate>();
        var adjacencyChanges = new List<AdjacencyChange>();
        var runSummaries = new List<RunSummary>();
        var placements = new List<RunPlacement>();

        foreach (var run in _stateStore.GetAllRuns())
        {
            var wall = _stateStore.GetWall(run.WallId);
            var spatialInfo = _stateStore.GetRunSpatialInfo(run.Id);
            if (wall is null || spatialInfo is null)
            {
                continue;
            }

            runSummaries.Add(new RunSummary(
                run.Id,
                run.Capacity,
                run.OccupiedLength,
                run.RemainingLength,
                run.Slots.Count));

            for (var index = 0; index < run.Slots.Count; index++)
            {
                var slot = run.Slots[index];
                if (slot.CabinetId is not CabinetId cabinetId)
                {
                    continue;
                }

                var cabinet = _stateStore.GetCabinet(cabinetId);
                if (cabinet is null)
                {
                    continue;
                }

                var leftEdge = spatialInfo.StartWorld + wall.Direction * run.SlotOffset(index).Inches;
                var bounds = SceneProjectionGeometry.CreateWorldBounds(leftEdge, wall.Direction, slot.OccupiedWidth, cabinet.NominalDepth);

                slotPositionUpdates.Add(new SlotPositionUpdate(slot.Id, cabinetId, run.Id, slot.SlotIndex, leftEdge, slot.OccupiedWidth));
                adjacencyChanges.Add(new AdjacencyChange(
                    cabinetId,
                    FindAdjacentCabinetId(run.Slots, index - 1),
                    FindAdjacentCabinetId(run.Slots, index + 1)));
                placements.Add(new RunPlacement(run.Id, cabinetId, leftEdge, wall.Direction, bounds, slot.OccupiedWidth));
            }
        }

        context.SpatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates = slotPositionUpdates,
            AdjacencyChanges = adjacencyChanges,
            RunSummaries = runSummaries,
            Placements = placements
        };

        return StageResult.Succeeded(StageNumber);
    }

    private static CabinetId? FindAdjacentCabinetId(IReadOnlyList<RunSlot> slots, int index) =>
        index >= 0 && index < slots.Count ? slots[index].CabinetId : null;
}
