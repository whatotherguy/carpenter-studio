using System.Threading;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class ApplicationCommitCommandExecutor : ICommitCommandExecutor
{
    private readonly IDesignCommandHandler _handler;

    public ApplicationCommitCommandExecutor(IDesignCommandHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public async Task<DragCommitResult> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        return result.Success
            ? new DragCommitResult(true, command, null)
            : DragCommitResult.Failed(result.Issues.FirstOrDefault()?.Message ?? "Command rejected.");
    }
}
