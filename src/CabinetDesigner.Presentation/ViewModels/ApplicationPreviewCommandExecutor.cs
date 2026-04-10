using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ApplicationPreviewCommandExecutor : IPreviewCommandExecutor
{
    private readonly IPreviewCommandHandler _handler;

    public ApplicationPreviewCommandExecutor(IPreviewCommandHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public DragPreviewResult Preview(IDesignCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = _handler.Preview(command);
        return result.IsValid
            ? new DragPreviewResult(true, command, null)
            : DragPreviewResult.Invalid(result.RejectionReason ?? "Preview rejected.");
    }
}
