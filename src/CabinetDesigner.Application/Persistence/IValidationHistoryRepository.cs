using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface IValidationHistoryRepository
{
    Task SaveIssuesAsync(
        RevisionId revisionId,
        IReadOnlyList<ValidationIssueRecord> issues,
        CancellationToken ct = default);

    Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(
        RevisionId revisionId,
        CancellationToken ct = default);
}
