using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface IExplanationRepository
{
    Task AppendNodeAsync(ExplanationNodeRecord node, CancellationToken ct = default);

    Task<IReadOnlyList<ExplanationNodeRecord>> LoadForEntityAsync(
        string entityId,
        RevisionId revisionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ExplanationNodeRecord>> LoadForCommandAsync(
        CommandId commandId,
        CancellationToken ct = default);
}
