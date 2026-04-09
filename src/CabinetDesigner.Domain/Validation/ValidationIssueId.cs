using System;
using System.Collections.Generic;
using System.Linq;

namespace CabinetDesigner.Domain.Validation;

public readonly record struct ValidationIssueId
{
    public string Value { get; }

    public ValidationIssueId(string ruleCode, IReadOnlyList<string> affectedEntityIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleCode);

        var sortedIds = affectedEntityIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Value = $"{ruleCode}:{string.Join("+", sortedIds)}";
    }

    public override string ToString() => Value;
}
