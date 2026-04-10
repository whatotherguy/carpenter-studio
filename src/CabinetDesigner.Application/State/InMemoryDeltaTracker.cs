using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryDeltaTracker : IDeltaTracker
{
    private readonly object _sync = new();
    private readonly List<StateDelta> _pending = [];

    public void Begin()
    {
        lock (_sync)
        {
            _pending.Clear();
        }
    }

    public void RecordDelta(StateDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        lock (_sync)
        {
            _pending.Add(delta);
        }
    }

    public IReadOnlyList<StateDelta> Finalize()
    {
        lock (_sync)
        {
            var deltas = _pending.ToArray();
            _pending.Clear();
            return deltas;
        }
    }
}
