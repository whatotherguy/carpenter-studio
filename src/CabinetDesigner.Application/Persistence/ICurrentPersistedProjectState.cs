namespace CabinetDesigner.Application.Persistence;

public interface ICurrentPersistedProjectState
{
    PersistedProjectState? CurrentState { get; }

    void SetCurrentState(PersistedProjectState state);

    void Clear();
}
