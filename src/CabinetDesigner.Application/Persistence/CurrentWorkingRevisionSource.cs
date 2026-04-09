using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public sealed class CurrentWorkingRevisionSource : IWorkingRevisionSource, ICurrentPersistedProjectState
{
    private readonly InMemoryDesignStateStore _stateStore;

    public CurrentWorkingRevisionSource(InMemoryDesignStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public PersistedProjectState? CurrentState { get; private set; }

    public void SetCurrentState(PersistedProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        CurrentState = state;
        _stateStore.LoadWorkingRevision(state.WorkingRevision);
    }

    public void Clear()
    {
        CurrentState = null;
        _stateStore.ClearAll();
    }

    public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null)
    {
        if (CurrentState is null)
        {
            throw new InvalidOperationException("No project state is currently loaded.");
        }

        var workingRevision = new WorkingRevision(
            CurrentState.Revision,
            CurrentState.WorkingRevision.Rooms,
            _stateStore.GetAllWalls(),
            _stateStore.GetAllRuns(),
            BuildCabinets(CurrentState.Revision.Id),
            partResult?.Parts ?? CurrentState.WorkingRevision.Parts);

        return CurrentState with
        {
            WorkingRevision = workingRevision
        };
    }

    private IReadOnlyList<Cabinet> BuildCabinets(RevisionId revisionId) =>
        _stateStore.GetAllCabinets()
            .Select(cabinet => new Cabinet(
                cabinet.CabinetId,
                revisionId,
                cabinet.CabinetTypeId,
                CabinetCategory.Base,
                ConstructionMethod.Frameless,
                cabinet.NominalWidth,
                cabinet.NominalDepth,
                Length.FromInches(34.5m)))
            .ToArray();
}
