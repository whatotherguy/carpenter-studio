using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation.Rules;

public sealed class HardwareAssignmentRule : IValidationRule
{
    public string RuleCode => "constraint.hardware_missing";

    public string RuleName => "Hardware Assignment";

    public string Description => "Cabinets that need hardware should surface a visible warning when no hardware assignment is available.";

    public ValidationRuleCategory Category => ValidationRuleCategory.Constraints;

    public ValidationRuleScope Scope => ValidationRuleScope.Cabinet;

    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context) =>
        context.Constraints
            .Where(constraint => constraint.ConstraintCode == "NO_HARDWARE_CATALOG")
            .Select(constraint => new ValidationIssue(
                ValidationSeverity.Warning,
                RuleCode,
                constraint.Message,
                constraint.AffectedEntityIds))
            .ToArray();
}
