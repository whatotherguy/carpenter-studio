namespace CabinetDesigner.Application.Services;

public sealed class ValidationSummaryService : IValidationSummaryService
{
    public bool HasManufactureBlockers =>
        throw new NotImplementedException("NOT IMPLEMENTED YET: validation indexing has not been introduced.");

    public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() =>
        throw new NotImplementedException("NOT IMPLEMENTED YET: validation indexing has not been introduced.");

    public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) =>
        throw new NotImplementedException("NOT IMPLEMENTED YET: validation indexing has not been introduced.");
}
