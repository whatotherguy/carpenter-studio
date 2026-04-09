namespace CabinetDesigner.Application.Services;

public interface IUndoRedoService
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    CommandResultDto Undo();

    CommandResultDto Redo();

    void Clear();
}
