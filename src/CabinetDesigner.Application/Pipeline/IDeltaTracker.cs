using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline;

public interface IDeltaTracker
{
    void Begin();

    void RecordDelta(StateDelta delta);

    IReadOnlyList<StateDelta> Finalize();
}
