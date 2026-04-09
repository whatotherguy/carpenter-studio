using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Services;

public sealed class UndoRedoService : IUndoRedoService
{
    private readonly IResolutionOrchestrator _orchestrator;
    private readonly IApplicationEventBus _eventBus;
    private readonly IUndoStack _undoStack;

    public UndoRedoService(
        IResolutionOrchestrator orchestrator,
        IApplicationEventBus eventBus,
        IUndoStack undoStack)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _undoStack = undoStack ?? throw new ArgumentNullException(nameof(undoStack));
    }

    public bool CanUndo => _undoStack.CanUndo;

    public bool CanRedo => _undoStack.CanRedo;

    public CommandResultDto Undo()
    {
        var result = _orchestrator.Undo();
        if (result is null)
        {
            return CommandResultDto.NoOp("undo");
        }

        var dto = CommandResultDto.From(result, "undo");
        if (result.Success)
        {
            _eventBus.Publish(new UndoAppliedEvent(dto));
        }

        return dto;
    }

    public CommandResultDto Redo()
    {
        var result = _orchestrator.Redo();
        if (result is null)
        {
            return CommandResultDto.NoOp("redo");
        }

        var dto = CommandResultDto.From(result, "redo");
        if (result.Success)
        {
            _eventBus.Publish(new RedoAppliedEvent(dto));
        }

        return dto;
    }

    public void Clear() => _undoStack.Clear();
}
