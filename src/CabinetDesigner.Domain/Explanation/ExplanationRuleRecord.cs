using System.Collections.Generic;

namespace CabinetDesigner.Domain.Explanation;

public sealed record ExplanationRuleRecord
{
    public required string RuleId { get; init; }

    public required string RuleName { get; init; }

    public required ExplanationRuleCategory Category { get; init; }

    public required string Source { get; init; }

    public required string Description { get; init; }

    public IReadOnlyDictionary<string, string>? EvaluatedParameters { get; init; }
}
