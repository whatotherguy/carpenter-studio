using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Validation;

public sealed record ExtendedValidationIssue
{
    public required ValidationIssueId IssueId { get; init; }

    public required ValidationIssue Issue { get; init; }

    public required string RuleCode { get; init; }

    public required ValidationRuleCategory Category { get; init; }

    public required ValidationRuleScope Scope { get; init; }

    public required IReadOnlyList<SuggestedFix> SuggestedFixes { get; init; }

    public ExplanationNodeId? ExplanationNodeId { get; init; }
}
