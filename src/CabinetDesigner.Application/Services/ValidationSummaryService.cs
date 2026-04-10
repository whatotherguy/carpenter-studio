using CabinetDesigner.Application.Pipeline;

namespace CabinetDesigner.Application.Services;

public sealed class ValidationSummaryService : IValidationSummaryService
{
    private readonly IValidationResultStore _resultStore;

    public ValidationSummaryService(IValidationResultStore resultStore)
    {
        _resultStore = resultStore ?? throw new ArgumentNullException(nameof(resultStore));
    }

    public bool HasManufactureBlockers =>
        _resultStore.Current?.HasManufactureBlockers ?? false;

    public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() =>
        _resultStore.Current?.AllBaseIssues
            .Select(ValidationIssueSummaryDto.From)
            .ToArray()
        ?? [];

    public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);

        return _resultStore.Current?.AllBaseIssues
            .Where(issue => issue.AffectedEntityIds?.Contains(entityId, StringComparer.Ordinal) == true)
            .Select(ValidationIssueSummaryDto.From)
            .ToArray()
        ?? [];
    }
}
