using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Domain.Validation.Rules;

public sealed class MaterialAssignmentRule : IValidationRule
{
    public string RuleCode => "constraint.material_unresolved";

    public string RuleName => "Material Assignment";

    public string Description => "Generated parts must resolve to a real material assignment before manufacturing.";

    public ValidationRuleCategory Category => ValidationRuleCategory.Constraints;

    public ValidationRuleScope Scope => ValidationRuleScope.Part;

    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context) =>
        context.Constraints
            .Where(constraint => constraint.ConstraintCode == "MATERIAL_UNRESOLVED")
            .Select(constraint => new ValidationIssue(
                ValidationSeverity.Error,
                RuleCode,
                constraint.Message,
                constraint.AffectedEntityIds))
            .ToArray();
}
