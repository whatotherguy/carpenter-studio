using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public interface IEditorStateManager
{
    void Apply(IEditorCommand command);
}
