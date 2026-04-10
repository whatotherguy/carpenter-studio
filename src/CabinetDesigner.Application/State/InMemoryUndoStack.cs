using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryUndoStack : IUndoStack
{
    private readonly object _sync = new();
    private readonly Stack<UndoEntry> _undo = [];
    private readonly Stack<UndoEntry> _redo = [];
    private readonly List<UndoEntry> _journal = [];

    public bool CanUndo { get { lock (_sync) return _undo.Count > 0; } }

    public bool CanRedo { get { lock (_sync) return _redo.Count > 0; } }

    public IReadOnlyList<UndoEntry> Journal { get { lock (_sync) return _journal.ToArray(); } }

    public void Push(UndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_sync)
        {
            _undo.Push(entry);
            _redo.Clear();
            _journal.Add(entry);
        }
    }

    public UndoEntry? Undo()
    {
        lock (_sync)
        {
            return _undo.TryPop(out var entry)
                ? ReturnAndQueue(entry, _redo)
                : null;
        }
    }

    public UndoEntry? Redo()
    {
        lock (_sync)
        {
            return _redo.TryPop(out var entry)
                ? ReturnAndQueue(entry, _undo)
                : null;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _undo.Clear();
            _redo.Clear();
            _journal.Clear();
        }
    }

    private static UndoEntry ReturnAndQueue(UndoEntry entry, Stack<UndoEntry> destination)
    {
        destination.Push(entry);
        return entry;
    }
}
