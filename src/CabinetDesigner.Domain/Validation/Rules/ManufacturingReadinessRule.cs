using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation.Rules;

public sealed class ManufacturingReadinessRule : IValidationRule
{
    public string RuleCode => "manufacturing.readiness";

    public string RuleName => "Manufacturing Readiness";

    public string Description => "Manufacturing-readiness blockers must be resolved before a revision can be approved.";

    public ValidationRuleCategory Category => ValidationRuleCategory.Manufacturing;

    public ValidationRuleScope Scope => ValidationRuleScope.Project;

    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context) =>
        context.ManufacturingBlockers
            .Select(blocker => new ValidationIssue(
                ValidationSeverity.ManufactureBlocker,
                $"manufacturing.{blocker.BlockerCode}",
                blocker.Message,
                blocker.AffectedEntityIds))
            .ToArray();
}
