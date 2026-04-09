using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface ISnapshotRepository
{
    Task WriteAsync(ApprovedSnapshot snapshot, CancellationToken ct = default);

    Task<ApprovedSnapshot?> ReadAsync(RevisionId revisionId, CancellationToken ct = default);

    Task<IReadOnlyList<SnapshotSummary>> ListAsync(ProjectId projectId, CancellationToken ct = default);
}
