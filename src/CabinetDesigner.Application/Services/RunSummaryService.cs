using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Services;

public sealed class RunSummaryService : IRunSummaryService
{
    private readonly ICurrentPersistedProjectState _currentProjectState;
    private readonly IDesignStateStore _stateStore;

    public RunSummaryService(
        ICurrentPersistedProjectState currentProjectState,
        IDesignStateStore stateStore)
    {
        _currentProjectState = currentProjectState ?? throw new ArgumentNullException(nameof(currentProjectState));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public RunSummaryProjection GetCurrentSummary(IReadOnlyList<Guid> selectedCabinetIds)
    {
        ArgumentNullException.ThrowIfNull(selectedCabinetIds);

        if (_currentProjectState.CurrentState is null)
        {
            return new RunSummaryProjection(false, null);
        }

        var activeRun = ResolveActiveRun(selectedCabinetIds) ?? _stateStore.GetAllRuns().FirstOrDefault();
        return activeRun is null
            ? new RunSummaryProjection(true, null)
            : new RunSummaryProjection(true, Map(activeRun));
    }

    private CabinetRun? ResolveActiveRun(IReadOnlyList<Guid> selectedCabinetIds)
    {
        foreach (var selectedCabinetId in selectedCabinetIds)
        {
            var cabinet = _stateStore.GetCabinet(new CabinetId(selectedCabinetId));
            if (cabinet is null)
            {
                continue;
            }

            var run = _stateStore.GetRun(cabinet.RunId);
            if (run is not null)
            {
                return run;
            }
        }

        return null;
    }

    private RunSummaryDto Map(CabinetRun run)
    {
        var slots = run.Slots
            .Where(slot => slot.SlotType == RunSlotType.Cabinet && slot.CabinetId is not null)
            .Select(slot =>
            {
                var cabinet = _stateStore.GetCabinet(slot.CabinetId!.Value);
                return new RunSlotSummaryDto(
                    slot.CabinetId.Value.Value,
                    cabinet?.CabinetTypeId ?? "Unknown cabinet",
                    slot.OccupiedWidth.Inches,
                    slot.SlotIndex);
            })
            .ToArray();

        return new RunSummaryDto(
            run.Id.Value,
            run.WallId.Value.ToString(),
            slots.Sum(slot => slot.NominalWidthInches),
            slots.Length,
            run.Slots.Any(slot => slot.SlotType == RunSlotType.Filler),
            run.OccupiedLength > run.Capacity,
            slots);
    }
}
