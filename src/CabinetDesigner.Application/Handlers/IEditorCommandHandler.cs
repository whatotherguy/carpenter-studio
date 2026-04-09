using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public interface IEditorCommandHandler
{
    void Execute(IEditorCommand command);
}
