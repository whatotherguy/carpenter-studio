using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryUndoStack : IUndoStack
{
    private readonly Stack<UndoEntry> _undo = [];
    private readonly Stack<UndoEntry> _redo = [];
    private readonly List<UndoEntry> _journal = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public IReadOnlyList<UndoEntry> Journal => _journal;

    public void Push(UndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _undo.Push(entry);
        _redo.Clear();
        _journal.Add(entry);
    }

    public UndoEntry? Undo() => _undo.TryPop(out var entry)
        ? ReturnAndQueue(entry, _redo)
        : null;

    public UndoEntry? Redo() => _redo.TryPop(out var entry)
        ? ReturnAndQueue(entry, _undo)
        : null;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _journal.Clear();
    }

    private static UndoEntry ReturnAndQueue(UndoEntry entry, Stack<UndoEntry> destination)
    {
        destination.Push(entry);
        return entry;
    }
}
