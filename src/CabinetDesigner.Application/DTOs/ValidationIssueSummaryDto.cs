using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.DTOs;

public sealed record ValidationIssueSummaryDto(
    string Severity,
    string Code,
    string Message,
    IReadOnlyList<string>? AffectedEntityIds)
{
    public static ValidationIssueSummaryDto From(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        return new ValidationIssueSummaryDto(
            issue.Severity.ToString(),
            issue.Code,
            issue.Message,
            issue.AffectedEntityIds);
    }
}
