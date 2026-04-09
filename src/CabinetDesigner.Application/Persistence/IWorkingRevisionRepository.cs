using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface IWorkingRevisionRepository
{
    Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default);

    Task SaveAsync(WorkingRevision revision, CancellationToken ct = default);
}
