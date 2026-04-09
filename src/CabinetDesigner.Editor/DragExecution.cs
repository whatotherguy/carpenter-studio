using System.Threading;
using System.Threading.Tasks;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Editor;

public sealed record DragPreviewResult(
    bool IsValid,
    IDesignCommand? PreviewCommand,
    string? RejectionReason)
{
    public static DragPreviewResult Invalid(string rejectionReason) =>
        new(false, null, rejectionReason);
}

public sealed record DragCommitResult(
    bool Success,
    IDesignCommand? CommittedCommand,
    string? FailureReason)
{
    public static DragCommitResult Failed(string failureReason) =>
        new(false, null, failureReason);
}

public interface IPreviewCommandExecutor
{
    DragPreviewResult Preview(IDesignCommand command);
}

public interface ICommitCommandExecutor
{
    Task<DragCommitResult> ExecuteAsync(IDesignCommand command, CancellationToken ct = default);
}
