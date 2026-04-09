using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface IProjectRepository
{
    Task<ProjectRecord?> FindAsync(ProjectId id, CancellationToken ct = default);

    Task SaveAsync(ProjectRecord project, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default);
}
