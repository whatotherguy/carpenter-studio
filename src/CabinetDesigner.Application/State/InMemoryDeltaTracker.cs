using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryDeltaTracker : IDeltaTracker
{
    private readonly List<StateDelta> _pending = [];

    public void Begin() => _pending.Clear();

    public void RecordDelta(StateDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        _pending.Add(delta);
    }

    public IReadOnlyList<StateDelta> Finalize()
    {
        var deltas = _pending.ToArray();
        _pending.Clear();
        return deltas;
    }
}
