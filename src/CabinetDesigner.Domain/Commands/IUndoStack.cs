using System.Collections.Generic;

namespace CabinetDesigner.Domain.Commands;

public interface IUndoStack
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    void Push(UndoEntry entry);

    UndoEntry? Undo();

    UndoEntry? Redo();

    IReadOnlyList<UndoEntry> Journal { get; }

    void Clear();
}
