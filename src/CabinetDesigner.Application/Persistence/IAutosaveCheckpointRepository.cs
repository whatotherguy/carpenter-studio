using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface IAutosaveCheckpointRepository
{
    Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default);

    Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default);

    Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default);
}
