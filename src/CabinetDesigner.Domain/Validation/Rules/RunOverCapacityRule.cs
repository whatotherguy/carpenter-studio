using System.Collections.Generic;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation.Rules;

public sealed class RunOverCapacityRule : IValidationRule
{
    public string RuleCode => "run_integrity.over_capacity";

    public string RuleName => "Run Over Capacity";

    public string Description => "A run's occupied length exceeds its total capacity.";

    public ValidationRuleCategory Category => ValidationRuleCategory.RunIntegrity;

    public ValidationRuleScope Scope => ValidationRuleScope.Run;

    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();

        foreach (var run in context.RunSnapshots)
        {
            if (run.OccupiedLength <= run.Capacity)
            {
                continue;
            }

            var overBy = run.OccupiedLength - run.Capacity;
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                RuleCode,
                $"Run {run.RunId} exceeds capacity by {overBy}.",
                [run.RunId]));
        }

        return issues;
    }
}
