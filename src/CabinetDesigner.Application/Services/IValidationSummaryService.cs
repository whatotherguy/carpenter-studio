namespace CabinetDesigner.Application.Services;

public interface IValidationSummaryService
{
    IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues();

    IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId);

    bool HasManufactureBlockers { get; }
}
