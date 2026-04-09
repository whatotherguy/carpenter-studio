using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public sealed class EditorCommandHandler : IEditorCommandHandler
{
    private readonly IEditorStateManager _editorStateManager;

    public EditorCommandHandler(IEditorStateManager editorStateManager)
    {
        _editorStateManager = editorStateManager ?? throw new ArgumentNullException(nameof(editorStateManager));
    }

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _editorStateManager.Apply(command);
    }
}
