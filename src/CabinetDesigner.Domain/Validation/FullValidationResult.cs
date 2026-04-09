using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation;

public sealed record FullValidationResult
{
    public required IReadOnlyList<ExtendedValidationIssue> CrossCuttingIssues { get; init; }

    public required IReadOnlyList<ValidationIssue> ContextualIssues { get; init; }

    public IReadOnlyList<ValidationIssue> AllBaseIssues =>
        ContextualIssues
            .Concat(CrossCuttingIssues.Select(issue => issue.Issue))
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Code, System.StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, System.StringComparer.Ordinal)
            .ToArray();

    public bool IsValid => !AllBaseIssues.Any(issue => issue.Severity >= ValidationSeverity.Error);

    public bool HasManufactureBlockers =>
        AllBaseIssues.Any(issue => issue.Severity == ValidationSeverity.ManufactureBlocker);

    public ValidationSeverityCounts SeverityCounts => new(
        Info: AllBaseIssues.Count(issue => issue.Severity == ValidationSeverity.Info),
        Warnings: AllBaseIssues.Count(issue => issue.Severity == ValidationSeverity.Warning),
        Errors: AllBaseIssues.Count(issue => issue.Severity == ValidationSeverity.Error),
        ManufactureBlockers: AllBaseIssues.Count(issue => issue.Severity == ValidationSeverity.ManufactureBlocker));
}

public sealed record ValidationSeverityCounts(
    int Info,
    int Warnings,
    int Errors,
    int ManufactureBlockers);
