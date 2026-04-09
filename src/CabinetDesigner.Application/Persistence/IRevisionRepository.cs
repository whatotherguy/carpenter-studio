using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface IRevisionRepository
{
    Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default);

    Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default);

    Task SaveAsync(RevisionRecord revision, CancellationToken ct = default);

    Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default);
}
